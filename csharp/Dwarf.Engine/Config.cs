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

// Configuration constants (with one runtime-mutable exception) controlling the
// logging and debugging behavior of the mesa engine.
//
// Using `const` for the boolean flags lets the JIT eliminate the
// `if (LOG_OPCODES)` branches at compile time when false — mirroring Java's
// `static final boolean` dead-code-elimination optimization.
public static class Config
{
    // log recognized opcode implementations as they are scanned and added to dispatch tables?
    public const bool LOG_OPCODE_INSTALLATION = false;

    // log opcodes and their locations as they are executed?
    public const bool LOG_OPCODES = false;

    // log as flight recorder?
    // (collect but do not write to stdout, speeding things up while keeping data
    // available if needed, e.g. on stack trap. Slows things down, but not as much
    // as direct stdout writes.)
    public const bool LOG_OPCODES_AS_FLIGHTRECORDER = false;

    // prepend the stack data before the logged instruction in flight recorder mode?
    public const bool FLIGHTRECORDER_WITH_STACK = false;

    // log memory access locations and operations?
    public const bool LOG_MEM_ACCESS = false;

    // use interactive utility for debugging opcode execution?
    public const bool USE_DEBUG_INTERPRETER = false;

    // log BITBLT/BITBLTX/COLORBLT arguments and the like
    public const bool LOG_BITBLT_INSNS = false;

    // If LOG_BITBLT_INSNS is true, logging of BITBLT and friends will only occur
    // while this flag is true. At runtime, pressing a specific key (normally F1,
    // see KeyboardMapper) sets this to true, restricting logging to specific
    // UI situations.
    public static volatile bool dynLogBitblts; // defaults to false; flipped at runtime by KeyboardMapper F1

    // Logging in io processors (agents / iop-handlers)

    public const bool IO_LOG_DISPLAY   = false;
    public const bool IO_LOG_MOUSE     = false;
    public const bool IO_LOG_KEYBOARD  = false;
    public const bool IO_LOG_DISK      = false;
    public const bool IO_LOG_FLOPPY    = false;
    public const bool IO_LOG_NETWORK   = false;
    public const bool IO_LOG_PROCESSOR = false;
    public const bool IO_LOG_TTY       = false;

    // IORegion logging (6085/daybreak only)

    public const bool IOR_LOG_MEM_ACCESS = false;
}
