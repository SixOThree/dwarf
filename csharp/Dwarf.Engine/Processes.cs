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
OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR
OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

namespace Dwarf.Engine;

// Basic data structures and common functions for handling processes in the
// mesa engine as defined in PrincOps chapter "10 Processes".
//
// This class provides the functionality required for implementing the
// process-related instructions including the process scheduler described
// in the PrincOps. It also provides bidirectional synchronization between
// the Dwarf mesa engine and the Dwarf UI:
//
//   UI => mesa engine:
//     the UI calls methods on the agents to inform about key-press changes,
//     mouse movements, etc.; the agent then enqueues a special interrupt
//     which the mesa engine honors as a callback to agents between two
//     instructions, ensuring no other changes occur in main memory while
//     agents copy UI data into the engine's virtual memory.
//
//   mesa engine => UI:
//     at more or less regular intervals, the mesa engine calls back the UI
//     through a registered callback object during the regular timeout scan,
//     again ensuring this synchronization occurs between two instructions.
public static class Processes
{
    /*
     * handler for synchronizing (external/device) changes to mesa memory
     */

    public delegate void MesaMemoryUpdater();

    private static MesaMemoryUpdater? mesaMemoryUpdater = null;
    private static readonly object _updaterLock = new();

    public static void setMesaMemoryUpdater(MesaMemoryUpdater? updater)
    {
        lock (_updaterLock)
        {
            mesaMemoryUpdater = updater;
        }
    }

    /*
     * statistics provider for providing counter data to the ui
     */

    public interface StatisticsProvider
    {
        int getDiskReads();
        int getDiskWrites();
        int getFloppyReads();
        int getFloppyWrites();
        int getNetworkpacketsSent();
        int getNetworkpacketsReceived();
    }

    private sealed class DefaultStatisticsProvider : StatisticsProvider
    {
        public int getDiskReads() => 0;
        public int getDiskWrites() => 0;
        public int getFloppyReads() => 0;
        public int getFloppyWrites() => 0;
        public int getNetworkpacketsSent() => 0;
        public int getNetworkpacketsReceived() => 0;
    }

    private static StatisticsProvider statisticsProvider = new DefaultStatisticsProvider();

    public static void setStatisticsProvider(StatisticsProvider p)
    {
        statisticsProvider = p;
    }

    /*
     * 10.1 Data Structures
     */

    /* Queue, Condition, Monitor, PsbFlags, PsbLink
     *
     * As the members of the data structures are defined at bit-boundaries inside a
     * single word, member access is defined either as FieldSpecs (intra-word field
     * access for members longer than 1 bit) or bit masks (for single-bit members).
     * For each member, accessors retrieve or update the member's value in a word,
     * with the updating methods returning the new word holding the new member value
     * and the other members left unchanged.
     */

    // common position/length of the PsbIndex in: Queue, Condition, Monitor, PsbFlags, PsbLink
    private const int Common_psbIndex_Field = 0x39;

    /*
     *  Queue: word, tail:PsbIndex in bits 3..12
     *  QueueHandler = LONG POINTER TO Queue
     */
    public static ushort getQueue_tail(ushort q) { return Mem.readField(q, Common_psbIndex_Field); }
    public static ushort setQueue_tail(ushort q, int idx) { return Mem.writeField(q, Common_psbIndex_Field, (ushort)idx); }

    /*
     *  Condition: word, tail:PsbIndex in bits 3..12, abortable: bit 14, wakeup: bit 15
     */
    private const int Condition_abortable_Mask = 0x0002;
    private const int Condition_wakeup_Mask = 0x0001;

    public static ushort getCondition_tail(ushort c) { return Mem.readField(c, Common_psbIndex_Field); }
    public static ushort setCondition_tail(ushort c, int idx) { return Mem.writeField(c, Common_psbIndex_Field, (ushort)idx); }

    public static bool isConditionAbortable(ushort w) { return (w & Condition_abortable_Mask) != 0; }
    public static ushort setConditionAbortable(ushort w) { return (ushort)(w | Condition_abortable_Mask); }
    public static ushort unsetConditionAbortable(ushort w) { return (ushort)(w & ~Condition_abortable_Mask); }

    public static bool isConditionWakeup(ushort w) { return (w & Condition_wakeup_Mask) != 0; }
    public static ushort setConditionWakeup(ushort w) { return (ushort)(w | Condition_wakeup_Mask); }
    public static ushort unsetConditionWakeup(ushort w) { return (ushort)(w & ~Condition_wakeup_Mask); }

    /*
     *  Monitor: word, tail:PsbIndex in bits 3..12, available: bits 13..14, locked: bit 15
     */
    private const int Monitor_locked_Mask = 0x0001;

    public static ushort getMonitor_tail(ushort m) { return Mem.readField(m, Common_psbIndex_Field); }
    public static ushort setMonitor_tail(ushort m, int idx) { return Mem.writeField(m, Common_psbIndex_Field, (ushort)idx); }

    public static bool isMonitorLocked(ushort w) { return (w & Monitor_locked_Mask) != 0; }
    public static ushort setMonitorLocked(ushort w) { return (ushort)(w | Monitor_locked_Mask); }
    public static ushort unsetMonitorLocked(ushort w) { return (ushort)(w & ~Monitor_locked_Mask); }

    /*
     *  PsbFlags: word, cleanup:PsbIndex in bits 3..12, waiting: bit 14, abort: bit 15
     */
    private const int PsbFlags_waiting_Mask = 0x0002;
    private const int PsbFlags_abort_Mask = 0x0001;

