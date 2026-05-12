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

// Java uses `RealMesaFaultTrapThrower` as an internal abstraction that throws
// a runtime exception to unwind control flow on a Mesa trap or fault. We do
// the same in C# with this `MesaTrap` exception type — Cpu's trap methods
// throw it, and the main dispatch loop (in Phase B / C) catches it to perform
// the trap dispatch.
public enum MesaTrapKind
{
    // PrincOps-defined faults (signalled by Cpu.signal*Fault)
    PageFault,
    WriteProtectFault,
    FrameFault,

    // PrincOps-defined traps (signalled by Cpu.*Trap)
    BreakTrap,
    BoundsTrap,
    CodeTrap,
    ControlTrap,
    DivCheckTrap,
    DivZeroTrap,
    OpcodeTrap,
    EscOpcodeTrap,
    PointerTrap,
    ProcessTrap,
    UnboundTrap,
    XferTrap,

    // Errors (uncatchable — emulator gives up)
    StackError,
    RescheduleError,
    InterruptError,
    HardwareError,
}

public sealed class MesaTrap : Exception
{
    public MesaTrapKind Kind { get; }

    // Trap-specific parameter (e.g., the long-pointer that caused a page fault,
    // or the opcode number that triggered OpcodeTrap). Zero when not applicable.
    public long Param { get; }

    public MesaTrap(MesaTrapKind kind, long param = 0)
        : base($"MesaTrap: {kind} (param=0x{param:X})")
    {
        Kind = kind;
        Param = param;
    }

    public MesaTrap(MesaTrapKind kind, string message)
        : base($"MesaTrap: {kind} -- {message}")
    {
        Kind = kind;
        Param = 0;
    }
}
