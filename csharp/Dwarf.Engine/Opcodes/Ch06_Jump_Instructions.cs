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

// Implementation of PrincOps 4.0 chapter 6: Jump Instructions.
public static class Ch06_Jump_Instructions
{
    // ---- Private helpers ----

    private static int signExtendByte(int b)
    {
        if ((b & 0x0080) != 0) { return (short)(b | unchecked((int)0xFFFFFF00)); }
        return b & 0x0000007F;
    }

    private static int signExtendWord(int b)
    {
        if ((b & 0x8000) != 0)
        {
            return b | unchecked((int)0xFFFF0000);
        }
        return b & 0x00007FFF;
    }

    // ==================================================================
    // 6.1 Unconditional Jumps
    // ==================================================================

    public static readonly OpImpl OPC_x81_J2 = () => { Cpu.PC = (Cpu.savedPC + 2) & 0xFFFF; };
    public static readonly OpImpl OPC_x82_J3 = () => { Cpu.PC = (Cpu.savedPC + 3) & 0xFFFF; };
    public static readonly OpImpl OPC_x83_J4 = () => { Cpu.PC = (Cpu.savedPC + 4) & 0xFFFF; };
    public static readonly OpImpl OPC_x84_J5 = () => { Cpu.PC = (Cpu.savedPC + 5) & 0xFFFF; };
    public static readonly OpImpl OPC_x85_J6 = () => { Cpu.PC = (Cpu.savedPC + 6) & 0xFFFF; };
    public static readonly OpImpl OPC_x86_J7 = () => { Cpu.PC = (Cpu.savedPC + 7) & 0xFFFF; };
    public static readonly OpImpl OPC_x87_J8 = () => { Cpu.PC = (Cpu.savedPC + 8) & 0xFFFF; };

    public static readonly OpImpl OPC_x88_JB_salpha = () =>
    {
        int disp = Mem.getNextCodeByte();
        Cpu.PC = (Cpu.savedPC + signExtendByte(disp)) & 0xFFFF;
    };

    public static readonly OpImpl OPC_x89_JW_sword = () =>
    {
        int disp = Mem.getNextCodeWord();
        Cpu.PC = (Cpu.savedPC + signExtendWord(disp)) & 0xFFFF;
    };

    public static readonly OpImpl ESC_x19_JS = () =>
    {
        Cpu.PC = Cpu.pop();
    };

    public static readonly OpImpl OPC_x80_CATCH_alpha = () =>
    {
        /* int alpha = */ Mem.getNextCodeByte();
    };

    // ==================================================================
    // 6.2 Equality Jumps
    // ==================================================================

    public static readonly OpImpl OPC_x98_JZ3 = () =>
    {
        ushort u = Cpu.pop();
        if (u == 0) { Cpu.PC = (Cpu.savedPC + 3) & 0xFFFF; }
    };

    public static readonly OpImpl OPC_x99_JZ4 = () =>
    {
        ushort u = Cpu.pop();
        if (u == 0) { Cpu.PC = (Cpu.savedPC + 4) & 0xFFFF; }
    };

    public static readonly OpImpl OPC_x9B_JNZ3 = () =>
    {
        ushort u = Cpu.pop();
        if (u != 0) { Cpu.PC = (Cpu.savedPC + 3) & 0xFFFF; }
    };

    public static readonly OpImpl OPC_x9C_JNZ4 = () =>
    {
        ushort u = Cpu.pop();
        if (u != 0) { Cpu.PC = (Cpu.savedPC + 4) & 0xFFFF; }
    };

    public static readonly OpImpl OPC_x9A_JZB_salpha = () =>
    {
        int disp = Mem.getNextCodeByte();
        ushort data = Cpu.pop();
        if (data == 0) { Cpu.PC = (Cpu.savedPC + signExtendByte(disp)) & 0xFFFF; }
    };

    public static readonly OpImpl OPC_x9D_JNZB_salpha = () =>
    {
        int disp = Mem.getNextCodeByte();
        ushort data = Cpu.pop();
        if (data != 0) { Cpu.PC = (Cpu.savedPC + signExtendByte(disp)) & 0xFFFF; }
    };

    public static readonly OpImpl OPC_x8B_JEB_salpha = () =>
    {
        int disp = Mem.getNextCodeByte();
        ushort v = Cpu.pop();
        ushort u = Cpu.pop();
        if (u == v) { Cpu.PC = (Cpu.savedPC + signExtendByte(disp)) & 0xFFFF; }
    };

    public static readonly OpImpl OPC_x8E_JNEB_salpha = () =>
    {
        int disp = Mem.getNextCodeByte();
        ushort v = Cpu.pop();
        ushort u = Cpu.pop();
        if (u != v) { Cpu.PC = (Cpu.savedPC + signExtendByte(disp)) & 0xFFFF; }
    };

