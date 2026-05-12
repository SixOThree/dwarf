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
}