    public static ushort getPsbFlags_cleanup(ushort f) { return Mem.readField(f, Common_psbIndex_Field); }
    public static ushort setPsbFlags_cleanup(ushort f, int idx) { return Mem.writeField(f, Common_psbIndex_Field, (ushort)idx); }

    public static bool isPsbFlagsWaiting(ushort w) { return (w & PsbFlags_waiting_Mask) != 0; }
    public static ushort setPsbFlagsWaiting(ushort w) { return (ushort)(w | PsbFlags_waiting_Mask); }
    public static ushort unsetPsbFlagsWaiting(ushort w) { return (ushort)(w & ~PsbFlags_waiting_Mask); }

    public static bool isPsbFlagsAbort(ushort w) { return (w & PsbFlags_abort_Mask) != 0; }
    public static ushort setPsbFlagsAbort(ushort w) { return (ushort)(w | PsbFlags_abort_Mask); }
    public static ushort unsetPsbFlagsAbort(ushort w) { return (ushort)(w & ~PsbFlags_abort_Mask); }

    /*
     *  PsbLink: word, priority: bits 0..2, next:PsbIndex in bits 3..12,
     *  failed: bit 13, permanent: bit 14, preempted: bit 15
     */
    private const int PsbLink_priority_Field = 0x02;
    private const int PsbLink_failed_Mask = 0x0004;
    private const int PsbLink_permanent_Mask = 0x0002;
    private const int PsbLink_preempted_Mask = 0x0001;

    public static ushort getPsbLink_priority(ushort l) { return Mem.readField(l, PsbLink_priority_Field); }
    public static ushort setPsbLink_priority(ushort l, int prio) { return Mem.writeField(l, PsbLink_priority_Field, (ushort)prio); }

    public static ushort getPsbLink_next(ushort l) { return Mem.readField(l, Common_psbIndex_Field); }
    public static ushort setPsbLink_next(ushort l, int idx) { return Mem.writeField(l, Common_psbIndex_Field, (ushort)idx); }

    public static bool isPsbLinkFailed(ushort w) { return (w & PsbLink_failed_Mask) != 0; }
    public static ushort setPsbLinkFailed(ushort w) { return (ushort)(w | PsbLink_failed_Mask); }
    public static ushort unsetPsbLinkFailed(ushort w) { return (ushort)(w & ~PsbLink_failed_Mask); }

    public static bool isPsbLinkPermanent(ushort w) { return (w & PsbLink_permanent_Mask) != 0; }
    public static ushort setPsbLinkPermanent(ushort w) { return (ushort)(w | PsbLink_permanent_Mask); }
    public static ushort unsetPsbLinkPermanent(ushort w) { return (ushort)(w & ~PsbLink_permanent_Mask); }

    public static bool isPsbLinkPreempted(ushort w) { return (w & PsbLink_preempted_Mask) != 0; }
    public static ushort setPsbLinkPreempted(ushort w) { return (ushort)(w | PsbLink_preempted_Mask); }
    public static ushort unsetPsbLinkPreempted(ushort w) { return (ushort)(w & ~PsbLink_preempted_Mask); }

    /*
     *  ProcessStateBlock member offsets
     */
    public const int ProcessStateBlock_link = 0;      // PsbLink
    public const int ProcessStateBlock_flags = 1;     // PsbFlags
    public const int ProcessStateBlock_context = 2;   // POINTER (to frame or state-vector)
    public const int ProcessStateBlock_timeout = 3;   // Ticks
    public const int ProcessStateBlock_mds = 4;       // CARDINAL (high order 16 bits of MDS register)
    public const int ProcessStateBlock_available = 5; // UNSPECIFIED (unused)
    public const int ProcessStateBlock_sticky = 6;    // LONG UNSPECIFIED (for floating point ops)
    public const int ProcessStateBlock_Size = 8;

    // StateAllocationTable: TYPE = ARRAY Priority OF POINTER TO StateVector
    public const int StateAllocationTable_Size = 8; // 8 words: 8 priorities => 8 POINTERs

    // InterruptVector: TYPE = ARRAY InterruptLevel OF InterruptItem
    // InterruptLevel: TYPE = [0 .. WordSize)
    // InterruptItem: TYPE = MACHINE DEPENDENT RECORD [condition (0): Condition, available (1): UNSPECIFIED]
    private const int InterruptLevels = 16;
    private const int InterruptItem_condition = 0;
    private const int InterruptItem_available = 1;
    public const int InterruptItem_Size = 2;
    public const int InterruptVector_Size = InterruptItem_Size * InterruptLevels;

    // FaultVector: TYPE = ARRAY Faultlndex OF FaultQueue
    // Faultlndex: TYPE = [O..8);
    // FaultQueue: TYPE = MACHINE DEPENDENT RECORD [queue (0): Queue, condition (1): Condition];
    private const int FaultQueue_Size = 2;
    private const int FaultVector_queue = 0;
    private const int FaultVector_condition = 1;
    public const int FaultVector_Size = 16; // 8 indices x 2 words

    // header area in the ProcessDataArea overlaid over the first entries
    // these are LONG POINTERs (Cpu.PDA must be a const).
    public const int PDA_LP_header_ready = Cpu.PDA;
    public const int PDA_LP_header_count = Cpu.PDA + 1;
    public const int PDA_LP_header_state = Cpu.PDA + 8;
    public const int PDA_LP_header_interrupt = Cpu.PDA + 8 + StateAllocationTable_Size;
    public const int PDA_LP_header_fault = Cpu.PDA + 8 + StateAllocationTable_Size + InterruptVector_Size;

