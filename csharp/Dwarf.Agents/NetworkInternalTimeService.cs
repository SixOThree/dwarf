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

namespace Dwarf.Agents;

// Implementation of an internal time service for the case where no NetHub
// is configured. Acts as a fake NetHub: receives XNS PEX time-request
// packets and synthesizes time-response packets directly without going off
// the machine.
public class NetworkInternalTimeService : iNetDeviceInterface
{
    private iNetDeviceInterface.PacketActor? notifier = null;
    private ushort[]? timeResponse = null;

    private readonly short direction; // 0 = west, 1 = east
    private readonly short offsetHours;
    private readonly short offsetMinutes;

    private static long timeShiftMilliSeconds = 0;

    // Java relied on `synchronized` on the instance for getPacket / setPacket
    // / setNewPacketNotifier. C# uses an explicit lock object.
    private readonly object _lock = new();

    // Set the time adjustment ("days back in time") offset used when the
    // "current" time is retrieved by mesa machine programs.
    public static void setTimeShiftSeconds(long seconds)
    {
        timeShiftMilliSeconds = seconds * 1000;
    }

    // Initialize with the time zone information (without DST — if DST is
    // active, the caller must adjust gmtOffsetMinutes accordingly).
    //
    // gmtOffsetMinutes : difference between local time and GMT in minutes,
    //                    with positive values to the east and negative to
    //                    the west (e.g. Germany is +60 without DST and +120
    //                    with DST; Alaska is -560 without DST, -480 with).
    public NetworkInternalTimeService(int gmtOffsetMinutes)
    {
        if (gmtOffsetMinutes >= 0)
        {
            this.direction = 1;
        }
        else
        {
            this.direction = 0;
            gmtOffsetMinutes = -gmtOffsetMinutes;
        }
        gmtOffsetMinutes = gmtOffsetMinutes % 720;
        this.offsetHours = (short)(gmtOffsetMinutes / 60);
        this.offsetMinutes = (short)(gmtOffsetMinutes % 60);
    }

    public void shutdown()
    {
        // nothing to shutdown
    }

    public void setNewPacketNotifier(iNetDeviceInterface.PacketActor notifier)
    {
        lock (_lock)
        {
            this.notifier = notifier;
        }
    }

