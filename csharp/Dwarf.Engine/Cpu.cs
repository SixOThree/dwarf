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

// Implementation of the core CPU functionality defined in the PrincOps:
//   - registers
//   - functional register access (e.g. evaluation stack push/pop)
//   - traps and faults
//   - (Phase B) inner mesa interpreter
//
// This Phase A port covers everything *except* the dispatch loop (Phase B),
// the low-level debug interpreter (deferred), and the Xfer-based trap dispatch
// (Phase C — RealMesaFaultTrapThrower currently throws a placeholder
// MesaERROR instead of doing the Xfer dance).
//
// Stack and register conventions deviate from Java:
//   - `stack` is `ushort[]` rather than `short[]` to match Mem's word width
//     and eliminate `& 0xFFFF` masking in chapter opcodes
//   - `push`/`pop` use `ushort` for the word value
//   - signed interpretations are explicit at the call site: `(short)Cpu.pop()`
//
// See DECISIONS.md §2 and §3 for the rationale.
public static class Cpu
{
    // ------------------------------------------------------------------
    // Logging
    // ------------------------------------------------------------------

    public static bool unsilenced = true; // modified by the low-level debugger

    public static void logError(string msg) =>
        Console.WriteLine("ERR: -------------------------------------- " + msg);

    public static void logWarning(string msg) =>
        Console.WriteLine("WRN: -------------------------------------- " + msg);

    public static void logInfo(string msg) =>
        Console.WriteLine("INF: -------------------------------------- " + msg);

    private const int FLIGHTRECORDER_MAX = 0x1FFFF;
    private static readonly string?[] oplog = new string?[FLIGHTRECORDER_MAX + 1];
    private static int currOplog;

    private static void opcodesLogf(string format, params object[] args)
    {
        if (Config.LOG_OPCODES_AS_FLIGHTRECORDER)
        {
            if (Config.FLIGHTRECORDER_WITH_STACK)
            {
                var sb = new System.Text.StringBuilder();
                sb.Append($"stack[{SP:D2}] ");
                for (int i = 0; i < SP; i++)
                {
                    sb.Append($" 0x{stack[i]:X4} (");
                    char c = (char)((stack[i] >> 8) & 0xFF);
                    sb.Append((c >= ' ' && c < (char)0x7F) ? c : ' ');
                    c = (char)(stack[i] & 0xFF);
                    sb.Append((c >= ' ' && c < (char)0x7F) ? c : ' ');
                    sb.Append(')');
                }
                sb.Append('\n');
                oplog[currOplog] = sb.ToString();
                currOplog = (currOplog + 1) & FLIGHTRECORDER_MAX;
            }
            oplog[currOplog] = string.Format(format, args);
            currOplog = (currOplog + 1) & FLIGHTRECORDER_MAX;
        }
        else
        {
            Console.Write(string.Format(format, args));
        }
    }

    public static void dumpOplog()
    {
        int idx = currOplog;
        while (true)
        {
            string? line = oplog[currOplog];
            currOplog = (currOplog + 1) & FLIGHTRECORDER_MAX;
            if (line != null) { Console.Write(line); }
            if (idx == currOplog) { break; }
        }
    }

    public static void logf(string format, params object[] args)
    {
        if (Config.LOG_OPCODES && unsilenced) { opcodesLogf(format, args); }
    }

    // Opcode-trace logging hooks — called from Opcodes.Dispatch in Phase B.
    // For Phase A these are stubs (with `Config.LOG_OPCODES` false they're DCE'd).
    public static void logOpcode(string name)               { /* Phase B */ }
    public static void logOpcode_alpha(string name)         { /* Phase B */ }
    public static void logOpcode_salpha(string name)        { /* Phase B */ }
    public static void logOpcode_word(string name)          { /* Phase B */ }
    public static void logOpcode_sword(string name)         { /* Phase B */ }
    public static void logOpcode_pair(string name)          { /* Phase B */ }
    public static void logOpcode_alphabeta(string name)     { /* Phase B */ }
    public static void logOpcode_alphasbeta(string name)    { /* Phase B */ }
    public static void logEscOpcode(string name)            { /* Phase B */ }
    public static void logEscOpcode_alpha(string name)      { /* Phase B */ }
    public static void logEscOpcode_word(string name)       { /* Phase B */ }

    // ------------------------------------------------------------------
    // Registers
    // ------------------------------------------------------------------

    public static int MDS;