    public const int ProcessDataArea_header_Size =
          1   // ready: Queue (1 word)
        + 1   // count: CARDINAL
        + 1   // unused: UNSPECIFIED
        + 5   // available: ARRAY [0..5) OF UNSPECIFIED
        + StateAllocationTable_Size
        + InterruptVector_Size
        + FaultVector_Size; // 64 words total == 8 PDA entries out of 1024

    // PsbIndex ; 0..1023
    public const int PsbIndex_Max = 1024;
    public const ushort PsbNull = 0;
    public const int PsbStart = (ProcessDataArea_header_Size + ProcessStateBlock_Size - 1) / ProcessStateBlock_Size;

    public static /* LONG POINTER */ int lengthenPdaPtr(/* POINTER */ int ptr)
    {
        return Cpu.PDA + (ptr & 0xFFFF);
    }

    public static /* POINTER */ ushort offsetPda(/* LONG POINTER */ int ptr)
    {
        if (((ptr - Cpu.PDA) & unchecked((int)0xFFFF0000)) != 0) { Cpu.ERROR("offsetPda :: highHalf(ptr - Cpu.PDA) != 0"); }
        return (ushort)((ptr - Cpu.PDA) & 0x0000FFFF);
    }

    public static /* word */ ushort readPdaWord(/* POINTER */ int ptr)
    {
        return Mem.readWord(lengthenPdaPtr(ptr));
    }

    public static void writePdaWord(/* POINTER */ int ptr, ushort word)
    {
        Mem.writeWord(lengthenPdaPtr(ptr), word);
    }

    public static int psbHandle(int index)
    {
        return (index & 0x03FF) * ProcessStateBlock_Size; // limit to 0..1023
    }

    public static ushort psbIndex(int handle)
    {
        return (ushort)((handle & 0x0000FFFF) / ProcessStateBlock_Size); // TODO: !! this may deliver >= 1024 ??
    }

    //	// get PsbIndex from Queue | PsbLink | Condition | Monitor
    //	private static int psbIndexOf(short item) {
    //		return readField(item, Queue_tail);
    //	}

    private static ushort readPSBword(int index, int offset)
    {
        int ptr = Cpu.PDA + ((index & 0x0000FFFF) * ProcessStateBlock_Size) + offset;
        return Mem.readWord(ptr);
    }

    private static void writePSBword(int index, int offset, ushort word)
    {
        int ptr = Cpu.PDA + ((index & 0x0000FFFF) * ProcessStateBlock_Size) + offset;
        Mem.writeWord(ptr, word);
    }

    /*
     * access utils for members of the PSB at index
     */

    public static ushort fetchPSB_link(int index)
    {
        return readPSBword(index, ProcessStateBlock_link);
    }

    public static void storePSB_link(int index, ushort value)
    {
        writePSBword(index, ProcessStateBlock_link, value);
    }

    public static ushort fetchPSB_flags(int index)
    {
        return readPSBword(index, ProcessStateBlock_flags);
    }

    public static void storePSB_flags(int index, ushort value)
    {
        writePSBword(index, ProcessStateBlock_flags, value);
    }

    public static ushort fetchPSB_context(int index)
    {
        return readPSBword(index, ProcessStateBlock_context);
    }

    public static void storePSB_context(int index, ushort value)
    {
        writePSBword(index, ProcessStateBlock_context, value);
    }

    public static ushort fetchPSB_timeout(int index)
    {
        return readPSBword(index, ProcessStateBlock_timeout);
    }

    public static void storePSB_timeout(int index, ushort value)
    {
        writePSBword(index, ProcessStateBlock_timeout, value);
    }

    public static ushort fetchPSB_mds(int index)
    {
        return readPSBword(index, ProcessStateBlock_mds);
    }

    public static void storePSB_mds(int index, ushort value)
    {
        writePSBword(index, ProcessStateBlock_mds, value);
    }

    /*
     * process support routines
     */

    /*
     * 10.2.1 Monitor Entry
     */

    public static void enterFailed(/* LONG POINTER TO Monitor */ int m)
    {
        ushort link = fetchPSB_link(Cpu.PSB);
        link = setPsbLinkFailed(link);
        storePSB_link(Cpu.PSB, link);
        requeue(PDA_LP_header_ready, m, Cpu.PSB);
        reschedule(false);
    }

    /*
     * 10.2.2 Monitor Exit
     */

    public static bool exit(/* LONG POINTER TO Monitor */ int m)
    {
        ushort mon = Mem.readWord(m);
        if (!isMonitorLocked(mon)) { Cpu.ERROR("exit :: exiting non-locked monitor"); }
        mon = unsetMonitorLocked(mon);
        Mem.writeWord(m, mon);

        ushort mon_tail = getMonitor_tail(mon);
        bool needsRequeue = (mon_tail != PsbNull);
        if (needsRequeue)
        {
            ushort link = fetchPSB_link(mon_tail);
            requeue(m, PDA_LP_header_ready, getPsbLink_next(link));
        }
        return needsRequeue;
    }

    /*
     * 10.2.5 Notify and Broadcast
     */

