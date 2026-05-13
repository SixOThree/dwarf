/*
Copyright (c) 2017, Dr. Hans-Walter Latz (original Java implementation)
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

using System.Diagnostics;
using Dwarf.Engine;

namespace Dwarf.Agents;

// Agent for the network interface of a Dwarf machine.
//
// Network access is supported either to the Dodo NetHub (TCP-connected via
// NetworkHubInterface) or, as a fallback, to a local-time-service synthesizer
// (NetworkInternalTimeService). Selection depends on `setHubParameters`:
// if `hostname` is non-empty and `port` is in (0, 0xFFFF) the NetHub path is
// used, otherwise the internal time service kicks in.
public class NetworkAgent : Agent
{
    /*
     * EthernetFCBType
     */
    private const int fcb_lp_receiveIOCB = 0;
    private const int fcb_lp_transmitIOCB = 2;
    private const int fcb_w_receiveInterruptSelector = 4;
    private const int fcb_w_transmitInterruptSelector = 5;
    private const int fcb_w_stopAgent = 6;
    private const int fcb_w_receiveStopped = 7;
    private const int fcb_w_transmitStopped = 8;
    private const int fcb_w_hearSelf = 9;
    private const int fcb_w_processorID0 = 10;
    private const int fcb_w_processorID1 = 11;
    private const int fcb_w_processorID2 = 12;
    private const int fcb_w_packetsMissed = 13;
    private const int fcb_w_agentBlockSize = 14;
    private const int FCB_SIZE = 15;

    // EthernetIOCBType (8 words)
    private const int iocb_lp_bufferAddress = 0;
    private const int iocb_w_bufferLength = 2;
    private const int iocb_w_actualLength = 3;
    private const int iocb_w_dequeuedPacketTypeStatus = 4; // dequeued(1), packetType(1..7), status(8..15)
    private const int iocb_w_retries = 5;
    private const int iocb_lp_nextIocb = 6;

    // bits for iocb_w_dequeuedPacketTypeStatus
    private const int Q_queued   = 0x00000000;
    private const int Q_dequeued = 0x00008000;
    private const int T_receive  = 0x00000000;
    private const int T_transmit = 0x00000100;
    private const ushort S_inProgress              =   1;
    private const ushort S_completedOK             =   2;
    private const ushort S_tooManyCollisions       =   4;
    private const ushort S_badCRC                  =   8;
    private const ushort S_alignmentError          =  16;
    private const ushort S_packetTooLong           =  32;
    private const ushort S_badCRDAndAlignmentError = 128;

    // other constants for network
    private const int MaxPacketSize = 1536; // max. ethernet packet size, XNS uses max. 576 bytes = 546 payload + 30 header

    // configuration parameters (set by setHubParameters before the next ctor call)
    private static string hubHostname = "";
    private static int hubPort = 0;
    private static int localTimeOffsetMinutes = 0;

    // the network device the agent is connected to
    private iNetDeviceInterface? netIf = null;

    // packet receiving status
    private bool receiveStopped = true; // is receiving packets stopped and are all incoming packets to be dropped?

    // list of IOCB addresses ready to receive packets — using List<int> rather
    // than Queue<int> so that Java's `LinkedList.remove(Integer.valueOf(...))`
    // pattern in enqueueReceiveIocb has a direct C# analog
    private readonly List<int> receiveIocbs = new();

    // temp buffer for word <-> byte conversion of packets
    private readonly byte[] packetBuffer = new byte[2048];

    // Configure the (next created instance of the) agent with connection
    // data to the NetHub or, as backup, the internal time-service offset.
    //
    // hostname                       : NetHub host name (or null/empty to
    //                                  disable NetHub)
    // port                           : NetHub listening port (or 0 to disable)
    // fallbackLocalTimeOffsetMinutes : if no NetHub: GMT offset in minutes
    //                                  (positive = east, negative = west;
    //                                   e.g. Germany is +60 without DST,
    //                                   +120 with DST)
    public static void setHubParameters(string hostname, int port, int fallbackLocalTimeOffsetMinutes)
    {
        hubHostname = hostname;
        hubPort = port;
        localTimeOffsetMinutes = fallbackLocalTimeOffsetMinutes;
    }

    public NetworkAgent(int fcbAddress)
        : base(AgentDevice.networkAgent, fcbAddress, FCB_SIZE)
    {
        this.enableLogging(Config.IO_LOG_NETWORK);

        if (!string.IsNullOrEmpty(hubHostname) && hubPort > 0 && hubPort < 0xFFFF)
        {
            this.netIf = new NetworkHubInterface(hubHostname, hubPort);
        }
        else
        {
            this.netIf = new NetworkInternalTimeService(localTimeOffsetMinutes);
        }
        this.netIf.setNewPacketNotifier(() =>
        {
            logf("\n+++ requesting datarefresh for new packet (at: {0} , insns = {1})\n",
                getNanoMs(), Cpu.insns);
            Processes.requestDataRefresh();
        });
    }

    public override void shutdown(System.Text.StringBuilder errMsgTarget)
    {
        if (this.netIf != null)
        {
            this.netIf.shutdown();
            this.netIf = null;
        }
    }

    public override void refreshMesaMemory()
    {
        bool logIntro = true;

        // no network => nothing to do
        if (this.netIf == null) { return; }

        // did Pilot stop packet transmission?
        if (this.receiveStopped)
        {
            // drain incoming packets
            while (this.netIf.dequeuePacket(this.packetBuffer, this.packetBuffer.Length) > 0)
            {
                // ignore packet content
            }
        }

        // receive incoming packets into waiting IOCBs
        bool didInterrupt = false;
        while (this.receiveIocbs.Count > 0)
        {
            int packetByteCount = this.netIf.dequeuePacket(this.packetBuffer, this.packetBuffer.Length);
            if (packetByteCount < 1) { return; }

            if (logIntro)
            {
                logIntro = this.logTimeIntro();
                logf("begin refreshMesaMemory()\n");
            }

            int recvIocb = this.dequeueReceiveIocb();

            int bufferAddress = Mem.readDblWord(recvIocb + iocb_lp_bufferAddress);
            int bufferByteLength = Mem.readWord(recvIocb + iocb_w_bufferLength);

            int trfBytes = Math.Min(packetByteCount, bufferByteLength);
            int wlen = 0;
            logf("\n -- raw packet data\n");
            for (int i = 0; i < trfBytes; i += 2)
            {
                if ((i % 32) == 0) { slogf("\n 0x{0:X3} : ", i); }
                int b1 = ((this.packetBuffer[i] & 0x00FF) << 8);
                int b2 = this.packetBuffer[i + 1] & 0x00FF;
                ushort w = (ushort)(b1 | b2);
                slogf(" {0:X4}", w);
                Mem.writeWord(bufferAddress + wlen, w);
                wlen++;
            }
            slogf("\n\n");
            Mem.writeWord(recvIocb + iocb_w_actualLength, (ushort)(wlen * 2));

            int status = (trfBytes == packetByteCount) ? S_completedOK : S_packetTooLong;
            int packetTypeBits = Mem.readWord(recvIocb + iocb_w_dequeuedPacketTypeStatus) & 0x0000FF00;
            Mem.writeWord(recvIocb + iocb_w_dequeuedPacketTypeStatus, (ushort)(packetTypeBits | status));

            dumpIocb(recvIocb, true);
            slogf("\n");

            this.packetsReceived++;
            logf("     transfered {0} words (trfBytes {1}, bufferLen {2}) to Mesa memory at 0x{3:X8} => status = {4}\n",
                wlen, trfBytes, bufferByteLength, bufferAddress, status);

            if (!didInterrupt)
            {
                ushort recvIntrMask = this.getFcbWord(fcb_w_receiveInterruptSelector);
                Processes.requestMesaInterrupt(recvIntrMask);
                didInterrupt = true;
                logf("     requested MesaInterrupt(0x{0:X4})\n", recvIntrMask);
            }
        }
        if (!logIntro)
        {
            logf("done refreshMesaMemory()\n");
        }
    }

    private int packetsSent = 0;
    private int packetsReceived = 0;

    public int getPacketsSentCount() => this.packetsSent;
    public int getPacketsReceivedCount() => this.packetsReceived;

    public override void call()
    {
        this.logTimeIntro();
        bool stop = (this.getFcbWord(fcb_w_stopAgent) != PrincOpsDefs.FALSE);
        if (stop)
        {
            logf("call() - stop transmissions\n");

            // stop transmissions
            this.setFcbWord(fcb_w_receiveStopped, PrincOpsDefs.TRUE);
            this.setFcbWord(fcb_w_transmitStopped, PrincOpsDefs.TRUE);
            this.receiveStopped = true;

            // drop pending packets in network interface and IOCB queue
            if (this.netIf != null)
            {
                while (this.netIf.dequeuePacket(this.packetBuffer, this.packetBuffer.Length) > 0)
                {
                    // ignore dropped packets
                }
            }
            this.receiveIocbs.Clear();
            logf("call() - end\n");

            return; // nothing else to do if stopping transmissions
        }
        else
        {
            // (re)start transmissions
            this.setFcbWord(fcb_w_receiveStopped, PrincOpsDefs.FALSE);
            this.setFcbWord(fcb_w_transmitStopped, PrincOpsDefs.FALSE);
            if (this.receiveStopped)
            {
                this.receiveIocbs.Clear();
                logf("call() - (re)starting transmissions\n");
            }
            this.receiveStopped = false;
        }

        ushort receiveInterruptSelector = this.getFcbWord(fcb_w_receiveInterruptSelector);
        ushort transmitInterruptSelector = this.getFcbWord(fcb_w_transmitInterruptSelector);
        bool doTransmitInterrupt = false;

        bool hearSelf = (this.getFcbWord(fcb_w_hearSelf) != PrincOpsDefs.FALSE);
        int recvIocb = this.getFcbDblWord(fcb_lp_receiveIOCB);
        int sendIocb = this.getFcbDblWord(fcb_lp_transmitIOCB);

        logf("call() - recvIocb = 0x{0:X8} , sendIocb = 0x{1:X8} , stopAgent = {2} , hearSelf = {3}\n",
            recvIocb, sendIocb, stop ? "true" : "false", hearSelf ? "true" : "false");
        logf("         recvIntr = 0x{0:X4} , xmitIntr = 0x{1:X4}\n",
            receiveInterruptSelector & 0xFFFF, transmitInterruptSelector & 0xFFFF);
        if (stop) { return; }

        int recvCnt = 0;
        while (recvIocb != 0)
        {
            int bufferAddress = Mem.readDblWord(recvIocb + iocb_lp_bufferAddress);
            int bufferLength = Mem.readWord(recvIocb + iocb_w_bufferLength);
            logf("         recvIocb[{0}] :: recvIocb = 0x{1:X8} , bufferAddress = 0x{2:X8} , bufferLength = {3}\n",
                recvCnt, recvIocb, bufferAddress, bufferLength);

            this.enqueueReceiveIocb(recvIocb);

            recvCnt++;
            recvIocb = Mem.readDblWord(recvIocb + iocb_lp_nextIocb);
        }

        int sendCnt = 0;
        while (sendIocb != 0)
        {
            int bufferAddress = Mem.readDblWord(sendIocb + iocb_lp_bufferAddress);
            int bufferLength = Mem.readWord(sendIocb + iocb_w_bufferLength);
            int actualLength = Mem.readWord(sendIocb + iocb_w_actualLength);

            logf("         sendIocb[{0}] :: sendIocb = 0x{1:X8} , bufferAddress = 0x{2:X8} , bufferLength = {3} , actualLength = {4}\n",
                sendCnt, sendIocb, bufferAddress, bufferLength, actualLength);

            doTransmitInterrupt = true; // we have something to handle for transmission, so inform about the outcome

            int status;
            if (bufferAddress == 0 || bufferLength < NetworkHubInterface.MIN_NET_PACKET_LEN)
            {
                status = S_badCRC;
            }
            else if (bufferLength > Math.Min(NetworkHubInterface.MAX_NET_PACKET_LEN, MaxPacketSize))
            {
                status = S_packetTooLong;
            }
            else if (this.netIf == null)
            {
                status = S_badCRC; // S_tooManyCollisions ??
            }
            else
            {
                int bpos = 0;
                for (int i = 0; i < (bufferLength + 1) / 2; i++)
                {
                    ushort w = Mem.readWord(bufferAddress + i);
                    this.packetBuffer[bpos++] = (byte)(w >>> 8);
                    this.packetBuffer[bpos++] = (byte)(w & 0xFF);
                }
                int trfLength = this.netIf.enqueuePacket(this.packetBuffer, bufferLength, hearSelf);
                status = (trfLength == bufferLength) ? S_completedOK : S_packetTooLong;
                packetsSent++;
                Mem.writeWord(sendIocb + iocb_w_actualLength, (ushort)trfLength);
                Mem.writeWord(sendIocb + iocb_w_retries, (ushort)0); // the packet was sent on the 1st try
            }
            int oldDequeuedPacketTypeBits = Mem.readWord(sendIocb + iocb_w_dequeuedPacketTypeStatus) & 0x0000FF00;
            Mem.writeWord(sendIocb + iocb_w_dequeuedPacketTypeStatus, (ushort)(oldDequeuedPacketTypeBits | status));

            dumpIocb(sendIocb);

            if (status == S_completedOK)
            {
                logf("          => packet [{0}] transmitted, length: {1}\n", packetsSent, bufferLength);
            }
            else
            {
                logf("          => packet not transmitted or transmittable\n");
            }

            sendCnt++;
            sendIocb = Mem.readDblWord(sendIocb + iocb_lp_nextIocb);
        }

        if (doTransmitInterrupt)
        {
            Processes.requestMesaInterrupt(transmitInterruptSelector);
            logf("     requested MesaInterrupt(0x{0:X4})\n", transmitInterruptSelector);
        }
        logf("call() - end\n");
    }

    protected override void initializeFcb()
    {
        this.setFcbDblWord(fcb_lp_receiveIOCB, 0);
        this.setFcbDblWord(fcb_lp_transmitIOCB, 0);
        this.setFcbWord(fcb_w_receiveInterruptSelector, (ushort)0);
        this.setFcbWord(fcb_w_transmitInterruptSelector, (ushort)0);
        this.setFcbWord(fcb_w_stopAgent, PrincOpsDefs.FALSE);
        this.setFcbWord(fcb_w_receiveStopped, PrincOpsDefs.TRUE);
        this.setFcbWord(fcb_w_transmitStopped, PrincOpsDefs.TRUE);
        this.setFcbWord(fcb_w_hearSelf, PrincOpsDefs.FALSE);
        this.setFcbWord(fcb_w_processorID0, Cpu.getPIDword(1));
        this.setFcbWord(fcb_w_processorID1, Cpu.getPIDword(2));
        this.setFcbWord(fcb_w_processorID2, Cpu.getPIDword(3));
        this.setFcbWord(fcb_w_packetsMissed, (ushort)0);
        this.setFcbWord(fcb_w_agentBlockSize, (ushort)0); // no agent specific space needed in IOCBs ... ?
    }

    private void enqueueReceiveIocb(int iocb)
    {
        if (iocb == 0) { return; }

        if (this.receiveIocbs.Count > 0)
        {
            int bufferAddress = Mem.readDblWord(iocb + iocb_lp_bufferAddress);
            bool doRemove = false;
            foreach (int queued in this.receiveIocbs)
            {
                int queuedAddress = Mem.readDblWord(queued + iocb_lp_bufferAddress);
                if (queuedAddress == bufferAddress)
                {
                    doRemove = true;
                    break;
                }
            }
            if (doRemove)
            {
                logf("     ** removed iocb 0x{0:X8} with same buffer address\n", iocb);
                // Java upstream bug preserved verbatim: this calls
                // `LinkedList.remove(Integer.valueOf(iocb))` but `iocb` is the
                // NEW IOCB which isn't in the list yet — the intent was
                // probably to remove the *queued* IOCB with the matching
                // buffer address. The call is effectively a no-op.
                this.receiveIocbs.Remove(iocb);
            }
        }

        int packetTypeBits = Mem.readWord(iocb + iocb_w_dequeuedPacketTypeStatus) & 0x0000FF00;
        Mem.writeWord(iocb + iocb_w_dequeuedPacketTypeStatus, (ushort)(packetTypeBits | S_inProgress));

        this.receiveIocbs.Add(iocb);

        logf("     enqueued iocb 0x{0:X8}\n", iocb);
        dumpReceiveIocbs();
    }

    private int dequeueReceiveIocb()
    {
        if (this.receiveIocbs.Count == 0) { return 0; }

        int currIocb = this.receiveIocbs[0];
        this.receiveIocbs.RemoveAt(0);

        logf("     dequeued iocb 0x{0:X8}\n", currIocb);
        dumpIocb(currIocb);
        dumpReceiveIocbs();

        return currIocb;
    }

    private static string getNanoMs()
    {
        // Java upstream uses System.nanoTime() and formats "%9d.%06d ms"
        // showing milliseconds.fractional-microseconds. C# uses Stopwatch
        // for high-resolution timing; precision is platform-dependent but
        // typically nanosecond-scale on modern hardware.
        long ticks = Stopwatch.GetTimestamp();
        long nanos = (long)(ticks * (1_000_000_000.0 / Stopwatch.Frequency));
        return string.Format("{0,9}.{1:D6} ms", nanos / 1_000_000, nanos % 1_000_000);
    }

    // returns whether log must still be done (false after introducing once)
    private bool logTimeIntro()
    {
        slogf("\n\n--\n-- at {0} (insns: {1})\n--\n", getNanoMs(), Cpu.insns);
        return false;
    }

    private void dumpIocb(int iocb)
    {
        dumpIocb(iocb, false);
    }

    private void dumpIocb(int iocb, bool dumpBuffer)
    {
        int bAddr = Mem.readDblWord(iocb);
        int bLen = Mem.readWord(iocb + 2);
        int actLen = Mem.readWord(iocb + 3);
        int qtStatus = Mem.readWord(iocb + 4);
        int retries = Mem.readWord(iocb + 5);
        int nextIocb = Mem.readDblWord(iocb + 6);
        string dequeued = ((qtStatus & Q_dequeued) != 0) ? "dequeue" : "queued";
        string type = ((qtStatus & T_transmit) != 0) ? "transmit" : "receive";
        string status = ((qtStatus & S_inProgress) != 0) ? " inProgress" : "";
        status += ((qtStatus & S_completedOK) != 0) ? " completedOK" : "";
        status += ((qtStatus & S_tooManyCollisions) != 0) ? " tooManyCollisions" : "";
        status += ((qtStatus & S_badCRC) != 0) ? " badCRC" : "";
        status += ((qtStatus & S_alignmentError) != 0) ? " alignmentError" : "";
        status += ((qtStatus & S_packetTooLong) != 0) ? " packetTooLong" : "";
        status += ((qtStatus & 64) != 0) ? " d64" : "";
        status += ((qtStatus & S_badCRDAndAlignmentError) != 0) ? " badCRDAndAlignmentError" : "";

        slogf("-> IOCB @ 0x{0:X8} [ bAddr = 0x{1:X8} , bLen = {2,4} , actLen = {3,4} , qts = 0x{4:X4} , retries = {5,4} , nextIocb = 0x{6:X8} ; ( {7} {8}{9} ) ]\n",
            iocb, bAddr, bLen, actLen, qtStatus, retries, nextIocb, dequeued, type, status);
        if (dumpBuffer)
        {
            int wlen = (actLen + 1) / 2;
            for (int i = 0; i < wlen; i++)
            {
                if ((i % 16) == 0) { slogf("\n 0x{0:X3} : ", i); }
                int w = Mem.readWord(bAddr + i) & 0xFFFF;
                slogf(" {0:X4}", w);
            }
            slogf("\n");
        }
    }

    private void dumpReceiveIocbs()
    {
        logf("     receive iocb queue now:\n");
        foreach (int iocb in receiveIocbs)
        {
            dumpIocb(iocb);
        }
        logf("     -----------------------\n");
    }
}