    // current global frame: this MesaMachine uses several GF-registers depending
    // on the PrincOps-level currently active.
    // GF16 : PrincOps up to 4.0 :: POINTER relative to MDS => int & 0xFFFF
    // GF32 : PrincOps after 4.0 ("MDS-relieved") :: absolute LONG POINTER
    // GFI  : PrincOps after 4.0 :: Global Frame Index
    public static int GF16;
    public static int GF32;
    public static int GFI;

    // global frame table base, only for PrincOps after 4.0
    public const int GFT = PrincOpsDefs.mGLOBAL_FRAME_TABLE;

    // current local frame (CARDINAL relative to MDS)
    public static int LF;

    // current Code Base
    public static int CB;

    // current instruction pointer (byte offset in the word-address CB)
    public static int PC;       // byte-offset 0..0xFFFF
    public static int savedPC;

    // evaluation stack — only accessible through push/pop methods
    internal static readonly ushort[] stack = new ushort[PrincOpsDefs.cSTACK_LENGTH];
    private const int SP_LIMIT = PrincOpsDefs.cSTACK_LENGTH;
    public static int SP;
    public static int savedSP;

    // break byte
    public static int breakByte;

    // xfer traps
    public static int XTS;

    // immutable register for the Process Data Area structure
    public const int PDA = PrincOpsDefs.mPROCESS_DATA_AREA;

    // index of currently running process
    public static ushort PSB; // PsbIndex : 0..1023

    // process timeout timer
    public static int PTC; // Ticks = CARDINAL

    // wake up pending register (32-bit allows implementation-owned interrupts)
    public static readonly AtomicInteger WP = new();

    // wake up mask for this architecture (no reserved interrupt)
    public const ushort WM = 0;

    // wake up disable counter register
    public static ushort WDC;

    // interval timer
    public const int MicrosecondsPerPulse = 16;
    public const int TimeOutInterval = 3200;

    private static long lastITpulse;
    private static int currIT;
    private static int extIToffset;

    private static int internalIT()
    {
        // .NET equivalent of System.nanoTime() & 0xFFFFFFFFFFFFC000L
        long nanos = (long)((double)Environment.TickCount64 * 1_000_000); // millis -> nanos
        long newITpulse = nanos & unchecked((long)0xFFFFFFFFFFFFC000UL);
        if (newITpulse != lastITpulse)
        {
            lastITpulse = newITpulse;
            currIT = (int)(((ulong)newITpulse >> 14) & 0xFFFFFFFFUL);
        }
        return currIT;
    }

    public static int IT() => internalIT() + extIToffset;
    public static void setIT(int newIT) => extIToffset = newIT - internalIT();

    // processor id
    private static readonly int[] PID = new int[4];

    public static int getPIDword(int idx)
    {
        if (idx < 0 || idx >= PID.Length) { return 0; }
        return PID[idx];
    }

    public static void setPID(int id0, int id1, int id2)
    {
        PID[0] = 0; // PrincOps section 3.3.3: first word "currently" unused
        PID[1] = id0 & 0xFFFF;
        PID[2] = id1 & 0xFFFF;
        PID[3] = id2 & 0xFFFF;
    }

    // maintenance panel code
    private static int MP;

    public delegate void MPHandler(int mp);

    private static MPHandler? mpHandler;

    public static void setMPHandler(MPHandler? h)
    {
        mpHandler = h;
        mpHandler?.Invoke(MP);
    }

    public static void setMP(int mp)
    {
        MP = mp;
        mpHandler?.Invoke(MP);
    }

    public static int getMP() => MP;

    // running state of the cpu
    public static bool running = true;

    // number of instructions executed so far
    public static long insns;

    // ------------------------------------------------------------------
    // Reset / initialize (4.7 Initial State)
    // ------------------------------------------------------------------

    public static void resetRegisters()
    {
        // process registers
        WP.set(0);
        WDC = 1; // disable interrupts
        XTS = 0;
        // Processes.resetPTC(1) — Phase C, when Processes exists
        running = true;

        // context initialization
        savedSP = 0;
        SP = 0;
        breakByte = 0;
        PSB = 0;
        MDS = 0;

        // others (initial values not explicitly defined by PrincOps)
        GF16 = 0;
        GF32 = 0;
        GFI = 0;
        LF = 0;
        savedPC = 0;
        PC = 0;
    }