    public static void wakeHead(/* LONG POINTER TO Condition */ int c)
    {
        ushort cond = Mem.readWord(c);
        ushort link = fetchPSB_link(getCondition_tail(cond));
        ushort link_next = getPsbLink_next(link);
        ushort flags = fetchPSB_flags(link_next);
        flags = unsetPsbFlagsWaiting(flags);
        storePSB_flags(link_next, flags);
        storePSB_timeout(link_next, 0);
        requeue(c, PDA_LP_header_ready, link_next);
    }

    /*
     * 10.3.1 Queuing Procedures
     */

    public static void requeue(int src, int dst, ushort psb)
    {
        if (psb == PsbNull) { Cpu.ERROR("requeue :: psb == psnBull"); }
        dequeue(src, psb);
        enqueue(dst, psb);
    }

    private static void dequeue(int src, ushort psb)
    {
        ushort queue = 0;
        int que = src;
        if (que != 0) { queue = Mem.readWord(que); }

        ushort prev = PsbNull; // PsbIndex
        ushort link = fetchPSB_link(psb);
        if (getPsbLink_next(link) != psb)
        {
            ushort temp; // PsbLink
            prev = (que == 0) ? psb : getQueue_tail(queue);
            while (true)
            {
                temp = fetchPSB_link(prev);
                if (getPsbLink_next(temp) == psb) { break; }
                prev = getPsbLink_next(temp);
            }
            temp = setPsbLink_next(temp, getPsbLink_next(link));
            storePSB_link(prev, temp);
        }

        if (que == 0)
        {
            ushort flags = fetchPSB_flags(psb);
            flags = setPsbFlags_cleanup(flags, getPsbLink_next(link));
            storePSB_flags(psb, flags);
        }
        else if (getQueue_tail(queue) == psb)
        {
            queue = setQueue_tail(queue, prev);
            Mem.writeWord(que, queue);
        }
    }

    private static void enqueue(int dst, ushort psb)
    {
        int que = dst;
        ushort queue = Mem.readWord(que);
        ushort link = fetchPSB_link(psb);

        if (getQueue_tail(queue) == PsbNull)
        {
            link = setPsbLink_next(link, psb);
            storePSB_link(psb, link);
            queue = setQueue_tail(queue, psb);
            Mem.writeWord(que, queue);
        }
        else
        {
            ushort prev = getQueue_tail(queue);
            ushort currentlink = fetchPSB_link(prev);
            if (getPsbLink_priority(currentlink) >= getPsbLink_priority(link))
            {
                queue = setQueue_tail(queue, psb);
                Mem.writeWord(que, queue);
            }
            else
            {
                while (true)
                {
                    ushort nextlink = fetchPSB_link(getPsbLink_next(currentlink));
                    if (getPsbLink_priority(link) > getPsbLink_priority(nextlink)) { break; }
                    prev = getPsbLink_next(currentlink);
                    currentlink = nextlink;
                }
            }
            link = setPsbLink_next(link, getPsbLink_next(currentlink));
            storePSB_link(psb, link);
            currentlink = setPsbLink_next(currentlink, psb);
            storePSB_link(prev, currentlink);
        }
    }

    /*
     * 10.3.2 Cleanup Links
     */

    public static void cleanupCondition(/* LONG POINTER TO Condition */ int c)
    {
        ushort cond = Mem.readWord(c);
        ushort psb = getCondition_tail(cond);
        if (psb != PsbNull)
        {
            ushort flags = fetchPSB_flags(psb);
            if (getPsbFlags_cleanup(flags) != PsbNull)
            {
                while (true)
                {
                    if (getPsbFlags_cleanup(flags) == psb)
                    {
                        cond = unsetConditionWakeup(cond);
                        cond = setCondition_tail(cond, PsbNull);
                        Mem.writeWord(c, cond);
                        return;
                    }
                    psb = getPsbFlags_cleanup(flags);
                    flags = fetchPSB_flags(psb);
                    if (getPsbFlags_cleanup(flags) == PsbNull) { break; }
                }
                ushort head = psb;
                while (true)
                {
                    ushort link = fetchPSB_link(psb);
                    if (getPsbLink_next(link) == head) { break; }
                    psb = getPsbLink_next(link);
                }
                cond = setCondition_tail(cond, psb);
                Mem.writeWord(c, cond);
            }
        }
    }

    /*
     * 10.4.1 Scheduler
     */

    public static void reschedule(bool preemption)
    {
        if (Cpu.running)
        {
            saveProcess(preemption);
        }

        ushort queue = Mem.readWord(PDA_LP_header_ready);
        ushort queueTail = getQueue_tail(queue);
        if (queueTail == PsbNull)
        {
            rescheduleBusyWait();
            return;
        }

        ushort link = fetchPSB_link(queueTail);
        ushort psb;
        while (true)
        {
            psb = getPsbLink_next(link);
            link = fetchPSB_link(psb);
            if (isPsbLinkPermanent(link) || isPsbLinkPreempted(link) || !emptyState(getPsbLink_priority(link)))
            {
                break;
            }
            if (psb == queueTail)
            {
                rescheduleBusyWait();
                return;
            }
        }

        Cpu.PSB = psb;
        Cpu.savedPC = 0;
        Cpu.PC = 0;
        Cpu.LF = loadProcess();
        Cpu.running = true;
        Xfer.impl.xfer(Cpu.LF, 0, Xfer.XferType.xprocessSwitch, false);
    }

