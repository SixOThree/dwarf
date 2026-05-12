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

// Implementation of instructions defined in PrincOps 4.0 chapter 5:
// Stack Instructions.
//
// **Sign-extension audit zone** (RISKS R1). The Java code uses `short` for
// 16-bit values throughout, which is signed. Where signed semantics is needed
// (NEG, SDIV, LINT, SHIFT count, comparisons, ADDSB), the C# port casts pop
// results to `short` explicitly. Where unsigned 16-bit semantics is needed,
// it stays as `ushort`. Watch for it.
public static class Ch05_Stack_Instructions
{
    // ---- Private helpers ----

    private static int signExtendByte(int b)
    {
        if ((b & 0x0080) != 0) { return (short)(b | unchecked((int)0xFFFFFF00)); }
        return b & 0x0000007F;
    }

    private static ushort shiftShort(ushort value, int shiftBy)
    {
        if (shiftBy == 0) { return value; }
        if (shiftBy > 0)
        {
            if (shiftBy < 16) { return (ushort)((value << shiftBy) & 0xFFFF); }
            return 0;
        }
        if (shiftBy > -16)
        {
            return (ushort)(value >>> -shiftBy);
        }
        return 0;
    }

    private static int shiftLong(int value, int shiftBy)
    {
        if (shiftBy == 0) { return value; }
        if (shiftBy > 0)
        {
            if (shiftBy < 32) { return value << shiftBy; }
            return 0;
        }
        if (shiftBy > -32)
        {
            return value >>> -shiftBy;
        }
        return 0;
    }

    private static ushort rotateShort(ushort value, int by)
    {
        if (by == 0) { return value; }

        if (by > 0)
        {
            by %= 16;
            int tmp = value << by;
            return (ushort)((tmp & 0xFFFF) | ((tmp >>> 16) & 0xFFFF));
        }

        by = -by % 16;
        int tmp2 = (value << 16) >>> by;
        return (ushort)((tmp2 | (tmp2 >>> 16)) & 0xFFFF);
    }

    // ---- 5.1 Stack Primitives ----

    // REC - Recover
    public static readonly OpImpl OPC_xA2_REC = () => { Cpu.recover(); };

    // REC2 - Recover Two
    public static readonly OpImpl OPC_xA3_REC2 = () =>
    {
        Cpu.recover();
        Cpu.recover();
    };

    // DIS - Discard
    public static readonly OpImpl OPC_xA4_DIS = () => { Cpu.discard(); };

    // DIS2 - Discard Two
    public static readonly OpImpl OPC_xA5_DIS2 = () =>
    {
        Cpu.discard();
        Cpu.discard();
    };

    // EXCH - Exchange
    public static readonly OpImpl OPC_xA6_EXCH = () =>
    {
        ushort v = Cpu.pop();
        ushort u = Cpu.pop();
        Cpu.push(v);
        Cpu.push(u);
    };

    // DEXCH - Double Exchange
    public static readonly OpImpl OPC_xA7_DEXCH = () =>
    {
        int v = Cpu.popLong();
        int u = Cpu.popLong();
        Cpu.pushLong(v);
        Cpu.pushLong(u);
    };

    // DUP - Duplicate
    public static readonly OpImpl OPC_xA8_DUP = () =>
    {
        ushort u = Cpu.pop();
        Cpu.push(u);
        Cpu.push(u);
    };

    // DDUP - Double Duplicate
    public static readonly OpImpl OPC_xA9_DDUP = () =>
    {
        int u = Cpu.popLong();
        Cpu.pushLong(u);
        Cpu.pushLong(u);
    };

    // EXDIS - Exchange Discard
    public static readonly OpImpl OPC_xAA_EXDIS = () =>
    {
        ushort u = Cpu.pop();
        _ = Cpu.pop(); // unused
        Cpu.push(u);
    };

    // ---- 5.2 Check Instructions ----

    // BNDCK - Bounds Check
    public static readonly OpImpl OPC_x3C_BNDCK = () =>
    {
        int range = Cpu.pop();
        int index = Cpu.pop();
        Cpu.push((ushort)index);
        if (index >= range) { Cpu.boundsTrap(); }
    };