    public static readonly OpImpl OPC_x9E_JDEB_salpha = () =>
    {
        int disp = Mem.getNextCodeByte();
        int v = Cpu.popLong();
        int u = Cpu.popLong();
        if (u == v) { Cpu.PC = (Cpu.savedPC + signExtendByte(disp)) & 0xFFFF; }
    };

    public static readonly OpImpl OPC_x9F_JDNEB_salpha = () =>
    {
        int disp = Mem.getNextCodeByte();
        int v = Cpu.popLong();
        int u = Cpu.popLong();
        if (u != v) { Cpu.PC = (Cpu.savedPC + signExtendByte(disp)) & 0xFFFF; }
    };

    public static readonly OpImpl OPC_x8A_JEP_pair = () =>
    {
        int pair = Mem.getNextCodeByte();
        int data = Cpu.pop();
        if (data == (pair >>> 4)) { Cpu.PC = (Cpu.savedPC + (pair & 0x0F) + 4) & 0xFFFF; }
    };

    public static readonly OpImpl OPC_x8D_JNEP_pair = () =>
    {
        int pair = Mem.getNextCodeByte();
        int data = Cpu.pop();
        if (data != (pair >>> 4)) { Cpu.PC = (Cpu.savedPC + (pair & 0x0F) + 4) & 0xFFFF; }
    };

    public static readonly OpImpl OPC_x8C_JEBB_alphasbeta = () =>
    {
        int b = Mem.getNextCodeByte();
        int disp = Mem.getNextCodeByte();
        int data = Cpu.pop();
        if (data == b) { Cpu.PC = (Cpu.savedPC + signExtendByte(disp)) & 0xFFFF; }
    };

    public static readonly OpImpl OPC_x8F_JNEBB_alphasbeta = () =>
    {
        int b = Mem.getNextCodeByte();
        int disp = Mem.getNextCodeByte();
        int data = Cpu.pop();
        if (data != b) { Cpu.PC = (Cpu.savedPC + signExtendByte(disp)) & 0xFFFF; }
    };

    // ==================================================================
    // 6.3 Signed Jumps
    // ==================================================================

    public static readonly OpImpl OPC_x90_JLB_salpha = () =>
    {
        int disp = Mem.getNextCodeByte();
        short k = (short)Cpu.pop();
        short j = (short)Cpu.pop();
        if (j < k) { Cpu.PC = (Cpu.savedPC + signExtendByte(disp)) & 0xFFFF; }
    };

    public static readonly OpImpl OPC_x93_JLEB_salpha = () =>
    {
        int disp = Mem.getNextCodeByte();
        short k = (short)Cpu.pop();
        short j = (short)Cpu.pop();
        if (j <= k) { Cpu.PC = (Cpu.savedPC + signExtendByte(disp)) & 0xFFFF; }
    };

    public static readonly OpImpl OPC_x92_JGB_salpha = () =>
    {
        int disp = Mem.getNextCodeByte();
        short k = (short)Cpu.pop();
        short j = (short)Cpu.pop();
        if (j > k) { Cpu.PC = (Cpu.savedPC + signExtendByte(disp)) & 0xFFFF; }
    };

    public static readonly OpImpl OPC_x91_JGEB_salpha = () =>
    {
        int disp = Mem.getNextCodeByte();
        short k = (short)Cpu.pop();
        short j = (short)Cpu.pop();
        if (j >= k) { Cpu.PC = (Cpu.savedPC + signExtendByte(disp)) & 0xFFFF; }
    };

    // ==================================================================
    // 6.4 Unsigned Jumps
    // ==================================================================

    public static readonly OpImpl OPC_x94_JULB_salpha = () =>
    {
        int disp = Mem.getNextCodeByte();
        int v = Cpu.pop();
        int u = Cpu.pop();
        if (u < v) { Cpu.PC = (Cpu.savedPC + signExtendByte(disp)) & 0xFFFF; }
    };

    public static readonly OpImpl OPC_x97_JULEB_salpha = () =>
    {
        int disp = Mem.getNextCodeByte();
        int v = Cpu.pop();
        int u = Cpu.pop();
        if (u <= v) { Cpu.PC = (Cpu.savedPC + signExtendByte(disp)) & 0xFFFF; }
    };

    public static readonly OpImpl OPC_x96_JUGB_salpha = () =>
    {
        int disp = Mem.getNextCodeByte();
        int v = Cpu.pop();
        int u = Cpu.pop();
        if (u > v) { Cpu.PC = (Cpu.savedPC + signExtendByte(disp)) & 0xFFFF; }
    };

