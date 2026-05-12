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

// Implementation of PrincOps 4.0 chapter 7: Assignment Instructions
// (plus some instructions from the "changed chapters" document).
public static class Ch07_Assignment_Instructions
{
    // ==================================================================
    // 7.1 Immediate Instructions
    // ==================================================================

    public static readonly OpImpl OPC_xCB_LIN1 = () => { Cpu.push((short)-1); };
    public static readonly OpImpl OPC_xCC_LINI = () => { Cpu.push((ushort)0x8000); };

    public static readonly OpImpl OPC_xD1_LID0 = () =>
    {
        Cpu.push((ushort)0); // a bit faster than ...
        Cpu.push((ushort)0); // ... pushLong(0)
    };

    public static readonly OpImpl OPC_xC0_LI0  = () => { Cpu.push((ushort)0); };
    public static readonly OpImpl OPC_xC1_LI1  = () => { Cpu.push((ushort)1); };
    public static readonly OpImpl OPC_xC2_LI2  = () => { Cpu.push((ushort)2); };
    public static readonly OpImpl OPC_xC3_LI3  = () => { Cpu.push((ushort)3); };
    public static readonly OpImpl OPC_xC4_LI4  = () => { Cpu.push((ushort)4); };
    public static readonly OpImpl OPC_xC5_LI5  = () => { Cpu.push((ushort)5); };
    public static readonly OpImpl OPC_xC6_LI6  = () => { Cpu.push((ushort)6); };
    public static readonly OpImpl OPC_xC7_LI7  = () => { Cpu.push((ushort)7); };
    public static readonly OpImpl OPC_xC8_LI8  = () => { Cpu.push((ushort)8); };
    public static readonly OpImpl OPC_xC9_LI9  = () => { Cpu.push((ushort)9); };
    public static readonly OpImpl OPC_xCA_LI10 = () => { Cpu.push((ushort)10); };

