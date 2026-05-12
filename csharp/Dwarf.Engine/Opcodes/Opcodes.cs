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

namespace Dwarf.Engine.Opcodes;

// Opcode dispatch table + Install API.
//
// Deviation from Java: where the Java port discovers opcodes by reflection over
// fixed-name static fields ("OPC_xA2_REC" etc.) in a known list of 8 chapter
// classes, the C# port has each chapter class expose a `RegisterAll()` method
// that calls `Opcodes.Install(...)` etc. explicitly. The Initialize entry
// points iterate the chapter classes in order. Benefits: no startup reflection
// cost; duplicate-opcode bugs surface at runtime with a clear console message;
// AOT-friendly; the "load-bearing field naming convention" landmine disappears.
// See DECISIONS.md §1.
//
// PrincOps version handling:
//   - Always-installed:  `Install` / `InstallEsc`
//   - PrincOps <= 4.0:   `InstallOld` / `InstallEscOld`  (only effective when InitializeInstructionsPrincOps40() set the mode)
//   - PrincOps >  4.0:   `InstallNew` / `InstallEscNew`  (only effective when InitializeInstructionsPrincOpsPost40() set the mode)
//
// A chapter's RegisterAll() unconditionally calls all of Install / InstallOld
// / InstallNew; Opcodes decides whether each call lands based on the current
// mode. This makes the chapter code easy to read and lets re-initialization
// in a different mode work without surprises.
public static class Opcodes
{
    public enum PrincOpsMode { None, V40, Post40 }

    // the instruction dispatch tables for regular and ESC(L) instructions
    private static readonly OpImpl[] opcTable = new OpImpl[256];
    private static readonly OpImpl[] escTable = new OpImpl[256];

    // the instruction names for regular and ESC(L) instructions
    public static readonly string[] opcNames = new string[256];
    public static readonly string[] escNames = new string[256];

    // the regular codes for the ESC(L) sub-dispatchers
    public const int zESC = 0xF8;
    public const int zESCL = 0xF9;

    // current registration mode (set by InitializeInstructions*)
    private static PrincOpsMode currentMode = PrincOpsMode.None;
    public static PrincOpsMode CurrentMode => currentMode;

    // Dispatch an instruction code for execution.
    public static void dispatch(int opcode)
    {
        opcTable[opcode]();
    }

    // ESC(L) sub-dispatcher — reads the next code byte and dispatches via escTable.
    private static readonly OpImpl opEscImpl = () => { escTable[Mem.getNextCodeByte()](); };

    // Pre-fill all instruction codes in the dispatch tables with instruction
    // traps and "invalid" names.
    private static void prepareOpcodeTables()
    {
        for (int i = 0; i < 256; i++)
        {
            int code = i; // capture
            string codeName = $"INVx{code:X2}";
            opcTable[code] = () => Cpu.opcodeTrap(code);
            escTable[code] = () => Cpu.escOpcodeTrap(code);
            opcNames[code] = codeName;
            escNames[code] = "ESC." + codeName;
        }
    }

    // Post-fill the regular dispatch table to ensure the ESC(L) dispatches are
    // present (and not overwritten by some rogue instruction).
    private static void postpareOpcodeTables()
    {
        opcTable[zESC] = opEscImpl;
        opcTable[zESCL] = opEscImpl;
    }

    // ---- Install API (called from chapter RegisterAll methods) ----

    // Register an instruction implementation (PrincOps-version-agnostic).
    public static void Install(int opcode, string opname, OpImpl impl)
    {
        innerImplant(opcode, opname, impl, opcTable, opcNames);
    }

    // Register an instruction implementation for PrincOps <= 4.0 ("old-style" GF).
    public static void InstallOld(int opcode, string opname, OpImpl impl)
    {
        if (currentMode == PrincOpsMode.V40)
        {
            innerImplant(opcode, opname, impl, opcTable, opcNames);
        }
    }

    // Register an instruction implementation for PrincOps > 4.0 ("new-style", MDS-relieved GF).
    public static void InstallNew(int opcode, string opname, OpImpl impl)
    {
        if (currentMode == PrincOpsMode.Post40)
        {
            innerImplant(opcode, opname, impl, opcTable, opcNames);
        }
    }

    // Register an ESC(L) instruction implementation.
    public static void InstallEsc(int opcode, string opname, OpImpl impl)
    {
        innerImplant(opcode, "ESC." + opname, impl, escTable, escNames);
    }

    public static void InstallEscOld(int opcode, string opname, OpImpl impl)
    {
        if (currentMode == PrincOpsMode.V40)
        {
            innerImplant(opcode, "ESC." + opname, impl, escTable, escNames);
        }
    }

    public static void InstallEscNew(int opcode, string opname, OpImpl impl)
    {
        if (currentMode == PrincOpsMode.Post40)
        {
            innerImplant(opcode, "ESC." + opname, impl, escTable, escNames);
        }
    }

    // Override an already-installed opcode (used by Misc tests).
    public static void implantOverride(int opcode, string opname, OpImpl impl)
    {
        innerImplant(opcode, opname, impl, opcTable, opcNames);
    }

    public static void implantEscOverride(int opcode, string opname, OpImpl impl)
    {
        innerImplant(opcode, "ESC." + opname, impl, escTable, escNames);
    }

    private static void innerImplant(int opcode, string opname, OpImpl impl, OpImpl[] tblOps, string[] tblNames)
    {
        if (opcode < 0 || opcode > 255)
        {
            Console.WriteLine($"** attempt to implant invalid opcode 0x{opcode:X4} - {opname}");
            return;
        }
        if (Config.LOG_OPCODE_INSTALLATION)
        {
            Console.WriteLine($"** Opcode 0x{opcode:X2} -> {opname}");
        }
        // Java's LOG_OPCODES wrapper variants (alpha/word/pair/etc.) are
        // omitted — Config.LOG_OPCODES is a `const false` and the wrappers
        // would be DCE'd anyway. Re-add in Phase F if instruction tracing
        // is wired up.
        tblOps[opcode] = impl;
        tblNames[opcode] = opname;
    }

    // ---- Initialization entry points ----

    // Install the instruction implementations for an "old-style" mesa engine
    // (PrincOps up to version 4.0).
    public static void initializeInstructionsPrincOps40()
    {
        currentMode = PrincOpsMode.V40;
        prepareOpcodeTables();
        registerAllChapters();
        postpareOpcodeTables();
    }

    // Install the instruction implementations for a "new-style" mesa engine
    // (PrincOps after version 4.0, MDS-relieved global frames).
    public static void initializeInstructionsPrincOpsPost40()
    {
        currentMode = PrincOpsMode.Post40;
        prepareOpcodeTables();
        registerAllChapters();
        postpareOpcodeTables();
    }

    // Force each chapter class to register its opcodes. The chapter list is
    // the canonical equivalent of Opcodes.java's `classes` field (lines
    // 154-163 in the Java source).
    private static void registerAllChapters()
    {
        Ch03_Memory_Organization.RegisterAll();
        Ch05_Stack_Instructions.RegisterAll();
        // Ch06_Jump_Instructions.RegisterAll();       // Phase B — pending
        // Ch07_Assignment_Instructions.RegisterAll(); // Phase B — pending
        // Ch08_Block_Transfers.RegisterAll();         // Phase B — pending
        // Ch09_Control_Transfers.RegisterAll();       // Phase B — pending
        // Ch10_Processes.RegisterAll();               // Phase B — pending
        // ChXX_Undocumented.RegisterAll();            // Phase B — pending
    }
}