    public static readonly OpImpl OPC_x95_JUGEB_salpha = () =>
    {
        int disp = Mem.getNextCodeByte();
        int v = Cpu.pop();
        int u = Cpu.pop();
        if (u >= v) { Cpu.PC = (Cpu.savedPC + signExtendByte(disp)) & 0xFFFF; }
    };

    // ==================================================================
    // 6.5 Indexed Jumps
    // ==================================================================

    public static readonly OpImpl OPC_xA0_JIB_word = () =>
    {
        int @base = Mem.getNextCodeWord();
        int limit = Cpu.pop();
        int index = Cpu.pop();
        if (index < limit)
        {
            int dispPair = Mem.readCode(@base + (index / 2));
            int offset = ((index % 2) == 0) ? (dispPair >>> 8) : (dispPair & 0x00FF);
            Cpu.PC = (Cpu.savedPC + offset) & 0xFFFF;
        }
    };

    public static readonly OpImpl OPC_xA1_JIW_word = () =>
    {
        int @base = Mem.getNextCodeWord();
        int limit = Cpu.pop();
        int index = Cpu.pop();
        if (index < limit)
        {
            int disp = Mem.readCode(@base + index);
            Cpu.PC = (Cpu.savedPC + disp) & 0xFFFF;
        }
    };

    // ==================================================================
    // Registration
    // ==================================================================

    public static void RegisterAll()
    {
        // Unconditional
        Opcodes.Install(0x81, "J2", OPC_x81_J2);
        Opcodes.Install(0x82, "J3", OPC_x82_J3);
        Opcodes.Install(0x83, "J4", OPC_x83_J4);
        Opcodes.Install(0x84, "J5", OPC_x84_J5);
        Opcodes.Install(0x85, "J6", OPC_x85_J6);
        Opcodes.Install(0x86, "J7", OPC_x86_J7);
        Opcodes.Install(0x87, "J8", OPC_x87_J8);
        Opcodes.Install(0x88, "JB", OPC_x88_JB_salpha);
        Opcodes.Install(0x89, "JW", OPC_x89_JW_sword);
        Opcodes.InstallEsc(0x19, "JS", ESC_x19_JS);
        Opcodes.Install(0x80, "CATCH", OPC_x80_CATCH_alpha);

        // Equality
        Opcodes.Install(0x98, "JZ3",   OPC_x98_JZ3);
        Opcodes.Install(0x99, "JZ4",   OPC_x99_JZ4);
        Opcodes.Install(0x9B, "JNZ3",  OPC_x9B_JNZ3);
        Opcodes.Install(0x9C, "JNZ4",  OPC_x9C_JNZ4);
        Opcodes.Install(0x9A, "JZB",   OPC_x9A_JZB_salpha);
        Opcodes.Install(0x9D, "JNZB",  OPC_x9D_JNZB_salpha);
        Opcodes.Install(0x8B, "JEB",   OPC_x8B_JEB_salpha);
        Opcodes.Install(0x8E, "JNEB",  OPC_x8E_JNEB_salpha);
        Opcodes.Install(0x9E, "JDEB",  OPC_x9E_JDEB_salpha);
        Opcodes.Install(0x9F, "JDNEB", OPC_x9F_JDNEB_salpha);
        Opcodes.Install(0x8A, "JEP",   OPC_x8A_JEP_pair);
        Opcodes.Install(0x8D, "JNEP",  OPC_x8D_JNEP_pair);
        Opcodes.Install(0x8C, "JEBB",  OPC_x8C_JEBB_alphasbeta);
        Opcodes.Install(0x8F, "JNEBB", OPC_x8F_JNEBB_alphasbeta);

        // Signed
        Opcodes.Install(0x90, "JLB",  OPC_x90_JLB_salpha);
        Opcodes.Install(0x93, "JLEB", OPC_x93_JLEB_salpha);
        Opcodes.Install(0x92, "JGB",  OPC_x92_JGB_salpha);
        Opcodes.Install(0x91, "JGEB", OPC_x91_JGEB_salpha);

        // Unsigned
        Opcodes.Install(0x94, "JULB",  OPC_x94_JULB_salpha);
        Opcodes.Install(0x97, "JULEB", OPC_x97_JULEB_salpha);
        Opcodes.Install(0x96, "JUGB",  OPC_x96_JUGB_salpha);
        Opcodes.Install(0x95, "JUGEB", OPC_x95_JUGEB_salpha);

        // Indexed
        Opcodes.Install(0xA0, "JIB", OPC_xA0_JIB_word);
        Opcodes.Install(0xA1, "JIW", OPC_xA1_JIW_word);
    }
}