    private static void rescheduleBusyWait()
    {
        if (!interruptsEnabled()) { Cpu.rescheduleError(); }
        Cpu.running = false;
    }

    private static void saveProcess(bool preemption)
    {
        // start of: BEGIN ENABLE Abort => ERROR;
        try
        {
            ushort link = fetchPSB_link(Cpu.PSB);
            bool link_permanent = isPsbLinkPermanent(link);
            if (Cpu.validContext())
            {
                Mem.writeMDSWord(Cpu.LF, PrincOpsDefs.LocalOverhead_pc, Cpu.PC);
            }
            if (preemption)
            {
                link = setPsbLinkPreempted(link);
                int state = (!link_permanent)
                    ? allocState(getPsbLink_priority(link))
                    : lengthenPdaPtr(fetchPSB_context(Cpu.PSB));
                Cpu.saveStack(state);
                Mem.writeWord(state + Cpu.StateVector_frame, (ushort)(Cpu.LF & 0xFFFF));
                if (!link_permanent)
                {
                    storePSB_context(Cpu.PSB, offsetPda(state));
                }
            }
            else
            {
                link = unsetPsbLinkPreempted(link);
                if (!link_permanent)
                {
                    storePSB_context(Cpu.PSB, (ushort)(Cpu.LF & 0xFFFF));
                }
                else
                {
                    int state = lengthenPdaPtr(fetchPSB_context(Cpu.PSB));
                    Mem.writeWord(state + Cpu.StateVector_frame, (ushort)(Cpu.LF & 0xFFFF));
                }
            }
            storePSB_link(Cpu.PSB, link);
        }
        catch (Cpu.MesaAbort)
        {
            // end of: BEGIN ENABLE Abort => ERROR;
            Cpu.ERROR("saveProcess :: received Abort-exception");
        }
    }

    private static int /* LF */ loadProcess()
    {
        // start of: BEGIN ENABLE Abort => ERROR;
        try
        {
            ushort link = fetchPSB_link(Cpu.PSB);
            bool link_permanent = isPsbLinkPermanent(link);
            ushort frame = fetchPSB_context(Cpu.PSB);
            if (isPsbLinkPreempted(link))
            {
                int state = lengthenPdaPtr(frame);
                Cpu.loadStack(state);
                frame = Mem.readWord(state + Cpu.StateVector_frame);
                if (!link_permanent)
                {
                    freeState(getPsbLink_priority(link), state);
                }
            }
            else
            {
                if (isPsbLinkFailed(link))
                {
                    Cpu.push(PrincOpsDefs.FALSE);
                    link = unsetPsbLinkFailed(link);
                    storePSB_link(Cpu.PSB, link);
                }
                if (isPsbLinkPermanent(link))
                {
                    int state = lengthenPdaPtr(frame);
                    frame = Mem.readWord(state + Cpu.StateVector_frame);
                }
            }
            int mds = fetchPSB_mds(Cpu.PSB);
            Cpu.MDS = mds << PrincOpsDefs.WORD_BITS;
            return frame;
        }
        catch (Cpu.MesaAbort)
        {
            // end of: BEGIN ENABLE Abort => ERROR;
            Cpu.ERROR("loadProcess :: received Abort-exception");
            return -1; // keep compiler happy (Cpu.ERROR does not return)
        }
    }

    /*
     * 10.4.2.2 State Vector Allocation
     */

    private static bool emptyState(int pri)
    {
        ushort state = Mem.readWord(PDA_LP_header_state + (pri & 0x0007));
        return state == 0;
    }

    private static /* StateHandle */ int allocState(int pri)
    {
        int statesCell = PDA_LP_header_state + (pri & 0x0007);
        ushort offset = Mem.readWord(statesCell);
        if (offset == 0) { Cpu.ERROR("allocState :: offset == 0 for priority " + pri); }
        int state = lengthenPdaPtr(offset);
        Mem.writeWord(statesCell, Mem.readWord(state));
        return state;
    }

    private static void freeState(int pri, /* StateHandle */ int state)
    {
        int statesCell = PDA_LP_header_state + (pri & 0x0007);
        Mem.writeWord(state, Mem.readWord(statesCell));
        Mem.writeWord(statesCell, offsetPda(state));
    }

    /*
     * 10.4.3 Faults
     */

    public static int faultOne(int fi, ushort parameter)
    {
        int psb = fault(fi);
        ushort state = fetchPSB_context(psb);
        writePdaWord(state + Cpu.StateVector_data, parameter);
        throw new Cpu.MesaAbort();
    }

    public static int faultTwo(int fi, int parameter)
    {
        int psb = fault(fi);
        ushort state = fetchPSB_context(psb);
        writePdaWord(state + Cpu.StateVector_data, (ushort)(parameter & 0x0000FFFF));
        writePdaWord(state + Cpu.StateVector_data + 1, (ushort)(parameter >>> PrincOpsDefs.WORD_BITS));
        throw new Cpu.MesaAbort();
    }

    private static /* PsbIndex */ int fault(int fi)
    {
        ushort faulted = Cpu.PSB;
        int lpFaultIndexCell = PDA_LP_header_fault + (fi * FaultQueue_Size);
        requeue(PDA_LP_header_ready, lpFaultIndexCell + FaultVector_queue, faulted);
        notifyWakeup(lpFaultIndexCell + FaultVector_condition);
        Cpu.PC = Cpu.savedPC;
        Cpu.SP = Cpu.savedSP;
        reschedule(true);
        return faulted;
    }

