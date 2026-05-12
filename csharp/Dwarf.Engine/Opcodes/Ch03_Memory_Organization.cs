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

// Implementation of instructions defined in PrincOps 4.0 chapter 3:
// Memory Organization.
public static class Ch03_Memory_Organization
{
    // ---- 3.1.2 Memory Map Instructions ----

    // SM - Set Map
    public static readonly OpImpl ESC_x07_SM = () =>
    {
        ushort mf = Cpu.pop();
        int rp = Cpu.popLong();
        int vp = Cpu.popLong();
        Mem.setMap("SM", vp, rp, mf);
    };

    // GMF - Get Map Flags
    public static readonly OpImpl ESC_x09_GMF = () =>
    {
        int vp = Cpu.popLong();
        ushort mf = Mem.getVPageFlags(vp);
        int rp = Mem.getVPageRealPage(vp);

        Cpu.push(mf);
        Cpu.pushLong(rp);
    };

    // SMF - Set Map Flags
    public static readonly OpImpl ESC_x08_SMF = () =>
    {
        ushort newMf = Cpu.pop();
        int vp = Cpu.popLong();
        ushort mf = Mem.getVPageFlags(vp);
        int rp = Mem.getVPageRealPage(vp);

        Cpu.push(mf);
        Cpu.pushLong(rp);
        if (!Mem.isVacant(mf))
        {
            Cpu.logf("   SMF -> setMap(vp, rp, newMf)\n");
            Mem.setMap("SMF", vp, rp, newMf);
        }
    };

    // ---- 3.2.1 Main Data Space Access ----

    // LP - Lengthen Pointer
    public static readonly OpImpl OPC_xF7_LP = () =>
    {
        ushort ptr = Cpu.pop();
        Cpu.pushLong((ptr == 0) ? 0 : Cpu.lengthenPointer((int)ptr));
    };

    // ---- 3.2.3 Frame Overhead Access ----

