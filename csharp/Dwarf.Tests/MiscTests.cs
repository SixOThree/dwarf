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
using Dwarf.Engine.Opcodes;

namespace Dwarf.Tests;

// Unittest checking the timeout-check-throttling concept and giving a rough
// performance number. The single test runs a 12-instruction loop a million
// times, six iterations apart with 40 ms sleeps to let the JIT warm up.
public sealed class MiscTests : AbstractInstructionTest
{
    [Fact]
    public void test_SampleCode()
    {
        // Prepare global frame
        mkGlobalFrame(
            0x1234,         // [0] parameter for step 2
            0,              // [1] unused
            testLongMemLow, // [2] address of ...
            testLongMemHigh // [3] long-pointer storage
        );

        // Prepare local frame
        mkLocalFrame(
            0x1122,         // [0] dbl-data for step 5 (low)
            0x3344,         // [1] dbl-data for step 5 (high)
            0x0000,         // [2] target for step 4
            testShortMem    // [3] address of short-pointer storage
        );

        // Short pointer memory
        mkShortMem(
            0x0000, 0x0000, 0x0000, 0x0000,
            0x5566, // [4] dbl-data for step 6 (low)
            0x7788  // [5] dbl-data for step 6 (high)
        );

        // Long pointer memory
        mkLongMem(0x4321); // parameter for step 1

        // Hand-crafted 12-instruction loop adding shorts and longs
        mkCode(
            PC,
            // step 1: load word from long pointer using GF[2]
            0x39,        // LGD2 — Load Global Double 2
            0x43,        // RL0  — Read Long 0
            // step 2: load word from GF[0]
            0x34,        // LG0  — Load Global 0
            // step 3: add words
            0xB5,        // ADD
            // step 4: store word in LF[2]
            0x1B,        // SL2  — Store Local 2
            // step 5: load dbl-word from LF[0]
            0x0E,        // LLD0 — Load Local Double 0
            // step 6: load dbl-word from LF[3] (short pointer) +4
            0x04,        // LL3  — Load Local 3
            0x46, 0x04,  // RDB  — Read Double Byte, alpha=4
            // step 7: subtract
            0xB8,        // DSUB — Double Subtract
            // step 8: store dbl-word at GF[2] long-pointer + 2
            0x39,        // LGD2 — Load Global Double 2
            0x51, 0x02,  // WDLB — Write Double Long Byte, alpha=2
            // step 9: jump back -13 bytes
            0x88, -13    // JB
        );

        // Initialize PrincOps 4.0 dispatch table (registers already reset by ctor)
        Opcodes.initializeInstructionsPrincOps40();

        const int sleepTime = 40;
        runLoop();
        Thread.Sleep(sleepTime); runLoop();
        Thread.Sleep(sleepTime); runLoop();
        Thread.Sleep(sleepTime); runLoop();
        Thread.Sleep(sleepTime); runLoop();
        Thread.Sleep(sleepTime); runLoop();
    }

    // Simulated interpreter loop with throttled timeout checks.
    private const int timeoutThrottleCount = 32 * 1024;

    private static void runLoop()
    {
        const int loopInstructions = 12;
        const int loopCount = 1_000_000;
        const int maxInstructions = loopInstructions * loopCount;
        int count = 0;
        int startPC = Cpu.PC;
        long startMs = Environment.TickCount64;
        int timeoutCountDown = timeoutThrottleCount;
        try
        {
            while (count < maxInstructions)
            {
                try
                {
                    bool interrupt = Processes.checkforInterrupts();
                    bool timeout = false;
                    if (timeoutCountDown < 1)
                    {
                        timeout = Processes.checkForTimeouts();
                        timeoutCountDown = timeoutThrottleCount;
                    }
                    else
                    {
                        timeoutCountDown--;
                    }
                    if (interrupt || timeout)
                    {
                        Assert.Fail("Simulated interpreter loop should not get interrupts or timeouts");
                    }
                    else if (Cpu.running)
                    {
                        count++;
                        Cpu.savedPC = Cpu.PC;
                        Cpu.savedSP = Cpu.SP;
                        Opcodes.dispatch(Mem.getNextCodeByte());
                    }
                    else
                    {
                        Assert.Fail("Simulated interpreter loop should not stop running");
                    }
                }
                catch (Cpu.MesaAbort)
                {
                    Assert.Fail("Simulated interpreter loop should not get a MesaAbort");
                }
            }
        }
        catch (Cpu.MesaERROR)
        {
            Assert.Fail("unexpected MesaERROR");
        }
        catch (Xunit.Sdk.FailException)
        {
            throw; // let test framework see assertion failures
        }
        catch (Exception re)
        {
            Assert.Fail($"Unexpected exception: {re.GetType().Name}: {re.Message}");
        }
        long endMs = Environment.TickCount64;
        Assert.Equal(startPC, Cpu.PC);

        long runtime = endMs + 1 - startMs;
        Console.WriteLine(
            $"\n** time elapsed for {maxInstructions} instructions : {runtime} ms => {(maxInstructions * 1000L) / runtime} insns/sec");
    }
}