    /*
     * Extension to PrincOps: section "4.1 Interpreter" defines that instructions
     * are executed only if the cpu state is running, but continuing to check for
     * interrupts and timeouts. This creates a very tight loop in the interpreter
     * while most of the time there are no interrupts or timeouts to honor,
     * needlessly consuming (real hardware) CPU. For this reason, a real idling
     * mechanism is used in Dwarf's mesa engine, putting the interpreter thread
     * to sleep for a limited time. PrincOps requirements (interrupt responsiveness
     * and maximum timeout scan intervals) are met by restarting the interpreter
     * (ending sleep) when an interrupt is enqueued and limiting the sleep time
     * to a time frame below the required timeout scan interval.
     */

    private const int NOT_RUNNING_SLEEP_MSECS = 2;

    private static readonly object _idleLock = new();

    // Hold execution for a limited time, restarting execution when an
    // interrupt is enqueued.
    public static void idle()
    {
        lock (_idleLock)
        {
            try
            {
                System.Threading.Monitor.Wait(_idleLock, NOT_RUNNING_SLEEP_MSECS);
            }
            catch (ThreadInterruptedException)
            {
                // ignored
            }
        }
    }

    /*
     * 10.4.4 Interrupts
     */

    public static bool interruptPending()
    {
        return (Cpu.WP.get() != 0) && interruptsEnabled();
    }

    public static bool checkforInterrupts()
    {
        if (interruptPending())
        {
            return interrupt();
        }
        else
        {
            return false;
        }
    }

    // special interrupts for starting / stopping flight recorder on the fly :-)
    private const int FLIGHTRECORDER_START = 0x08000000;
    private const int FLIGHTRECORDER_STOP_AND_DUMP = 0x04000000;

    public static void requestFlightRecorderStart()
    {
        Console.WriteLine("requestFlightRecorderStart()");
        Console.Out.Flush();
        innerRequestInterrupt(FLIGHTRECORDER_START);
    }

    public static void requestFlightRecorderStopAndDump()
    {
        Console.WriteLine("requestFlightRecorderStopAndDump()");
        Console.Out.Flush();
        innerRequestInterrupt(FLIGHTRECORDER_STOP_AND_DUMP);
    }

    // special interrupt for agents requesting to access the mesa virtual memory
    private const int DATA_REFRESH_INTERRUPT = 0x40000000;

    // special interrupt requesting to stop the mesa engine (e.g. by a UI button)
    private const int EXTERNAL_STOP_INTERRUPT = 0x10000000;

    private static void innerRequestInterrupt(int intMask)
    {
        int oldWP = Cpu.WP.get();
        int newWP = oldWP | intMask;
        while (!Cpu.WP.compareAndSet(oldWP, newWP))
        {
            oldWP = Cpu.WP.get();
            newWP = oldWP | intMask;
        }
        lock (_idleLock)
        {
            System.Threading.Monitor.PulseAll(_idleLock);
        }
    }

    // Enqueue one or more standard interrupts to the mesa engine.
    public static void requestMesaInterrupt(ushort intMask)
    {
        innerRequestInterrupt(intMask & 0xFFFF);
    }

    // Enqueue a data refresh interrupt for processing pending data transfers
    // from agents to mesa virtual memory.
    public static void requestDataRefresh()
    {
        innerRequestInterrupt(DATA_REFRESH_INTERRUPT);
    }

    // Enqueue the request to stop the mesa engine.
    public static void requestMesaEngineStop()
    {
        innerRequestInterrupt(EXTERNAL_STOP_INTERRUPT);
    }

    public static bool interrupt()
    {
        ushort mask = 1;
        bool needsRequeue = false;

        // atomically get the wake-up bits
        int pendingWakeups = Cpu.WP.get();
        while (!Cpu.WP.compareAndSet(pendingWakeups, 0))
        {
            pendingWakeups = Cpu.WP.get();
        }
        if (pendingWakeups == 0) { return false; }

        // the mesa PrincOps wakeups (subset of all possible interrupts to the mesa engine)
        ushort wakeups = (ushort)(pendingWakeups & 0xFFFF);

        // flight-recorder requests
        if (Config.LOG_OPCODES && Config.LOG_OPCODES_AS_FLIGHTRECORDER)
        {
            if ((pendingWakeups & FLIGHTRECORDER_START) != 0)
            {
                Cpu.armDebugInterpreter();
            }
            if ((pendingWakeups & FLIGHTRECORDER_STOP_AND_DUMP) != 0)
            {
                Cpu.dumpOplog();
                Cpu.disarmDebugInterpreter();
            }
        }

        // is a "stop the engine" request pending?
        if ((pendingWakeups & EXTERNAL_STOP_INTERRUPT) != 0)
        {
            throw new Cpu.MesaStopped("Mesa engine stopped by external request");
        }

        // ensure that the mesa memory has all in-gone external data if requested
        if ((pendingWakeups & DATA_REFRESH_INTERRUPT) != 0 && mesaMemoryUpdater != null)
        {
            mesaMemoryUpdater();
        }

        // atomically get the wake-up bits possibly added during import of external data
        pendingWakeups = Cpu.WP.get();
        while (!Cpu.WP.compareAndSet(pendingWakeups, 0))
        {
            pendingWakeups = Cpu.WP.get();
        }
        ushort newWakeups = (ushort)(pendingWakeups & 0xFFFF);
        //		if (newWakeups != 0) {
        //			System.out.printf("#+#+#+# new interrupts after memory update: 0x%04X\n", newWakeups);
        //		}
        wakeups |= newWakeups;

        // if no mesa wakeups pending => done
        if (wakeups == 0)
        {
            return false;
        }

        // notify the conditions to be woken up and request requeuing if necessary
        for (int i = InterruptLevels - 1; i >= 0; i--)
        {
            if ((wakeups & mask) != 0)
            {
                needsRequeue |= notifyWakeup(PDA_LP_header_interrupt + (i * InterruptItem_Size) + InterruptItem_condition);
            }
            mask <<= 1;
        }
        return needsRequeue;
    }

