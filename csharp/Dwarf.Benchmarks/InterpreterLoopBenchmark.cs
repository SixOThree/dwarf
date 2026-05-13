/*
Copyright (c) 2026, Matthew Dugal
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

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDER "AS IS" AND ANY EXPRESS
OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS;
OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR
OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using BenchmarkDotNet.Attributes;
using Dwarf.Engine;
using Dwarf.Engine.Opcodes;

namespace Dwarf.Benchmarks;

// Measures the inner-loop performance of the Mesa interpreter using a
// hand-crafted 12-instruction loop. The loop and engine setup mirror
// `Dwarf.Tests.MiscTests.test_SampleCode` verbatim. Two variants:
//
//   - **PureDispatch**: just `Opcodes.dispatch(Mem.getNextCodeByte())` per
//     instruction. Lower bound on per-instruction cost — what the JIT
//     achieves when the dispatch table is the only overhead.
//   - **InterpreterLoop**: the full MiscTests.runLoop body including
//     `Processes.checkforInterrupts()` and the 32K-throttled
//     `checkForTimeouts()`. Realistic per-instruction cost during normal
//     boot.
//
// Java upstream's MiscTests reports ~36 M insns/sec (72M insns / ~2 sec).
// RISKS R3 target: ≥80% of Java throughput, i.e. ≥28.8 M insns/sec on the
// **InterpreterLoop** variant since that matches the Java measurement shape.
[MemoryDiagnoser]
public class InterpreterLoopBenchmark
{
    private const int LoopInstructions = 12;
    private const int LoopIterations = 1_000_000;
    private const int TotalInstructions = LoopInstructions * LoopIterations;

    private int startPC;

    [GlobalSetup]
    public void Setup()
    {
        EngineFixture.Initialize();

        // Prepare global frame
        EngineFixture.MkGlobalFrame(
            0x1234,                       // [0] parameter for step 2
            0,                            // [1] unused
            EngineFixture.testLongMemLow, // [2] address of ...
            EngineFixture.testLongMemHigh // [3] long-pointer storage
        );

        // Prepare local frame
        EngineFixture.MkLocalFrame(
            0x1122,                       // [0] dbl-data for step 5 (low)
            0x3344,                       // [1] dbl-data for step 5 (high)
            0x0000,                       // [2] target for step 4
            EngineFixture.testShortMem    // [3] address of short-pointer storage
        );

        // Short pointer memory
        EngineFixture.MkShortMem(
            0x0000, 0x0000, 0x0000, 0x0000,
            0x5566, // [4] dbl-data for step 6 (low)
            0x7788  // [5] dbl-data for step 6 (high)
        );

        // Long pointer memory
        EngineFixture.MkLongMem(0x4321); // parameter for step 1

        // Hand-crafted 12-instruction loop (mirrors MiscTests verbatim)
        EngineFixture.MkCode(
            EngineFixture.PC_SENTINEL,
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

        // Initialize PrincOps 4.0 dispatch table
        Opcodes.initializeInstructionsPrincOps40();

        startPC = Cpu.PC;
    }

    [IterationSetup]
    public void ResetForIteration()
    {
        // Reset PC so each measured iteration runs the same 12M instructions
        // from the same starting point. The loop is stack-balanced
        // (push2 push2 ... pop4 pop4) so SP and savedSP should be 0 at the
        // end of each iteration; reset defensively in case prior iterations
        // were aborted mid-flight.
        Cpu.PC = startPC;
        Cpu.savedPC = startPC;
        Cpu.SP = 0;
        Cpu.savedSP = 0;
    }

    // OperationsPerInvoke=12_000_000 makes BenchmarkDotNet report time/op
    // as per-instruction cost. The 12M figure is significant enough that
    // BDN's per-invocation overhead (single-digit microseconds) is
    // negligible (<0.001% of total time).
    [Benchmark(Baseline = true, OperationsPerInvoke = TotalInstructions, Description = "Pure dispatch (no interrupt/timeout checks)")]
    public void PureDispatch()
    {
        for (int i = 0; i < TotalInstructions; i++)
        {
            Cpu.savedPC = Cpu.PC;
            Cpu.savedSP = Cpu.SP;
            Opcodes.dispatch(Mem.getNextCodeByte());
        }
    }

    private const int TimeoutThrottleCount = 32 * 1024;

    [Benchmark(OperationsPerInvoke = TotalInstructions, Description = "Full interpreter loop (with throttled timeout)")]
    public void InterpreterLoop()
    {
        int timeoutCountDown = TimeoutThrottleCount;
        for (int count = 0; count < TotalInstructions; count++)
        {
            bool interrupt = Processes.checkforInterrupts();
            bool timeout = false;
            if (timeoutCountDown < 1)
            {
                timeout = Processes.checkForTimeouts();
                timeoutCountDown = TimeoutThrottleCount;
            }
            else
            {
                timeoutCountDown--;
            }
            if (!(interrupt || timeout) && Cpu.running)
            {
                Cpu.savedPC = Cpu.PC;
                Cpu.savedSP = Cpu.SP;
                Opcodes.dispatch(Mem.getNextCodeByte());
            }
        }
    }
}