    // BNDCKL - Bounds Check Long
    public static readonly OpImpl ESC_x24_BNDCKL = () =>
    {
        long range = Cpu.popLong() & 0xFFFFFFFFL;
        long index = Cpu.popLong() & 0xFFFFFFFFL;
        Cpu.pushLong((int)index);
        if (index >= range) { Cpu.boundsTrap(); }
    };

    // NILCK - Nil Check (? undocumented / forgotten ?)
    public static readonly OpImpl ESC_x25_NILCK = () =>
    {
        ushort pointer = Cpu.pop();
        Cpu.push(pointer);
        if (pointer == 0) { Cpu.pointerTrap(); }
    };

    // NILCKL - Nil Check Long
    public static readonly OpImpl ESC_x26_NILCKL = () =>
    {
        int longPointer = Cpu.popLong();
        Cpu.pushLong(longPointer);
        if (longPointer == 0) { Cpu.pointerTrap(); }
    };

    // ---- 5.3 Unary Operations ----

    // NEG - Negate
    public static readonly OpImpl OPC_xAB_NEG = () =>
    {
        short i = (short)Cpu.pop();
        Cpu.push(-i);
    };

    // INC - Increment
    public static readonly OpImpl OPC_xAC_INC = () =>
    {
        int s = Cpu.pop();
        Cpu.push((s + 1) & 0xFFFF);
    };

    // DINC - Double Increment
    public static readonly OpImpl OPC_xAE_DINC = () =>
    {
        long s = Cpu.popLong() & 0xFFFFFFFFL;
        Cpu.pushLong((int)((s + 1) & 0xFFFFFFFFL));
    };

    // DEC - Decrement
    public static readonly OpImpl OPC_xAD_DEC = () =>
    {
        int s = Cpu.pop();
        Cpu.push((s - 1) & 0xFFFF);
    };