    public static readonly OpImpl OPC_xCD_LIB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        Cpu.push(alpha);
    };

    public static readonly OpImpl OPC_xCF_LINB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        Cpu.push(alpha | 0xFF00);
    };

    public static readonly OpImpl OPC_xD0_LIHB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        Cpu.push(alpha << 8);
    };

    public static readonly OpImpl OPC_xCE_LIW_word = () =>
    {
        int u = Mem.getNextCodeWord();
        Cpu.push(u);
    };

    // ==================================================================
    // 7.2 Frame Instructions
    // ==================================================================

    // ---- 7.2.1 Local Frame Access ----

    public static readonly OpImpl OPC_xD2_LA0 = () => { Cpu.push(Cpu.LF); };
    public static readonly OpImpl OPC_xD3_LA1 = () => { Cpu.push(Cpu.LF + 1); };
    public static readonly OpImpl OPC_xD4_LA2 = () => { Cpu.push(Cpu.LF + 2); };
    public static readonly OpImpl OPC_xD5_LA3 = () => { Cpu.push(Cpu.LF + 3); };
    public static readonly OpImpl OPC_xD6_LA6 = () => { Cpu.push(Cpu.LF + 6); };
    public static readonly OpImpl OPC_xD7_LA8 = () => { Cpu.push(Cpu.LF + 8); };

    public static readonly OpImpl OPC_xD8_LAB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        Cpu.push(Cpu.LF + alpha);
    };

    public static readonly OpImpl OPC_xD9_LAW_word = () =>
    {
        int word = Mem.getNextCodeWord();
        Cpu.push(Cpu.LF + word);
    };

    // ---- 7.2.1.1 Load Local ----

    public static readonly OpImpl OPC_x01_LL0  = () => { Cpu.push(Mem.readMDSWord(Cpu.LF)); };
    public static readonly OpImpl OPC_x02_LL1  = () => { Cpu.push(Mem.readMDSWord(Cpu.LF, 1)); };
    public static readonly OpImpl OPC_x03_LL2  = () => { Cpu.push(Mem.readMDSWord(Cpu.LF, 2)); };
    public static readonly OpImpl OPC_x04_LL3  = () => { Cpu.push(Mem.readMDSWord(Cpu.LF, 3)); };
    public static readonly OpImpl OPC_x05_LL4  = () => { Cpu.push(Mem.readMDSWord(Cpu.LF, 4)); };
    public static readonly OpImpl OPC_x06_LL5  = () => { Cpu.push(Mem.readMDSWord(Cpu.LF, 5)); };
    public static readonly OpImpl OPC_x07_LL6  = () => { Cpu.push(Mem.readMDSWord(Cpu.LF, 6)); };
    public static readonly OpImpl OPC_x08_LL7  = () => { Cpu.push(Mem.readMDSWord(Cpu.LF, 7)); };
    public static readonly OpImpl OPC_x09_LL8  = () => { Cpu.push(Mem.readMDSWord(Cpu.LF, 8)); };
    public static readonly OpImpl OPC_x0A_LL9  = () => { Cpu.push(Mem.readMDSWord(Cpu.LF, 9)); };
    public static readonly OpImpl OPC_x0B_LL10 = () => { Cpu.push(Mem.readMDSWord(Cpu.LF, 10)); };
    public static readonly OpImpl OPC_x0C_LL11 = () => { Cpu.push(Mem.readMDSWord(Cpu.LF, 11)); };

    public static readonly OpImpl OPC_x0D_LLB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        Cpu.push(Mem.readMDSWord(Cpu.LF, alpha));
    };

    public static readonly OpImpl OPC_x0E_LLD0 = () =>
    {
        Cpu.push(Mem.readMDSWord(Cpu.LF));
        Cpu.push(Mem.readMDSWord(Cpu.LF, 1));
    };
    public static readonly OpImpl OPC_x0F_LLD1 = () =>
    {
        Cpu.push(Mem.readMDSWord(Cpu.LF, 1));
        Cpu.push(Mem.readMDSWord(Cpu.LF, 2));
    };
    public static readonly OpImpl OPC_x10_LLD2 = () =>
    {
        Cpu.push(Mem.readMDSWord(Cpu.LF, 2));
        Cpu.push(Mem.readMDSWord(Cpu.LF, 3));
    };
    public static readonly OpImpl OPC_x11_LLD3 = () =>
    {
        Cpu.push(Mem.readMDSWord(Cpu.LF, 3));
        Cpu.push(Mem.readMDSWord(Cpu.LF, 4));
    };
    public static readonly OpImpl OPC_x12_LLD4 = () =>
    {
        Cpu.push(Mem.readMDSWord(Cpu.LF, 4));
        Cpu.push(Mem.readMDSWord(Cpu.LF, 5));
    };
    public static readonly OpImpl OPC_x13_LLD5 = () =>
    {
        Cpu.push(Mem.readMDSWord(Cpu.LF, 5));
        Cpu.push(Mem.readMDSWord(Cpu.LF, 6));
    };
    public static readonly OpImpl OPC_x14_LLD6 = () =>
    {
        Cpu.push(Mem.readMDSWord(Cpu.LF, 6));
        Cpu.push(Mem.readMDSWord(Cpu.LF, 7));
    };
    public static readonly OpImpl OPC_x15_LLD7 = () =>
    {
        Cpu.push(Mem.readMDSWord(Cpu.LF, 7));
        Cpu.push(Mem.readMDSWord(Cpu.LF, 8));
    };
    public static readonly OpImpl OPC_x16_LLD8 = () =>
    {
        Cpu.push(Mem.readMDSWord(Cpu.LF, 8));
        Cpu.push(Mem.readMDSWord(Cpu.LF, 9));
    };
    public static readonly OpImpl OPC_x17_LLD10 = () =>
    {
        Cpu.push(Mem.readMDSWord(Cpu.LF, 10));
        Cpu.push(Mem.readMDSWord(Cpu.LF, 11));
    };

    public static readonly OpImpl OPC_x18_LLDB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        Cpu.push(Mem.readMDSWord(Cpu.LF, alpha));
        Cpu.push(Mem.readMDSWord(Cpu.LF, alpha + 1));
    };

    // ---- 7.2.1.2 Store Local ----

    public static readonly OpImpl OPC_x19_SL0  = () => { Mem.writeMDSWord(Cpu.LF, Cpu.pop()); };
    public static readonly OpImpl OPC_x1A_SL1  = () => { Mem.writeMDSWord(Cpu.LF, 1, Cpu.pop()); };
    public static readonly OpImpl OPC_x1B_SL2  = () => { Mem.writeMDSWord(Cpu.LF, 2, Cpu.pop()); };
    public static readonly OpImpl OPC_x1C_SL3  = () => { Mem.writeMDSWord(Cpu.LF, 3, Cpu.pop()); };
    public static readonly OpImpl OPC_x1D_SL4  = () => { Mem.writeMDSWord(Cpu.LF, 4, Cpu.pop()); };
    public static readonly OpImpl OPC_x1E_SL5  = () => { Mem.writeMDSWord(Cpu.LF, 5, Cpu.pop()); };
    public static readonly OpImpl OPC_x1F_SL6  = () => { Mem.writeMDSWord(Cpu.LF, 6, Cpu.pop()); };
    public static readonly OpImpl OPC_x20_SL7  = () => { Mem.writeMDSWord(Cpu.LF, 7, Cpu.pop()); };
    public static readonly OpImpl OPC_x21_SL8  = () => { Mem.writeMDSWord(Cpu.LF, 8, Cpu.pop()); };
    public static readonly OpImpl OPC_x22_SL9  = () => { Mem.writeMDSWord(Cpu.LF, 9, Cpu.pop()); };
    public static readonly OpImpl OPC_x23_SL10 = () => { Mem.writeMDSWord(Cpu.LF, 10, Cpu.pop()); };

    public static readonly OpImpl OPC_x24_SLB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        Mem.writeMDSWord(Cpu.LF, alpha, Cpu.pop());
    };

    public static readonly OpImpl OPC_x25_SLD0 = () =>
    {
        Mem.writeMDSWord(Cpu.LF, 1, Cpu.pop());
        Mem.writeMDSWord(Cpu.LF, Cpu.pop());
    };
    public static readonly OpImpl OPC_x26_SLD1 = () =>
    {
        Mem.writeMDSWord(Cpu.LF, 2, Cpu.pop());
        Mem.writeMDSWord(Cpu.LF, 1, Cpu.pop());
    };
    public static readonly OpImpl OPC_x27_SLD2 = () =>
    {
        Mem.writeMDSWord(Cpu.LF, 3, Cpu.pop());
        Mem.writeMDSWord(Cpu.LF, 2, Cpu.pop());
    };
    public static readonly OpImpl OPC_x28_SLD3 = () =>
    {
        Mem.writeMDSWord(Cpu.LF, 4, Cpu.pop());
        Mem.writeMDSWord(Cpu.LF, 3, Cpu.pop());
    };
    public static readonly OpImpl OPC_x29_SLD4 = () =>
    {
        Mem.writeMDSWord(Cpu.LF, 5, Cpu.pop());
        Mem.writeMDSWord(Cpu.LF, 4, Cpu.pop());
    };
    public static readonly OpImpl OPC_x2A_SLD5 = () =>
    {
        Mem.writeMDSWord(Cpu.LF, 6, Cpu.pop());
        Mem.writeMDSWord(Cpu.LF, 5, Cpu.pop());
    };
    public static readonly OpImpl OPC_x2B_SLD6 = () =>
    {
        Mem.writeMDSWord(Cpu.LF, 7, Cpu.pop());
        Mem.writeMDSWord(Cpu.LF, 6, Cpu.pop());
    };
    public static readonly OpImpl OPC_x2C_SLD8 = () =>
    {
        Mem.writeMDSWord(Cpu.LF, 9, Cpu.pop());
        Mem.writeMDSWord(Cpu.LF, 8, Cpu.pop());
    };

    public static readonly OpImpl OPC_x75_SLDB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        Mem.writeMDSWord(Cpu.LF, alpha + 1, Cpu.pop());
        Mem.writeMDSWord(Cpu.LF, alpha, Cpu.pop());
    };

    // ---- 7.2.1.3 Put Local ----

    public static readonly OpImpl OPC_x2D_PL0 = () => { Mem.writeMDSWord(Cpu.LF, Cpu.popRecover()); };
    public static readonly OpImpl OPC_x2E_PL1 = () => { Mem.writeMDSWord(Cpu.LF, 1, Cpu.popRecover()); };
    public static readonly OpImpl OPC_x2F_PL2 = () => { Mem.writeMDSWord(Cpu.LF, 2, Cpu.popRecover()); };
    public static readonly OpImpl OPC_x30_PL3 = () => { Mem.writeMDSWord(Cpu.LF, 3, Cpu.popRecover()); };

    public static readonly OpImpl OPC_x31_PLB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        Mem.writeMDSWord(Cpu.LF, alpha, Cpu.popRecover());
    };

    public static readonly OpImpl OPC_x32_PLD0 = () =>
    {
        Mem.writeMDSWord(Cpu.LF, 1, Cpu.pop());
        Mem.writeMDSWord(Cpu.LF, Cpu.pop());
        Cpu.recover();
        Cpu.recover();
    };

    public static readonly OpImpl OPC_x33_PLDB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        Mem.writeMDSWord(Cpu.LF, alpha + 1, Cpu.pop());
        Mem.writeMDSWord(Cpu.LF, alpha, Cpu.pop());
        Cpu.recover();
        Cpu.recover();
    };

    // ---- 7.2.1.4 Add Local ----

    public static readonly OpImpl OPC_xBB_AL0IB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        // assuming that UNSPECIFIED is unsigned
        Cpu.push((ushort)((Mem.readMDSWord(Cpu.LF) + alpha) & 0xFFFF));
    };

    // ---- 7.2.2 Global Frame Access ----
    //
    // post 4.0-PrincOps assumption: GF32 is a 32-bit address into the MDS.
    // The MDS base is 64Kword aligned, so the lower 16 bits of GF32 are the
    // valid POINTER to the global frame in the MDS. ` & 0xFFFF ` is explicit.

    public static readonly OpImpl OPCo_xDA_GA0 = () => { Cpu.push(Cpu.GF16); };
    public static readonly OpImpl OPCn_xDA_GA0 = () => { Cpu.push(Cpu.GF32 & 0xFFFF); };

    public static readonly OpImpl OPCo_xDB_GA1 = () => { Cpu.push(Cpu.GF16 + 1); };
    public static readonly OpImpl OPCn_xDB_GA1 = () => { Cpu.push((Cpu.GF32 + 1) & 0xFFFF); };

    public static readonly OpImpl OPCo_xDC_GAB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        Cpu.push(Cpu.GF16 + alpha);
    };
    public static readonly OpImpl OPCn_xDC_GAB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        Cpu.push((Cpu.GF32 + alpha) & 0xFFFF);
    };

    public static readonly OpImpl OPCo_xDD_GAW_word = () =>
    {
        int word = Mem.getNextCodeWord();
        Cpu.push(Cpu.GF16 + word);
    };
    public static readonly OpImpl OPCn_xDD_GAW_word = () =>
    {
        int word = Mem.getNextCodeWord();
        Cpu.push((Cpu.GF32 + word) & 0xFFFF);
    };

    // ---- 7.2.2 Global Frame Access (changed chapters, post-4.0 only) ----

    public static readonly OpImpl OPCn_xFA_LGA0 = () => { Cpu.pushLong(Cpu.GF32); };

    public static readonly OpImpl OPCn_xFB_LGAB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        Cpu.pushLong(Cpu.GF32 + alpha);
    };

    public static readonly OpImpl OPCn_xFC_LGAW_word = () =>
    {
        int word = Mem.getNextCodeWord();
        Cpu.pushLong(Cpu.GF32 + word);
    };

    // ---- 7.2.2.1 Load Global ----

    public static readonly OpImpl OPCo_x34_LG0 = () => { Cpu.push(Mem.readMDSWord(Cpu.GF16)); };
    public static readonly OpImpl OPCn_x34_LG0 = () => { Cpu.push(Mem.readWord(Cpu.GF32)); };

    public static readonly OpImpl OPCo_x35_LG1 = () => { Cpu.push(Mem.readMDSWord(Cpu.GF16, 1)); };
    public static readonly OpImpl OPCn_x35_LG1 = () => { Cpu.push(Mem.readWord(Cpu.GF32 + 1)); };

    public static readonly OpImpl OPCo_x36_LG2 = () => { Cpu.push(Mem.readMDSWord(Cpu.GF16, 2)); };
    public static readonly OpImpl OPCn_x36_LG2 = () => { Cpu.push(Mem.readWord(Cpu.GF32 + 2)); };

    public static readonly OpImpl OPCo_x37_LGB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        Cpu.push(Mem.readMDSWord(Cpu.GF16, alpha));
    };
    public static readonly OpImpl OPCn_x37_LGB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        Cpu.push(Mem.readWord(Cpu.GF32 + alpha));
    };

    public static readonly OpImpl OPCo_x38_LGD0 = () =>
    {
        Cpu.push(Mem.readMDSWord(Cpu.GF16));
        Cpu.push(Mem.readMDSWord(Cpu.GF16, 1));
    };
    public static readonly OpImpl OPCn_x38_LGD0 = () =>
    {
        Cpu.push(Mem.readWord(Cpu.GF32));
        Cpu.push(Mem.readWord(Cpu.GF32 + 1));
    };

    public static readonly OpImpl OPCo_x39_LGD2 = () =>
    {
        Cpu.push(Mem.readMDSWord(Cpu.GF16, 2));
        Cpu.push(Mem.readMDSWord(Cpu.GF16, 3));
    };
    public static readonly OpImpl OPCn_x39_LGD2 = () =>
    {
        Cpu.push(Mem.readWord(Cpu.GF32 + 2));
        Cpu.push(Mem.readWord(Cpu.GF32 + 3));
    };

    public static readonly OpImpl OPCo_x3A_LGDB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        Cpu.push(Mem.readMDSWord(Cpu.GF16, alpha));
        Cpu.push(Mem.readMDSWord(Cpu.GF16, alpha + 1));
    };
    public static readonly OpImpl OPCn_x3A_LGDB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        Cpu.push(Mem.readWord(Cpu.GF32 + alpha));
        Cpu.push(Mem.readWord(Cpu.GF32 + alpha + 1));
    };

    // ---- 7.2.2.2 Store Global ----

    public static readonly OpImpl OPCo_x3B_SGB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        Mem.writeMDSWord(Cpu.GF16, alpha, Cpu.pop());
    };
    public static readonly OpImpl OPCn_x3B_SGB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        Mem.writeWord(Cpu.GF32 + alpha, Cpu.pop());
    };

    public static readonly OpImpl OPCo_x76_SGDB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        Mem.writeMDSWord(Cpu.GF16, alpha + 1, Cpu.pop());
        Mem.writeMDSWord(Cpu.GF16, alpha, Cpu.pop());
    };
    public static readonly OpImpl OPCn_x76_SGDB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        Mem.writeWord(Cpu.GF32 + alpha + 1, Cpu.pop());
        Mem.writeWord(Cpu.GF32 + alpha, Cpu.pop());
    };

    // ==================================================================
    // 7.3 Pointer Instructions
    // ==================================================================

    // ---- 7.3.1.1 Read Direct ----

    public static readonly OpImpl OPC_x40_R0 = () =>
    {
        ushort pointer = Cpu.pop();
        Cpu.push(Mem.readMDSWord(pointer));
    };

    public static readonly OpImpl OPC_x41_R1 = () =>
    {
        ushort pointer = Cpu.pop();
        Cpu.push(Mem.readMDSWord(pointer, 1));
    };

    public static readonly OpImpl OPC_x42_RB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        ushort pointer = Cpu.pop();
        Cpu.push(Mem.readMDSWord(pointer, alpha));
    };

    public static readonly OpImpl OPC_x43_RL0 = () =>
    {
        int longPointer = Cpu.popLong();
        Cpu.push(Mem.readWord(longPointer));
    };

    public static readonly OpImpl OPC_x44_RLB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        int longPointer = Cpu.popLong();
        Cpu.push(Mem.readWord(longPointer + alpha));
    };

    public static readonly OpImpl OPC_x45_RD0 = () =>
    {
        ushort pointer = Cpu.pop();
        ushort u = Mem.readMDSWord(pointer);
        ushort v = Mem.readMDSWord(pointer, 1);
        Cpu.push(u);
        Cpu.push(v);
    };

    public static readonly OpImpl OPC_x46_RDB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        ushort pointer = Cpu.pop();
        ushort u = Mem.readMDSWord(pointer, alpha);
        ushort v = Mem.readMDSWord(pointer, alpha + 1);
        Cpu.push(u);
        Cpu.push(v);
    };

    public static readonly OpImpl OPC_x47_RDL0 = () =>
    {
        int longPointer = Cpu.popLong();
        ushort u = Mem.readWord(longPointer);
        ushort v = Mem.readWord(longPointer + 1);
        Cpu.push(u);
        Cpu.push(v);
    };

    public static readonly OpImpl OPC_x48_RDLB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        int longPointer = Cpu.popLong();
        ushort u = Mem.readWord(longPointer + alpha);
        ushort v = Mem.readWord(longPointer + alpha + 1);
        Cpu.push(u);
        Cpu.push(v);
    };

    public static readonly OpImpl ESC_x1B_RC_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        int offset = Cpu.pop();
        Cpu.push(Mem.readCode(offset + alpha));
    };

    // ---- 7.3.1.2 Write Direct ----

    public static readonly OpImpl OPC_x49_W0 = () =>
    {
        ushort pointer = Cpu.pop();
        Mem.writeMDSWord(pointer, Cpu.pop());
    };

    public static readonly OpImpl OPC_x4A_WB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        ushort pointer = Cpu.pop();
        Mem.writeMDSWord(pointer, alpha, Cpu.pop());
    };

    public static readonly OpImpl OPC_x4C_WLB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        int longPointer = Cpu.popLong();
        Mem.writeWord(longPointer + alpha, Cpu.pop());
    };

    public static readonly OpImpl OPC_x4E_WDB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        ushort pointer = Cpu.pop();
        Mem.writeMDSWord(pointer, alpha + 1, Cpu.pop());
        Mem.writeMDSWord(pointer, alpha, Cpu.pop());
    };

    public static readonly OpImpl OPC_x51_WDLB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        int longPointer = Cpu.popLong();
        Mem.writeWord(longPointer + alpha + 1, Cpu.pop());
        Mem.writeWord(longPointer + alpha, Cpu.pop());
    };

    // ---- 7.3.1.3 Put Swapped Direct ----

    public static readonly OpImpl OPC_x4B_PSB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        ushort u = Cpu.pop();
        ushort pointer = Cpu.pop();
        Mem.writeMDSWord(pointer, alpha, u);
        Cpu.recover();
    };

    public static readonly OpImpl OPC_x4F_PSD0 = () =>
    {
        ushort v = Cpu.pop();
        ushort u = Cpu.pop();
        ushort pointer = Cpu.pop();
        Mem.writeMDSWord(pointer, 1, v);
        Mem.writeMDSWord(pointer, u);
        Cpu.recover();
    };

    public static readonly OpImpl OPC_x50_PSDB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        ushort v = Cpu.pop();
        ushort u = Cpu.pop();
        ushort pointer = Cpu.pop();
        Mem.writeMDSWord(pointer, alpha + 1, v);
        Mem.writeMDSWord(pointer, alpha, u);
        Cpu.recover();
    };

    public static readonly OpImpl OPC_x4D_PSLB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        ushort u = Cpu.pop();
        int longPointer = Cpu.popLong();
        Mem.writeWord(longPointer + alpha, u);
        Cpu.recover();
        Cpu.recover();
    };

    public static readonly OpImpl OPC_x52_PSDLB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        ushort v = Cpu.pop();
        ushort u = Cpu.pop();
        int longPointer = Cpu.popLong();
        Mem.writeWord(longPointer + alpha + 1, v);
        Mem.writeWord(longPointer + alpha, u);
        Cpu.recover();
        Cpu.recover();
    };

    // ---- 7.3.2.1 Read Indirect ----

    public static readonly OpImpl OPC_x53_RLI00 = () =>
    {
        ushort pointer = Mem.readMDSWord(Cpu.LF);
        Cpu.push(Mem.readMDSWord(pointer));
    };

    public static readonly OpImpl OPC_x54_RLI01 = () =>
    {
        ushort pointer = Mem.readMDSWord(Cpu.LF);
        Cpu.push(Mem.readMDSWord(pointer, 1));
    };

    public static readonly OpImpl OPC_x55_RLI02 = () =>
    {
        ushort pointer = Mem.readMDSWord(Cpu.LF);
        Cpu.push(Mem.readMDSWord(pointer, 2));
    };

    public static readonly OpImpl OPC_x56_RLI03 = () =>
    {
        ushort pointer = Mem.readMDSWord(Cpu.LF);
        Cpu.push(Mem.readMDSWord(pointer, 3));
    };

    public static readonly OpImpl OPC_x57_RLIP_pair = () =>
    {
        int pair = Mem.getNextCodeByte();
        ushort pointer = Mem.readMDSWord(Cpu.LF + (pair >>> 4));
        Cpu.push(Mem.readMDSWord(pointer + (pair & 0x0F)));
    };

    public static readonly OpImpl OPC_x58_RLILP_pair = () =>
    {
        int pair = Mem.getNextCodeByte();
        int longPointer = Mem.readMDSDblWord(Cpu.LF + (pair >>> 4));
        Cpu.push(Mem.readWord(longPointer + (pair & 0x0F)));
    };

    public static readonly OpImpl OPCo_x5C_RGIP_pair = () =>
    {
        int pair = Mem.getNextCodeByte();
        ushort pointer = Mem.readMDSWord(Cpu.GF16 + (pair >>> 4));
        Cpu.push(Mem.readMDSWord(pointer + (pair & 0x0F)));
    };
    public static readonly OpImpl OPCn_x5C_RGIP_pair = () =>
    {
        int pair = Mem.getNextCodeByte();
        ushort pointer = Mem.readWord(Cpu.GF32 + (pair >>> 4));
        Cpu.push(Mem.readMDSWord(pointer + (pair & 0x0F)));
    };

    public static readonly OpImpl OPCo_x5D_RGILP_pair = () =>
    {
        int pair = Mem.getNextCodeByte();
        int longPointer = Mem.readMDSDblWord(Cpu.GF16 + (pair >>> 4));
        Cpu.push(Mem.readWord(longPointer + (pair & 0x0F)));
    };
    public static readonly OpImpl OPCn_x5D_RGILP_pair = () =>
    {
        int pair = Mem.getNextCodeByte();
        int longPointer = Mem.readDblWord(Cpu.GF32 + (pair >>> 4));
        Cpu.push(Mem.readWord(longPointer + (pair & 0x0F)));
    };

    public static readonly OpImpl OPC_x59_RLDI00 = () =>
    {
        ushort pointer = Mem.readMDSWord(Cpu.LF);
        ushort u = Mem.readMDSWord(pointer);
        ushort v = Mem.readMDSWord(pointer + 1);
        Cpu.push(u);
        Cpu.push(v);
    };

    public static readonly OpImpl OPC_x5A_RLDIP_pair = () =>
    {
        int pair = Mem.getNextCodeByte();
        ushort pointer = Mem.readMDSWord(Cpu.LF + (pair >>> 4));
        ushort u = Mem.readMDSWord(pointer + (pair & 0x0F));
        ushort v = Mem.readMDSWord(pointer + (pair & 0x0F) + 1);
        Cpu.push(u);
        Cpu.push(v);
    };

    public static readonly OpImpl OPC_x5B_RLDILP_pair = () =>
    {
        int pair = Mem.getNextCodeByte();
        int longPointer = Mem.readMDSDblWord(Cpu.LF + (pair >>> 4));
        ushort u = Mem.readWord(longPointer + (pair & 0x0F));
        ushort v = Mem.readWord(longPointer + (pair & 0x0F) + 1);
        Cpu.push(u);
        Cpu.push(v);
    };

    // ---- 7.3.2.2 Write Indirect ----

    public static readonly OpImpl OPC_x5E_WLIP_pair = () =>
    {
        int pair = Mem.getNextCodeByte();
        ushort pointer = Mem.readMDSWord(Cpu.LF + (pair >>> 4));
        Mem.writeMDSWord(pointer + (pair & 0x0F), Cpu.pop());
    };

    public static readonly OpImpl OPC_x5F_WLILP_pair = () =>
    {
        int pair = Mem.getNextCodeByte();
        int longPointer = Mem.readMDSDblWord(Cpu.LF + (pair >>> 4));
        Mem.writeWord(longPointer + (pair & 0x0F), Cpu.pop());
    };

    public static readonly OpImpl OPC_x60_WLDILP_pair = () =>
    {
        int pair = Mem.getNextCodeByte();
        int longPointer = Mem.readMDSDblWord(Cpu.LF + (pair >>> 4));
        Mem.writeWord(longPointer + (pair & 0x0F) + 1, Cpu.pop());
        Mem.writeWord(longPointer + (pair & 0x0F), Cpu.pop());
    };

    // ==================================================================
    // 7.4 String Instructions
    // ==================================================================

    public static readonly OpImpl OPC_x61_RS_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        int index = Cpu.pop();
        int pointer = Cpu.pop();
        Cpu.push(Mem.fetchByte(Cpu.lengthenPointer(pointer), alpha + index));
    };

    public static readonly OpImpl OPC_x62_RLS_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        int index = Cpu.pop();
        int longPointer = Cpu.popLong();
        Cpu.push(Mem.fetchByte(longPointer, alpha + index));
    };

    public static readonly OpImpl OPC_x63_WS_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        int index = Cpu.pop();
        int pointer = Cpu.pop();
        ushort data = (ushort)(Cpu.pop() & 0x00FF);
        Mem.storeByte(Cpu.lengthenPointer(pointer), alpha + index, data);
    };

    public static readonly OpImpl OPC_x64_WLS_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        int index = Cpu.pop();
        int longPointer = Cpu.popLong();
        ushort data = (ushort)(Cpu.pop() & 0x00FF);
        Mem.storeByte(longPointer, alpha + index, data);
    };

    // ==================================================================
    // 7.5 Field Instructions
    // ==================================================================

    // ---- 7.5.1 Read Field ----

    public static readonly OpImpl OPC_x66_RF_word = () =>
    {
        int fieldDesc = Mem.getNextCodeWord();
        int pointer = Cpu.pop();
        Cpu.push(Mem.readField(Mem.readMDSWord(pointer, fieldDesc >>> 8), fieldDesc & 0xFF));
    };

    public static readonly OpImpl OPC_x65_R0F_alpha = () =>
    {
        int spec = Mem.getNextCodeByte();
        int pointer = Cpu.pop();
        Cpu.push(Mem.readField(Mem.readMDSWord(pointer), spec));
    };

    public static readonly OpImpl OPC_x68_RLF_word = () =>
    {
        int fieldDesc = Mem.getNextCodeWord();
        int longPointer = Cpu.popLong();
        Cpu.push(Mem.readField(Mem.readWord(longPointer + (fieldDesc >>> 8)), fieldDesc & 0xFF));
    };

    public static readonly OpImpl OPC_x67_RL0F_alpha = () =>
    {
        int spec = Mem.getNextCodeByte();
        int longPointer = Cpu.popLong();
        Cpu.push(Mem.readField(Mem.readWord(longPointer), spec));
    };

    public static readonly OpImpl OPC_x69_RLFS = () =>
    {
        int fieldDesc = Cpu.pop();
        int longPointer = Cpu.popLong();
        Cpu.push(Mem.readField(Mem.readWord(longPointer + (fieldDesc >>> 8)), fieldDesc & 0xFF));
    };

    public static readonly OpImpl ESC_x1A_RCFS = () =>
    {
        int fieldDesc = Cpu.pop();
        int offset = Cpu.pop();
        Cpu.push(Mem.readField(Mem.readCode(offset + (fieldDesc >>> 8)), fieldDesc & 0xFF));
    };

    public static readonly OpImpl OPC_x6A_RLIPF_alphabeta = () =>
    {
        int pair = Mem.getNextCodeByte();
        int spec = Mem.getNextCodeByte();
        int pointer = Mem.readMDSWord(Cpu.LF, pair >>> 4);
        Cpu.push(Mem.readField(Mem.readMDSWord(pointer, pair & 0x0F), spec));
    };

    public static readonly OpImpl OPC_x6B_RLILPF_alphabeta = () =>
    {
        int pair = Mem.getNextCodeByte();
        int spec = Mem.getNextCodeByte();
        int longPointer = Mem.readMDSDblWord(Cpu.LF, pair >>> 4);
        Cpu.push(Mem.readField(Mem.readWord(longPointer + (pair & 0x0F)), spec));
    };

    // ---- 7.5.2 Write Field ----

    public static readonly OpImpl OPC_x6D_WF_word = () =>
    {
        int fieldDesc = Mem.getNextCodeWord();
        int pointer = Cpu.pop();
        ushort data = Cpu.pop();
        int offset = fieldDesc >>> 8;
        ushort src = Mem.readMDSWord(pointer, offset);
        Mem.writeMDSWord(pointer, offset, Mem.writeField(src, fieldDesc & 0xFF, data));
    };

    public static readonly OpImpl OPC_x6C_W0F_alpha = () =>
    {
        int spec = Mem.getNextCodeByte();
        int pointer = Cpu.pop();
        ushort data = Cpu.pop();
        ushort src = Mem.readMDSWord(pointer);
        Mem.writeMDSWord(pointer, Mem.writeField(src, spec, data));
    };

    public static readonly OpImpl OPC_x72_WLF_word = () =>
    {
        int fieldDesc = Mem.getNextCodeWord();
        int longPointer = Cpu.popLong();
        ushort data = Cpu.pop();
        longPointer += fieldDesc >>> 8;
        ushort src = Mem.readWord(longPointer);
        Mem.writeWord(longPointer, Mem.writeField(src, fieldDesc & 0xFF, data));
    };

    public static readonly OpImpl OPC_x71_WL0F_alpha = () =>
    {
        int spec = Mem.getNextCodeByte();
        int longPointer = Cpu.popLong();
        ushort data = Cpu.pop();
        ushort src = Mem.readWord(longPointer);
        Mem.writeWord(longPointer, Mem.writeField(src, spec, data));
    };

    public static readonly OpImpl OPC_x74_WLFS = () =>
    {
        int fieldDesc = Cpu.pop();
        int longPointer = Cpu.popLong();
        ushort data = Cpu.pop();
        longPointer += fieldDesc >>> 8;
        ushort src = Mem.readWord(longPointer);
        Mem.writeWord(longPointer, Mem.writeField(src, fieldDesc & 0xFF, data));
    };

    public static readonly OpImpl OPC_x70_WS0F_alpha = () =>
    {
        int spec = Mem.getNextCodeByte();
        ushort data = Cpu.pop();
        int pointer = Cpu.pop();
        ushort src = Mem.readMDSWord(pointer);
        Mem.writeMDSWord(pointer, Mem.writeField(src, spec, data));
    };

    // ---- 7.5.3 Put Swapped Field ----

    public static readonly OpImpl OPC_x6F_PS0F = () =>
    {
        OPC_x70_WS0F_alpha();
        Cpu.recover();
    };

    public static readonly OpImpl OPC_x6E_PSF_word = () =>
    {
        int fieldDesc = Mem.getNextCodeWord();
        ushort data = Cpu.pop();
        int pointer = Cpu.pop();
        int offset = fieldDesc >>> 8;
        ushort src = Mem.readMDSWord(pointer, offset);
        Mem.writeMDSWord(pointer, offset, Mem.writeField(src, fieldDesc & 0xFF, data));
        Cpu.recover();
    };

    public static readonly OpImpl OPC_x73_PSLF_word = () =>
    {
        int fieldDesc = Mem.getNextCodeWord();
        ushort data = Cpu.pop();
        int longPointer = Cpu.popLong();
        longPointer += fieldDesc >>> 8;
        ushort src = Mem.readWord(longPointer);
        Mem.writeWord(longPointer, Mem.writeField(src, fieldDesc & 0xFF, data));
        Cpu.recover();
        Cpu.recover();
    };

    // ==================================================================
    // Registration
    // ==================================================================

    public static void RegisterAll()
    {
        // 7.1 Immediate
        Opcodes.Install(0xCB, "LIN1", OPC_xCB_LIN1);
        Opcodes.Install(0xCC, "LINI", OPC_xCC_LINI);
        Opcodes.Install(0xD1, "LID0", OPC_xD1_LID0);
        Opcodes.Install(0xC0, "LI0",  OPC_xC0_LI0);
        Opcodes.Install(0xC1, "LI1",  OPC_xC1_LI1);
        Opcodes.Install(0xC2, "LI2",  OPC_xC2_LI2);
        Opcodes.Install(0xC3, "LI3",  OPC_xC3_LI3);
        Opcodes.Install(0xC4, "LI4",  OPC_xC4_LI4);
        Opcodes.Install(0xC5, "LI5",  OPC_xC5_LI5);
        Opcodes.Install(0xC6, "LI6",  OPC_xC6_LI6);
        Opcodes.Install(0xC7, "LI7",  OPC_xC7_LI7);
        Opcodes.Install(0xC8, "LI8",  OPC_xC8_LI8);
        Opcodes.Install(0xC9, "LI9",  OPC_xC9_LI9);
        Opcodes.Install(0xCA, "LI10", OPC_xCA_LI10);
        Opcodes.Install(0xCD, "LIB",  OPC_xCD_LIB_alpha);
        Opcodes.Install(0xCF, "LINB", OPC_xCF_LINB_alpha);
        Opcodes.Install(0xD0, "LIHB", OPC_xD0_LIHB_alpha);
        Opcodes.Install(0xCE, "LIW",  OPC_xCE_LIW_word);

        // 7.2.1 Local Frame Access
        Opcodes.Install(0xD2, "LA0", OPC_xD2_LA0);
        Opcodes.Install(0xD3, "LA1", OPC_xD3_LA1);
        Opcodes.Install(0xD4, "LA2", OPC_xD4_LA2);
        Opcodes.Install(0xD5, "LA3", OPC_xD5_LA3);
        Opcodes.Install(0xD6, "LA6", OPC_xD6_LA6);
        Opcodes.Install(0xD7, "LA8", OPC_xD7_LA8);
        Opcodes.Install(0xD8, "LAB", OPC_xD8_LAB_alpha);
        Opcodes.Install(0xD9, "LAW", OPC_xD9_LAW_word);

        // 7.2.1.1 Load Local
        Opcodes.Install(0x01, "LL0",  OPC_x01_LL0);
        Opcodes.Install(0x02, "LL1",  OPC_x02_LL1);
        Opcodes.Install(0x03, "LL2",  OPC_x03_LL2);
        Opcodes.Install(0x04, "LL3",  OPC_x04_LL3);
        Opcodes.Install(0x05, "LL4",  OPC_x05_LL4);
        Opcodes.Install(0x06, "LL5",  OPC_x06_LL5);
        Opcodes.Install(0x07, "LL6",  OPC_x07_LL6);
        Opcodes.Install(0x08, "LL7",  OPC_x08_LL7);
        Opcodes.Install(0x09, "LL8",  OPC_x09_LL8);
        Opcodes.Install(0x0A, "LL9",  OPC_x0A_LL9);
        Opcodes.Install(0x0B, "LL10", OPC_x0B_LL10);
        Opcodes.Install(0x0C, "LL11", OPC_x0C_LL11);
        Opcodes.Install(0x0D, "LLB",  OPC_x0D_LLB_alpha);
        Opcodes.Install(0x0E, "LLD0", OPC_x0E_LLD0);
        Opcodes.Install(0x0F, "LLD1", OPC_x0F_LLD1);
        Opcodes.Install(0x10, "LLD2", OPC_x10_LLD2);
        Opcodes.Install(0x11, "LLD3", OPC_x11_LLD3);
        Opcodes.Install(0x12, "LLD4", OPC_x12_LLD4);
        Opcodes.Install(0x13, "LLD5", OPC_x13_LLD5);
        Opcodes.Install(0x14, "LLD6", OPC_x14_LLD6);
        Opcodes.Install(0x15, "LLD7", OPC_x15_LLD7);
        Opcodes.Install(0x16, "LLD8", OPC_x16_LLD8);
        Opcodes.Install(0x17, "LLD10", OPC_x17_LLD10);
        Opcodes.Install(0x18, "LLDB", OPC_x18_LLDB_alpha);

        // 7.2.1.2 Store Local
        Opcodes.Install(0x19, "SL0",  OPC_x19_SL0);
        Opcodes.Install(0x1A, "SL1",  OPC_x1A_SL1);
        Opcodes.Install(0x1B, "SL2",  OPC_x1B_SL2);
        Opcodes.Install(0x1C, "SL3",  OPC_x1C_SL3);
        Opcodes.Install(0x1D, "SL4",  OPC_x1D_SL4);
        Opcodes.Install(0x1E, "SL5",  OPC_x1E_SL5);
        Opcodes.Install(0x1F, "SL6",  OPC_x1F_SL6);
        Opcodes.Install(0x20, "SL7",  OPC_x20_SL7);
        Opcodes.Install(0x21, "SL8",  OPC_x21_SL8);
        Opcodes.Install(0x22, "SL9",  OPC_x22_SL9);
        Opcodes.Install(0x23, "SL10", OPC_x23_SL10);
        Opcodes.Install(0x24, "SLB",  OPC_x24_SLB_alpha);
        Opcodes.Install(0x25, "SLD0", OPC_x25_SLD0);
        Opcodes.Install(0x26, "SLD1", OPC_x26_SLD1);
        Opcodes.Install(0x27, "SLD2", OPC_x27_SLD2);
        Opcodes.Install(0x28, "SLD3", OPC_x28_SLD3);
        Opcodes.Install(0x29, "SLD4", OPC_x29_SLD4);
        Opcodes.Install(0x2A, "SLD5", OPC_x2A_SLD5);
        Opcodes.Install(0x2B, "SLD6", OPC_x2B_SLD6);
        Opcodes.Install(0x2C, "SLD8", OPC_x2C_SLD8);
        Opcodes.Install(0x75, "SLDB", OPC_x75_SLDB_alpha);

        // 7.2.1.3 Put Local
        Opcodes.Install(0x2D, "PL0",  OPC_x2D_PL0);
        Opcodes.Install(0x2E, "PL1",  OPC_x2E_PL1);
        Opcodes.Install(0x2F, "PL2",  OPC_x2F_PL2);
        Opcodes.Install(0x30, "PL3",  OPC_x30_PL3);
        Opcodes.Install(0x31, "PLB",  OPC_x31_PLB_alpha);
        Opcodes.Install(0x32, "PLD0", OPC_x32_PLD0);
        Opcodes.Install(0x33, "PLDB", OPC_x33_PLDB_alpha);

        // 7.2.1.4 Add Local
        Opcodes.Install(0xBB, "AL0IB", OPC_xBB_AL0IB_alpha);

        // 7.2.2 Global Frame Access
        Opcodes.InstallOld(0xDA, "GA0", OPCo_xDA_GA0);
        Opcodes.InstallNew(0xDA, "GA0", OPCn_xDA_GA0);
        Opcodes.InstallOld(0xDB, "GA1", OPCo_xDB_GA1);
        Opcodes.InstallNew(0xDB, "GA1", OPCn_xDB_GA1);
        Opcodes.InstallOld(0xDC, "GAB", OPCo_xDC_GAB_alpha);
        Opcodes.InstallNew(0xDC, "GAB", OPCn_xDC_GAB_alpha);
        Opcodes.InstallOld(0xDD, "GAW", OPCo_xDD_GAW_word);
        Opcodes.InstallNew(0xDD, "GAW", OPCn_xDD_GAW_word);

        // 7.2.2 Global Frame Access (changed chapters, post-4.0 only)
        Opcodes.InstallNew(0xFA, "LGA0", OPCn_xFA_LGA0);
        Opcodes.InstallNew(0xFB, "LGAB", OPCn_xFB_LGAB_alpha);
        Opcodes.InstallNew(0xFC, "LGAW", OPCn_xFC_LGAW_word);

        // 7.2.2.1 Load Global
        Opcodes.InstallOld(0x34, "LG0", OPCo_x34_LG0);
        Opcodes.InstallNew(0x34, "LG0", OPCn_x34_LG0);
        Opcodes.InstallOld(0x35, "LG1", OPCo_x35_LG1);
        Opcodes.InstallNew(0x35, "LG1", OPCn_x35_LG1);
        Opcodes.InstallOld(0x36, "LG2", OPCo_x36_LG2);
        Opcodes.InstallNew(0x36, "LG2", OPCn_x36_LG2);
        Opcodes.InstallOld(0x37, "LGB", OPCo_x37_LGB_alpha);
        Opcodes.InstallNew(0x37, "LGB", OPCn_x37_LGB_alpha);
        Opcodes.InstallOld(0x38, "LGD0", OPCo_x38_LGD0);
        Opcodes.InstallNew(0x38, "LGD0", OPCn_x38_LGD0);
        Opcodes.InstallOld(0x39, "LGD2", OPCo_x39_LGD2);
        Opcodes.InstallNew(0x39, "LGD2", OPCn_x39_LGD2);
        Opcodes.InstallOld(0x3A, "LGDB", OPCo_x3A_LGDB_alpha);
        Opcodes.InstallNew(0x3A, "LGDB", OPCn_x3A_LGDB_alpha);

        // 7.2.2.2 Store Global
        Opcodes.InstallOld(0x3B, "SGB", OPCo_x3B_SGB_alpha);
        Opcodes.InstallNew(0x3B, "SGB", OPCn_x3B_SGB_alpha);
        Opcodes.InstallOld(0x76, "SGDB", OPCo_x76_SGDB_alpha);
        Opcodes.InstallNew(0x76, "SGDB", OPCn_x76_SGDB_alpha);

        // 7.3.1.1 Read Direct
        Opcodes.Install(0x40, "R0",   OPC_x40_R0);
        Opcodes.Install(0x41, "R1",   OPC_x41_R1);
        Opcodes.Install(0x42, "RB",   OPC_x42_RB_alpha);
        Opcodes.Install(0x43, "RL0",  OPC_x43_RL0);
        Opcodes.Install(0x44, "RLB",  OPC_x44_RLB_alpha);
        Opcodes.Install(0x45, "RD0",  OPC_x45_RD0);
        Opcodes.Install(0x46, "RDB",  OPC_x46_RDB_alpha);
        Opcodes.Install(0x47, "RDL0", OPC_x47_RDL0);
        Opcodes.Install(0x48, "RDLB", OPC_x48_RDLB_alpha);
        Opcodes.InstallEsc(0x1B, "RC", ESC_x1B_RC_alpha);

        // 7.3.1.2 Write Direct
        Opcodes.Install(0x49, "W0",   OPC_x49_W0);
        Opcodes.Install(0x4A, "WB",   OPC_x4A_WB_alpha);
        Opcodes.Install(0x4C, "WLB",  OPC_x4C_WLB_alpha);
        Opcodes.Install(0x4E, "WDB",  OPC_x4E_WDB_alpha);
        Opcodes.Install(0x51, "WDLB", OPC_x51_WDLB_alpha);

        // 7.3.1.3 Put Swapped Direct
        Opcodes.Install(0x4B, "PSB",   OPC_x4B_PSB_alpha);
        Opcodes.Install(0x4F, "PSD0",  OPC_x4F_PSD0);
        Opcodes.Install(0x50, "PSDB",  OPC_x50_PSDB_alpha);
        Opcodes.Install(0x4D, "PSLB",  OPC_x4D_PSLB_alpha);
        Opcodes.Install(0x52, "PSDLB", OPC_x52_PSDLB_alpha);

        // 7.3.2.1 Read Indirect
        Opcodes.Install(0x53, "RLI00",  OPC_x53_RLI00);
        Opcodes.Install(0x54, "RLI01",  OPC_x54_RLI01);
        Opcodes.Install(0x55, "RLI02",  OPC_x55_RLI02);
        Opcodes.Install(0x56, "RLI03",  OPC_x56_RLI03);
        Opcodes.Install(0x57, "RLIP",   OPC_x57_RLIP_pair);
        Opcodes.Install(0x58, "RLILP",  OPC_x58_RLILP_pair);
        Opcodes.InstallOld(0x5C, "RGIP",  OPCo_x5C_RGIP_pair);
        Opcodes.InstallNew(0x5C, "RGIP",  OPCn_x5C_RGIP_pair);
        Opcodes.InstallOld(0x5D, "RGILP", OPCo_x5D_RGILP_pair);
        Opcodes.InstallNew(0x5D, "RGILP", OPCn_x5D_RGILP_pair);
        Opcodes.Install(0x59, "RLDI00", OPC_x59_RLDI00);
        Opcodes.Install(0x5A, "RLDIP",  OPC_x5A_RLDIP_pair);
        Opcodes.Install(0x5B, "RLDILP", OPC_x5B_RLDILP_pair);

        // 7.3.2.2 Write Indirect
        Opcodes.Install(0x5E, "WLIP",   OPC_x5E_WLIP_pair);
        Opcodes.Install(0x5F, "WLILP",  OPC_x5F_WLILP_pair);
        Opcodes.Install(0x60, "WLDILP", OPC_x60_WLDILP_pair);

        // 7.4 String
        Opcodes.Install(0x61, "RS",  OPC_x61_RS_alpha);
        Opcodes.Install(0x62, "RLS", OPC_x62_RLS_alpha);
        Opcodes.Install(0x63, "WS",  OPC_x63_WS_alpha);
        Opcodes.Install(0x64, "WLS", OPC_x64_WLS_alpha);

        // 7.5.1 Read Field
        Opcodes.Install(0x66, "RF",     OPC_x66_RF_word);
        Opcodes.Install(0x65, "R0F",    OPC_x65_R0F_alpha);
        Opcodes.Install(0x68, "RLF",    OPC_x68_RLF_word);
        Opcodes.Install(0x67, "RL0F",   OPC_x67_RL0F_alpha);
        Opcodes.Install(0x69, "RLFS",   OPC_x69_RLFS);
        Opcodes.InstallEsc(0x1A, "RCFS", ESC_x1A_RCFS);
        Opcodes.Install(0x6A, "RLIPF",  OPC_x6A_RLIPF_alphabeta);
        Opcodes.Install(0x6B, "RLILPF", OPC_x6B_RLILPF_alphabeta);

        // 7.5.2 Write Field
        Opcodes.Install(0x6D, "WF",   OPC_x6D_WF_word);
        Opcodes.Install(0x6C, "W0F",  OPC_x6C_W0F_alpha);
        Opcodes.Install(0x72, "WLF",  OPC_x72_WLF_word);
        Opcodes.Install(0x71, "WL0F", OPC_x71_WL0F_alpha);
        Opcodes.Install(0x74, "WLFS", OPC_x74_WLFS);
        Opcodes.Install(0x70, "WS0F", OPC_x70_WS0F_alpha);

        // 7.5.3 Put Swapped Field
        Opcodes.Install(0x6F, "PS0F", OPC_x6F_PS0F);
        Opcodes.Install(0x6E, "PSF",  OPC_x6E_PSF_word);
        Opcodes.Install(0x73, "PSLF", OPC_x73_PSLF_word);
    }
}