    // ROB - Read Overhead Byte
    public static readonly OpImpl ESC_x1E_ROB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        int ptr = Cpu.pop();
        if (alpha < 1 || alpha > 4) { Cpu.ERROR("ROB :: invalid alpha = " + alpha); }
        Cpu.push(Mem.readMDSWord(ptr - alpha));
    };

    // WOB - Write Overhead Byte
    public static readonly OpImpl ESC_x1F_WOB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        int ptr = Cpu.pop();
        if (alpha < 1 || alpha > 4) { Cpu.ERROR("WOB :: invalid alpha = " + alpha); }
        Mem.writeMDSWord(ptr - alpha, Cpu.pop());
    };

    // ---- 3.3.4 Register Instructions ----

    // RRIT - Read Register IT
    public static readonly OpImpl ESC_x7D_RRIT = () =>
    {
        Cpu.pushLong(Cpu.IT());
    };

    // RRMDS - Read Register MDS
    public static readonly OpImpl ESC_x79_RRMDS = () =>
    {
        Cpu.push(Cpu.MDS >>> PrincOpsDefs.WORD_BITS);
    };

    // RRPSB - Read Register PSB
    public static readonly OpImpl ESC_x78_RRPSB = () =>
    {
        Cpu.push(Processes.psbHandle(Cpu.PSB));
    };

    // RRPTC - Read Register PTC
    public static readonly OpImpl ESC_x7C_RRPTC = () =>
    {
        Cpu.push(Cpu.PTC);
    };

    // RRWDC - Read Register WDC
    public static readonly OpImpl ESC_x7B_RRWDC = () =>
    {
        Cpu.push(Cpu.WDC);
    };

    // RRWP - Read Register WP
    public static readonly OpImpl ESC_x7A_RRWP = () =>
    {
        Cpu.push(Cpu.WP.get());
    };

    // RRXTS - Read Register XTS
    public static readonly OpImpl ESC_x7E_RRXTS = () =>
    {
        Cpu.push(Cpu.XTS);
    };

    // WRIT - Write Register IT
    public static readonly OpImpl ESC_x75_WRIT = () =>
    {
        Cpu.setIT(Cpu.popLong());
    };

    // WRMDS - Write Register MDS
    public static readonly OpImpl ESC_x71_WRMDS = () =>
    {
        Cpu.MDS = Cpu.pop() << PrincOpsDefs.WORD_BITS;
    };

    // WRMP - Write Register MP
    public static readonly OpImpl ESC_x77_WRMP = () =>
    {
        Cpu.setMP(Cpu.pop());
    };

    // WRPSB - Write Register PSB
    public static readonly OpImpl ESC_x70_WRPSB = () =>
    {
        Cpu.PSB = Processes.psbIndex(Cpu.pop());
    };

    // WRPTC - Write Register PTC
    public static readonly OpImpl ESC_x74_WRPTC = () =>
    {
        Processes.resetPTC(Cpu.pop());
    };

    // WRWDC - Write Register WDC
    public static readonly OpImpl ESC_x73_WRWDC = () =>
    {
        Cpu.WDC = Cpu.pop();
    };

    // WRWP - Write Register WP
    public static readonly OpImpl ESC_x72_WRWP = () =>
    {
        Cpu.WP.set(Cpu.pop());
    };

    // WRXTS - Write Register XTS
    public static readonly OpImpl ESC_x76_WRXTS = () =>
    {
        Cpu.XTS = Cpu.pop();
    };

    // ---- Registration ----
    //
    // Called by Opcodes.initializeInstructionsPrincOps40() and
    // Opcodes.initializeInstructionsPrincOpsPost40() to fill the dispatch
    // tables with this chapter's implementations.
    public static void RegisterAll()
    {
        // Mapping
        Opcodes.InstallEsc(0x07, "SM",  ESC_x07_SM);
        Opcodes.InstallEsc(0x08, "SMF", ESC_x08_SMF);
        Opcodes.InstallEsc(0x09, "GMF", ESC_x09_GMF);

        // Main Data Space Access
        Opcodes.Install(0xF7, "LP", OPC_xF7_LP);

        // Frame Overhead Access
        Opcodes.InstallEsc(0x1E, "ROB", ESC_x1E_ROB_alpha);
        Opcodes.InstallEsc(0x1F, "WOB", ESC_x1F_WOB_alpha);

        // Register reads
        Opcodes.InstallEsc(0x78, "RRPSB", ESC_x78_RRPSB);
        Opcodes.InstallEsc(0x79, "RRMDS", ESC_x79_RRMDS);
        Opcodes.InstallEsc(0x7A, "RRWP",  ESC_x7A_RRWP);
        Opcodes.InstallEsc(0x7B, "RRWDC", ESC_x7B_RRWDC);
        Opcodes.InstallEsc(0x7C, "RRPTC", ESC_x7C_RRPTC);
        Opcodes.InstallEsc(0x7D, "RRIT",  ESC_x7D_RRIT);
        Opcodes.InstallEsc(0x7E, "RRXTS", ESC_x7E_RRXTS);

        // Register writes
        Opcodes.InstallEsc(0x70, "WRPSB", ESC_x70_WRPSB);
        Opcodes.InstallEsc(0x71, "WRMDS", ESC_x71_WRMDS);
        Opcodes.InstallEsc(0x72, "WRWP",  ESC_x72_WRWP);
        Opcodes.InstallEsc(0x73, "WRWDC", ESC_x73_WRWDC);
        Opcodes.InstallEsc(0x74, "WRPTC", ESC_x74_WRPTC);
        Opcodes.InstallEsc(0x75, "WRIT",  ESC_x75_WRIT);
        Opcodes.InstallEsc(0x76, "WRXTS", ESC_x76_WRXTS);
        Opcodes.InstallEsc(0x77, "WRMP",  ESC_x77_WRMP);
    }
}
