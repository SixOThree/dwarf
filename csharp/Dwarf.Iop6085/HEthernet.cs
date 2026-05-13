/*
Copyright (c) 2019, Dr. Hans-Walter Latz (original Java implementation)
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
using Dwarf.Agents;
using Dwarf.Engine;
using static Dwarf.Iop6085.IORegion;

namespace Dwarf.Iop6085;

// IOP device handler for the network interface of a Daybreak/6085 machine.
// The network interface is in fact the NetHub-interface for Dodo or a local
// internal time service, both implemented with the Guam agents (reused from
// Phase D — see `Dwarf.Agents.NetworkHubInterface` / `NetworkInternalTimeService`).
public class HEthernet : DeviceHandler
{
    /*
     * Function Context Block
     */

    private const string NetworkFCB = "NetworkFCB";

    private static class MesaClientState
    {
        public const ushort off = 0;
        public const ushort on = 1;
    }

    private sealed class EHF_SystemControlBlock
    {
        public readonly Word w0;
        public readonly Field stat;
        public readonly Field zeroA;
        public readonly Field cus;
        public readonly Field zeroB;
        public readonly Field rus;
        public readonly Field zeroC;
        public readonly Word w1;
        public readonly Field ack;
        public readonly Field unusedA;
        public readonly Field cuc;
        public readonly BoolField reset;
        public readonly Field ruc;
        public readonly Field unusedB;
        public readonly Word cblOffset; // SCBBase RELATIVE POINTER TO CommandBlock
        public readonly Word rfaOffset; // SCBBase RELATIVE POINTER TO ReceiveFrameDescriptor
        public readonly Word crcErrs;
        public readonly Word alnErrs;
        public readonly Word rscErrs;
        public readonly Word ovrnErrs;

        public EHF_SystemControlBlock(string name)
        {
            this.w0 = mkWord(name, "word0");
            this.stat = mkField("stat", this.w0, 0xF000);
            this.zeroA = mkField("zeroA", this.w0, 0x0800);
            this.cus = mkField("cus", this.w0, 0x0700);
            this.zeroB = mkField("zeroB", this.w0, 0x0080);
            this.rus = mkField("rus", this.w0, 0x0070);
            this.zeroC = mkField("zeroC", this.w0, 0x000F);
            this.w1 = mkWord(name, "word1");
            this.ack = mkField("ack", this.w1, 0xF000);
            this.unusedA = mkField("unusedA", this.w1, 0x0800);
            this.cuc = mkField("cuc", this.w1, 0x0700);
            this.reset = mkBoolField("", this.w1, 0x0080);
            this.ruc = mkField("ruc", this.w1, 0x0070);
            this.unusedB = mkField("unusedB", this.w1, 0x000F);
            this.cblOffset = mkWord(name, "cblOffset");
            this.rfaOffset = mkWord(name, "rfaOffset");
            this.crcErrs = mkWord(name, "crcErrs");
            this.alnErrs = mkWord(name, "alnErrs");
            this.rscErrs = mkWord(name, "rscErrs");
            this.ovrnErrs = mkWord(name, "ovrnErrs");
        }
    }

    private sealed class NonMesaContext
    {
        // fake non mesa context by reserving 160 words (length of NonMesaContext)
        // and see if Pilot accesses it in any way
        public readonly Word[] nmc;

        public NonMesaContext(string name)
        {
            this.nmc = new Word[160];
            for (int i = 0; i < this.nmc.Length; i++)
            {
                this.nmc[i] = mkWord(name, "word[" + i + "]");
            }
        }
    }

    private sealed class FCB : IORAddress
    {
        private readonly int startAddress;

        public readonly IOPTypes.QueueBlock mesaOutQueue;
        public readonly IOPTypes.QueueBlock mesaInQueue;
        public readonly Word mesaClientStateRequest; // ==> MesaClientState

        public readonly EHF_SystemControlBlock scb;
        public readonly IOPTypes.NotifyMask etherOutWorkMask;
        public readonly IOPTypes.NotifyMask etherInWorkMask;
        public readonly Word etherLockMask;
        public readonly Word mesaInClientState;  // ==> MesaClientState
        public readonly Word mesaOutClientState; // ==> MesaClientState

        public readonly Word iopEtherOutQueSemaphore;
        public readonly Word mesaEtherOutQueSemaphore;
        public readonly Word iopEtherInQueSemaphore;
        public readonly Word mesaEtherInQueSemaphore;

        public readonly NonMesaContext nonMesaContext;

        public FCB()
        {
            this.startAddress = syncToSegment() + IOR_BASE;

            this.mesaOutQueue = new IOPTypes.QueueBlock(NetworkFCB, "mesaOutQueue");
            this.mesaInQueue = new IOPTypes.QueueBlock(NetworkFCB, "mesaInQueue");
            this.mesaClientStateRequest = mkWord(NetworkFCB, "mesaClientStateRequest");

            this.scb = new EHF_SystemControlBlock(NetworkFCB + ".scb");
            this.etherOutWorkMask = new IOPTypes.NotifyMask(NetworkFCB, "etherOutWorkMask");
            this.etherInWorkMask = new IOPTypes.NotifyMask(NetworkFCB, "etherInWorkMask");
            this.etherLockMask = mkWord(NetworkFCB, "etherLockMask");
            this.mesaInClientState = mkWord(NetworkFCB, "mesaInClientState");
            this.mesaOutClientState = mkWord(NetworkFCB, "mesaOutClientState");

            this.iopEtherOutQueSemaphore = mkWord(NetworkFCB, "iopEtherOutQueSemaphore");
            this.mesaEtherOutQueSemaphore = mkWord(NetworkFCB, "mesaEtherOutQueSemaphore");
            this.iopEtherInQueSemaphore = mkWord(NetworkFCB, "iopEtherInQueSemaphore");
            this.mesaEtherInQueSemaphore = mkWord(NetworkFCB, "mesaEtherInQueSemaphore");

            this.nonMesaContext = new NonMesaContext(NetworkFCB + ".nonMesaContext");

            // initialize masks for communication with mesahead
            this.etherOutWorkMask.byteMaskAndOffset.set(mkMask());
            this.etherInWorkMask.byteMaskAndOffset.set(mkMask());
            this.etherLockMask.set(mkMask());
        }

        public string getName() => NetworkFCB;

        public int getRealAddress() => this.startAddress;
    }

    /*
     * Input Output Control Block
     */

    private const string NetworkIOCB = "NetworkIOCB";

    private static class OpType
    {
        public const int command = 0;
        public const int output = 1;
        public const int reset = 2;
        public const int startRU = 3;
        public const int input = 15;
    }

    private sealed class IOCB : IOStruct
    {
        public readonly IOPTypes.OpieAddress next;
        public readonly IOPTypes.ClientCondition clientCondition;
        public readonly Word i586Status; // uninterpreted for now
        public readonly Word w6;
        public readonly BoolField status_done;
        public readonly BoolField status_handled;
        public readonly BoolField status_okay;
        public readonly BoolField status_frameTooLong;
        public readonly BoolField status_interruptTimeout;
        public readonly Field status_unused;
        public readonly BoolField status_isDequeued;
        public readonly Field opType; // { command(0), output(1), reset(2), startRU(3), input(15) }
        public readonly BoolField op_io_dontProcess;
        public readonly IOPTypes.OpieAddress op_io_address; // ... this is word7...
        public readonly Word op_io_length; // in bytes (input: max recv, output: bytes to send)
        public readonly Word op_io_count;  // in bytes (input: bytes received)
        // CommandSelect has max. 7 words, starting at word 7 (overlapping with the 4 words [op_io_address..op_io_count])
        // => total IOCB length == 14
        // => 3 words for CommandSelect still missing
        public readonly Word op_command_select_word11;
        public readonly Word op_command_select_word12;
        public readonly Word op_command_select_word13;

        public IOCB(int @base) : base(@base, NetworkIOCB)
        {
            this.next = new IOPTypes.OpieAddress(this, "next");
            this.clientCondition = new IOPTypes.ClientCondition(this, "clientCondition");
            this.i586Status = mkWord("i586Status");
            this.w6 = mkWord("word6");
            this.status_done = mkBoolField("status.done", this.w6, 0x8000);
            this.status_handled = mkBoolField("status.handled", this.w6, 0x4000);
            this.status_okay = mkBoolField("status.okay", this.w6, 0x2000);
            this.status_frameTooLong = mkBoolField("status.frameTooLong", this.w6, 0x1000);
            this.status_interruptTimeout = mkBoolField("status.interruptTimeout", this.w6, 0x0800);
            this.status_unused = mkField("status.unused", this.w6, 0x0600);
            this.status_isDequeued = mkBoolField("status.isDequeued", this.w6, 0x0100);
            this.opType = mkField("op.type", this.w6, 0x00F0);
            this.op_io_dontProcess = mkBoolField("op.io.dontProcess", this.w6, 0x0008);
            this.op_io_address = new IOPTypes.OpieAddress(this, "op.io.address");
            this.op_io_length = mkByteSwappedWord("op.io.length");
            this.op_io_count = mkByteSwappedWord("op.io.count");
            this.op_command_select_word11 = mkWord("op.command.select.word11");
            this.op_command_select_word12 = mkWord("op.command.select.word12");
            this.op_command_select_word13 = mkWord("op.command.select.word13");

            this.endStruct();
        }

        public void dump(TextWriter ps, string prefix)
        {
            int aNext = this.next.toLP();
            ushort intrMask = this.clientCondition.maskValue.get();
            string status = this.status_done.@is() ? " done" : "";
            status += this.status_handled.@is() ? " handled" : "";
            status += this.status_okay.@is() ? " ok" : "";
            status += this.status_frameTooLong.@is() ? " frameTooLong" : "";
            status += this.status_interruptTimeout.@is() ? "interruptTimeout" : "";
            status += this.status_isDequeued.@is() ? " dequeued" : " ";
            int opTypeVal = this.opType.get();
            string opTypeName = opTypeVal switch
            {
                0 => "command",
                1 => "output",
                2 => "reset",
                3 => "startRU",
                15 => "input",
                _ => "invalid(" + opTypeVal + ")",
            };
            ps.Write(string.Format(
                "{0}-> IOCB @ 0x{1:X8} [{2} next = 0x{3:X8} , intrMask = 0x{4:X4} , status ={5} , opType = {6}{7} , bAddr = 0x{8:X8} , length = {9} , count = {10} ]\n",
                prefix,
                this.getRealAddress(),
                this.op_io_dontProcess.@is() ? " DONT_PROCESS" : "",
                aNext,
                intrMask,
                status,
                opTypeName,
                this.op_io_dontProcess.@is() ? " dontProcess" : "",
                this.op_io_address.toLP(),
                this.op_io_length.get(),
                this.op_io_count.get()));
        }
    }

    /*
     * statistical data
     */

    private int packetsSent = 0;
    private int packetsReceived = 0;

    public int getPacketsSentCount() => this.packetsSent;
    public int getPacketsReceivedCount() => this.packetsReceived;

    /*
     * implementation
     */

    // central component: Function Control Block
    private readonly FCB fcb;

    // other constants for network
    private const int MaxPacketSize = 1536; // max ethernet packet, XNS uses max 576 = 546 payload + 30 header

    // configuration parameters
    private static string hubHostname = "";
    private static int hubPort = 0;
    private static int localTimeOffsetMinutes = 0;

    // Configure the (next created instance of the) handler with connection data
    // to the NetHub or as backup the internal (local) time service parameters.
    public static void setHubParameters(string hostname, int port, int fallbackLocalTimeOffsetMinutes)
    {
        hubHostname = hostname;
        hubPort = port;
        localTimeOffsetMinutes = fallbackLocalTimeOffsetMinutes;
    }

    // the network device the handler is connected to
    private iNetDeviceInterface? netIf = null;

    // packet receiving status
    private bool receiveStopped = true;

    // some status infos
    private readonly bool hearSelf = false;

    // the (relocatable) working IOCBs
    private readonly IOCB workIocb = new IOCB(0);
    private readonly IOCB tmpIocb = new IOCB(0);

    // list of IOCB addresses ready to receive packets
    // Java upstream uses LinkedList<Integer> (Queue interface). C# `List<int>`
    // gives the same semantics — `Remove(int)` removes by value, mirroring Java's
    // `LinkedList.remove(Integer.valueOf(iocb))` pattern from `enqueueReceiveIocb`.
    private readonly List<int> receiveIocbs = new();

    // temp buffer for word <-> byte conversion of packets
    private readonly byte[] packetBuffer = new byte[2048];

    public HEthernet() : base(NetworkFCB, Config.IO_LOG_NETWORK)
    {
        this.fcb = new FCB();

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
            this.logf("\n+++ requesting datarefresh for new packet (at: {0} , insns = {1})\n", getNanoMs(), Cpu.insns);
            Processes.requestDataRefresh();
        });
    }

    public override int getFcbRealAddress() => this.fcb.getRealAddress();

    public override ushort getFcbSegment() => ((IORAddress)this.fcb).getIOPSegment();

    private const ushort STATUS_TRANSMIT_NONE = 0x8000;       // completion
    private const ushort STATUS_TRANSMIT_COLLISIONS = 0x902F; // completion, aborted, tooManyCollisions, collisions=15
    private const ushort STATUS_TRANSMIT_OTHER = 0x9010;      // completion, aborted, unusedB

    private static int wordSwapBytes(int val)
    {
        val &= 0xFFFF;
        int res = (val << 8) | (val >>> 8);
        return res & 0xFFFF;
    }

    public override bool processNotify(ushort notifyMask)
    {
        if (notifyMask != this.fcb.etherInWorkMask.byteMaskAndOffset.get()
            && notifyMask != this.fcb.etherOutWorkMask.byteMaskAndOffset.get())
        {
            // not for us, let an other handler take care of this
            return false;
        }

        this.logf(
            "IOP::HEthernet.processNotify() -- working for ether{0}WorkMask at 0x{1:X8}+0x{2:X4} [insn# {3} ]\n",
            (notifyMask == this.fcb.etherInWorkMask.byteMaskAndOffset.get()) ? "In" : "Out", Cpu.CB, Cpu.savedPC, Cpu.insns);

        // check for stopping transmissions
        if (this.fcb.mesaClientStateRequest.get() == MesaClientState.off)
        {
            this.logf("IOP::HEthernet.processNotify() -> stop transmissions\n");

            this.fcb.mesaInClientState.set(MesaClientState.off);
            this.fcb.mesaOutClientState.set(MesaClientState.off);
            this.receiveStopped = true;

            // drop pending packets in network interface, iocb queue and interrupt queue
            if (this.netIf != null)
            {
                while (this.netIf.dequeuePacket(this.packetBuffer, this.packetBuffer.Length) > 0)
                {
                    // ignore packets dropped
                }
            }
            this.receiveIocbs.Clear();
            this.logf("IOP::HEthernet.processNotify() -> end\n");
            return true;
        }

        // (re)start transmissions
        this.fcb.mesaInClientState.set(MesaClientState.on);
        this.fcb.mesaOutClientState.set(MesaClientState.on);
        if (this.receiveStopped)
        {
            this.receiveIocbs.Clear();
            this.logf("IOP::HEthernet.processNotify() -> (re)starting transmissions\n");
        }
        this.receiveStopped = false;

        // handle new input buffer enqueued to receive ingoing packets (IOCBs are in fcb.etherInWorkMask)
        if (notifyMask == this.fcb.etherInWorkMask.byteMaskAndOffset.get())
        {
            int recvCnt = 0;
            bool hasNewInputBuffer = false;
            int iocbAddr = this.fcb.mesaInQueue.queueHead.toLP();
            this.logf("IOP::HEthernet.processNotify() -> fcb.mesaInQueue.queueHead as LP: 0x{0:X6}\n", iocbAddr);
            while (iocbAddr != 0)
            {
                this.workIocb.rebaseToVirtualAddress(iocbAddr);
                if (this.workIocb.op_io_dontProcess.@is())
                {
                    this.logf("         recvIocb[{0}] -> recvIocb = 0x{1:X8} => dontProcess !\n", recvCnt, iocbAddr);
                    recvCnt++;
                    continue;
                }
                int opType = this.workIocb.opType.get();
                if (opType == OpType.input)
                {
                    int bufferAddress = this.workIocb.op_io_address.toLP();
                    int bufferLength = this.workIocb.op_io_length.get();
                    this.logf("         recvIocb[{0}] -> recvIocb = 0x{1:X8} , bufferAddress = 0x{2:X8} , bufferLength = {3}\n",
                            recvCnt, iocbAddr, bufferAddress, bufferLength);

                    this.enqueueReceiveIocb(iocbAddr);
                    recvCnt++;
                    hasNewInputBuffer = true;
                }

                iocbAddr = this.workIocb.next.toLP();
            }
            if (hasNewInputBuffer)
            {
                this.refreshMesaMemory(); // transfer received packets already waiting (will be a no-op if none waiting)
            }
            this.logf("IOP::HEthernet.processNotify() -> end\n");
            return true;
        }

        // here all IOCBs to process are in fcb.etherOutWorkMask, but these can do different operations
        ushort interruptsToRaise = 0;
        int sendCnt = 0;
        int outIocbAddr = this.fcb.mesaOutQueue.queueHead.toLP();
        this.logf("IOP::HEthernet.processNotify() -> fcb.mesaOutQueue.queueHead as LP: 0x{0:X6}\n", outIocbAddr);
        while (outIocbAddr != 0)
        {
            this.workIocb.rebaseToVirtualAddress(outIocbAddr);
            if (this.logging) { this.workIocb.dump(Console.Out, "         sendIocb[" + sendCnt + "] "); }
            if (this.workIocb.op_io_dontProcess.@is())
            {
                this.logf("         sendIocb[{0}] -> op.io.dontProcess == true => ignoring IOCB\n", sendCnt);
                sendCnt++;
                outIocbAddr = this.workIocb.next.toLP();
                continue;
            }
            int opType = this.workIocb.opType.get();
            switch (opType)
            {
                case OpType.output:
                {
                    int bufferAddress = this.workIocb.op_io_address.toLP();
                    int bufferLength = this.workIocb.op_io_length.get();

                    this.workIocb.i586Status.set(STATUS_TRANSMIT_NONE);

                    if (bufferAddress == 0 || bufferLength < NetworkHubInterface.MIN_NET_PACKET_LEN)
                    {
                        this.logf("         sendIocb[{0}] -> null buffer or too short\n", sendCnt);
                        this.workIocb.i586Status.set(STATUS_TRANSMIT_OTHER);
                        this.workIocb.status_okay.set(false);
                    }
                    else if (bufferLength > Math.Min(NetworkHubInterface.MAX_NET_PACKET_LEN, MaxPacketSize))
                    {
                        this.logf("         sendIocb[{0}] -> too long\n", sendCnt);
                        this.workIocb.status_frameTooLong.set(true);
                        this.workIocb.status_okay.set(false);
                    }
                    else if (this.netIf == null)
                    {
                        this.logf("         sendIocb[{0}] -> netIf is null\n", sendCnt);
                        this.workIocb.i586Status.set(STATUS_TRANSMIT_COLLISIONS);
                        this.workIocb.status_okay.set(false);
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
                        int trfLength = this.netIf.enqueuePacket(this.packetBuffer, bufferLength, this.hearSelf);
                        if (trfLength == bufferLength)
                        {
                            this.workIocb.status_frameTooLong.set(false);
                            this.workIocb.status_okay.set(true);
                            this.logf("         sendIocb[{0}] -> successfully enqueued to netIf\n", sendCnt);
                        }
                        else
                        {
                            this.logf("         sendIocb[{0}] -> enqueued to netIf but trfLength = {1}\n", sendCnt, trfLength);
                            this.workIocb.status_frameTooLong.set(true);
                            this.workIocb.status_okay.set(false);
                        }
                        this.workIocb.op_io_count.set((ushort)trfLength);

                        interruptsToRaise |= this.workIocb.clientCondition.maskValue.get();

                        this.packetsSent++;
                    }
                    this.workIocb.status_done.set(true);

                    break;
                }

                case OpType.reset:
                {
                    // simulate a reset by stopping all
                    this.fcb.mesaInClientState.set(MesaClientState.off);
                    this.fcb.mesaOutClientState.set(MesaClientState.off);
                    this.receiveStopped = true;

                    if (this.netIf != null)
                    {
                        while (this.netIf.dequeuePacket(this.packetBuffer, this.packetBuffer.Length) > 0)
                        {
                            // ignore packets dropped
                        }
                    }
                    this.receiveIocbs.Clear();

                    this.workIocb.status_okay.set(true);
                    this.logf("         sendIocb[{0}] -> resetted handler\n", sendCnt);
                    break;
                }

                case OpType.command:
                {
                    // TODO: implement the actions...
                    // for now we "assume" that anything will be configured to our defaults...
                    this.workIocb.status_okay.set(true);
                    this.logf("         sendIocb[{0}] -> command assumed ok (but ignored)\n", sendCnt);
                    break;
                }

                case OpType.startRU:
                {
                    this.receiveStopped = false;
                    this.workIocb.status_okay.set(true);
                    this.logf("         sendIocb[{0}] -> (re)started receive unit\n", sendCnt);
                    break;
                }

                default: // unknown opType (or input enqueued in the wrong queue) ??
                    this.logf("         sendIocb[{0}] -> invalid opType {1}\n", sendCnt, opType);
                    this.workIocb.status_okay.set(false);
                    break;
            }
            this.workIocb.status_done.set(true);
            this.workIocb.status_handled.set(true);

            sendCnt++;
            outIocbAddr = this.workIocb.next.toLP();
        }

        // enqueue "packet(s) sent" interrupt
        if (interruptsToRaise != 0)
        {
            Processes.requestMesaInterrupt(interruptsToRaise);
            this.logf("     requested MesaInterrupt(0x{0:X4})\n", interruptsToRaise);
        }

        // done
        this.logf("IOP::HEthernet.processNotify() -> end\n");
        return true;
    }

    public override void handleLockmem(ushort lockMask, int realAddress, MemOperation memOp, ushort oldValue, ushort newValue)
    {
        // check if it is for us
        if (lockMask != this.fcb.etherLockMask.get()) { return; }

        // log the lockmem operation
        this.logf("IOP::HEthernet.handleLockmem( rAddr = 0x{0:X6} , memOp = {1} , oldValue = 0x{2:X4} , newValue = 0x{3:X4} )\n",
                realAddress, memOp.ToString(), oldValue, newValue);

        // handle handshake for setting one of the 2 semaphore words for one of the 2 queues
        IORAddress location = IORegion.resolveRealAddress(realAddress);
        if (location == this.fcb.iopEtherInQueSemaphore)
        {
            // phase 1 of locking access to the mesaInQueue
            this.logf("IOP::HEthernet.handleLockmem() -> accessed fcb.iopEtherInQueSemaphore\n");
        }
        else if (location == this.fcb.mesaEtherInQueSemaphore)
        {
            // phase 2 of locking access to the mesaInQueue
            this.logf("IOP::HEthernet.handleLockmem() -> accessed fcb.mesaEtherInQueSemaphore\n");
            this.fcb.iopEtherInQueSemaphore.set(0);
        }
        else if (location == this.fcb.iopEtherOutQueSemaphore)
        {
            // phase 1 of locking access to the mesaOutQueue
            this.logf("IOP::HEthernet.handleLockmem() -> accessed fcb.iopEtherOutQueSemaphore\n");
        }
        else if (location == this.fcb.mesaEtherOutQueSemaphore)
        {
            // phase 2 of locking access to the mesaOutQueue
            this.logf("IOP::HEthernet.handleLockmem() -> accessed fcb.mesaEtherOutQueSemaphore\n");
            this.fcb.iopEtherOutQueSemaphore.set(0);
        }
    }

    public override void handleLockqueue(int vAddr, int rAddr)
    {
        this.logf("IOP::HEthernet.handleLockqueue( vAddr = 0x{0:X6} , rAddr = 0x{1:X6} )", vAddr, rAddr);
    }

    public override void refreshMesaMemory()
    {
        lock (this)
        {
            bool logIntro = true;

            // no network => nothing to do...
            if (this.netIf == null) { return; }

            // did Pilot stop packet transmission?
            if (this.receiveStopped)
            {
                while (this.netIf.dequeuePacket(this.packetBuffer, this.packetBuffer.Length) > 0)
                {
                    // ignore packet content
                }
            }

            // move ingone packets into waiting receive iocbs
            bool didInterrupt = false;
            while (this.receiveIocbs.Count > 0)
            {
                int packetByteCount = this.netIf.dequeuePacket(this.packetBuffer, this.packetBuffer.Length);
                if (packetByteCount < 1) { return; }

                if (logIntro)
                {
                    logIntro = this.logTimeIntro();
                    this.logf("begin refreshMesaMemory()\n");
                }

                int recvIocb = this.dequeueReceiveIocb();
                this.workIocb.rebaseToVirtualAddress(recvIocb);

                int bufferAddress = this.workIocb.op_io_address.toLP();
                int bufferByteLength = this.workIocb.op_io_length.get();

                int trfBytes = Math.Min(packetByteCount, bufferByteLength);
                int wlen = 0;
                this.logf("\n -- raw packet data\n");
                for (int i = 0; i < trfBytes; i += 2)
                {
                    if ((i % 32) == 0) { this.slogf("\n 0x{0:X3} : ", i); }
                    int b1 = ((this.packetBuffer[i] & 0x00FF) << 8);
                    int b2 = this.packetBuffer[i + 1] & 0x00FF;
                    ushort w = (ushort)(b1 | b2);
                    this.slogf(" {0:X4}", w);
                    Mem.writeWord(bufferAddress + wlen, w);
                    wlen++;
                }
                this.slogf("\n\n");
                this.workIocb.op_io_count.set((ushort)(wlen * 2));

                string status;
                if (trfBytes == packetByteCount)
                {
                    this.workIocb.status_okay.set(true);
                    status = "okay";
                }
                else
                {
                    this.workIocb.status_okay.set(false);
                    this.workIocb.status_frameTooLong.set(true);
                    status = "frameTooLong";
                }
                this.workIocb.status_done.set(true);
                this.workIocb.status_handled.set(true);

                this.dumpIocb(recvIocb, true);
                this.slogf("\n");

                this.packetsReceived++;
                this.logf("     transfered {0} words (trfBytes {1}, bufferLen {2}) to Mesa memory at 0x{3:X8} => status = {4}\n", wlen, trfBytes, bufferByteLength, bufferAddress, status);

                if (!didInterrupt)
                {
                    ushort recvIntrMask = this.workIocb.clientCondition.maskValue.get();
                    Processes.requestMesaInterrupt(recvIntrMask);
                    didInterrupt = true;
                    this.logf("     requested MesaInterrupt(0x{0:X4})\n", recvIntrMask);
                }
            }
            if (!logIntro)
            {
                this.logf("done refreshMesaMemory()\n");
            }
        }
    }

    public override void shutdown(System.Text.StringBuilder errMsgTarget)
    {
        if (this.netIf != null)
        {
            this.netIf.shutdown();
            this.netIf = null;
        }
    }

    // Java upstream bug preserved verbatim: the de-dup-by-buffer-address loop sets
    // `doRemove = true` if any queued IOCB has the same buffer address as the new
    // one, then calls `receiveIocbs.remove(iocb)` — but `iocb` is the *new* IOCB
    // which isn't in the list yet, so the remove is a no-op. The intent was
    // probably to remove the *queued* IOCB with the matching buffer address.
    // Same as Phase D-11 NetworkAgent.cs.
    private void enqueueReceiveIocb(int iocb)
    {
        if (iocb == 0) { return; }

        this.workIocb.rebaseToVirtualAddress(iocb);

        if (this.receiveIocbs.Count > 0)
        {
            int bufferAddress = this.workIocb.op_io_address.toLP();
            bool doRemove = false;
            foreach (int queued in this.receiveIocbs)
            {
                this.tmpIocb.rebaseToVirtualAddress(queued);
                int queuedAddress = this.tmpIocb.op_io_address.toLP();
                if (queuedAddress == bufferAddress)
                {
                    doRemove = true;
                    break;
                }
            }
            if (doRemove)
            {
                this.logf("     ** removed iocb 0x{0:X8} with same buffer address\n", iocb);
                this.receiveIocbs.Remove(iocb);
            }
        }

        this.workIocb.status_done.set(false);
        this.workIocb.status_handled.set(false);

        this.receiveIocbs.Add(iocb);

        this.logf("     enqueued iocb 0x{0:X8}\n", iocb);
        this.dumpReceiveIocbs();
    }

    private int dequeueReceiveIocb()
    {
        if (this.receiveIocbs.Count == 0) { return 0; }

        int currIocb = this.receiveIocbs[0];
        this.receiveIocbs.RemoveAt(0);

        this.logf("     dequeued iocb 0x{0:X8}\n", currIocb);
        this.dumpIocb(currIocb);
        this.dumpReceiveIocbs();

        return currIocb;
    }

    private void dumpIocb(int iocbAddr) => this.dumpIocb(iocbAddr, false);

    private void dumpIocb(int iocbAddr, bool dumpBuffer)
    {
        if (!this.logging) { return; }
        this.tmpIocb.rebaseToVirtualAddress(iocbAddr);
        this.tmpIocb.dump(Console.Out, "");

        if (dumpBuffer)
        {
            int bAddr = this.tmpIocb.op_io_address.toLP();
            int actLen = this.tmpIocb.op_io_count.get();
            int wlen = (actLen + 1) / 2;
            for (int i = 0; i < wlen; i++)
            {
                if ((i % 16) == 0) { this.slogf("\n 0x{0:X3} : ", i); }
                int w = Mem.readWord(bAddr + i) & 0xFFFF;
                this.slogf(" {0:X4}", w);
            }
            this.slogf("\n");
        }
    }

    private void dumpReceiveIocbs()
    {
        this.logf("     receive iocb queue now:\n");
        foreach (int iocb in this.receiveIocbs)
        {
            this.dumpIocb(iocb);
        }
        this.logf("     -----------------------\n");
    }

    // Java's `System.nanoTime()` format: nanoseconds since arbitrary origin.
    // C# `Stopwatch.GetTimestamp()` scaled by Stopwatch.Frequency gives the same
    // semantic. Same pattern as Phase D-11 NetworkAgent.
    private static string getNanoMs()
    {
        long nanoTs = Stopwatch.GetTimestamp() * 1_000_000_000L / Stopwatch.Frequency;
        return string.Format("{0,9}.{1:D6} ms", nanoTs / 1_000_000L, nanoTs % 1_000_000L);
    }

    // returns false (meaning log-intro is no longer needed)
    private bool logTimeIntro()
    {
        this.slogf("\n\n--\n-- at {0} (insns: {1})\n--\n", getNanoMs(), Cpu.insns);
        return false;
    }

    /*
     * loading boot files over the network using the simpleRequest/simpleData boot protocol
     * (only the germ, as (initial or mesa) microcodes are irrelevant and the boot file proper
     * is loaded by the germ using the spp boot protocol)
     */

    private static readonly byte[] bootBuffer = new byte[1024];

    private static void putWord(int wordAt, ushort w)
    {
        int byteAt = wordAt * 2;
        if (byteAt < 0 || byteAt > bootBuffer.Length) { return; }
        bootBuffer[byteAt] = (byte)((w >> 8) & 0xFF);
        bootBuffer[byteAt + 1] = (byte)(w & 0xFF);
    }

    private static int getWord(int wordAt)
    {
        int byteAt = wordAt * 2;
        if (byteAt < 0 || byteAt > (bootBuffer.Length - 1)) { return 0; }
        int b0 = (bootBuffer[byteAt] & 0xFF);
        int b1 = (bootBuffer[byteAt + 1] & 0xFF);
        return (b0 << 8) | b1;
    }

    private static void sendPacket(iNetDeviceInterface zeNet, int wordLength) =>
        zeNet.enqueuePacket(bootBuffer, 2 * wordLength, false);

    private static int /* length in words */ recvPacket(iNetDeviceInterface zeNet)
    {
        int byteLength = zeNet.dequeuePacket(bootBuffer, bootBuffer.Length);
        return (byteLength + 1) / 2;
    }

    public static bool loadFileFromBootService(
            int mac0, int mac1, int mac2,
            long bfn,
            List<ushort[]> germ)
    {
        // get us a network interface
        if (string.IsNullOrEmpty(hubHostname) || hubPort < 1 || hubPort > 0xFFFF)
        {
            return false;
        }
        iNetDeviceInterface zeNet = new NetworkHubInterface(hubHostname, hubPort);
        const ushort localSocket = 1234;

        // where to collect the packets
        ushort[]?[] pages = new ushort[1024][]; // array of 1024 packets/pages
        int lastPage = -1; // highest index used in pages
        bool ok = true;

        // send the request after building the packet component-wise (idp-payload: 4 words => idp-length = 19 words)
        int bfn0 = (int)((bfn >> 32) & 0xFFFF);
        int bfn1 = (int)((bfn >> 16) & 0xFFFF);
        int bfn2 = (int)(bfn & 0xFFFF);

        // eth: dst
        putWord(0, 0xFFFF);
        putWord(1, 0xFFFF);
        putWord(2, 0xFFFF);

        // eth: src
        putWord(3, (ushort)mac0);
        putWord(4, (ushort)mac1);
        putWord(5, (ushort)mac2);

        // eth: type
        putWord(6, 0x0600);

        // idp: ckSum
        putWord(7, 0xFFFF); // no checksum

        // idp: length
        putWord(8, 38); // payload length (19 words -> 38 bytes)

        // idp: transport control & packet type
        putWord(9, 9); // hop count = 0 & packet type = BOOT_SERVER_PACKET

        // idp: destination endpoint: copy the source destination of the ingone packet
        putWord(10, 0); // local network
        putWord(11, 0);
        putWord(12, 0xFFFF); // broadcast
        putWord(13, 0xFFFF);
        putWord(14, 0xFFFF);
        putWord(15, 10); // BOOT socket

        // idp: source endpoint: put "our" address with the "local" net and "our" socket
        putWord(16, 0); // local network
        putWord(17, 0);
        putWord(18, (ushort)mac0);
        putWord(19, (ushort)mac1);
        putWord(20, (ushort)mac2);
        putWord(21, localSocket); // our socket

        // boot: etherBootPacketType simpleRequest
        putWord(22, 1);

        // boot: bootFileNumber
        putWord(23, (ushort)bfn0);
        putWord(24, (ushort)bfn1);
        putWord(25, (ushort)bfn2);

        // and send it
        sendPacket(zeNet, 30); // real ethernet length is 26 words, but minimal length is 60 bytes

        bool done = false;
        while (!done && ok)
        {
            // wait a bit and check for next packet
            try
            {
                Thread.Sleep(2);
            }
            catch (ThreadInterruptedException)
            {
                break;
            }
            int wLen = recvPacket(zeNet);
            if (wLen == 0)
            {
                continue;
            }

            // is it for us? (i.e.: our mac-address, our socket, etherBootPacketType simpleData and requested bootfile)
            if (getWord(12) == mac0
                && getWord(13) == mac1
                && getWord(14) == mac2
                && getWord(15) == localSocket
                && getWord(22) == 2
                && getWord(23) == bfn0
                && getWord(24) == bfn1
                && getWord(25) == bfn2)
            {
                int payloadWords = (getWord(8) + 1) / 2;
                int pageNo = getWord(26) - 1;
                int pageWordCount = payloadWords - 20; // payload starts at word offset 7, page content starts at word offset 27

                if (pageNo >= 0 && pageNo < pages.Length && pageWordCount == 256)
                {
                    ushort[] page = new ushort[256];
                    for (int i = 0; i < 256; i++)
                    {
                        page[i] = (ushort)getWord(27 + i);
                    }
                    pages[pageNo] = page;
                    if (pageNo > lastPage)
                    {
                        lastPage = pageNo;
                    }
                }
                else if (pageWordCount != 0)
                {
                    ok = false;
                }

                done = (pageWordCount <= 0);
            }
        }

        // transfer the pages into the result
        for (int i = 0; ok && i <= lastPage; i++)
        {
            ushort[]? page = pages[i];
            if (page == null) // this page was lost in transfer...
            {
                germ.Clear();
                ok = false;
            }
            else
            {
                germ.Add(page);
            }
        }

        // done
        zeNet.shutdown();
        return ok;
    }
}