    // ADDSB - Add Signed Byte
    public static readonly OpImpl OPC_xB4_ADDSB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        int i = Cpu.pop();
        Cpu.push(i + signExtendByte(alpha));
    };

    // DBL - Double
    public static readonly OpImpl OPC_xAF_DBL = () =>
    {
        int i = Cpu.pop();
        Cpu.push((i * 2) & 0xFFFF);
    };

    // DDBL - Double Double
    public static readonly OpImpl OPC_xB0_DDBL = () =>
    {
        int i = Cpu.popLong();
        Cpu.pushLong(i * 2);
    };

    // TRPL - Triple
    public static readonly OpImpl OPC_xB1_TRPL = () =>
    {
        int i = Cpu.pop();
        Cpu.push(i * 3);
    };

    // LINT - Lengthen Integer (sign-extend short to long)
    public static readonly OpImpl ESC_x18_LINT = () =>
    {
        short i = (short)Cpu.pop();
        Cpu.push((ushort)i);
        Cpu.push((ushort)(i < 0 ? 0xFFFF : 0));
    };

    // SHIFTSB - Shift Signed Byte
    public static readonly OpImpl OPC_x7C_SHIFTSB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        ushort u = Cpu.pop();
        int shift = signExtendByte(alpha);
        if (shift < -15 || shift > 15) { Cpu.ERROR("opcode SHIFTSB :: shift < -15 || shift > 15"); }
        Cpu.push(shiftShort(u, shift));
    };

    // ---- 5.4 Logical Operations ----

    // AND - And
    public static readonly OpImpl OPC_xB2_AND = () =>
    {
        ushort v = Cpu.pop();
        ushort u = Cpu.pop();
        Cpu.push((ushort)(v & u));
    };

    // DAND - Double And
    public static readonly OpImpl ESC_x13_DAND = () =>
    {
        int v = Cpu.popLong();
        int u = Cpu.popLong();
        Cpu.pushLong(v & u);
    };

    // IOR - Inclusive Or
    public static readonly OpImpl OPC_xB3_IOR = () =>
    {
        ushort v = Cpu.pop();
        ushort u = Cpu.pop();
        Cpu.push((ushort)(v | u));
    };

    // DIOR - Double Inclusive Or
    public static readonly OpImpl ESC_x14_DIOR = () =>
    {
        int v = Cpu.popLong();
        int u = Cpu.popLong();
        Cpu.pushLong(v | u);
    };

    // XOR - Exclusive Or
    public static readonly OpImpl ESC_x12_XOR = () =>
    {
        ushort v = Cpu.pop();
        ushort u = Cpu.pop();
        Cpu.push((ushort)(v ^ u));
    };

    // DXOR - Double Exclusive Or
    public static readonly OpImpl ESC_x15_DXOR = () =>
    {
        int v = Cpu.popLong();
        int u = Cpu.popLong();
        Cpu.pushLong(v ^ u);
    };

    // SHIFT - Shift (signed shift count)
    public static readonly OpImpl OPC_x7B_SHIFT = () =>
    {
        short shift = (short)Cpu.pop();
        ushort u = Cpu.pop();
        Cpu.push(shiftShort(u, shift));
    };

    // DSHIFT - Double Shift (signed shift count)
    public static readonly OpImpl ESC_x17_DSHIFT = () =>
    {
        short shift = (short)Cpu.pop();
        int u = Cpu.popLong();
        Cpu.pushLong(shiftLong(u, shift));
    };

    // ROTATE - Rotate (signed rotate count)
    public static readonly OpImpl ESC_x16_ROTATE = () =>
    {
        short rotate = (short)Cpu.pop();
        ushort u = Cpu.pop();
        Cpu.push(rotateShort(u, rotate));
    };

    // ---- 5.5 Arithmetic Operations ----

    // ADD - Add
    public static readonly OpImpl OPC_xB5_ADD = () =>
    {
        int t = Cpu.pop();
        int s = Cpu.pop();
        Cpu.push((s + t) & 0xFFFF);
    };

    // SUB - Subtract
    public static readonly OpImpl OPC_xB6_SUB = () =>
    {
        int t = Cpu.pop();
        int s = Cpu.pop();
        Cpu.push((s - t) & 0xFFFF);
    };

    // DADD - Double Add
    public static readonly OpImpl OPC_xB7_DADD = () =>
    {
        long t = Cpu.popLong() & 0xFFFFFFFFL;
        long s = Cpu.popLong() & 0xFFFFFFFFL;
        Cpu.pushLong((int)((s + t) & 0xFFFFFFFFL));
    };

    // DSUB - Double Subtract
    public static readonly OpImpl OPC_xB8_DSUB = () =>
    {
        long t = Cpu.popLong() & 0xFFFFFFFFL;
        long s = Cpu.popLong() & 0xFFFFFFFFL;
        Cpu.pushLong((int)((s - t) & 0xFFFFFFFFL));
    };

    // ADC - Add Double to Cardinal
    public static readonly OpImpl OPC_xB9_ADC = () =>
    {
        int t = Cpu.pop();
        long s = Cpu.popLong() & 0xFFFFFFFFL;
        Cpu.pushLong((int)((s + t) & 0xFFFFFFFFL));
    };

    // ACD - Add Cardinal to Double
    public static readonly OpImpl OPC_xBA_ACD = () =>
    {
        long t = Cpu.popLong() & 0xFFFFFFFFL;
        int s = Cpu.pop();
        Cpu.pushLong((int)((s + t) & 0xFFFFFFFFL));
    };

    // MUL - Multiply (16-bit, low half kept)
    public static readonly OpImpl OPC_xBC_MUL = () =>
    {
        long t = Cpu.pop();
        long s = Cpu.pop();
        Cpu.pushLong((int)((s * t) & 0xFFFFFFFFL));
        Cpu.discard();
    };

    // DMUL - Multiply (32-bit, low half kept)
    public static readonly OpImpl ESC_x30_DMUL = () =>
    {
        long t = Cpu.popLong() & 0xFFFFFFFFL;
        long s = Cpu.popLong() & 0xFFFFFFFFL;
        Cpu.pushLong((int)((s * t) & 0xFFFFFFFFL));
    };

    // SDIV - Signed Divide
    public static readonly OpImpl ESC_x31_SDIV = () =>
    {
        short k = (short)Cpu.pop();
        short j = (short)Cpu.pop();
        if (k == 0) { Cpu.divZeroTrap(); }
        Cpu.push(j / k);
        Cpu.push(j % k);
        Cpu.discard();
    };

    // UDIV - Unsigned Divide
    public static readonly OpImpl ESC_x1C_UDIV = () =>
    {
        int t = Cpu.pop();
        int s = Cpu.pop();
        if (t == 0) { Cpu.divZeroTrap(); }
        Cpu.push((ushort)(s / t));
        Cpu.push((ushort)(s % t));
        Cpu.discard();
    };

    // LUDIV - Long Unsigned Divide
    public static readonly OpImpl ESC_x1D_LUDIV = () =>
    {
        int t = Cpu.pop();
        long s = Cpu.popLong() & 0xFFFFFFFFL;
        if (t == 0) { Cpu.divZeroTrap(); }
        if ((s >>> 16) >= t) { Cpu.divCheckTrap(); }
        Cpu.push((ushort)((s / t) & 0xFFFFL));
        Cpu.push((ushort)((s % t) & 0xFFFFL));
        Cpu.discard();
    };

    // SDDIV - Signed Double Divide
    public static readonly OpImpl ESC_x32_SDDIV = () =>
    {
        int k = Cpu.popLong();
        int j = Cpu.popLong();
        if (k == 0) { Cpu.divZeroTrap(); }
        Cpu.pushLong(j / k);
        Cpu.pushLong(j % k);
        Cpu.discard();
        Cpu.discard();
    };

    // UDDIV - Unsigned Double Divide
    public static readonly OpImpl ESC_x33_UDDIV = () =>
    {
        long t = Cpu.popLong() & 0xFFFFFFFFL;
        long s = Cpu.popLong() & 0xFFFFFFFFL;
        if (t == 0) { Cpu.divZeroTrap(); }
        Cpu.pushLong((int)((s / t) & 0xFFFFFFFFL));
        Cpu.pushLong((int)((s % t) & 0xFFFFFFFFL));
        Cpu.discard();
        Cpu.discard();
    };

    // ---- 5.6 Comparison Operations ----

    // DCMP - Double Compare (signed)
    public static readonly OpImpl OPC_xBD_DCMP = () =>
    {
        int k = Cpu.popLong();
        int j = Cpu.popLong();
        Cpu.push((j > k) ? (short)1 : (j < k) ? (short)-1 : (short)0);
    };

    // UDCMP - Unsigned Double Compare
    public static readonly OpImpl OPC_xBE_UDCMP = () =>
    {
        long t = Cpu.popLong() & 0xFFFFFFFFL;
        long s = Cpu.popLong() & 0xFFFFFFFFL;
        Cpu.push((s > t) ? (short)1 : (s < t) ? (short)-1 : (short)0);
    };

    // ---- 5.7 Floating Point Operations ----
    //
    // Unspecified in PrincOps 4.0 except for the existence of these
    // instructions. The Java port reverse-engineered the most-used ones
    // (FADD/FSUB/FMUL/FDIV/FCOMP/FLOAT) assuming IEEE 754 compatibility.
    // Less-used ones delegate to Pilot's software emulation by raising
    // signalEscOpcodeTrap.

    private static float popFloat()
    {
        int floatRepr = Cpu.popLong();
        return BitConverter.Int32BitsToSingle(floatRepr);
    }

    private static void pushFloat(float value)
    {
        int floatRepr = BitConverter.SingleToInt32Bits(value);
        Cpu.pushLong(floatRepr);
    }

    // FADD - Floating Point Add
    public static readonly OpImpl ESC_x40_FADD = () =>
    {
        float t = popFloat();
        float s = popFloat();
        pushFloat(s + t);
    };

    // FSUB - Floating Point Subtract
    public static readonly OpImpl ESC_x41_FSUB = () =>
    {
        float t = popFloat();
        float s = popFloat();
        pushFloat(s - t);
    };

    // FMUL - Floating Point Multiply
    public static readonly OpImpl ESC_x42_FMUL = () =>
    {
        float t = popFloat();
        float s = popFloat();
        pushFloat(s * t);
    };

    // FDIV - Floating Point Divide
    public static readonly OpImpl ESC_x43_FDIV = () =>
    {
        float t = popFloat();
        float s = popFloat();
        if (t == 0.0f) { Cpu.divZeroTrap(); }
        pushFloat(s / t);
    };

    // FCOMP - Floating Point Compare (not exact — apparently "equal" if |diff| < 0.005??)
    public static readonly OpImpl ESC_x44_FCOMP = () =>
    {
        float t = popFloat();
        float s = popFloat();
        int result = (s > t) ? 1 : (s == t) ? 0 : -1;
        Cpu.push((short)result);
    };

    // FIX - Floating Point Fix (not implemented; delegates to Pilot)
    public static readonly OpImpl ESC_x45_FIX = () =>
    {
        Cpu.thrower.signalEscOpcodeTrap(0x45);
    };

    // FLOAT - Floating Point convert from long integer to float
    public static readonly OpImpl ESC_x46_FLOAT = () =>
    {
        int s = Cpu.popLong();
        pushFloat((float)s);
    };

    // FIXI - Floating Point convert to integer (not implemented)
    public static readonly OpImpl ESC_x47_FIXI = () =>
    {
        Cpu.thrower.signalEscOpcodeTrap(0x47);
    };

    // FIXC - Floating Point convert to cardinal (not implemented)
    public static readonly OpImpl ESC_x48_FIXC = () =>
    {
        Cpu.thrower.signalEscOpcodeTrap(0x48);
    };

    // FSTICKY - Floating Point (re)set sticky bit (not implemented)
    public static readonly OpImpl ESC_x49_FSTICKY = () =>
    {
        Cpu.thrower.signalEscOpcodeTrap(0x49);
    };

    // FREM - Floating Point Remainder (not implemented)
    public static readonly OpImpl ESC_x4A_FREM = () =>
    {
        Cpu.thrower.signalEscOpcodeTrap(0x4A);
    };

    // FROUND - Floating Point Round (not implemented)
    public static readonly OpImpl ESC_x4B_FROUND = () =>
    {
        Cpu.thrower.signalEscOpcodeTrap(0x4B);
    };

    // FROUNDI - Floating Point Round to integer (not implemented)
    public static readonly OpImpl ESC_x4C_FROUNDI = () =>
    {
        Cpu.thrower.signalEscOpcodeTrap(0x4C);
    };

    // FROUNDC - Floating Point Round to cardinal (not implemented)
    public static readonly OpImpl ESC_x4D_FROUNDC = () =>
    {
        Cpu.thrower.signalEscOpcodeTrap(0x4D);
    };

    // FSQRT - Floating Point Square Root (not implemented)
    public static readonly OpImpl ESC_x4E_FSQRT = () =>
    {
        Cpu.thrower.signalEscOpcodeTrap(0x4E);
    };

    // FSC - Floating Point SC?? (not implemented)
    public static readonly OpImpl ESC_x4F_FSC = () =>
    {
        Cpu.thrower.signalEscOpcodeTrap(0x4F);
    };

    // ---- Registration ----

    public static void RegisterAll()
    {
        // Stack Primitives
        Opcodes.Install(0xA2, "REC",   OPC_xA2_REC);
        Opcodes.Install(0xA3, "REC2",  OPC_xA3_REC2);
        Opcodes.Install(0xA4, "DIS",   OPC_xA4_DIS);
        Opcodes.Install(0xA5, "DIS2",  OPC_xA5_DIS2);
        Opcodes.Install(0xA6, "EXCH",  OPC_xA6_EXCH);
        Opcodes.Install(0xA7, "DEXCH", OPC_xA7_DEXCH);
        Opcodes.Install(0xA8, "DUP",   OPC_xA8_DUP);
        Opcodes.Install(0xA9, "DDUP",  OPC_xA9_DDUP);
        Opcodes.Install(0xAA, "EXDIS", OPC_xAA_EXDIS);

        // Check Instructions
        Opcodes.Install(0x3C, "BNDCK",     OPC_x3C_BNDCK);
        Opcodes.InstallEsc(0x24, "BNDCKL", ESC_x24_BNDCKL);
        Opcodes.InstallEsc(0x25, "NILCK",  ESC_x25_NILCK);
        Opcodes.InstallEsc(0x26, "NILCKL", ESC_x26_NILCKL);

        // Unary Operations
        Opcodes.Install(0xAB, "NEG",     OPC_xAB_NEG);
        Opcodes.Install(0xAC, "INC",     OPC_xAC_INC);
        Opcodes.Install(0xAD, "DEC",     OPC_xAD_DEC);
        Opcodes.Install(0xAE, "DINC",    OPC_xAE_DINC);
        Opcodes.Install(0xAF, "DBL",     OPC_xAF_DBL);
        Opcodes.Install(0xB0, "DDBL",    OPC_xB0_DDBL);
        Opcodes.Install(0xB1, "TRPL",    OPC_xB1_TRPL);
        Opcodes.Install(0xB4, "ADDSB",   OPC_xB4_ADDSB_alpha);
        Opcodes.Install(0x7C, "SHIFTSB", OPC_x7C_SHIFTSB_alpha);
        Opcodes.InstallEsc(0x18, "LINT", ESC_x18_LINT);

        // Logical Operations
        Opcodes.Install(0xB2, "AND",     OPC_xB2_AND);
        Opcodes.Install(0xB3, "IOR",     OPC_xB3_IOR);
        Opcodes.Install(0x7B, "SHIFT",   OPC_x7B_SHIFT);
        Opcodes.InstallEsc(0x12, "XOR",    ESC_x12_XOR);
        Opcodes.InstallEsc(0x13, "DAND",   ESC_x13_DAND);
        Opcodes.InstallEsc(0x14, "DIOR",   ESC_x14_DIOR);
        Opcodes.InstallEsc(0x15, "DXOR",   ESC_x15_DXOR);
        Opcodes.InstallEsc(0x16, "ROTATE", ESC_x16_ROTATE);
        Opcodes.InstallEsc(0x17, "DSHIFT", ESC_x17_DSHIFT);

        // Arithmetic Operations
        Opcodes.Install(0xB5, "ADD",  OPC_xB5_ADD);
        Opcodes.Install(0xB6, "SUB",  OPC_xB6_SUB);
        Opcodes.Install(0xB7, "DADD", OPC_xB7_DADD);
        Opcodes.Install(0xB8, "DSUB", OPC_xB8_DSUB);
        Opcodes.Install(0xB9, "ADC",  OPC_xB9_ADC);
        Opcodes.Install(0xBA, "ACD",  OPC_xBA_ACD);
        Opcodes.Install(0xBC, "MUL",  OPC_xBC_MUL);
        Opcodes.InstallEsc(0x1C, "UDIV",  ESC_x1C_UDIV);
        Opcodes.InstallEsc(0x1D, "LUDIV", ESC_x1D_LUDIV);
        Opcodes.InstallEsc(0x30, "DMUL",  ESC_x30_DMUL);
        Opcodes.InstallEsc(0x31, "SDIV",  ESC_x31_SDIV);
        Opcodes.InstallEsc(0x32, "SDDIV", ESC_x32_SDDIV);
        Opcodes.InstallEsc(0x33, "UDDIV", ESC_x33_UDDIV);

        // Comparison Operations
        Opcodes.Install(0xBD, "DCMP",  OPC_xBD_DCMP);
        Opcodes.Install(0xBE, "UDCMP", OPC_xBE_UDCMP);

        // Floating Point Operations
        Opcodes.InstallEsc(0x40, "FADD",    ESC_x40_FADD);
        Opcodes.InstallEsc(0x41, "FSUB",    ESC_x41_FSUB);
        Opcodes.InstallEsc(0x42, "FMUL",    ESC_x42_FMUL);
        Opcodes.InstallEsc(0x43, "FDIV",    ESC_x43_FDIV);
        Opcodes.InstallEsc(0x44, "FCOMP",   ESC_x44_FCOMP);
        Opcodes.InstallEsc(0x45, "FIX",     ESC_x45_FIX);
        Opcodes.InstallEsc(0x46, "FLOAT",   ESC_x46_FLOAT);
        Opcodes.InstallEsc(0x47, "FIXI",    ESC_x47_FIXI);
        Opcodes.InstallEsc(0x48, "FIXC",    ESC_x48_FIXC);
        Opcodes.InstallEsc(0x49, "FSTICKY", ESC_x49_FSTICKY);
        Opcodes.InstallEsc(0x4A, "FREM",    ESC_x4A_FREM);
        Opcodes.InstallEsc(0x4B, "FROUND",  ESC_x4B_FROUND);
        Opcodes.InstallEsc(0x4C, "FROUNDI", ESC_x4C_FROUNDI);
        Opcodes.InstallEsc(0x4D, "FROUNDC", ESC_x4D_FROUNDC);
        Opcodes.InstallEsc(0x4E, "FSQRT",   ESC_x4E_FSQRT);
        Opcodes.InstallEsc(0x4F, "FSC",     ESC_x4F_FSC);
    }
}