    public static void initialize()
    {
        // Phase C will wire this up to load the boot link from the SD table
        // and call Xfer.impl.xfer(bootLink, 0, XferType.xcall, false).
        throw new NotImplementedException("Cpu.initialize() requires Xfer (Phase C)");
    }

    // ------------------------------------------------------------------
    // Register-based utilities
    // ------------------------------------------------------------------

    // getStack() : intended for unit tests only
    public static ushort[] getStack() => stack;

    public static int lengthenPointer(short pointer) => MDS + (pointer & 0xFFFF);
    public static int lengthenPointer(int pointer)   => MDS + (pointer & 0xFFFF);

    // ------------------------------------------------------------------
    // Evaluation stack operations
    // ------------------------------------------------------------------

    public static void push(ushort value)
    {
        if (SP >= SP_LIMIT) { stackError(); }
        stack[SP++] = value;
    }

    public static void push(int value)
    {
        if (SP >= SP_LIMIT) { stackError(); }
        stack[SP++] = (ushort)(value & 0xFFFF);
    }

    public static void pushLong(int value)
    {
        push((ushort)(value & 0xFFFF));
        push((ushort)((uint)value >> 16));
    }

    public static ushort pop()
    {
        if (SP <= 0) { stackError(); }
        return stack[--SP];
    }

    public static ushort popRecover()
    {
        if (SP <= 0) { stackError(); }
        return stack[SP - 1];
    }

    public static int popLong()
    {
        int high = pop() << 16;
        return high | pop();
    }

    public static void recover()
    {
        if (SP >= SP_LIMIT) { stackError(); }
        SP++;
    }

    public static void discard()
    {
        if (SP <= 0) { stackError(); }
        SP--;
    }

    public static void checkEmptyStack()
    {
        if (SP != 0) { stackError(); }
    }

    // ------------------------------------------------------------------
    // Trap & fault handling
    // ------------------------------------------------------------------

    public const int StateVector_stateWord = 14;
    public const int StateVector_frame = 15;
    public const int StateVector_data = 16;

    public const int TransferDescriptor_src = 0;
    public const int TransferDescriptor_dst = 2;

    public static void saveStack(int stateHandle)
    {
        for (int i = 0; i < PrincOpsDefs.cSTACK_LENGTH; i++)
        {
            Mem.writeWord(stateHandle + i, stack[i]);
        }
        int stateWord = ((breakByte << 8) & 0xFF00) | (SP & 0x000F);
        Mem.writeWord(stateHandle + StateVector_stateWord, (ushort)stateWord);
        SP = 0;
        savedSP = 0;
        breakByte = 0;
    }

    public static void loadStack(int stateHandle)
    {
        ushort stateWord = Mem.readWord(stateHandle + StateVector_stateWord);
        for (int i = 0; i < PrincOpsDefs.cSTACK_LENGTH; i++)
        {
            stack[i] = Mem.readWord(stateHandle + i);
        }
        SP = stateWord & 0x000F;
        savedSP = SP;
        breakByte = (stateWord >> 8) & 0x00FF;
    }

    public static bool validContext() => PC > (PrincOpsDefs.cCODESEGMENT_SIZE * 2);

    // Fatal error in the mesa processor (violation of PrincOps invariants).
    // This exception is thrown when PrincOps states to raise ERROR.
    public sealed class MesaERROR : Exception
    {
        public MesaERROR(string message) : base(message) { }
    }

    // Dummy error indicating the processor was stopped (not a PrincOps feature).
    // Thrown by SUSPEND / STOPEMULATOR and a special interrupt to gracefully end
    // the interpreter loop with a message.
    public sealed class MesaStopped : Exception
    {
        public MesaStopped(string message) : base(message) { }
    }

    // Exception signalling that the instruction could not be processed —
    // either a trap or a fault occurred and execution should continue in a
    // different context. Thrown when PrincOps states to raise Abort.
    //
    // Some BLT instructions in deviation of PrincOps avoid pushing the current
    // stack with each item; instead they intercept MesaAbort, modify the saved
    // state, and re-throw. Scheme:
    //    ex.beginUpdateStack();
    //    /* push the required restart values */
    //    ex.updateStack();
    public sealed class MesaAbort : Exception
    {
        private readonly int stateHandle; // LONG POINTER TO StateVector

        public MesaAbort() { stateHandle = 0; }
        public MesaAbort(int savedStackLocation) { stateHandle = savedStackLocation; }

