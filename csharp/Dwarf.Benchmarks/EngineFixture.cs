/*
Copyright (c) 2017, Dr. Hans-Walter Latz (original Java implementation of the fixture concept)
Copyright (c) 2026, Matthew Dugal (C# .NET 10 port — Phase G-2 benchmark variant)
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

using Dwarf.Engine;

namespace Dwarf.Benchmarks;

// Engine setup for the dispatch benchmark — mirrors
// `Dwarf.Tests.AbstractInstructionTest.prepareCpuCommon` + `prepareCpuOld`
// inline, without the xunit assertion helpers (which the benchmark project
// has no need for and which would pull xunit into the Release binary).
//
// Called once from `InterpreterLoopBenchmark.GlobalSetup`. After this runs,
// the engine has memory + an allocation vector + a 128-word local frame +
// 4K of global frame space, with the dispatch table populated for PrincOps
// 4.0 ("old-style" / pre MDS-relieved).
internal static class EngineFixture
{
    // Memory areas used by the benchmark code (matches `testShortMem` /
    // `testLongMem` in AbstractInstructionTest).
    public static int testShortMem;
    public static int testLongMem;
    public static int testLongMemLow;
    public static int testLongMemHigh;

    private static bool initialized;

    public static void Initialize()
    {
        if (initialized) { return; }

        // 256 kwords = 512 kbytes real memory; 512 kwords = 1 mbyte virtual.
        // Same parameters as AbstractInstructionTest.
        Mem.initializeMemoryGuam(PrincOpsDefs.MIN_REAL_ADDRESSBITS, PrincOpsDefs.MIN_REAL_ADDRESSBITS + 1);

        Cpu.resetRegisters();
        Cpu.MDS = 128 * 1024;                  // MDS -> 3rd 64K block
        ClearMemory(Cpu.MDS, 64 * 1024);
        Cpu.CB = Cpu.MDS + (66 * 1024);        // code block: 2K behind MDS-end
        ClearMemory(Cpu.CB, 2048);

        // Allocation vector: set all fsi-slots to empty, then preallocate
        // local frames per FRAME_SIZE_MAP / FRAME_WEIGHT_MAP. Same logic as
        // AbstractInstructionTest.prepareCpuCommon.
        for (int i = 0; i < PrincOpsDefs.WORDS_PER_PAGE; i++)
        {
            Mem.writeMDSWord(PrincOpsDefs.mALLOCATION_VECTOR, i, PrincOpsDefs.AVITEM_EMPTY);
        }
        int f = 0x0600; // start just behind mETT
        for (int fsi = 0; fsi < PilotDefs.FRAME_SIZE_MAP.Length; fsi++)
        {
            int frameSize = PilotDefs.FRAME_SIZE_MAP[fsi];
            int frameCount = PilotDefs.FRAME_WEIGHT_MAP[fsi];
            for (int i = 0; i < frameCount; i++)
            {
                f = (f + 3) & ~0x03;
                int p0 = f & unchecked((int)0xFFFFFF00);
                int p7 = (f + PrincOpsDefs.LOCALOVERHEAD_SIZE + 3) & unchecked((int)0xFFFFFF00);
                if (p0 != p7) { f = p7; }

                int frame = f + PrincOpsDefs.LOCALOVERHEAD_SIZE;
                Mem.writeMDSWord(frame, PrincOpsDefs.LocalOverhead_word, fsi);
                Mem.writeMDSWord(frame, PrincOpsDefs.LocalOverhead_returnlink, 0);
                Mem.writeMDSWord(frame, PrincOpsDefs.LocalOverhead_globallink, 0);
                Mem.writeMDSWord(frame, PrincOpsDefs.LocalOverhead_pc, 0);

                // Link into AV list for this fsi
                Mem.writeMDSWord(frame, Mem.readMDSWord(PrincOpsDefs.mALLOCATION_VECTOR, fsi));
                Mem.writeMDSWord(PrincOpsDefs.mALLOCATION_VECTOR, fsi, (ushort)frame);

                f = frame + frameSize;
            }
        }

        testShortMem = 32 * 1024;
        testLongMem = Cpu.MDS + (96 * 1096); // 32K words to play with
        testLongMemLow = testLongMem & 0xFFFF;
        testLongMemHigh = (int)((uint)testLongMem >> 16);
        ClearMemory(testLongMem, 32 * 1024);

        // "create" a global frame at the end of the MDS with almost 4k
        // (AbstractInstructionTest.prepareCpuOld equivalent).
        ClearMemory(60 * 1024, 4 * 1024);
        Cpu.GF32 = Cpu.MDS + (60 * 1024) + 32; // 32 words overhead space
        Cpu.GF16 = Cpu.GF32 - Cpu.MDS;

        // Install a benchmark-safe fault thrower. The 12-insn loop is fully
        // arithmetic + load/store; nothing should fault. If something does,
        // throwing InvalidOperationException crashes the benchmark cleanly.
        Cpu.thrower = new BenchmarkThrower();

        // Get an 128 word local frame for the benchmark code
        int benchFsi = 14;
        Cpu.LF = Mem.readMDSWord(PrincOpsDefs.mALLOCATION_VECTOR, benchFsi);
        Mem.writeMDSWord(PrincOpsDefs.mALLOCATION_VECTOR, benchFsi, Mem.readMDSWord(Cpu.LF));

        initialized = true;
    }

    private static void ClearMemory(int lp, int count)
    {
        while (count-- > 0)
        {
            Mem.writeWord(lp++, 0);
        }
    }

    public const int PC_SENTINEL = unchecked((int)0xF0FFFF11);

    public static void MkCode(params int[] codeBytes)
    {
        Cpu.PC = 0;
        Cpu.savedPC = 0;

        int codeOffset = 0;
        int pc = 0;
        int w = 0;
        for (int i = 0; i < codeBytes.Length; i++)
        {
            int b = codeBytes[i];
            if (b == PC_SENTINEL) { Cpu.PC = pc; }
            else if ((pc & 1) == 0)
            {
                w = b << 8;
                pc++;
            }
            else
            {
                w |= b & 0x00FF;
                pc++;
                Mem.writeWord(Cpu.CB + codeOffset, (ushort)(w & 0xFFFF));
                codeOffset++;
            }
        }
        Mem.writeWord(Cpu.CB + codeOffset, (ushort)(w & 0xFFFF));
    }

    public static void MkLocalFrame(params int[] locals)
    {
        for (int i = 0; i < locals.Length; i++)
        {
            Mem.writeMDSWord(Cpu.LF, i, locals[i]);
        }
    }

    public static void MkGlobalFrame(params int[] globals)
    {
        for (int i = 0; i < globals.Length; i++)
        {
            Mem.writeWord(Cpu.GF32 + i, (ushort)globals[i]);
        }
    }

    public static void MkShortMem(params int[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            Mem.writeWord(Cpu.MDS + testShortMem + i, (ushort)values[i]);
        }
    }

    public static void MkLongMem(params int[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            Mem.writeWord(testLongMem + i, (ushort)values[i]);
        }
    }

    // No-throw fault/trap thrower for the benchmark. Any signal aborts the
    // benchmark loudly — the 12-insn loop is supposed to be fault-free.
    private sealed class BenchmarkThrower : Cpu.MesaFaultTrapThrower
    {
        private static InvalidOperationException Bug(string what) =>
            new($"Benchmark fixture: unexpected mesa trap/fault: {what}. This is a benchmark setup bug.");

        public void signalOpcodeTrap(int code) => throw Bug($"signalOpcodeTrap({code})");
        public void signalEscOpcodeTrap(int code) => throw Bug($"signalEscOpcodeTrap({code})");
        public void signalPointerTrap() => throw Bug("signalPointerTrap");
        public void signalPageFault(int faultingLongPointer) => throw Bug($"signalPageFault(0x{faultingLongPointer:X8})");
        public void signalWriteProtectFault(int faultingLongPointer) => throw Bug($"signalWriteProtectFault(0x{faultingLongPointer:X8})");
        public void signalStackError() => throw Bug("signalStackError");
        public void signalBoundsTrap() => throw Bug("signalBoundsTrap");
        public void signalHardwareError() => throw Bug("signalHardwareError");
        public void signalDivideZeroTrap() => throw Bug("signalDivideZeroTrap");
        public void signalDivideCheckTrap() => throw Bug("signalDivideCheckTrap");
        public void signalFrameFault(int fsi) => throw Bug($"signalFrameFault({fsi})");
        public void signalUnboundTrap(int dst) => throw Bug($"signalUnboundTrap({dst})");
        public void signalCodeTrap(int gf) => throw Bug($"signalCodeTrap({gf})");
        public void signalControlTrap(int src) => throw Bug($"signalControlTrap({src})");
        public void ERROR(string reason) => throw Bug($"ERROR({reason})");
        public void trap(int controlLinkIdx) => throw Bug($"trap({controlLinkIdx})");
        public void signalBreakTrap() => throw Bug("signalBreakTrap");
        public void signalInterruptError() => throw Bug("signalInterruptError");
        public void signalProcessTrap() => throw Bug("signalProcessTrap");
        public void signalRescheduleError() => throw Bug("signalRescheduleError");
    }
}
