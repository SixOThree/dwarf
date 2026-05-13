/*
Copyright (c) 2018, Dr. Hans-Walter Latz (original Java implementation)
Copyright (c) 2026, Matthew Dugal (C# .NET 10 port)
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
    * Redistributions of source code must retain the above copyright
      notice, this list of conditions and the following disclaimer.
    * Redistributions in binary form must reproduce the above copyright
      notice, this list of conditions and the following disclaimer in the
      documentation and/or other materials provided with the distribution.
    * The names of the authors may not be used to endorse or promote products
      derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS "AS IS" AND ANY EXPRESS
OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS;
OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ANY THEORY OF LIABILITY,
WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR
OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System.Net.Sockets;
using System.Threading.Channels;

namespace Dwarf.Agents;

// Implementation of the NetHub interface for the mesa NetworkAgent.
//
// **Port refactor per DECISIONS.md §4**: Java's two `IOThread`s + synchronized
// `PacketQueue`s become two long-running async Tasks driving a pair of
// `Channel<Packet>` queues. A `CancellationTokenSource` carries the shutdown
// signal. The wire protocol (2-byte big-endian length prefix + payload) is
// preserved **byte-exactly** per DECISIONS.md §7 + RISKS R4.
//
// Threading model:
//   - Engine thread calls enqueuePacket / dequeuePacket / setNewPacketNotifier
//     synchronously. These touch the channels (thread-safe by design) and do
//     not block.
//   - Sender task: awaits outgoing.Reader, writes each packet to the TCP
//     stream, reconnects on I/O failure.
//   - Receiver task: reads bytes from the TCP stream, packages into a
//     Packet, writes to incoming channel, invokes the new-packet notifier.
//   - Both tasks share a TcpClient guarded by `_sockLock` (SemaphoreSlim for
//     async-safe mutex).
public class NetworkHubInterface : iNetDeviceInterface
{
    public const int MIN_NET_PACKET_LEN = 14;  // 2x 48-bit MAC + 16-bit etherType
    public const int MAX_NET_PACKET_LEN = 766; // +2 byte length prefix ~> 768 bytes

    private const int RETRY_INTERVAL_MS = 2000; // wait 2 seconds before retrying a reconnect

    private const int MAX_QUEUE_LEN = 64;

    private readonly string hubHost;
    private readonly int hubSocket;

    // shared TCP connection state, guarded by _sockLock
    private readonly SemaphoreSlim _sockLock = new(1, 1);
    private TcpClient? _tcp = null;
    private NetworkStream? _stream = null;

    private readonly Channel<Packet> outgoing;
    private readonly Channel<Packet> incoming;

    private iNetDeviceInterface.PacketActor? notifier = null;
    private readonly object _notifierLock = new();

    private readonly CancellationTokenSource _cts = new();
    private readonly Task _senderTask;
    private readonly Task _receiverTask;

    // Construct the hub interface and start sending/receiving — i.e.
    // "plug the connector cable into the wall".
    public NetworkHubInterface(string hubHost, int hubSocket)
    {
        this.hubHost = hubHost;
        this.hubSocket = hubSocket;

        // Bounded channels with `Wait` full-mode mean TryWrite returns false
        // when the channel is full — matches Java's `hasSpace` check
        // semantics for backpressure.
        this.outgoing = Channel.CreateBounded<Packet>(new BoundedChannelOptions(MAX_QUEUE_LEN)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });
        this.incoming = Channel.CreateBounded<Packet>(new BoundedChannelOptions(MAX_QUEUE_LEN)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = true,
        });

        // Start the background sender/receiver loops. Task.Run hops onto the
        // ThreadPool — the underlying I/O is genuinely async (NetworkStream
        // ReadAsync/WriteAsync), so the threadpool doesn't get pinned.
        _senderTask = Task.Run(() => SenderLoopAsync(_cts.Token));
        _receiverTask = Task.Run(() => ReceiverLoopAsync(_cts.Token));
    }

    // This cannot be undone — reconnecting to the hub requires a new
    // instance of this class.
    public void shutdown()
    {
        _cts.Cancel();
        // The tasks observe cancellation, close the TCP socket, and exit
        // promptly. We don't `await` them here because shutdown() is called
        // synchronously from the engine thread.
    }

    public void setNewPacketNotifier(iNetDeviceInterface.PacketActor notifier)
    {
        lock (_notifierLock)
        {
            this.notifier = notifier;
        }
    }

    public int enqueuePacket(byte[] srcBuffer, int byteCount, bool feedback)
    {
        var p = new Packet();
        int len = p.copyFrom(srcBuffer, byteCount);
        if (len < 1)
        {
            return 0;
        }
        if (!outgoing.Writer.TryWrite(p))
        {
            return 0; // packet cannot be sent (possibly lost) due to "buffer overrun"
        }

        if (feedback)
        {
            var fb = new Packet();
            fb.copyFrom(srcBuffer, byteCount);
            if (incoming.Writer.TryWrite(fb))
            {
                Console.WriteLine(">>>>> sent Packet fed back!!");
                FireNotifier();
            }
        }

        return len;
    }

    public int dequeuePacket(byte[] trgBuffer, int maxLength)
    {
        if (!incoming.Reader.TryRead(out Packet? p))
        {
            return 0;
        }
        int len = p.copyTo(trgBuffer, maxLength);
        return len;
    }

    private void FireNotifier()
    {
        iNetDeviceInterface.PacketActor? n;
        lock (_notifierLock) { n = this.notifier; }
        try
        {
            n?.Invoke();
        }
        catch
        {
            // notifier failures must not crash the receiver loop
        }
    }

    /*
     * Background loops
     */

    private async Task SenderLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (Packet p in outgoing.Reader.ReadAllAsync(ct))
            {
                await SendOneAsync(p, ct);
            }
        }
        catch (OperationCanceledException) { /* expected on shutdown */ }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Stopping NetworkHubInterface sender due to {e.GetType().Name}: {e.Message}");
        }
        finally
        {
            await _sockLock.WaitAsync(CancellationToken.None);
            try { DisconnectInLock(); }
            finally { _sockLock.Release(); }
        }
    }

    // Mirror of Java's `send(Packet p)` retry loop: keep trying until the
    // packet is sent or the connection cannot be re-established.
    private async Task SendOneAsync(Packet p, CancellationToken ct)
    {
        NetworkStream? lastStream = null;
        while (!ct.IsCancellationRequested)
        {
            NetworkStream? stream;
            await _sockLock.WaitAsync(ct);
            try
            {
                if (_stream == null || ReferenceEquals(lastStream, _stream))
                {
                    DisconnectInLock();
                    await ConnectInLockAsync(ct);
                }
                stream = _stream;
            }
            finally { _sockLock.Release(); }
            if (stream == null) { return; } // cancelled while connecting

            try
            {
                await stream.WriteAsync(p.buffer.AsMemory(0, p.currLength), ct);
                return;
            }
            catch (OperationCanceledException) { return; }
            catch (Exception e)
            {
                Console.Error.WriteLine($"IOException while sending: {e.Message}");
                lastStream = stream;
                // continue: next iteration will see the failed stream and reconnect
            }
        }
    }

    private async Task ReceiverLoopAsync(CancellationToken ct)
    {
        try
        {
            NetworkStream? lastStream = null;
            while (!ct.IsCancellationRequested)
            {
                NetworkStream? stream;
                await _sockLock.WaitAsync(ct);
                try
                {
                    if (_stream == null || ReferenceEquals(lastStream, _stream))
                    {
                        DisconnectInLock();
                        await ConnectInLockAsync(ct);
                    }
                    stream = _stream;
                }
                finally { _sockLock.Release(); }
                if (stream == null) { return; }

                var p = new Packet();
                int n;
                try
                {
                    n = await stream.ReadAsync(p.buffer.AsMemory(), ct);
                }
                catch (OperationCanceledException) { return; }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"IOException while receiving: {e.Message}");
                    lastStream = stream;
                    continue;
                }
                if (n <= 0)
                {
                    // graceful close — reconnect
                    lastStream = stream;
                    continue;
                }
                p.currLength = n;

                // Mirror Java: drop the packet if the channel is full
                // (the upstream design treats this as "buffer overrun").
                if (incoming.Writer.TryWrite(p))
                {
                    FireNotifier();
                }
                // else: packet dropped
            }
        }
        catch (OperationCanceledException) { /* expected on shutdown */ }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Stopping NetworkHubInterface receiver due to {e.GetType().Name}: {e.Message}");
        }
        finally
        {
            await _sockLock.WaitAsync(CancellationToken.None);
            try { DisconnectInLock(); }
            finally { _sockLock.Release(); }
        }
    }

    /*
     * Connection management — must be called holding _sockLock
     */

    private async Task ConnectInLockAsync(CancellationToken ct)
    {
        while (_tcp == null && !ct.IsCancellationRequested)
        {
            try
            {
                var tcp = new TcpClient();
                tcp.NoDelay = true;
                await tcp.ConnectAsync(hubHost, hubSocket, ct);
                _tcp = tcp;
                _stream = tcp.GetStream();
            }
            catch (SocketException se) when (se.SocketErrorCode == SocketError.HostNotFound)
            {
                Console.Error.WriteLine($"** Unknown host: '{hubHost}', network hub unreachable");
                // wait forever (or until cancelled); matches Java's `this.wait()` on UnknownHost
                try { await Task.Delay(Timeout.Infinite, ct); }
                catch (OperationCanceledException) { throw; }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                Console.Error.WriteLine($"IOException while connecting: {e.Message}");
                DisconnectInLock();
                try { await Task.Delay(RETRY_INTERVAL_MS, ct); }
                catch (OperationCanceledException) { throw; }
            }
        }
    }

    private void DisconnectInLock()
    {
        if (_stream != null)
        {
            try { _stream.Close(); } catch { /* ignore */ }
            _stream = null;
        }
        if (_tcp != null)
        {
            try { _tcp.Close(); } catch { /* ignore */ }
            _tcp = null;
        }
    }

    /*
     * Packet — wire-format data container
     */

    // Class for a packet being transmitted. Wire layout:
    //   [byte 0]      length high byte (big-endian)
    //   [byte 1]      length low byte
    //   [bytes 2..]   payload (length bytes)
    private sealed class Packet
    {
        // packet data buffer
        public byte[] buffer = new byte[MAX_NET_PACKET_LEN + 2];

        // number of valid bytes in the buffer (including the 2-byte length prefix)
        public int currLength = 0;

        // Fill this packet from an external data source.
        //
        // src   : source buffer
        // count : number of significant bytes in src
        // returns: number of payload bytes that will be sent on the wire
        public int copyFrom(byte[] src, int count)
        {
            count = Math.Min(count, src.Length);
            int len = Math.Min(MAX_NET_PACKET_LEN, Math.Max(MIN_NET_PACKET_LEN, count));
            if (len == MIN_NET_PACKET_LEN) { return 0; } // no payload!

            this.buffer[0] = (byte)((len >> 8) & 0xFF);
            this.buffer[1] = (byte)(len & 0xFF);

            Array.Copy(src, 0, this.buffer, 2, len);
            this.currLength = len + 2;
            return len;
        }

        // Fill an external data target with this packet's content (payload only).
        public int copyTo(byte[] trg, int maxLength)
        {
            if (this.currLength < 2) { return 0; } // not even content length?
            int contentLength = ((this.buffer[0] << 8) & 0xFF00) | (this.buffer[1] & 0xFF);
            maxLength = Math.Min(maxLength, trg.Length);
            int len = Math.Min(Math.Min(contentLength, this.currLength - 2), Math.Max(0, maxLength));
            if (len > 0)
            {
                Array.Copy(this.buffer, 2, trg, 0, len);
            }
            return len;
        }
    }
}