    public static bool notifyWakeup(/* LONG POINTER TO Condition */ int c)
    {
        bool needsRequeue = false;
        cleanupCondition(c);
        ushort cond = Mem.readWord(c);
        if (getCondition_tail(cond) == PsbNull)
        {
            cond = setConditionWakeup(cond);
            Mem.writeWord(c, cond);
        }
        else
        {
            wakeHead(c);
            needsRequeue = true;
        }
        return needsRequeue;
    }

    /*
     * 10.4.4.3 Disabling Interrupts
     */

    public static void enableInterrupts()
    {
        Cpu.WDC = (ushort)Math.Max(0, Cpu.WDC - 1);
    }

    public static void disableInterrupts()
    {
        Cpu.WDC++;
    }

    public static bool interruptsEnabled()
    {
        return Cpu.WDC == 0;
    }

    /*
     * 10.4.5 Timeouts
     */

    // Ticks: TYPE = CARDINAL
    // TimeOutInterval: TYPE = LONG POINTER

    private static int time = 0;

    public static void resetPTC(int to)
    {
        Cpu.PTC = to;
        time = Cpu.IT();
    }

    // UI refreshing:
    //  -> 25 screen refreshes per second means one refresh each 40 ms
    //  -> 5 statistics refreshes per second means ~ 1 refresh after 5 screen refreshes
    private const long UI_REFRESH_INTERVAL = 37; // milliseconds
    private const int STATS_REFRESH_INTERVAL = 5;
    private static long nextUiRefresh = 0;
    private static int lastMpNotified = -1;
    private static int statisticsThrottle = STATS_REFRESH_INTERVAL;

    private static volatile iMesaMachineDataAccessor? displayRefresher = null;

    private static ushort[]? dummyPageFlags = null;

    public static void registerUiRefreshCallback(iMesaMachineDataAccessor refresher)
    {
        displayRefresher = refresher;
    }

    // The invoker must throttle usage of this method, for optimizing to avoid
    // checking too often as Environment.TickCount64 / DateTime.UtcNow effectively
    // slow things down...
    public static bool checkForTimeouts()
    {
        // Dwarf implementation specific part: refresh UI at (more or less) regular intervals

        // ensure that the mesa memory has all in-gone external data
        if (mesaMemoryUpdater != null)
        {
            mesaMemoryUpdater();
        }

        // cyclically refresh the ui
        long now = Environment.TickCount64;
        if (now > nextUiRefresh)
        {
            // set next refresh wakeup timestamp
            nextUiRefresh = now + UI_REFRESH_INTERVAL;

            // refresh if we have a connected UI
            iMesaMachineDataAccessor? refresher = displayRefresher;
            if (refresher != null)
            {
                // notify MP if changed
                int currMP = Cpu.getMP();
                if (currMP != lastMpNotified)
                {
                    refresher.acceptMP(currMP);
                    lastMpNotified = currMP;
                }

                // notify statistics at a lower pace
                if (--statisticsThrottle <= 0)
                {
                    refresher.acceptStatistics(
                        Cpu.insns,
                        statisticsProvider.getDiskReads(),
                        statisticsProvider.getDiskWrites(),
                        statisticsProvider.getFloppyReads(),
                        statisticsProvider.getFloppyWrites(),
                        statisticsProvider.getNetworkpacketsReceived(),
                        statisticsProvider.getNetworkpacketsSent());
                    statisticsThrottle = STATS_REFRESH_INTERVAL;
                }

                // refresh screen, handling the case when the display memory is not mapped into virtual memory
                ushort[] vPageFlags = Mem.pageFlags;
                if (Mem.displayFirstMappedVirtualPage == 0)
                {
                    if (dummyPageFlags == null)
                    {
                        dummyPageFlags = new ushort[Mem.getDisplayPageSize()];
                        for (int i = 0; i < dummyPageFlags.Length; i++)
                        {
                            dummyPageFlags[i] = PrincOpsDefs.MAPFLAGS_REFERENCED | PrincOpsDefs.MAPFLAGS_DIRTY;
                        }
                    }
                    vPageFlags = dummyPageFlags;
                }
                else
                {
                    dummyPageFlags = null;
                }
                refresher.accessRealMemory(
                    Mem.getDisplayRealMemory(),
                    Mem.getDisplayRealPage() * PrincOpsDefs.WORDS_PER_PAGE,
                    Mem.getDisplayPageSize() * PrincOpsDefs.WORDS_PER_PAGE,
                    vPageFlags,
                    Mem.displayFirstMappedVirtualPage
                );
                Mem.resetDisplayPagesFlags();
            }
        }

        // PrincOps part
        int temp = Cpu.IT();
        if (interruptsEnabled() && (temp - time) > Cpu.TimeOutInterval)
        {
            time = temp;
            Cpu.PTC = (Cpu.PTC + 1) & 0xFFFF; // Cpu.PTC++;
            if (Cpu.PTC == 0) { Cpu.PTC++; }
            return timeoutScan();
        }
        else
        {
            return false;
        }
    }

