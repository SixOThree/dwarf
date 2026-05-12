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

// Process scheduling primitives.
//
// Phase B stub: only the helpers needed by Ch03_Memory_Organization opcodes
// and the trap dispatch path are implemented (psbHandle, psbIndex, resetPTC,
// faultOne, faultTwo stubs). The full scheduler — ready/wait queues, monitor
// entry/exit, condition wait/notify, timeout handling, UI refresh — lands in
// Phase C.
public static class Processes
{
    public const int ProcessStateBlock_Size = 8;

    private static int time;

    // Convert a PSB index (0..1023) into the PsbHandle (byte offset) form used
    // by mesa opcodes that read/write the running PSB register.
    public static int psbHandle(int index)
    {
        return (index & 0x03FF) * ProcessStateBlock_Size; // limit to 0..1023
    }

    // Decode a PsbHandle back into a PSB index.
    public static ushort psbIndex(int handle)
    {
        // Java upstream notes a TODO: this may deliver >= 1024. Faithful port.
        return (ushort)((handle & 0x0000FFFF) / ProcessStateBlock_Size);
    }

    // For opcode WRPTC.
    public static void resetPTC(int to)
    {
        Cpu.PTC = to;
        time = Cpu.IT();
    }

    // Trap helpers — invoked by the real fault thrower in Phase C.
    // Stubs throw so a Phase A/B caller hitting this knows it's incomplete.
    public static void faultOne(int faultQueueIndex, ushort parameter)
    {
        throw new NotImplementedException("Processes.faultOne: Phase C — full scheduler needed");
    }

    public static void faultTwo(int faultQueueIndex, int parameter)
    {
        throw new NotImplementedException("Processes.faultTwo: Phase C — full scheduler needed");
    }

    // Interpreter-loop hooks — Phase B/C will fill these in once Cpu.processor()
    // is ported.
    public static bool checkforInterrupts() => false;
    public static bool checkForTimeouts() => false;
    public static void reschedule(bool _) { /* Phase C */ }
    public static void idle() { /* Phase C */ }

    // For block-transfer interrupt checks (Ch08). Phase B: no interrupts during tests.
    public static bool interruptPending() => false;

    // ---- Ch09/Ch10 stubs (Phase C will implement) ----

    public static void enableInterrupts()  => throw new NotImplementedException("Phase C: Processes.enableInterrupts");
    public static void disableInterrupts() => throw new NotImplementedException("Phase C: Processes.disableInterrupts");

    // Monitor / condition / PSB helpers used by Ch10
    public const int PDA_LP_header_ready = 0; // placeholder; Phase C wires up the real PDA offset
    public const ushort PsbNull = 0;

    public static bool isMonitorLocked(ushort mon) => throw new NotImplementedException("Phase C: Processes.isMonitorLocked");
    public static ushort setMonitorLocked(ushort mon) => throw new NotImplementedException("Phase C: Processes.setMonitorLocked");
    public static void enterFailed(int m) => throw new NotImplementedException("Phase C: Processes.enterFailed");
    public static bool exit(int m) => throw new NotImplementedException("Phase C: Processes.exit");
    public static void cleanupCondition(int c) => throw new NotImplementedException("Phase C: Processes.cleanupCondition");
    public static ushort fetchPSB_flags(ushort psb) => throw new NotImplementedException("Phase C: Processes.fetchPSB_flags");
    public static bool isPsbFlagsAbort(ushort flags) => throw new NotImplementedException("Phase C: Processes.isPsbFlagsAbort");
    public static bool isConditionAbortable(ushort cond) => throw new NotImplementedException("Phase C: Processes.isConditionAbortable");
    public static bool isConditionWakeup(ushort cond) => throw new NotImplementedException("Phase C: Processes.isConditionWakeup");
    public static ushort unsetConditionWakeup(ushort cond) => throw new NotImplementedException("Phase C: Processes.unsetConditionWakeup");
    public static void storePSB_timeout(ushort psb, ushort timeout) => throw new NotImplementedException("Phase C: Processes.storePSB_timeout");
    public static ushort setPsbFlagsWaiting(ushort flags) => throw new NotImplementedException("Phase C: Processes.setPsbFlagsWaiting");
    public static void storePSB_flags(ushort psb, ushort flags) => throw new NotImplementedException("Phase C: Processes.storePSB_flags");
    public static void requeue(int src, int dst, ushort psb) => throw new NotImplementedException("Phase C: Processes.requeue");
    public static ushort setPsbFlags_cleanup(ushort flags, ushort psb) => throw new NotImplementedException("Phase C: Processes.setPsbFlags_cleanup");
    public static ushort getCondition_tail(ushort cond) => throw new NotImplementedException("Phase C: Processes.getCondition_tail");
    public static void wakeHead(int c) => throw new NotImplementedException("Phase C: Processes.wakeHead");
    public static ushort fetchPSB_link(ushort psb) => throw new NotImplementedException("Phase C: Processes.fetchPSB_link");
    public static ushort setPsbLink_priority(ushort link, int priority) => throw new NotImplementedException("Phase C: Processes.setPsbLink_priority");
    public static void storePSB_link(ushort psb, ushort link) => throw new NotImplementedException("Phase C: Processes.storePSB_link");
}
