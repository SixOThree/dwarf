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

using Dwarf.Engine;

namespace Dwarf.Tests;

// Parent class for unit test classes, providing common functionality for
// building up the environment before executing an instruction, and for
// verifying the state of the engine after execution.
//
// xUnit creates a new instance of a test class for each test method, so the
// constructor takes the place of Java's @Before — running prepareCpu() once
// per test, exactly as the Java suite does.
public abstract class AbstractInstructionTest : IDisposable
{
    // xUnit calls Dispose after each test — equivalent to Java's @After.
    // Subclasses override OnAfterTest for per-test cleanup (e.g., restoring
    // memory mappings mutated by a SetMap test).
    public void Dispose()
    {
        OnAfterTest();
        GC.SuppressFinalize(this);
    }

    protected virtual void OnAfterTest() { }


    // Exception thrown in unit tests when a trap or fault occurred.
    public sealed class MesaTrapOrFault : Exception
    {
        public MesaTrapOrFault() { }
        public MesaTrapOrFault(string message) : base(message) { }
    }

    // Callback that can be registered in the unittest MesaFaultTrapThrower
    // to be called before the MesaTrapOrFault is thrown.
    protected delegate void ChkThrowerCheck();

    // Unittest variant for handling faults or traps. Lets a test mark which
    // events are expected (failing the test on unexpected ones) so we can
    // intercept them instead of dispatching to the OS (which is absent here).
    protected sealed class ChkThrower : Cpu.MesaFaultTrapThrower
    {
        public ChkThrowerCheck? beforeCheck;

        public bool expect_signalBreakTrap;
        public bool expect_signalOpcodeTrap;
        public bool expect_signalEscOpcodeTrap;
        public bool expect_signalPointerTrap;
        public bool expect_signalPageFault;
        public bool expect_signalWriteProtectFault;
        public bool expect_signalCodeFault;
        public bool expect_signalStackError;
        public bool expect_signalBoundsTrap;
        public bool expect_signalHardwareError;
        public bool expect_signalDivideZeroTrap;
        public bool expect_signalDivideCheckTrap;
        public bool expect_signalFrameFault;
        public bool expect_signalUnboundTrap;
        public bool expect_signalCodeTrap;
        public bool expect_signalControlTrap;
        public bool expect_ERROR;
        public bool expect_signalRescheduleError;
        public bool expect_signalProcessTrap;
        public bool expect_signalInterruptError;
        public bool expect_nakedTrap;

        public void signalOpcodeTrap(int code)
        {
            if (!expect_signalOpcodeTrap) { Assert.Fail("unexpected mesa trap or fault"); }
            beforeCheck?.Invoke();
            throw new MesaTrapOrFault();
        }

        public void signalEscOpcodeTrap(int code)
        {
            if (!expect_signalEscOpcodeTrap) { Assert.Fail("unexpected mesa trap or fault"); }
            beforeCheck?.Invoke();
            throw new MesaTrapOrFault();
        }

        public void signalPointerTrap()
        {
            if (!expect_signalPointerTrap) { Assert.Fail("unexpected mesa trap or fault"); }
            beforeCheck?.Invoke();
            throw new MesaTrapOrFault();
        }

        public void signalPageFault(int faultingLongPointer)
        {
            if (!expect_signalPageFault) { Assert.Fail("unexpected mesa trap or fault"); }
            beforeCheck?.Invoke();
            throw new MesaTrapOrFault();
        }

        public void signalWriteProtectFault(int faultingLongPointer)
        {
            if (!expect_signalWriteProtectFault) { Assert.Fail("unexpected mesa trap or fault"); }
            beforeCheck?.Invoke();
            throw new MesaTrapOrFault();
        }

        public void signalStackError()
        {
            if (!expect_signalStackError) { Assert.Fail("unexpected mesa trap or fault"); }
            beforeCheck?.Invoke();
            throw new MesaTrapOrFault();
        }

        public void signalBoundsTrap()
        {
            if (!expect_signalBoundsTrap) { Assert.Fail("unexpected mesa trap or fault"); }
            beforeCheck?.Invoke();
            throw new MesaTrapOrFault();
        }

        public void signalHardwareError()
        {
            if (!expect_signalHardwareError) { Assert.Fail("unexpected mesa trap or fault"); }
            beforeCheck?.Invoke();
            throw new MesaTrapOrFault();
        }

        public void signalDivideZeroTrap()
        {
            if (!expect_signalDivideZeroTrap) { Assert.Fail("unexpected mesa trap or fault"); }
            beforeCheck?.Invoke();
            throw new MesaTrapOrFault();
        }