        public void beginUpdateStack()
        {
            if (stateHandle == 0) { throw this; }

            ushort stateWord = Mem.readWord(stateHandle + StateVector_stateWord);
            SP = stateWord & 0x000F;
        }

        public MesaAbort updateStack()
        {
            if (stateHandle != 0)
            {
                for (int i = 0; i < PrincOpsDefs.cSTACK_LENGTH; i++)
                {
                    Mem.writeWord(stateHandle + i, stack[i]);
                }
                ushort oldStateWord = Mem.readWord(stateHandle + StateVector_stateWord);
                int newStateWord = (oldStateWord & 0xFF00) | (SP & 0x000F);
                Mem.writeWord(stateHandle + StateVector_stateWord, (ushort)newStateWord);
            }
            return this;
        }
    }

    // Interface defining methods for raising traps and faults. The existence
    // of this abstraction (beyond Cpu's public methods) is to allow unit tests
    // to intercept traps for verification of instruction behavior.
    public interface MesaFaultTrapThrower
    {
        // traps
        void trap(int controlLinkIdx);
        void signalBoundsTrap();
        void signalBreakTrap();
        void signalCodeTrap(int gf);
        void signalControlTrap(int src);
        void signalDivideCheckTrap();
        void signalDivideZeroTrap();
        void signalEscOpcodeTrap(int code);
        void signalInterruptError();
        void signalOpcodeTrap(int code);
        void signalPointerTrap();
        void signalProcessTrap();
        void signalRescheduleError();
        void signalStackError();
        void signalUnboundTrap(int dst);
        void signalHardwareError();

        // faults
        void signalPageFault(int faultingLongPointer);
        void signalWriteProtectFault(int faultingLongPointer);
        void signalFrameFault(int fsi);

        // finalizing ERROR exception
        void ERROR(string reason);
    }

    // Default implementation of MesaFaultTrapThrower.
    //
    // Phase A skeleton: throws MesaERROR with a "Phase C" message. The real
    // Xfer-based trap dispatch lands in Phase C when Xfer is ported.
    //
    // Tests install a different thrower (ChkThrower in AbstractInstructionTest)
    // that records the trap kind without throwing.
    private sealed class RealMesaFaultTrapThrower : MesaFaultTrapThrower
    {
        private static Exception NotYet(string what) =>
            new MesaERROR($"RealMesaFaultTrapThrower.{what}: requires Xfer (Phase C)");

        public void trap(int controlLinkIdx) => throw NotYet(nameof(trap));
        public void signalBoundsTrap() => throw NotYet(nameof(signalBoundsTrap));
        public void signalBreakTrap() => throw NotYet(nameof(signalBreakTrap));
        public void signalCodeTrap(int gf) => throw NotYet(nameof(signalCodeTrap));
        public void signalControlTrap(int src) => throw NotYet(nameof(signalControlTrap));
        public void signalDivideCheckTrap() => throw NotYet(nameof(signalDivideCheckTrap));
        public void signalDivideZeroTrap() => throw NotYet(nameof(signalDivideZeroTrap));
        public void signalEscOpcodeTrap(int code) => throw NotYet(nameof(signalEscOpcodeTrap));
        public void signalInterruptError() => throw NotYet(nameof(signalInterruptError));
        public void signalOpcodeTrap(int code) => throw NotYet(nameof(signalOpcodeTrap));
        public void signalPointerTrap() => throw NotYet(nameof(signalPointerTrap));
        public void signalProcessTrap() => throw NotYet(nameof(signalProcessTrap));
        public void signalRescheduleError() => throw NotYet(nameof(signalRescheduleError));
        public void signalStackError() => throw NotYet(nameof(signalStackError));
        public void signalUnboundTrap(int dst) => throw NotYet(nameof(signalUnboundTrap));
        public void signalHardwareError() => throw NotYet(nameof(signalHardwareError));
        public void signalPageFault(int faultingLongPointer) => throw NotYet(nameof(signalPageFault));
        public void signalWriteProtectFault(int faultingLongPointer) => throw NotYet(nameof(signalWriteProtectFault));
        public void signalFrameFault(int fsi) => throw NotYet(nameof(signalFrameFault));
        public void ERROR(string reason) => throw new MesaERROR(reason);
    }

    // The installed thrower — overridden by unit tests via Cpu.thrower = new ChkThrower().
    public static MesaFaultTrapThrower thrower = new RealMesaFaultTrapThrower();