    private static bool timeoutScan()
    {
        bool needsRequeue = false;
        int count = Mem.readWord(PDA_LP_header_count) & 0xFFFF;
        for (ushort psb = PsbStart; psb < (PsbStart + count); psb++)
        {
            int timeout = fetchPSB_timeout(psb) & 0xFFFF;
            if (timeout != 0 && timeout == Cpu.PTC)
            {
                ushort flags = fetchPSB_flags(psb);
                flags = unsetPsbFlagsWaiting(flags);
                storePSB_flags(psb, flags);
                storePSB_timeout(psb, 0);
                requeue(0, PDA_LP_header_ready, psb);
                needsRequeue = true;
            }
        }
        return needsRequeue;
    }

    /*
     * dump utilities for Cpu-debugger
     */

    public static void dumpQueueChain(string header, int lpQueue)
    {
        ushort count = readPdaWord(PDA_LP_header_count);
        ushort queue = readPdaWord(lpQueue);
        int idx = getPsbLink_next(queue);
        int queueIdx = idx;
        Cpu.logf("             {0} - queue 0x{1:X4} at 0x{2:X8}\n", header, queue, lpQueue);
        while (true)
        {
            ushort link = fetchPSB_link(idx);
            ushort flags = fetchPSB_flags(idx);
            ushort context = fetchPSB_context(idx);
            ushort timeout = fetchPSB_timeout(idx);
            ushort mds = fetchPSB_mds(idx);
            Cpu.logf("               PSB[{0}] : link = 0x{1:X4} , flags = 0x{2:X4} , context = 0x{3:X4} , timeout = 0x{4:X4} , mds = 0x{5:X4}\n",
                idx, link, flags, context, timeout, mds);
            Cpu.logf("                      link[ priority = {0} , next = {1,3} , {2}failed , {3}permanent , {4}preempted ]\n",
                getPsbLink_priority(link),
                getPsbLink_next(link),
                isPsbLinkFailed(link) ? "+" : "-",
                isPsbLinkPermanent(link) ? "+" : "-",
                isPsbLinkPreempted(link) ? "+" : "-"
            );
            Cpu.logf("                     flags[ cleanup = {0,3} , {1}waiting , {2}abort ]\n",
                getPsbFlags_cleanup(flags),
                isPsbFlagsWaiting(flags) ? "+" : "-",
                isPsbFlagsAbort(flags) ? "+" : "-");

            ushort next = fetchPSB_link(idx);
            Cpu.logf("               [{0}] -> 0x{1:X4}\n", idx, next);
            idx = getPsbLink_next(next);
            if (idx == queueIdx) { break; }
            if (idx >= (count + PsbStart))
            {
                Cpu.logf("        ## next idx({0}) >= count({1})+PsbStart({2}) ##\n", idx, count, PsbStart);
                break;
            }
        }
    }

    public static void dumpProcessStatusArea()
    {
        ushort count = readPdaWord(PDA_LP_header_count);
        ushort ready = readPdaWord(PDA_LP_header_ready);
        Cpu.logf("\n        ** ProcessStatusArea: PDA = 0x{0:X8} , header_size = {1} , 1st PSB-idx = {2} , ready = 0x{3:X4} , count = {4:D3}\n",
            Cpu.PDA,
            ProcessDataArea_header_Size,
            PsbStart,
            ready,
            count);

        int idx = getPsbLink_next(ready);
        int readyIdx = idx;
        Cpu.logf("             ready queue (indexes):\n");
        Cpu.logf("               0x{0:X4}\n", ready);
        while (true)
        {
            ushort next = fetchPSB_link(idx);
            Cpu.logf("               [{0}] -> 0x{1:X4}\n", idx, next);
            idx = getPsbLink_next(next);
            if (idx == readyIdx) { break; }
            if (idx >= (count + PsbStart))
            {
                Cpu.logf("        ## next idx >= count+PsbStart ##\n");
                break;
            }
        }

        for (int i = PsbStart; i < PsbStart + count; i++)
        {
            ushort link = fetchPSB_link(i);
            ushort flags = fetchPSB_flags(i);
            ushort context = fetchPSB_context(i);
            ushort timeout = fetchPSB_timeout(i);
            ushort mds = fetchPSB_mds(i);
            Cpu.logf("         PSB[{0}] : link = 0x{1:X4} , flags = 0x{2:X4} , context = 0x{3:X4} , timeout = 0x{4:X4} , mds = 0x{5:X4}\n",
                i, link, flags, context, timeout, mds);
            Cpu.logf("                      link[ priority = {0} , next = {1,3} , {2}failed , {3}permanent , {4}preempted ]\n",
                getPsbLink_priority(link),
                getPsbLink_next(link),
                isPsbLinkFailed(link) ? "+" : "-",
                isPsbLinkPermanent(link) ? "+" : "-",
                isPsbLinkPreempted(link) ? "+" : "-"
            );
            Cpu.logf("                     flags[ cleanup = {0,3} , {1}waiting , {2}abort ]\n",
                getPsbFlags_cleanup(flags),
                isPsbFlagsWaiting(flags) ? "+" : "-",
                isPsbFlagsAbort(flags) ? "+" : "-");
        }
    }
}