        public void signalDivideCheckTrap()
        {
            if (!expect_signalDivideCheckTrap) { Assert.Fail("unexpected mesa trap or fault"); }
            beforeCheck?.Invoke();
            throw new MesaTrapOrFault();
        }

        public void signalFrameFault(int fsi)
        {
            if (!expect_signalFrameFault) { Assert.Fail("unexpected mesa trap or fault"); }
            beforeCheck?.Invoke();
            throw new MesaTrapOrFault();
        }

        public void signalUnboundTrap(int dst)
        {
            if (!expect_signalUnboundTrap) { Assert.Fail("unexpected mesa trap or fault"); }
            beforeCheck?.Invoke();
            throw new MesaTrapOrFault();
        }

        public void signalCodeTrap(int gf)
        {
            if (!expect_signalCodeTrap) { Assert.Fail("unexpected mesa trap or fault"); }
            beforeCheck?.Invoke();
            throw new MesaTrapOrFault();
        }

        // Java: this checks expect_signalCodeTrap (intentional, as in upstream).
        public void signalControlTrap(int src)
        {
            if (!expect_signalCodeTrap) { Assert.Fail("unexpected mesa trap or fault"); }
            beforeCheck?.Invoke();
            throw new MesaTrapOrFault();
        }

        public void ERROR(string reason)
        {
            if (!expect_ERROR) { Assert.Fail("unexpected mesa trap or fault"); }
            beforeCheck?.Invoke();
            throw new MesaTrapOrFault();
        }

        public void trap(int controlLinkIdx)
        {
            if (!expect_nakedTrap) { Assert.Fail("unexpected mesa trap or fault"); }
            beforeCheck?.Invoke();
            throw new MesaTrapOrFault();
        }

        public void signalBreakTrap()
        {
            if (!expect_signalBreakTrap) { Assert.Fail("unexpected mesa trap or fault"); }
            beforeCheck?.Invoke();
            throw new MesaTrapOrFault();
        }

        public void signalInterruptError()
        {
            if (!expect_signalInterruptError) { Assert.Fail("unexpected mesa trap or fault"); }
            beforeCheck?.Invoke();
            throw new MesaTrapOrFault();
        }

        public void signalProcessTrap()
        {
            if (!expect_signalProcessTrap) { Assert.Fail("unexpected mesa trap or fault"); }
            beforeCheck?.Invoke();
            throw new MesaTrapOrFault();
        }

        public void signalRescheduleError()
        {
            if (!expect_signalRescheduleError) { Assert.Fail("unexpected mesa trap or fault"); }
            beforeCheck?.Invoke();
            throw new MesaTrapOrFault();
        }
    }

    // Special sentinel values for mkStack / checkStack
    protected const int SP = unchecked((int)0xF0FFFF01);      // register SP is here (set or check)
    protected const int savedSP = unchecked((int)0xF0FFFF02); // register savedSP is here
    protected const int any = unchecked((int)0xF0FFFF0F);     // the stack entry here is irrelevant

    // Setup the evaluation stack with the given values / set SP / savedSP
    protected static void mkStack(params int[] values)
    {
        int newSP = -1;
        int newSavedSP = -1;

        ushort[] stack = Cpu.getStack();
        int sp = 0;
        for (int i = 0; i < values.Length && sp < PrincOpsDefs.cSTACK_LENGTH; i++)
        {
            int v = values[i];
            if (v == SP) { newSP = sp; }
            else if (v == savedSP) { newSavedSP = sp; }
            else if (v == any) { sp++; }
            else { stack[sp++] = (ushort)(v & 0xFFFF); }
        }

        Cpu.SP = (newSP >= 0) ? newSP : sp;
        // Faithful to the Java code (which has a typo: assigns SP again rather
        // than savedSP). Keep behavior identical so the 608 ported tests behave
        // exactly as upstream.
        if (newSavedSP >= 0) { Cpu.SP = newSavedSP; } else { Cpu.savedSP = sp; }
    }

    // Verify that the evaluation stack has the given values / SP / savedSP
    protected static void checkStack(params int[] values)
    {
        int expSP = -1;
        int expSavedSP = -1;

        ushort[] stack = Cpu.getStack();
        int sp = 0;
        for (int i = 0; i < values.Length && sp < PrincOpsDefs.cSTACK_LENGTH; i++)
        {
            int v = values[i];
            if (v == SP) { expSP = sp; }
            else if (v == savedSP) { expSavedSP = sp; }
            else if (v == any) { sp++; }
            else
            {
                ushort expected = (ushort)(v & 0xFFFF);
                ushort actual = stack[sp];
                Assert.True(expected == actual, $"stack[{sp}] expected 0x{expected:X4}, got 0x{actual:X4}");
                sp++;
            }
        }

        Assert.Equal((expSP >= 0) ? expSP : sp, Cpu.SP);
        if (expSavedSP >= 0)
        {
            Assert.Equal(expSavedSP, Cpu.savedSP);
        }
    }