    public static void logTrapOrFault(string msg) => logTrapOrFault(false, msg);

    public static void logTrapOrFault(bool logOnly, string msg)
    {
        if (!Config.LOG_OPCODES || !Config.USE_DEBUG_INTERPRETER) { return; }
        if (logOnly) { return; }
        if (!unsilenced) { return; }

        Console.Write($"## at 0x{CB:X8}+0x{savedPC:X4} [insn# {insns}]\n## {msg}");
        // The interactive (C)ontinue / (D)ebug / (A)bort prompt is omitted
        // until Phase C ports the low-level debug interpreter.
    }

    public static void boundsTrap()
    {
        logTrapOrFault(" ## boundsTrap\n");
        thrower.signalBoundsTrap();
    }

    public static void breakTrap()
    {
        logTrapOrFault(" ## breakTrap\n");
        thrower.signalBreakTrap();
    }

    public static void codeTrap(int gf)
    {
        logTrapOrFault(true, $" ## codeTrap gf=0x{gf:X4}\n");
        thrower.signalCodeTrap(gf);
    }

    public static void controlTrap(int src)
    {
        logTrapOrFault($" ## controlTrap src=0x{src:X4}\n");
        thrower.signalControlTrap(src);
    }

    public static void divCheckTrap()
    {
        logTrapOrFault(" ## divCheckTrap\n");
        thrower.signalDivideCheckTrap();
    }

    public static void divZeroTrap()
    {
        logTrapOrFault(" ## divZeroTrap\n");
        thrower.signalDivideZeroTrap();
    }

    public static void escOpcodeTrap(int code)
    {
        logError($"unimplemented ESC-opcode 0x{code & 0xFF:X2}");
        logTrapOrFault($"unimplemented ESC-opcode 0x{code & 0xFF:X2}");
        thrower.signalEscOpcodeTrap(code);
    }

    public static void interruptError()
    {
        logTrapOrFault(" ## interruptError\n");
        thrower.signalInterruptError();
    }

    public static void opcodeTrap(int code)
    {
        logError($"unimplemented opcode 0x{code & 0xFF:X2}");
        logTrapOrFault($"unimplemented opcode 0x{code & 0xFF:X2}");
        thrower.signalOpcodeTrap(code);
    }

    public static void pointerTrap()
    {
        logTrapOrFault(" ## pointerTrap\n");
        thrower.signalPointerTrap();
    }

    public static void processTrap()
    {
        logTrapOrFault(" ## processTrap\n");
        thrower.signalProcessTrap();
    }

    public static void rescheduleError()
    {
        logTrapOrFault(" ## rescheduleError\n");
        thrower.signalRescheduleError();
    }

    public static void stackError()
    {
        logTrapOrFault(" ## stackError\n");
        dumpOplog();
        thrower.signalStackError();
    }

    public static void unboundTrap(int dst)
    {
        logTrapOrFault($" ## unboundTrap dst=0x{dst:X8}\n");
        thrower.signalUnboundTrap(dst);
    }

    public static void hardwareError()
    {
        logTrapOrFault(" ## hardwareError\n");
        thrower.signalHardwareError();
    }

    public static void ERROR(string reason)
    {
        Console.WriteLine();
        Console.Out.Flush();
        Console.Error.Write($"\n**\n*** raising ERROR for reason: {reason}\n**\n");
        Console.Error.Flush();
        logTrapOrFault("** ** ** raising ERROR\n");
        thrower.ERROR(reason);
    }

    public static void nakedTrap(int controlLinkIdx)
    {
        logTrapOrFault($" ## naked trap for controlLinkIdx: {controlLinkIdx}\n");
        thrower.trap(controlLinkIdx);
    }

    public static void signalPageFault(int faultingLongPointer)
    {
        logTrapOrFault($" ## page fault for LP = 0x{faultingLongPointer:X8} at 0x{CB:X8}+0x{savedPC:X4} [insn# {insns} ]\n ");
        thrower.signalPageFault(faultingLongPointer);
    }

    public static void signalWriteProtectFault(int faultingLongPointer)
    {
        logTrapOrFault($" ## write protect fault for LP = 0x{faultingLongPointer:X8}\n ");
        thrower.signalWriteProtectFault(faultingLongPointer);
    }

    public static void signalFrameFault(int fsi)
    {
        logTrapOrFault($" ## frame fault for fsi = {fsi}\n ");
        thrower.signalFrameFault(fsi);
    }
}