    public int enqueuePacket(byte[] srcBuffer, int byteCount, bool feedback)
    {
        if (this.readWord(srcBuffer, 0) != -1
            || this.readWord(srcBuffer, 1) != -1
            || this.readWord(srcBuffer, 2) != -1
            || this.readWord(srcBuffer, 6) != 0x0600) // not broadcast or not xns
        {
            return byteCount;
        }

        if (srcBuffer.Length < 54
            || this.readWord(srcBuffer, 15) != 0x0008
            || this.readWord(srcBuffer, 9) != 0x0004) // wrong length, target port not time or not PEX
        {
            return byteCount;
        }

        if (this.readWord(srcBuffer, 24) != 0x0001
            || this.readWord(srcBuffer, 25) != 0x0002
            || this.readWord(srcBuffer, 26) != 0x0001) // not time packet type, wrong version, not request
        {
            return byteCount;
        }

        // create time request response

        // the raw packet
        ushort[] b = new ushort[37];

        // address components
        ushort myNet0 = 0x0004;
        ushort myNet1 = 0x0001;
        ushort myMac0 = 0x1000;
        ushort myMac1 = 0x1A33;
        ushort myMac2 = 0x3333;
        ushort mySocket = 8;
        ushort mac0 = (ushort)this.readWord(srcBuffer, 3);
        ushort mac1 = (ushort)this.readWord(srcBuffer, 4);
        ushort mac2 = (ushort)this.readWord(srcBuffer, 5);

        // time data
        long unixTimeMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + timeShiftMilliSeconds;
        int milliSecs = (int)(unixTimeMillis % 1000);
        long unixTimeSecs = unixTimeMillis / 1000;
        int mesaSecs = (int)((unixTimeSecs + (731 * 86400) + 2114294400) & 0x00000000FFFFFFFFL);
        ushort mesaSecs0 = (ushort)(mesaSecs >>> 16);
        ushort mesaSecs1 = (ushort)(mesaSecs & 0xFFFF);

        // build the packet component-wise

        // eth: dst
        b[0] = mac0;
        b[1] = mac1;
        b[2] = mac2;

        // eth: src
        b[3] = myMac0;
        b[4] = myMac1;
        b[5] = myMac2;

        // eth: type
        b[6] = 0x0600;

        // xns: ckSum
        b[7] = 0xFFFF; // no checksum

        // xns: length
        b[8] = 60; // payload length

        // xns: transport control & packet type
        b[9] = 4; // hop count = 0 & packet type = PEX

        // xns: destination endpoint: copy the source destination of the ingone packet
        b[10] = (ushort)this.readWord(srcBuffer, 16);
        b[11] = (ushort)this.readWord(srcBuffer, 17);
        b[12] = (ushort)this.readWord(srcBuffer, 18);
        b[13] = (ushort)this.readWord(srcBuffer, 19);
        b[14] = (ushort)this.readWord(srcBuffer, 20);
        b[15] = (ushort)this.readWord(srcBuffer, 21);

        // xns: source endpoint: put "our" address with the "local" net and "our" socket
        b[16] = myNet0;
        b[17] = myNet1;
        b[18] = myMac0;
        b[19] = myMac1;
        b[20] = myMac2;
        b[21] = mySocket;

        // pex: identification from request
        b[22] = (ushort)this.readWord(srcBuffer, 22);
        b[23] = (ushort)this.readWord(srcBuffer, 23);

        // pex: client type
        b[24] = 1; // clientType "time"

        // payload: time response
        b[25] = 2;                            // version(0): WORD -- TimeVersion = 2
        b[26] = 2;                            // tsBody(1): SELECT type(1): PacketType FROM -- timeResponse = 2
        b[27] = mesaSecs0;                    // time(2): WireLong -- computed time
        b[28] = mesaSecs1;
        b[29] = (ushort)this.direction;       // zoneS(4): System.WestEast -- east
        b[30] = (ushort)this.offsetHours;     // zoneH(5): [0..177B] -- +1 hour
        b[31] = (ushort)this.offsetMinutes;   // zoneM(6): [0..377B] -- +0 minutes
        b[32] = 0;                            // beginDST(7): WORD -- no dst (temp)
        b[33] = 0;                            // endDST(8): WORD -- no dst (temp)
        b[34] = 1;                            // errorAccurate(9): BOOLEAN -- true
        b[35] = 0;                            // absoluteError(10): WireLong]
        b[36] = (ushort)((milliSecs > 500) ? 1000 - milliSecs : milliSecs); // no direction ?? (plus or minus)?

        // enqueue for "receiving" the service response
        this.setPacket(b);

        return byteCount;
    }

    public int dequeuePacket(byte[] trgBuffer, int maxLength)
    {
        ushort[]? packet = this.getPacket();
        if (packet == null || trgBuffer == null || maxLength < 2)
        {
            return 0;
        }
        this.setPacket(null); // the only one was received...

        int trfWords = Math.Min(maxLength / 2, packet.Length);
        int bpos = 0;
        for (int i = 0; i < trfWords; i++)
        {
            trgBuffer[bpos++] = (byte)((packet[i] >> 8) & 0xFF);
            trgBuffer[bpos++] = (byte)(packet[i] & 0xFF);
        }

        return trfWords * 2;
    }

    /*
     * internals
     */

    // Reads a big-endian 16-bit word from a byte buffer at word position
    // `wpos` (so byte offset is wpos*2). Returns `short` to match Java's
    // sign-extending return type — the call sites compare with -1 (which
    // for short means 0xFFFF) so the sign matters.
    private short readWord(byte[] b, int wpos)
    {
        int bpos = wpos * 2;
        if (bpos < 0 || (bpos + 1) >= b.Length)
        {
            return 0;
        }
        int hi = b[bpos++] & 0xFF;
        int lo = b[bpos] & 0xFF;
        short res = (short)(((hi << 8) | lo) & 0xFFFF);
        return res;
    }

    private ushort[]? getPacket()
    {
        lock (_lock)
        {
            return this.timeResponse;
        }
    }

    private void setPacket(ushort[]? p)
    {
        lock (_lock)
        {
            this.timeResponse = p;

            if (this.notifier != null)
            {
                // Java caught InterruptedException here; C# delegates don't
                // declare throws, and the notifier (Processes.requestDataRefresh
                // via NetworkAgent) doesn't block.
                this.notifier();
            }
        }
    }
}