    // Special constants when setting up fake code segments
    protected const int PC = unchecked((int)0xF0FFFF11);
    protected const int savedPC = unchecked((int)0xF0FFFF12);

    // Setup a fake code segment with the given code bytes
    protected static void mkCode(params int[] codeBytes)
    {
        Cpu.PC = 0;
        Cpu.savedPC = 0;

        int codeOffset = 0;
        int pc = 0;
        int w = 0;
        for (int i = 0; i < codeBytes.Length; i++)
        {
            int b = codeBytes[i];
            if (b == PC) { Cpu.PC = pc; }
            else if (b == savedPC) { Cpu.savedPC = pc; }
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

    // Fill the local frame (preset register LF) with the given values
    protected static void mkLocalFrame(params int[] locals)
    {
        for (int i = 0; i < locals.Length; i++)
        {
            Mem.writeMDSWord(Cpu.LF, i, locals[i]);
        }
    }

    // Verify that the local frame (current register LF) has the given values
    protected static void checkLocalFrame(params int[] expLocals)
    {
        for (int i = 0; i < expLocals.Length; i++)
        {
            int exp = expLocals[i];
            if (exp == any) { continue; }
            int actual = Mem.readMDSWord(Cpu.LF, i);
            Assert.True(exp == actual, $"localFrame word #{i}: expected 0x{exp:X4}, got 0x{actual:X4}");
        }
    }

    // Fill the global frame (preset register GF16 and GF32) with the given values
    protected static void mkGlobalFrame(params int[] globals)
    {
        for (int i = 0; i < globals.Length; i++)
        {
            Mem.writeWord(Cpu.GF32 + i, (ushort)globals[i]);
        }
    }

    // Verify that the global frame has the given values
    protected static void checkGlobalFrame(params int[] expGlobals)
    {
        for (int i = 0; i < expGlobals.Length; i++)
        {
            int exp = expGlobals[i];
            if (exp == any) { continue; }
            int actual = Mem.readWord(Cpu.GF32 + i);
            Assert.True(exp == actual, $"globalFrame word #{i}: expected 0x{exp:X4}, got 0x{actual:X4}");
        }
    }

    // Fill the MDS memory area pointed to by 'testShortMem' with the given values
    protected static void mkShortMem(params int[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            Mem.writeWord(Cpu.MDS + testShortMem + i, (ushort)values[i]);
        }
    }

    // Verify the MDS memory area pointed to by 'testShortMem'
    protected static void checkShortMem(params int[] expValues)
    {
        for (int i = 0; i < expValues.Length; i++)
        {
            int exp = expValues[i];
            if (exp == any) { continue; }
            int actual = Mem.readWord(Cpu.MDS + testShortMem + i);
            Assert.True(exp == actual, $"shortMem word #{i}: expected 0x{exp:X4}, got 0x{actual:X4}");
        }
    }

    // Fill the general memory area pointed by 'testLongMem'
    protected static void mkLongMem(params int[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            Mem.writeWord(testLongMem + i, (ushort)values[i]);
        }
    }

    // Verify the general memory area pointed by 'testLongMem'
    protected static void checkLongMem(params int[] expValues)
    {
        for (int i = 0; i < expValues.Length; i++)
        {
            int exp = expValues[i];
            if (exp == any) { continue; }
            int actual = Mem.readWord(testLongMem + i);
            Assert.True(exp == actual, $"longMem word #{i}: expected 0x{exp:X4}, got 0x{actual:X4}");
        }
    }

    // The fault/trap handler instance replacing the "real" implementation
    protected ChkThrower mesaException = null!;

    // Memory initialization (one-time across all tests)
    private static bool memInitialized;
    private static int firstUnmappedLongPointer;
    protected static int firstUnmappedPage;

    // The memory areas used for tests
    protected static int testShortMem;    // POINTER to a memory area in MDS outside any frame
    protected static int testLongMem;     // LONG POINTER to a memory area outside the MDS
    protected static int testLongMemLow;  // low word of 'testLongMem' (for mkStack)
    protected static int testLongMemHigh; // high word of 'testLongMem' (for mkStack)

    // Clear a memory area starting at a LONG POINTER
    private static void clear(int lp, int count)
    {
        while (count-- > 0)
        {
            Mem.writeWord(lp++, 0);
        }
    }

    // Basic register/memory preparation for a single unittest
    private void prepareCpuCommon()
    {
        if (!memInitialized)
        {
            // 256 kwords = 512 kbytes real memory, 512 kwords = 1 mbyte virtual memory.
            // Another fixture (e.g. Phase C SmokeTests) may have initialized Mem
            // first; skip the re-init in that case to avoid the "already initialized"
            // guard, but still set up our derived offsets.
            if (Mem.pageFlags == null)
            {
                Mem.initializeMemoryGuam(PrincOpsDefs.MIN_REAL_ADDRESSBITS, PrincOpsDefs.MIN_REAL_ADDRESSBITS + 1);
            }
            memInitialized = true;
            firstUnmappedLongPointer = 1 << PrincOpsDefs.MIN_REAL_ADDRESSBITS;
            firstUnmappedPage = (int)((uint)firstUnmappedLongPointer >> 8);
        }

        // setup registers

        // drop values from last run
        Cpu.resetRegisters();

        // (absolute) long pointers (to words)
        Cpu.MDS = 128 * 1024; // MDS -> 3rd 64K block
        clear(Cpu.MDS, 64 * 1024);
        Cpu.CB = Cpu.MDS + (66 * 1024); // code block: 2K behind MDS-end
        clear(Cpu.CB, 2048);

        // setup MDS internal tables

        // allocation vector: set all fsi-slots to empty and preallocate local frames
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
                // restrictions:
                // -> 4 overhead words
                // -> overhead + frame quad-word aligned
                // -> the 4 overhead words + 4 first frame variables on the same page
                f = (f + 3) & ~0x03;
                int p0 = f & unchecked((int)0xFFFFFF00);
                int p7 = (f + PrincOpsDefs.LOCALOVERHEAD_SIZE + 3) & unchecked((int)0xFFFFFF00);
                if (p0 != p7) { f = p7; }

                // build the frame
                int frame = f + PrincOpsDefs.LOCALOVERHEAD_SIZE;
                Mem.writeMDSWord(frame, PrincOpsDefs.LocalOverhead_word, fsi);
                Mem.writeMDSWord(frame, PrincOpsDefs.LocalOverhead_returnlink, 0);
                Mem.writeMDSWord(frame, PrincOpsDefs.LocalOverhead_globallink, 0);
                Mem.writeMDSWord(frame, PrincOpsDefs.LocalOverhead_pc, 0);

                // link into AV list for this fsi
                Mem.writeMDSWord(frame, Mem.readMDSWord(PrincOpsDefs.mALLOCATION_VECTOR, fsi));
                Mem.writeMDSWord(PrincOpsDefs.mALLOCATION_VECTOR, fsi, (ushort)frame);

                // let the next frame start after this one
                f = frame + frameSize;
            }
        }

        // system data table: TBD (Phase B / C)
        // escape trap table:  TBD (Phase B / C)

        // others

        // test memory addresses
        testShortMem = 32 * 1024;
        testLongMem = Cpu.MDS + (96 * 1096); // 32K words to play with
        testLongMemLow = testLongMem & 0xFFFF;
        testLongMemHigh = (int)((uint)testLongMem >> 16);
        clear(testLongMem, 32 * 1024);

        // register our exception thrower object
        mesaException = new ChkThrower();
        Cpu.thrower = mesaException;
    }

    // Setup specific registers for PrincOps <= 4.0 ("old-style")
    private static void prepareCpuOld()
    {
        // "create" a global frame at the end of the MDS with almost 4k
        clear(60 * 1024, 4 * 1024);
        Cpu.GF32 = Cpu.MDS + (60 * 1024) + 32; // 32 words overhead space
        Cpu.GF16 = Cpu.GF32 - Cpu.MDS;
    }

    // Constructor takes the place of Java's @Before — xUnit creates a new
    // test class instance for every test method.
    protected AbstractInstructionTest()
    {
        prepareCpuCommon();
        prepareCpuOld(); // TBD: make this configurable to choose old/new PrincOps

        // get us an 128 word local frame for tests
        int fsi = 14;
        Cpu.LF = Mem.readMDSWord(PrincOpsDefs.mALLOCATION_VECTOR, fsi);
        Mem.writeMDSWord(PrincOpsDefs.mALLOCATION_VECTOR, fsi, Mem.readMDSWord(Cpu.LF));
    }
}
