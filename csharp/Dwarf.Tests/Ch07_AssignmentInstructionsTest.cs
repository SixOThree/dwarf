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

// Unittests for instructions implemented in class Ch07_Assignment_Instructions.
public sealed class Ch07_AssignmentInstructionsTest : AbstractInstructionTest
{
    // ==================================================================
    // 7.1 Immediate Instructions
    // ==================================================================

    [Fact] public void test_LIN1()
    {
        mkStack(123, 345, SP, 33, 44);
        Ch07_Assignment_Instructions.OPC_xCB_LIN1();
        checkStack(123, 345, -1, SP, 44);
    }

    [Fact] public void test_LINI()
    {
        mkStack(123, 345, SP, 33, 44);
        Ch07_Assignment_Instructions.OPC_xCC_LINI();
        checkStack(123, 345, unchecked((int)0xFFFF8000), SP, 44);
    }

    [Fact] public void test_LID0()
    {
        mkStack(123, 345, SP, 33, 44, 55);
        Ch07_Assignment_Instructions.OPC_xD1_LID0();
        checkStack(123, 345, 0, 0, SP, 55);
    }

    [Fact] public void test_LI0()  { mkStack(123, 345, SP, 33, 44, 55); Ch07_Assignment_Instructions.OPC_xC0_LI0();  checkStack(123, 345, 0, SP, 44, 55); }
    [Fact] public void test_LI1()  { mkStack(123, 345, SP, 33, 44, 55); Ch07_Assignment_Instructions.OPC_xC1_LI1();  checkStack(123, 345, 1, SP, 44, 55); }
    [Fact] public void test_LI2()  { mkStack(123, 345, SP, 33, 44, 55); Ch07_Assignment_Instructions.OPC_xC2_LI2();  checkStack(123, 345, 2, SP, 44, 55); }
    [Fact] public void test_LI3()  { mkStack(123, 345, SP, 33, 44, 55); Ch07_Assignment_Instructions.OPC_xC3_LI3();  checkStack(123, 345, 3, SP, 44, 55); }
    [Fact] public void test_LI4()  { mkStack(123, 345, SP, 33, 44, 55); Ch07_Assignment_Instructions.OPC_xC4_LI4();  checkStack(123, 345, 4, SP, 44, 55); }
    [Fact] public void test_LI5()  { mkStack(123, 345, SP, 33, 44, 55); Ch07_Assignment_Instructions.OPC_xC5_LI5();  checkStack(123, 345, 5, SP, 44, 55); }
    [Fact] public void test_LI6()  { mkStack(123, 345, SP, 33, 44, 55); Ch07_Assignment_Instructions.OPC_xC6_LI6();  checkStack(123, 345, 6, SP, 44, 55); }
    [Fact] public void test_LI7()  { mkStack(123, 345, SP, 33, 44, 55); Ch07_Assignment_Instructions.OPC_xC7_LI7();  checkStack(123, 345, 7, SP, 44, 55); }
    [Fact] public void test_LI8()  { mkStack(123, 345, SP, 33, 44, 55); Ch07_Assignment_Instructions.OPC_xC8_LI8();  checkStack(123, 345, 8, SP, 44, 55); }
    [Fact] public void test_LI9()  { mkStack(123, 345, SP, 33, 44, 55); Ch07_Assignment_Instructions.OPC_xC9_LI9();  checkStack(123, 345, 9, SP, 44, 55); }
    [Fact] public void test_LI10() { mkStack(123, 345, SP, 33, 44, 55); Ch07_Assignment_Instructions.OPC_xCA_LI10(); checkStack(123, 345, 10, SP, 44, 55); }

    // LIB
    [Fact] public void test_LIBa() { mkStack(123, 345, SP, 33, 44, 55); mkCode(1, 2, 3, savedPC, 0xCD, PC, 4,   5, 6, 7, 8); Ch07_Assignment_Instructions.OPC_xCD_LIB_alpha(); checkStack(123, 345, 4, SP, 44, 55); }
    [Fact] public void test_LIBb() { mkStack(123, 345, SP, 33, 44, 55); mkCode(1, 2, 3, savedPC, 0xCD, PC, 200, 5, 6, 7, 8); Ch07_Assignment_Instructions.OPC_xCD_LIB_alpha(); checkStack(123, 345, 200, SP, 44, 55); }
    [Fact] public void test_LIBc() { mkStack(123, 345, SP, 33, 44, 55); mkCode(1, 2, 3, savedPC, 0xCD, PC, 0,   5, 6, 7, 8); Ch07_Assignment_Instructions.OPC_xCD_LIB_alpha(); checkStack(123, 345, 0, SP, 44, 55); }
    [Fact] public void test_LIBd() { mkStack(123, 345, SP, 33, 44, 55); mkCode(1, 2, 3, savedPC, 0xCD, PC, 255, 5, 6, 7, 8); Ch07_Assignment_Instructions.OPC_xCD_LIB_alpha(); checkStack(123, 345, 255, SP, 44, 55); }

    // LINB
    [Fact] public void test_LINBa() { mkStack(123, 345, SP, 33, 44, 55); mkCode(1, 2, 3, savedPC, 0xCF, PC, 4,    5, 6, 7, 8); Ch07_Assignment_Instructions.OPC_xCF_LINB_alpha(); checkStack(123, 345, 0xFF04, SP, 44, 55); }
    [Fact] public void test_LINBb() { mkStack(123, 345, SP, 33, 44, 55); mkCode(1, 2, 3, savedPC, 0xCF, PC, 0xCC, 5, 6, 7, 8); Ch07_Assignment_Instructions.OPC_xCF_LINB_alpha(); checkStack(123, 345, 0xFFCC, SP, 44, 55); }
    [Fact] public void test_LINBc() { mkStack(123, 345, SP, 33, 44, 55); mkCode(1, 2, 3, savedPC, 0xCF, PC, 0,    5, 6, 7, 8); Ch07_Assignment_Instructions.OPC_xCF_LINB_alpha(); checkStack(123, 345, 0xFF00, SP, 44, 55); }
    [Fact] public void test_LINBd() { mkStack(123, 345, SP, 33, 44, 55); mkCode(1, 2, 3, savedPC, 0xCF, PC, 255,  5, 6, 7, 8); Ch07_Assignment_Instructions.OPC_xCF_LINB_alpha(); checkStack(123, 345, 0xFFFF, SP, 44, 55); }

    // LIHB
    [Fact] public void test_LIHBa() { mkStack(123, 345, SP, 33, 44, 55); mkCode(1, 2, 3, savedPC, 0xD0, PC, 255, 5, 6, 7, 8); Ch07_Assignment_Instructions.OPC_xD0_LIHB_alpha(); checkStack(123, 345, 0xFF00, SP, 44, 55); }
    [Fact] public void test_LIHBb() { mkStack(123, 345, SP, 33, 44, 55); mkCode(1, 2, 3, savedPC, 0xD0, PC, 33,  5, 6, 7, 8); Ch07_Assignment_Instructions.OPC_xD0_LIHB_alpha(); checkStack(123, 345, 0x2100, SP, 44, 55); }
    [Fact] public void test_LIHBc() { mkStack(123, 345, SP, 33, 44, 55); mkCode(1, 2, 3, savedPC, 0xD0, PC, 0,   5, 6, 7, 8); Ch07_Assignment_Instructions.OPC_xD0_LIHB_alpha(); checkStack(123, 345, 0x0000, SP, 44, 55); }

    // LIW
    [Fact] public void test_LIWa() { mkStack(123, 345, SP, 33, 44, 55); mkCode(1, 2, 3, savedPC, 0xCE, PC, 4, 5, 6, 7, 8);          Ch07_Assignment_Instructions.OPC_xCE_LIW_word(); checkStack(123, 345, 0x0405, SP, 44, 55); }
    [Fact] public void test_LIWb() { mkStack(123, 345, SP, 33, 44, 55); mkCode(0, 1, 2, 3, savedPC, 0xCE, PC, 4, 5, 6, 7, 8);       Ch07_Assignment_Instructions.OPC_xCE_LIW_word(); checkStack(123, 345, 0x0405, SP, 44, 55); }
    [Fact] public void test_LIWc() { mkStack(123, 345, SP, 33, 44, 55); mkCode(1, 2, 3, savedPC, 0xCE, PC, 0xCC, 0xEE, 6, 7, 8);    Ch07_Assignment_Instructions.OPC_xCE_LIW_word(); checkStack(123, 345, 0xCCEE, SP, 44, 55); }
    [Fact] public void test_LIWd() { mkStack(123, 345, SP, 33, 44, 55); mkCode(0, 1, 2, 3, savedPC, 0xCE, PC, 0xCC, 0xEE, 6, 7, 8); Ch07_Assignment_Instructions.OPC_xCE_LIW_word(); checkStack(123, 345, 0xCCEE, SP, 44, 55); }

    // ==================================================================
    // 7.2.1 Local Frame Access
    // ==================================================================

    [Fact] public void test_LA0() { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, SP, 33, 44, 55); Ch07_Assignment_Instructions.OPC_xD2_LA0(); checkStack(123, 345, Cpu.LF,     SP, 44, 55); }
    [Fact] public void test_LA1() { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, SP, 33, 44, 55); Ch07_Assignment_Instructions.OPC_xD3_LA1(); checkStack(123, 345, Cpu.LF + 1, SP, 44, 55); }
    [Fact] public void test_LA2() { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, SP, 33, 44, 55); Ch07_Assignment_Instructions.OPC_xD4_LA2(); checkStack(123, 345, Cpu.LF + 2, SP, 44, 55); }
    [Fact] public void test_LA3() { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, SP, 33, 44, 55); Ch07_Assignment_Instructions.OPC_xD5_LA3(); checkStack(123, 345, Cpu.LF + 3, SP, 44, 55); }
    [Fact] public void test_LA6() { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, SP, 33, 44, 55); Ch07_Assignment_Instructions.OPC_xD6_LA6(); checkStack(123, 345, Cpu.LF + 6, SP, 44, 55); }
    [Fact] public void test_LA8() { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, SP, 33, 44, 55); Ch07_Assignment_Instructions.OPC_xD7_LA8(); checkStack(123, 345, Cpu.LF + 8, SP, 44, 55); }

    [Fact] public void test_LABa() { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, SP, 33, 44, 55); mkCode(1, 2, 3, savedPC, 0xD8, PC, 4,   5, 6, 7, 8); Ch07_Assignment_Instructions.OPC_xD8_LAB_alpha(); checkStack(123, 345, Cpu.LF + 4, SP, 44, 55); }
    [Fact] public void test_LABb() { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, SP, 33, 44, 55); mkCode(1, 2, 3, savedPC, 0xD8, PC, 254, 5, 6, 7, 8); Ch07_Assignment_Instructions.OPC_xD8_LAB_alpha(); checkStack(123, 345, Cpu.LF + 254, SP, 44, 55); }

    [Fact] public void test_LAWa() { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, SP, 33, 44, 55); mkCode(1, 2, 3, savedPC, 0xD9, PC, 2, 250, 6, 7, 8);    Ch07_Assignment_Instructions.OPC_xD9_LAW_word(); checkStack(123, 345, Cpu.LF + 762, SP, 44, 55); }
    [Fact] public void test_LAWb() { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, SP, 33, 44, 55); mkCode(0, 1, 2, 3, savedPC, 0xD9, PC, 2, 251, 6, 7, 8); Ch07_Assignment_Instructions.OPC_xD9_LAW_word(); checkStack(123, 345, Cpu.LF + 763, SP, 44, 55); }

    // ==================================================================
    // 7.2.1.1 Load Local — LL0..LL11, LLB, LLD0..LLD8, LLD10, LLDB
    // ==================================================================

    private static readonly int[] LF_FRAME =
        { 0x7101, 0x7202, 0x7303, 0x7404, 0x7505, 0x7606, 0x7707, 0x7808, 0x7909, 0x7A0A, 0x7B0B, 0x7C0C, 0x7D0D, 0x7E0E, 0x7F0F };

    [Fact] public void test_LL0()  { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, SP, 33, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x01_LL0();  checkStack(123, 345, 0x7101, SP, 44, 55); }
    [Fact] public void test_LL1()  { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, SP, 33, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x02_LL1();  checkStack(123, 345, 0x7202, SP, 44, 55); }
    [Fact] public void test_LL2()  { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, SP, 33, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x03_LL2();  checkStack(123, 345, 0x7303, SP, 44, 55); }
    [Fact] public void test_LL3()  { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, SP, 33, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x04_LL3();  checkStack(123, 345, 0x7404, SP, 44, 55); }
    [Fact] public void test_LL4()  { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, SP, 33, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x05_LL4();  checkStack(123, 345, 0x7505, SP, 44, 55); }
    [Fact] public void test_LL5()  { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, SP, 33, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x06_LL5();  checkStack(123, 345, 0x7606, SP, 44, 55); }
    [Fact] public void test_LL6()  { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, SP, 33, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x07_LL6();  checkStack(123, 345, 0x7707, SP, 44, 55); }
    [Fact] public void test_LL7()  { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, SP, 33, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x08_LL7();  checkStack(123, 345, 0x7808, SP, 44, 55); }
    [Fact] public void test_LL8()  { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, SP, 33, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x09_LL8();  checkStack(123, 345, 0x7909, SP, 44, 55); }
    [Fact] public void test_LL9()  { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, SP, 33, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x0A_LL9();  checkStack(123, 345, 0x7A0A, SP, 44, 55); }
    [Fact] public void test_LL10() { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, SP, 33, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x0B_LL10(); checkStack(123, 345, 0x7B0B, SP, 44, 55); }
    [Fact] public void test_LL11() { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, SP, 33, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x0C_LL11(); checkStack(123, 345, 0x7C0C, SP, 44, 55); }

    [Fact] public void test_LLB()
    {
        Assert.NotEqual(0, Cpu.LF);
        mkStack(123, 345, SP, 33, 44, 55); mkLocalFrame(LF_FRAME);
        mkCode(1, 2, 3, savedPC, 0x0D, PC, 14, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x0D_LLB_alpha();
        checkStack(123, 345, 0x7F0F, SP, 44, 55);
    }

    [Fact] public void test_LLD0()  { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, SP, 33, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x0E_LLD0();  checkStack(123, 345, 0x7101, 0x7202, SP, 55); }
    [Fact] public void test_LLD1()  { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, SP, 33, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x0F_LLD1();  checkStack(123, 345, 0x7202, 0x7303, SP, 55); }
    [Fact] public void test_LLD2()  { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, SP, 33, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x10_LLD2();  checkStack(123, 345, 0x7303, 0x7404, SP, 55); }
    [Fact] public void test_LLD3()  { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, SP, 33, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x11_LLD3();  checkStack(123, 345, 0x7404, 0x7505, SP, 55); }
    [Fact] public void test_LLD4()  { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, SP, 33, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x12_LLD4();  checkStack(123, 345, 0x7505, 0x7606, SP, 55); }
    [Fact] public void test_LLD5()  { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, SP, 33, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x13_LLD5();  checkStack(123, 345, 0x7606, 0x7707, SP, 55); }
    [Fact] public void test_LLD6()  { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, SP, 33, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x14_LLD6();  checkStack(123, 345, 0x7707, 0x7808, SP, 55); }
    [Fact] public void test_LLD7()  { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, SP, 33, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x15_LLD7();  checkStack(123, 345, 0x7808, 0x7909, SP, 55); }
    [Fact] public void test_LLD8()  { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, SP, 33, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x16_LLD8();  checkStack(123, 345, 0x7909, 0x7A0A, SP, 55); }
    [Fact] public void test_LLD10() { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, SP, 33, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x17_LLD10(); checkStack(123, 345, 0x7B0B, 0x7C0C, SP, 55); }

    [Fact] public void test_LLDB()
    {
        Assert.NotEqual(0, Cpu.LF);
        mkStack(123, 345, SP, 33, 44, 55); mkLocalFrame(LF_FRAME);
        mkCode(1, 2, 3, savedPC, 0x0D, PC, 13, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x18_LLDB_alpha();
        checkStack(123, 345, 0x7E0E, 0x7F0F, SP, 55);
    }

    // ==================================================================
    // 7.2.1.2 Store Local — SL0..SL10, SLB, SLD0..SLD6, SLD8, SLDB
    // ==================================================================

    [Fact] public void test_SL0()  { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, 0x5555, SP, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x19_SL0();  checkStack(123, 345, SP, 0x5555, 44, 55); checkLocalFrame(0x5555, 0x7202, 0x7303, 0x7404, 0x7505, 0x7606, 0x7707, 0x7808, 0x7909, 0x7A0A, 0x7B0B, 0x7C0C, 0x7D0D, 0x7E0E, 0x7F0F); }
    [Fact] public void test_SL1()  { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, 0x5555, SP, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x1A_SL1();  checkStack(123, 345, SP, 0x5555, 44, 55); checkLocalFrame(0x7101, 0x5555, 0x7303, 0x7404, 0x7505, 0x7606, 0x7707, 0x7808, 0x7909, 0x7A0A, 0x7B0B, 0x7C0C, 0x7D0D, 0x7E0E, 0x7F0F); }
    [Fact] public void test_SL2()  { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, 0x5555, SP, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x1B_SL2();  checkStack(123, 345, SP, 0x5555, 44, 55); checkLocalFrame(0x7101, 0x7202, 0x5555, 0x7404, 0x7505, 0x7606, 0x7707, 0x7808, 0x7909, 0x7A0A, 0x7B0B, 0x7C0C, 0x7D0D, 0x7E0E, 0x7F0F); }
    [Fact] public void test_SL3()  { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, 0x5555, SP, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x1C_SL3();  checkStack(123, 345, SP, 0x5555, 44, 55); checkLocalFrame(0x7101, 0x7202, 0x7303, 0x5555, 0x7505, 0x7606, 0x7707, 0x7808, 0x7909, 0x7A0A, 0x7B0B, 0x7C0C, 0x7D0D, 0x7E0E, 0x7F0F); }
    [Fact] public void test_SL4()  { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, 0x5555, SP, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x1D_SL4();  checkStack(123, 345, SP, 0x5555, 44, 55); checkLocalFrame(0x7101, 0x7202, 0x7303, 0x7404, 0x5555, 0x7606, 0x7707, 0x7808, 0x7909, 0x7A0A, 0x7B0B, 0x7C0C, 0x7D0D, 0x7E0E, 0x7F0F); }
    [Fact] public void test_SL5()  { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, 0x5555, SP, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x1E_SL5();  checkStack(123, 345, SP, 0x5555, 44, 55); checkLocalFrame(0x7101, 0x7202, 0x7303, 0x7404, 0x7505, 0x5555, 0x7707, 0x7808, 0x7909, 0x7A0A, 0x7B0B, 0x7C0C, 0x7D0D, 0x7E0E, 0x7F0F); }
    [Fact] public void test_SL6()  { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, 0x5555, SP, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x1F_SL6();  checkStack(123, 345, SP, 0x5555, 44, 55); checkLocalFrame(0x7101, 0x7202, 0x7303, 0x7404, 0x7505, 0x7606, 0x5555, 0x7808, 0x7909, 0x7A0A, 0x7B0B, 0x7C0C, 0x7D0D, 0x7E0E, 0x7F0F); }
    [Fact] public void test_SL7()  { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, 0x5555, SP, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x20_SL7();  checkStack(123, 345, SP, 0x5555, 44, 55); checkLocalFrame(0x7101, 0x7202, 0x7303, 0x7404, 0x7505, 0x7606, 0x7707, 0x5555, 0x7909, 0x7A0A, 0x7B0B, 0x7C0C, 0x7D0D, 0x7E0E, 0x7F0F); }
    [Fact] public void test_SL8()  { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, 0x5555, SP, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x21_SL8();  checkStack(123, 345, SP, 0x5555, 44, 55); checkLocalFrame(0x7101, 0x7202, 0x7303, 0x7404, 0x7505, 0x7606, 0x7707, 0x7808, 0x5555, 0x7A0A, 0x7B0B, 0x7C0C, 0x7D0D, 0x7E0E, 0x7F0F); }
    [Fact] public void test_SL9()  { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, 0x5555, SP, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x22_SL9();  checkStack(123, 345, SP, 0x5555, 44, 55); checkLocalFrame(0x7101, 0x7202, 0x7303, 0x7404, 0x7505, 0x7606, 0x7707, 0x7808, 0x7909, 0x5555, 0x7B0B, 0x7C0C, 0x7D0D, 0x7E0E, 0x7F0F); }
    [Fact] public void test_SL10() { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, 0x5555, SP, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x23_SL10(); checkStack(123, 345, SP, 0x5555, 44, 55); checkLocalFrame(0x7101, 0x7202, 0x7303, 0x7404, 0x7505, 0x7606, 0x7707, 0x7808, 0x7909, 0x7A0A, 0x5555, 0x7C0C, 0x7D0D, 0x7E0E, 0x7F0F); }

    [Fact] public void test_SLB()
    {
        Assert.NotEqual(0, Cpu.LF);
        mkStack(123, 345, 0x5555, SP, 44, 55); mkLocalFrame(LF_FRAME);
        mkCode(1, 2, 3, savedPC, 0x24, PC, 14, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x24_SLB_alpha();
        checkStack(123, 345, SP, 0x5555, 44, 55);
        checkLocalFrame(0x7101, 0x7202, 0x7303, 0x7404, 0x7505, 0x7606, 0x7707, 0x7808, 0x7909, 0x7A0A, 0x7B0B, 0x7C0C, 0x7D0D, 0x7E0E, 0x5555);
    }

    [Fact] public void test_SLD0() { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, 0x5555, 0x6666, SP, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x25_SLD0(); checkStack(123, 345, SP, 0x5555, 0x6666, 44, 55); checkLocalFrame(0x5555, 0x6666, 0x7303, 0x7404, 0x7505, 0x7606, 0x7707, 0x7808, 0x7909, 0x7A0A, 0x7B0B, 0x7C0C, 0x7D0D, 0x7E0E, 0x7F0F); }
    [Fact] public void test_SLD1() { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, 0x5555, 0x6666, SP, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x26_SLD1(); checkStack(123, 345, SP, 0x5555, 0x6666, 44, 55); checkLocalFrame(0x7101, 0x5555, 0x6666, 0x7404, 0x7505, 0x7606, 0x7707, 0x7808, 0x7909, 0x7A0A, 0x7B0B, 0x7C0C, 0x7D0D, 0x7E0E, 0x7F0F); }
    [Fact] public void test_SLD2() { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, 0x5555, 0x6666, SP, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x27_SLD2(); checkStack(123, 345, SP, 0x5555, 0x6666, 44, 55); checkLocalFrame(0x7101, 0x7202, 0x5555, 0x6666, 0x7505, 0x7606, 0x7707, 0x7808, 0x7909, 0x7A0A, 0x7B0B, 0x7C0C, 0x7D0D, 0x7E0E, 0x7F0F); }
    [Fact] public void test_SLD3() { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, 0x5555, 0x6666, SP, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x28_SLD3(); checkStack(123, 345, SP, 0x5555, 0x6666, 44, 55); checkLocalFrame(0x7101, 0x7202, 0x7303, 0x5555, 0x6666, 0x7606, 0x7707, 0x7808, 0x7909, 0x7A0A, 0x7B0B, 0x7C0C, 0x7D0D, 0x7E0E, 0x7F0F); }
    [Fact] public void test_SLD4() { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, 0x5555, 0x6666, SP, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x29_SLD4(); checkStack(123, 345, SP, 0x5555, 0x6666, 44, 55); checkLocalFrame(0x7101, 0x7202, 0x7303, 0x7404, 0x5555, 0x6666, 0x7707, 0x7808, 0x7909, 0x7A0A, 0x7B0B, 0x7C0C, 0x7D0D, 0x7E0E, 0x7F0F); }
    [Fact] public void test_SLD5() { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, 0x5555, 0x6666, SP, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x2A_SLD5(); checkStack(123, 345, SP, 0x5555, 0x6666, 44, 55); checkLocalFrame(0x7101, 0x7202, 0x7303, 0x7404, 0x7505, 0x5555, 0x6666, 0x7808, 0x7909, 0x7A0A, 0x7B0B, 0x7C0C, 0x7D0D, 0x7E0E, 0x7F0F); }
    [Fact] public void test_SLD6() { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, 0x5555, 0x6666, SP, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x2B_SLD6(); checkStack(123, 345, SP, 0x5555, 0x6666, 44, 55); checkLocalFrame(0x7101, 0x7202, 0x7303, 0x7404, 0x7505, 0x7606, 0x5555, 0x6666, 0x7909, 0x7A0A, 0x7B0B, 0x7C0C, 0x7D0D, 0x7E0E, 0x7F0F); }
    [Fact] public void test_SLD8() { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, 0x5555, 0x6666, SP, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x2C_SLD8(); checkStack(123, 345, SP, 0x5555, 0x6666, 44, 55); checkLocalFrame(0x7101, 0x7202, 0x7303, 0x7404, 0x7505, 0x7606, 0x7707, 0x7808, 0x5555, 0x6666, 0x7B0B, 0x7C0C, 0x7D0D, 0x7E0E, 0x7F0F); }

    [Fact] public void test_SLDB()
    {
        Assert.NotEqual(0, Cpu.LF);
        mkStack(123, 345, 0x5555, 0x6666, SP, 44, 55); mkLocalFrame(LF_FRAME);
        mkCode(1, 2, 3, savedPC, 0x24, PC, 13, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x75_SLDB_alpha();
        checkStack(123, 345, SP, 0x5555, 0x6666, 44, 55);
        checkLocalFrame(0x7101, 0x7202, 0x7303, 0x7404, 0x7505, 0x7606, 0x7707, 0x7808, 0x7909, 0x7A0A, 0x7B0B, 0x7C0C, 0x7D0D, 0x5555, 0x6666);
    }

    // ==================================================================
    // 7.2.1.3 Put Local
    // ==================================================================

    [Fact] public void test_PL0() { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, 0x5555, SP, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x2D_PL0(); checkStack(123, 345, 0x5555, SP, 44, 55); checkLocalFrame(0x5555, 0x7202, 0x7303, 0x7404, 0x7505, 0x7606, 0x7707, 0x7808, 0x7909, 0x7A0A, 0x7B0B, 0x7C0C, 0x7D0D, 0x7E0E, 0x7F0F); }
    [Fact] public void test_PL1() { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, 0x5555, SP, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x2E_PL1(); checkStack(123, 345, 0x5555, SP, 44, 55); checkLocalFrame(0x7101, 0x5555, 0x7303, 0x7404, 0x7505, 0x7606, 0x7707, 0x7808, 0x7909, 0x7A0A, 0x7B0B, 0x7C0C, 0x7D0D, 0x7E0E, 0x7F0F); }
    [Fact] public void test_PL2() { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, 0x5555, SP, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x2F_PL2(); checkStack(123, 345, 0x5555, SP, 44, 55); checkLocalFrame(0x7101, 0x7202, 0x5555, 0x7404, 0x7505, 0x7606, 0x7707, 0x7808, 0x7909, 0x7A0A, 0x7B0B, 0x7C0C, 0x7D0D, 0x7E0E, 0x7F0F); }
    [Fact] public void test_PL3() { Assert.NotEqual(0, Cpu.LF); mkStack(123, 345, 0x5555, SP, 44, 55); mkLocalFrame(LF_FRAME); Ch07_Assignment_Instructions.OPC_x30_PL3(); checkStack(123, 345, 0x5555, SP, 44, 55); checkLocalFrame(0x7101, 0x7202, 0x7303, 0x5555, 0x7505, 0x7606, 0x7707, 0x7808, 0x7909, 0x7A0A, 0x7B0B, 0x7C0C, 0x7D0D, 0x7E0E, 0x7F0F); }

    [Fact] public void test_PLB()
    {
        Assert.NotEqual(0, Cpu.LF);
        mkStack(123, 345, 0x5555, SP, 44, 55); mkLocalFrame(LF_FRAME);
        mkCode(1, 2, 3, savedPC, 0x24, PC, 14, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x31_PLB_alpha();
        checkStack(123, 345, 0x5555, SP, 44, 55);
        checkLocalFrame(0x7101, 0x7202, 0x7303, 0x7404, 0x7505, 0x7606, 0x7707, 0x7808, 0x7909, 0x7A0A, 0x7B0B, 0x7C0C, 0x7D0D, 0x7E0E, 0x5555);
    }

    [Fact] public void test_PLD0()
    {
        Assert.NotEqual(0, Cpu.LF);
        mkStack(123, 345, 0x5555, 0x6666, SP, 44, 55); mkLocalFrame(LF_FRAME);
        Ch07_Assignment_Instructions.OPC_x32_PLD0();
        checkStack(123, 345, 0x5555, 0x6666, SP, 44, 55);
        checkLocalFrame(0x5555, 0x6666, 0x7303, 0x7404, 0x7505, 0x7606, 0x7707, 0x7808, 0x7909, 0x7A0A, 0x7B0B, 0x7C0C, 0x7D0D, 0x7E0E, 0x7F0F);
    }

    [Fact] public void test_PLDB()
    {
        Assert.NotEqual(0, Cpu.LF);
        mkStack(123, 345, 0x5555, 0x6666, SP, 44, 55); mkLocalFrame(LF_FRAME);
        mkCode(1, 2, 3, savedPC, 0x24, PC, 13, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x33_PLDB_alpha();
        checkStack(123, 345, 0x5555, 0x6666, SP, 44, 55);
        checkLocalFrame(0x7101, 0x7202, 0x7303, 0x7404, 0x7505, 0x7606, 0x7707, 0x7808, 0x7909, 0x7A0A, 0x7B0B, 0x7C0C, 0x7D0D, 0x5555, 0x6666);
    }

    // ==================================================================
    // 7.2.1.4 Add Local
    // ==================================================================

    [Fact] public void test_AL0IB()
    {
        Assert.NotEqual(0, Cpu.LF);
        mkStack(123, 345, SP, 44, 55); mkLocalFrame(LF_FRAME);
        mkCode(1, 2, 3, savedPC, 0xBB, PC, 0xF1, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_xBB_AL0IB_alpha();
        checkStack(123, 345, 0x71F2, SP, 55);
    }

    // ==================================================================
    // 7.2.2 Global Frame Access (old-style)
    // ==================================================================

    [Fact] public void test_GA0()
    {
        Assert.NotEqual(0, Cpu.GF32); Assert.NotEqual(0, Cpu.GF16);
        mkStack(123, 345, SP, 44, 55);
        Ch07_Assignment_Instructions.OPCo_xDA_GA0();
        checkStack(123, 345, Cpu.GF16, SP, 55);
    }
    [Fact] public void test_GA1()
    {
        Assert.NotEqual(0, Cpu.GF32); Assert.NotEqual(0, Cpu.GF16);
        mkStack(123, 345, SP, 44, 55);
        Ch07_Assignment_Instructions.OPCo_xDB_GA1();
        checkStack(123, 345, Cpu.GF16 + 1, SP, 55);
    }
    [Fact] public void test_GAB()
    {
        Assert.NotEqual(0, Cpu.GF32); Assert.NotEqual(0, Cpu.GF16);
        mkStack(123, 345, SP, 44, 55);
        mkCode(1, 2, 3, savedPC, 0xDC, PC, 0xFF, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPCo_xDC_GAB_alpha();
        checkStack(123, 345, Cpu.GF16 + 255, SP, 55);
    }
    [Fact] public void test_GAW()
    {
        Assert.NotEqual(0, Cpu.GF32); Assert.NotEqual(0, Cpu.GF16);
        mkStack(123, 345, SP, 44, 55);
        mkCode(1, 2, 3, savedPC, 0xDC, PC, 0x33, 0x44, 6, 7, 8);
        Ch07_Assignment_Instructions.OPCo_xDD_GAW_word();
        checkStack(123, 345, (Cpu.GF16 + 0x3344) & 0xFFFF, SP, 55);
    }

    // 7.2.2 Global Frame Access (changed chapters / post-4.0)
    [Fact] public void test_LGA0()
    {
        Assert.NotEqual(0, Cpu.GF32); Assert.NotEqual(0, Cpu.GF16);
        mkStack(123, 345, SP, 44, 55);
        Ch07_Assignment_Instructions.OPCn_xFA_LGA0();
        checkStack(123, 345, Cpu.GF32 & 0xFFFF, Cpu.GF32 >>> 16, SP);
    }
    [Fact] public void test_LGAB()
    {
        Assert.NotEqual(0, Cpu.GF32); Assert.NotEqual(0, Cpu.GF16);
        mkStack(123, 345, SP, 44, 55);
        mkCode(1, 2, 3, savedPC, 0xDC, PC, 0xFF, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPCn_xFB_LGAB_alpha();
        checkStack(123, 345, (Cpu.GF32 + 255) & 0xFFFF, (Cpu.GF32 + 255) >>> 16, SP);
    }
    [Fact] public void test_LGAW()
    {
        Assert.NotEqual(0, Cpu.GF32); Assert.NotEqual(0, Cpu.GF16);
        mkStack(123, 345, SP, 44, 55);
        mkCode(1, 2, 3, savedPC, 0xDC, PC, 0x33, 0x44, 6, 7, 8);
        Ch07_Assignment_Instructions.OPCn_xFC_LGAW_word();
        checkStack(123, 345, (Cpu.GF32 + 0x3344) & 0xFFFF, (Cpu.GF32 + 0x3344) >>> 16, SP);
    }

    // ==================================================================
    // 7.2.2.1 Load Global
    // ==================================================================

    private static readonly int[] GF_FRAME =
        { 0x07000, 0x7101, 0x7202, 0x7303, 0x7404, 0x7505, 0x7606, 0x7707, 0x7808, 0x7909, 0x7A0A, 0x7B0B, 0x7C0C, 0x7D0D, 0x7E0E, 0x7F0F };

    [Fact] public void test_o_LG0() { mkStack(123, 345, SP, 44, 55); mkGlobalFrame(GF_FRAME); Ch07_Assignment_Instructions.OPCo_x34_LG0(); checkStack(123, 345, 0x7000, SP, 55); }
    [Fact] public void test_n_LG0() { mkStack(123, 345, SP, 44, 55); mkGlobalFrame(GF_FRAME); Ch07_Assignment_Instructions.OPCn_x34_LG0(); checkStack(123, 345, 0x7000, SP, 55); }
    [Fact] public void test_o_LG1() { mkStack(123, 345, SP, 44, 55); mkGlobalFrame(GF_FRAME); Ch07_Assignment_Instructions.OPCo_x35_LG1(); checkStack(123, 345, 0x7101, SP, 55); }
    [Fact] public void test_n_LG1() { mkStack(123, 345, SP, 44, 55); mkGlobalFrame(GF_FRAME); Ch07_Assignment_Instructions.OPCn_x35_LG1(); checkStack(123, 345, 0x7101, SP, 55); }
    [Fact] public void test_o_LG2() { mkStack(123, 345, SP, 44, 55); mkGlobalFrame(GF_FRAME); Ch07_Assignment_Instructions.OPCo_x36_LG2(); checkStack(123, 345, 0x7202, SP, 55); }
    [Fact] public void test_n_LG2() { mkStack(123, 345, SP, 44, 55); mkGlobalFrame(GF_FRAME); Ch07_Assignment_Instructions.OPCn_x36_LG2(); checkStack(123, 345, 0x7202, SP, 55); }

    [Fact] public void test_o_LGB() { mkStack(123, 345, SP, 44, 55); mkGlobalFrame(GF_FRAME); mkCode(1, 2, 3, savedPC, 0xDC, PC, 0x0E, 5, 6, 7, 8); Ch07_Assignment_Instructions.OPCo_x37_LGB_alpha(); checkStack(123, 345, 0x7E0E, SP, 55); }
    [Fact] public void test_n_LGB() { mkStack(123, 345, SP, 44, 55); mkGlobalFrame(GF_FRAME); mkCode(1, 2, 3, savedPC, 0xDC, PC, 0x0E, 5, 6, 7, 8); Ch07_Assignment_Instructions.OPCn_x37_LGB_alpha(); checkStack(123, 345, 0x7E0E, SP, 55); }

    [Fact] public void test_o_LGD0() { mkStack(123, 345, SP, 44, 55); mkGlobalFrame(GF_FRAME); Ch07_Assignment_Instructions.OPCo_x38_LGD0(); checkStack(123, 345, 0x7000, 0x7101, SP); }
    [Fact] public void test_n_LGD0() { mkStack(123, 345, SP, 44, 55); mkGlobalFrame(GF_FRAME); Ch07_Assignment_Instructions.OPCn_x38_LGD0(); checkStack(123, 345, 0x7000, 0x7101, SP); }
    [Fact] public void test_o_LGD2() { mkStack(123, 345, SP, 44, 55); mkGlobalFrame(GF_FRAME); Ch07_Assignment_Instructions.OPCo_x39_LGD2(); checkStack(123, 345, 0x7202, 0x7303, SP); }
    [Fact] public void test_n_LGD2() { mkStack(123, 345, SP, 44, 55); mkGlobalFrame(GF_FRAME); Ch07_Assignment_Instructions.OPCn_x39_LGD2(); checkStack(123, 345, 0x7202, 0x7303, SP); }

    [Fact] public void test_o_LGDB() { mkStack(123, 345, SP, 44, 55); mkGlobalFrame(GF_FRAME); mkCode(1, 2, 3, savedPC, 0xDC, PC, 0x0E, 5, 6, 7, 8); Ch07_Assignment_Instructions.OPCo_x3A_LGDB_alpha(); checkStack(123, 345, 0x7E0E, 0x7F0F, SP); }
    [Fact] public void test_n_LGDB() { mkStack(123, 345, SP, 44, 55); mkGlobalFrame(GF_FRAME); mkCode(1, 2, 3, savedPC, 0xDC, PC, 0x0E, 5, 6, 7, 8); Ch07_Assignment_Instructions.OPCn_x3A_LGDB_alpha(); checkStack(123, 345, 0x7E0E, 0x7F0F, SP); }

    // ==================================================================
    // 7.2.2.2 Store Global
    // ==================================================================

    [Fact] public void test_o_SGB()
    {
        mkStack(123, 345, 0x8765, SP, 44, 55); mkGlobalFrame(GF_FRAME);
        mkCode(1, 2, 3, savedPC, 0x3B, PC, 0x0E, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPCo_x3B_SGB_alpha();
        checkStack(123, 345, SP);
        checkGlobalFrame(0x07000, 0x7101, 0x7202, 0x7303, 0x7404, 0x7505, 0x7606, 0x7707, 0x7808, 0x7909, 0x7A0A, 0x7B0B, 0x7C0C, 0x7D0D, 0x8765, 0x7F0F);
    }
    [Fact] public void test_n_SGB()
    {
        mkStack(123, 345, 0x8765, SP, 44, 55); mkGlobalFrame(GF_FRAME);
        mkCode(1, 2, 3, savedPC, 0x3B, PC, 0x0E, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPCn_x3B_SGB_alpha();
        checkStack(123, 345, SP);
        checkGlobalFrame(0x07000, 0x7101, 0x7202, 0x7303, 0x7404, 0x7505, 0x7606, 0x7707, 0x7808, 0x7909, 0x7A0A, 0x7B0B, 0x7C0C, 0x7D0D, 0x8765, 0x7F0F);
    }

    [Fact] public void test_o_SGDB()
    {
        mkStack(123, 345, 0x8765, 0x9876, SP, 44, 55); mkGlobalFrame(GF_FRAME);
        mkCode(1, 2, 3, savedPC, 0x3B, PC, 0x0E, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPCo_x76_SGDB_alpha();
        checkStack(123, 345, SP);
        checkGlobalFrame(0x07000, 0x7101, 0x7202, 0x7303, 0x7404, 0x7505, 0x7606, 0x7707, 0x7808, 0x7909, 0x7A0A, 0x7B0B, 0x7C0C, 0x7D0D, 0x8765, 0x9876);
    }
    [Fact] public void test_n_SGDB()
    {
        mkStack(123, 345, 0x8765, 0x9876, SP, 44, 55); mkGlobalFrame(GF_FRAME);
        mkCode(1, 2, 3, savedPC, 0x3B, PC, 0x0E, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPCn_x76_SGDB_alpha();
        checkStack(123, 345, SP);
        checkGlobalFrame(0x07000, 0x7101, 0x7202, 0x7303, 0x7404, 0x7505, 0x7606, 0x7707, 0x7808, 0x7909, 0x7A0A, 0x7B0B, 0x7C0C, 0x7D0D, 0x8765, 0x9876);
    }

    // ==================================================================
    // 7.3.1.1 Read Direct
    // ==================================================================

    private static readonly int[] FIVE_MEM = { 0x1234, 0x2345, 0x3456, 0x4567, 0x5678 };
    private static readonly int[] SIX_MEM  = { 0x1234, 0x2345, 0x3456, 0x4567, 0x5678, 0x6789 };

    [Fact] public void test_R0()
    {
        mkStack(22, 33, testShortMem); mkShortMem(FIVE_MEM);
        Ch07_Assignment_Instructions.OPC_x40_R0(); checkStack(22, 33, 0x1234);
    }
    [Fact] public void test_R1()
    {
        mkStack(22, 33, testShortMem); mkShortMem(FIVE_MEM);
        Ch07_Assignment_Instructions.OPC_x41_R1(); checkStack(22, 33, 0x2345);
    }
    [Fact] public void test_RB()
    {
        mkStack(22, 33, testShortMem); mkShortMem(FIVE_MEM);
        mkCode(1, 2, 3, savedPC, 0x42, PC, 0x03, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x42_RB_alpha(); checkStack(22, 33, 0x4567);
    }
    [Fact] public void test_RL0()
    {
        mkStack(22, 33, testLongMemLow, testLongMemHigh); mkLongMem(FIVE_MEM);
        Ch07_Assignment_Instructions.OPC_x43_RL0(); checkStack(22, 33, 0x1234);
    }
    [Fact] public void test_RLB()
    {
        mkStack(22, 33, testLongMemLow, testLongMemHigh); mkLongMem(FIVE_MEM);
        mkCode(1, 2, 3, savedPC, 0x42, PC, 0x03, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x44_RLB_alpha(); checkStack(22, 33, 0x4567);
    }
    [Fact] public void test_RD0()
    {
        mkStack(22, 33, testShortMem); mkShortMem(FIVE_MEM);
        Ch07_Assignment_Instructions.OPC_x45_RD0(); checkStack(22, 33, 0x1234, 0x2345);
    }
    [Fact] public void test_RDB()
    {
        mkStack(22, 33, testShortMem); mkShortMem(FIVE_MEM);
        mkCode(1, 2, 3, savedPC, 0x42, PC, 0x03, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x46_RDB_alpha(); checkStack(22, 33, 0x4567, 0x5678);
    }
    [Fact] public void test_RDL0()
    {
        mkStack(22, 33, testLongMemLow, testLongMemHigh); mkLongMem(FIVE_MEM);
        Ch07_Assignment_Instructions.OPC_x47_RDL0(); checkStack(22, 33, 0x1234, 0x2345);
    }
    [Fact] public void test_RDLB()
    {
        mkStack(22, 33, testLongMemLow, testLongMemHigh); mkLongMem(FIVE_MEM);
        mkCode(1, 2, 3, savedPC, 0x48, PC, 0x03, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x48_RDLB_alpha(); checkStack(22, 33, 0x4567, 0x5678);
    }
    [Fact] public void test_RC()
    {
        Mem.writeWord(Cpu.CB + 48, (ushort)0x9283);
        mkStack(22, 33, 44);
        mkCode(1, 2, 3, savedPC, 0x00, 0x48, PC, 0x04, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.ESC_x1B_RC_alpha(); checkStack(22, 33, 0x9283);
    }

    // ==================================================================
    // 7.3.1.2 Write Direct
    // ==================================================================

    [Fact] public void test_W0()
    {
        mkStack(22, 33, 0x7654, testShortMem); mkShortMem(FIVE_MEM);
        Ch07_Assignment_Instructions.OPC_x49_W0();
        checkStack(22, 33);
        checkShortMem(0x7654, 0x2345, 0x3456, 0x4567, 0x5678);
    }
    [Fact] public void test_WB()
    {
        mkStack(22, 33, 0x7654, testShortMem); mkShortMem(FIVE_MEM);
        mkCode(1, 2, 3, savedPC, 0x4A, PC, 0x03, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x4A_WB_alpha();
        checkStack(22, 33);
        checkShortMem(0x1234, 0x2345, 0x3456, 0x7654, 0x5678);
    }
    [Fact] public void test_WLB()
    {
        mkStack(22, 33, 0x7654, testLongMemLow, testLongMemHigh); mkLongMem(FIVE_MEM);
        mkCode(1, 2, 3, savedPC, 0x4C, PC, 0x03, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x4C_WLB_alpha();
        checkStack(22, 33);
        checkLongMem(0x1234, 0x2345, 0x3456, 0x7654, 0x5678);
    }
    [Fact] public void test_WDB()
    {
        mkStack(22, 33, 0x7654, 0xABCD, testShortMem); mkShortMem(SIX_MEM);
        mkCode(1, 2, 3, savedPC, 0x4E, PC, 0x03, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x4E_WDB_alpha();
        checkStack(22, 33);
        checkShortMem(0x1234, 0x2345, 0x3456, 0x7654, 0xABCD, 0x6789);
    }
    [Fact] public void test_WDLB()
    {
        mkStack(22, 33, 0x7654, 0xABCD, testLongMemLow, testLongMemHigh); mkLongMem(SIX_MEM);
        mkCode(1, 2, 3, savedPC, 0x51, PC, 0x03, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x51_WDLB_alpha();
        checkStack(22, 33);
        checkLongMem(0x1234, 0x2345, 0x3456, 0x7654, 0xABCD, 0x6789);
    }

    // ==================================================================
    // 7.3.1.3 Put Swapped Direct
    // ==================================================================

    [Fact] public void test_PSB()
    {
        mkStack(22, 33, testShortMem, 0x7654); mkShortMem(FIVE_MEM);
        mkCode(1, 2, 3, savedPC, 0x4B, PC, 0x03, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x4B_PSB_alpha();
        checkStack(22, 33, testShortMem);
        checkShortMem(0x1234, 0x2345, 0x3456, 0x7654, 0x5678);
    }
    [Fact] public void test_PSD0()
    {
        mkStack(22, 33, testShortMem, 0x7654, 0xFEDC); mkShortMem(FIVE_MEM);
        Ch07_Assignment_Instructions.OPC_x4F_PSD0();
        checkStack(22, 33, testShortMem);
        checkShortMem(0x7654, 0xFEDC, 0x3456, 0x4567, 0x5678);
    }
    [Fact] public void test_PSDB()
    {
        mkStack(22, 33, testShortMem, 0x7654, 0xFEDC); mkShortMem(FIVE_MEM);
        mkCode(1, 2, 3, savedPC, 0x50, PC, 0x03, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x50_PSDB_alpha();
        checkStack(22, 33, testShortMem);
        checkShortMem(0x1234, 0x2345, 0x3456, 0x7654, 0xFEDC);
    }
    [Fact] public void test_PSLB()
    {
        mkStack(22, 33, testLongMemLow, testLongMemHigh, 0x7654); mkLongMem(FIVE_MEM);
        mkCode(1, 2, 3, savedPC, 0x4B, PC, 0x03, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x4D_PSLB_alpha();
        checkStack(22, 33, testLongMemLow, testLongMemHigh);
        checkLongMem(0x1234, 0x2345, 0x3456, 0x7654, 0x5678);
    }
    [Fact] public void test_PSDLB()
    {
        mkStack(22, 33, testLongMemLow, testLongMemHigh, 0x7654, 0xFEDC); mkLongMem(FIVE_MEM);
        mkCode(1, 2, 3, savedPC, 0x52, PC, 0x03, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x52_PSDLB_alpha();
        checkStack(22, 33, testLongMemLow, testLongMemHigh);
        checkLongMem(0x1234, 0x2345, 0x3456, 0x7654, 0xFEDC);
    }

    // ==================================================================
    // 7.3.2.1 Read Indirect
    // ==================================================================

    private static readonly int[] EIGHT_MEM    = { 0x9393, 0x2233, 0x5858, 0x3737, 0x1616, 0x0505, 0x8181, 0x9933 };
    private static readonly int[] FOURTEEN_MEM = { 0x9393, 0x2233, 0x5858, 0x3737, 0x1616, 0x0505, 0x8181, 0x9933, 0x1234, 0x4321, 0x2222, 0x3333, 0x4444, 0x5555 };

    [Fact] public void test_RLI00() { mkLocalFrame(testShortMem, 111, 222); mkShortMem(EIGHT_MEM); mkStack(22, 33); Ch07_Assignment_Instructions.OPC_x53_RLI00(); checkStack(22, 33, 0x9393); }
    [Fact] public void test_RLI01() { mkLocalFrame(testShortMem, 111, 222); mkShortMem(EIGHT_MEM); mkStack(22, 33); Ch07_Assignment_Instructions.OPC_x54_RLI01(); checkStack(22, 33, 0x2233); }
    [Fact] public void test_RLI02() { mkLocalFrame(testShortMem, 111, 222); mkShortMem(EIGHT_MEM); mkStack(22, 33); Ch07_Assignment_Instructions.OPC_x55_RLI02(); checkStack(22, 33, 0x5858); }
    [Fact] public void test_RLI03() { mkLocalFrame(testShortMem, 111, 222); mkShortMem(EIGHT_MEM); mkStack(22, 33); Ch07_Assignment_Instructions.OPC_x56_RLI03(); checkStack(22, 33, 0x3737); }

    [Fact] public void test_RLIP_x15()
    {
        mkLocalFrame(333, testShortMem, 111, 222); mkShortMem(FOURTEEN_MEM); mkStack(22, 33);
        mkCode(1, 2, 3, savedPC, 0x57, PC, 0x15, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x57_RLIP_pair(); checkStack(22, 33, 0x0505);
    }
    [Fact] public void test_RLIP_x9C()
    {
        mkLocalFrame(111, 222, 333, 444, 555, 666, 777, 888, 999, testShortMem, 111, 222); mkShortMem(FOURTEEN_MEM); mkStack(22, 33);
        mkCode(1, 2, 3, savedPC, 0x57, PC, 0x9C, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x57_RLIP_pair(); checkStack(22, 33, 0x4444);
    }

    [Fact] public void test_RLILP_x15()
    {
        mkLocalFrame(333, testLongMemLow, testLongMemHigh, 111, 222); mkLongMem(FOURTEEN_MEM); mkStack(22, 33);
        mkCode(1, 2, 3, savedPC, 0x58, PC, 0x15, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x58_RLILP_pair(); checkStack(22, 33, 0x0505);
    }
    [Fact] public void test_RLILP_x9C()
    {
        mkLocalFrame(111, 222, 333, 444, 555, 666, 777, 888, 999, testLongMemLow, testLongMemHigh, 111, 222); mkLongMem(FOURTEEN_MEM); mkStack(22, 33);
        mkCode(1, 2, 3, savedPC, 0x58, PC, 0x9C, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x58_RLILP_pair(); checkStack(22, 33, 0x4444);
    }

    [Fact] public void test_o_RGIP_x15()
    {
        mkGlobalFrame(333, testShortMem, 111, 222); mkShortMem(FOURTEEN_MEM); mkStack(22, 33);
        mkCode(1, 2, 3, savedPC, 0x5C, PC, 0x15, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPCo_x5C_RGIP_pair(); checkStack(22, 33, 0x0505);
    }
    [Fact] public void test_n_RGIP_x15()
    {
        mkGlobalFrame(333, testShortMem, 111, 222); mkShortMem(FOURTEEN_MEM); mkStack(22, 33);
        mkCode(1, 2, 3, savedPC, 0x5C, PC, 0x15, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPCn_x5C_RGIP_pair(); checkStack(22, 33, 0x0505);
    }
    [Fact] public void test_o_RGIP_x9C()
    {
        mkGlobalFrame(111, 222, 333, 444, 555, 666, 777, 888, 999, testShortMem, 111, 222); mkShortMem(FOURTEEN_MEM); mkStack(22, 33);
        mkCode(1, 2, 3, savedPC, 0x5C, PC, 0x9C, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPCo_x5C_RGIP_pair(); checkStack(22, 33, 0x4444);
    }
    [Fact] public void test_n_RGIP_x9C()
    {
        mkGlobalFrame(111, 222, 333, 444, 555, 666, 777, 888, 999, testShortMem, 111, 222); mkShortMem(FOURTEEN_MEM); mkStack(22, 33);
        mkCode(1, 2, 3, savedPC, 0x5C, PC, 0x9C, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPCn_x5C_RGIP_pair(); checkStack(22, 33, 0x4444);
    }

    [Fact] public void test_o_RGILP_x15()
    {
        mkGlobalFrame(333, testLongMemLow, testLongMemHigh, 111, 222); mkLongMem(FOURTEEN_MEM); mkStack(22, 33);
        mkCode(1, 2, 3, savedPC, 0x5D, PC, 0x15, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPCo_x5D_RGILP_pair(); checkStack(22, 33, 0x0505);
    }
    [Fact] public void test_n_RGILP_x15()
    {
        mkGlobalFrame(333, testLongMemLow, testLongMemHigh, 111, 222); mkLongMem(FOURTEEN_MEM); mkStack(22, 33);
        mkCode(1, 2, 3, savedPC, 0x5D, PC, 0x15, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPCn_x5D_RGILP_pair(); checkStack(22, 33, 0x0505);
    }
    [Fact] public void test_o_RGILP_x9C()
    {
        mkGlobalFrame(111, 222, 333, 444, 555, 666, 777, 888, 999, testLongMemLow, testLongMemHigh, 111, 222); mkLongMem(FOURTEEN_MEM); mkStack(22, 33);
        mkCode(1, 2, 3, savedPC, 0x5D, PC, 0x9C, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPCo_x5D_RGILP_pair(); checkStack(22, 33, 0x4444);
    }
    [Fact] public void test_n_RGILP_x9C()
    {
        mkGlobalFrame(111, 222, 333, 444, 555, 666, 777, 888, 999, testLongMemLow, testLongMemHigh, 111, 222); mkLongMem(FOURTEEN_MEM); mkStack(22, 33);
        mkCode(1, 2, 3, savedPC, 0x5D, PC, 0x9C, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPCn_x5D_RGILP_pair(); checkStack(22, 33, 0x4444);
    }

    [Fact] public void test_RLDI00()
    {
        mkLocalFrame(testShortMem + 3, 111, 222); mkShortMem(FOURTEEN_MEM); mkStack(22, 33);
        Ch07_Assignment_Instructions.OPC_x59_RLDI00(); checkStack(22, 33, 0x3737, 0x1616);
    }
    [Fact] public void test_RLDIP_x15()
    {
        mkLocalFrame(333, testShortMem, 111, 222); mkShortMem(FOURTEEN_MEM); mkStack(22, 33);
        mkCode(1, 2, 3, savedPC, 0x5A, PC, 0x15, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x5A_RLDIP_pair(); checkStack(22, 33, 0x0505, 0x8181);
    }
    [Fact] public void test_RLDIP_x9C()
    {
        mkLocalFrame(111, 222, 333, 444, 555, 666, 777, 888, 999, testShortMem, 111, 222); mkShortMem(FOURTEEN_MEM); mkStack(22, 33);
        mkCode(1, 2, 3, savedPC, 0x5A, PC, 0x9C, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x5A_RLDIP_pair(); checkStack(22, 33, 0x4444, 0x5555);
    }
    [Fact] public void test_RLDILP_x15()
    {
        mkLocalFrame(333, testLongMemLow, testLongMemHigh, 111, 222); mkLongMem(FOURTEEN_MEM); mkStack(22, 33);
        mkCode(1, 2, 3, savedPC, 0x5B, PC, 0x15, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x5B_RLDILP_pair(); checkStack(22, 33, 0x0505, 0x8181);
    }
    [Fact] public void test_RLDILP_x9C()
    {
        mkLocalFrame(111, 222, 333, 444, 555, 666, 777, 888, 999, testLongMemLow, testLongMemHigh, 111, 222); mkLongMem(FOURTEEN_MEM); mkStack(22, 33);
        mkCode(1, 2, 3, savedPC, 0x5B, PC, 0x9C, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x5B_RLDILP_pair(); checkStack(22, 33, 0x4444, 0x5555);
    }

    // ==================================================================
    // 7.3.2.2 Write Indirect
    // ==================================================================

    [Fact] public void test_WLIP_x15()
    {
        mkLocalFrame(333, testShortMem, 111, 222); mkShortMem(FOURTEEN_MEM); mkStack(22, 33, 0xFEFE);
        mkCode(1, 2, 3, savedPC, 0x57, PC, 0x15, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x5E_WLIP_pair();
        checkStack(22, 33);
        checkShortMem(0x9393, 0x2233, 0x5858, 0x3737, 0x1616, 0xFEFE, 0x8181, 0x9933, 0x1234, 0x4321, 0x2222, 0x3333, 0x4444, 0x5555);
    }
    [Fact] public void test_WLIP_x9C()
    {
        mkLocalFrame(111, 222, 333, 444, 555, 666, 777, 888, 999, testShortMem, 111, 222); mkShortMem(FOURTEEN_MEM); mkStack(22, 33, 0xEFEF);
        mkCode(1, 2, 3, savedPC, 0x5E, PC, 0x9C, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x5E_WLIP_pair();
        checkStack(22, 33);
        checkShortMem(0x9393, 0x2233, 0x5858, 0x3737, 0x1616, 0x0505, 0x8181, 0x9933, 0x1234, 0x4321, 0x2222, 0x3333, 0xEFEF, 0x5555);
    }
    [Fact] public void test_WLILP_x15()
    {
        mkLocalFrame(333, testLongMemLow, testLongMemHigh, 111, 222); mkLongMem(FOURTEEN_MEM); mkStack(22, 33, 0xDEDE);
        mkCode(1, 2, 3, savedPC, 0x5F, PC, 0x15, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x5F_WLILP_pair();
        checkStack(22, 33);
        checkLongMem(0x9393, 0x2233, 0x5858, 0x3737, 0x1616, 0xDEDE, 0x8181, 0x9933, 0x1234, 0x4321, 0x2222, 0x3333, 0x4444, 0x5555);
    }
    [Fact] public void test_WLILP_x9C()
    {
        mkLocalFrame(111, 222, 333, 444, 555, 666, 777, 888, 999, testLongMemLow, testLongMemHigh, 111, 222); mkLongMem(FOURTEEN_MEM); mkStack(22, 33, 0xEDED);
        mkCode(1, 2, 3, savedPC, 0x5F, PC, 0x9C, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x5F_WLILP_pair();
        checkStack(22, 33);
        checkLongMem(0x9393, 0x2233, 0x5858, 0x3737, 0x1616, 0x0505, 0x8181, 0x9933, 0x1234, 0x4321, 0x2222, 0x3333, 0xEDED, 0x5555);
    }
    [Fact] public void test_WLDILP_x15()
    {
        mkLocalFrame(333, testLongMemLow, testLongMemHigh, 111, 222); mkLongMem(FOURTEEN_MEM); mkStack(22, 33, 0xBBAA, 0xCCDD);
        mkCode(1, 2, 3, savedPC, 0x60, PC, 0x15, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x60_WLDILP_pair();
        checkStack(22, 33);
        checkLongMem(0x9393, 0x2233, 0x5858, 0x3737, 0x1616, 0xBBAA, 0xCCDD, 0x9933, 0x1234, 0x4321, 0x2222, 0x3333, 0x4444, 0x5555);
    }
    [Fact] public void test_WLDILP_x9C()
    {
        mkLocalFrame(111, 222, 333, 444, 555, 666, 777, 888, 999, testLongMemLow, testLongMemHigh, 111, 222); mkLongMem(FOURTEEN_MEM); mkStack(22, 33, 0xFFEE, 0xDDCC);
        mkCode(1, 2, 3, savedPC, 0x60, PC, 0x9C, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x60_WLDILP_pair();
        checkStack(22, 33);
        checkLongMem(0x9393, 0x2233, 0x5858, 0x3737, 0x1616, 0x0505, 0x8181, 0x9933, 0x1234, 0x4321, 0x2222, 0x3333, 0xFFEE, 0xDDCC);
    }

    // ==================================================================
    // 7.4 String Instructions
    // ==================================================================

    private static readonly int[] STR_MEM = { 0, 0x0102, 0x0304, 0x0506, 0x0708, 0x090A, 0x0B0C, 0x0D0E, 0x0F10, 0x1112, 0x1314, 0x1516, 0x1718 };
    private static readonly int[] STR_MEM_HBIT = { 0, 0x8182, 0x8384, 0x8586, 0x8788, 0x898A, 0x8B8C, 0x8D8E, 0x8F90, 0x9192, 0x9394, 0x9596, 0x9798 };

    [Fact] public void test_RS_alpha4_index3()
    {
        mkShortMem(STR_MEM); mkStack(11, 22, testShortMem + 1, 3);
        mkCode(1, 2, 3, savedPC, 0x61, PC, 0x04, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x61_RS_alpha(); checkStack(11, 22, 0x0008);
    }
    [Fact] public void test_RS_alpha4_index16()
    {
        mkShortMem(STR_MEM); mkStack(11, 22, testShortMem + 1, 16);
        mkCode(1, 2, 3, savedPC, 0x61, PC, 0x04, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x61_RS_alpha(); checkStack(11, 22, 0x0015);
    }
    [Fact] public void test_RS_alpha0_index0()
    {
        mkShortMem(STR_MEM); mkStack(11, 22, testShortMem + 1, 0);
        mkCode(1, 2, 3, savedPC, 0x61, PC, 0x00, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x61_RS_alpha(); checkStack(11, 22, 0x0001);
    }

    [Fact] public void test_RLS_alpha4_index3()
    {
        mkLongMem(STR_MEM); mkStack(11, 22, testLongMemLow + 1, testLongMemHigh, 3);
        mkCode(1, 2, 3, savedPC, 0x61, PC, 0x04, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x62_RLS_alpha(); checkStack(11, 22, 0x0008);
    }
    [Fact] public void test_RLS_alpha4_index3_hbit()
    {
        mkLongMem(STR_MEM_HBIT); mkStack(11, 22, testLongMemLow + 1, testLongMemHigh, 3);
        mkCode(1, 2, 3, savedPC, 0x61, PC, 0x04, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x62_RLS_alpha(); checkStack(11, 22, 0x0088);
    }
    [Fact] public void test_RLS_alpha5_index3_hbit()
    {
        mkLongMem(STR_MEM_HBIT); mkStack(11, 22, testLongMemLow + 1, testLongMemHigh, 3);
        mkCode(1, 2, 3, savedPC, 0x61, PC, 0x05, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x62_RLS_alpha(); checkStack(11, 22, 0x0089);
    }
    [Fact] public void test_RLS_alpha4_index16()
    {
        mkLongMem(STR_MEM); mkStack(11, 22, testLongMemLow + 1, testLongMemHigh, 16);
        mkCode(1, 2, 3, savedPC, 0x62, PC, 0x04, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x62_RLS_alpha(); checkStack(11, 22, 0x0015);
    }
    [Fact] public void test_RLS_alpha0_index0()
    {
        mkLongMem(STR_MEM); mkStack(11, 22, testLongMemLow + 1, testLongMemHigh, 0);
        mkCode(1, 2, 3, savedPC, 0x62, PC, 0x00, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x62_RLS_alpha(); checkStack(11, 22, 0x0001);
    }

    [Fact] public void test_WS_alpha4_index3()
    {
        mkShortMem(STR_MEM); mkStack(11, 22, 0xAA33, testShortMem + 1, 3);
        mkCode(1, 2, 3, savedPC, 0x63, PC, 0x04, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x63_WS_alpha();
        checkShortMem(0, 0x0102, 0x0304, 0x0506, 0x0733, 0x090A, 0x0B0C, 0x0D0E, 0x0F10, 0x1112, 0x1314, 0x1516, 0x1718);
        checkStack(11, 22);
    }
    [Fact] public void test_WS_alpha4_index16()
    {
        mkShortMem(STR_MEM); mkStack(11, 22, 0xAA44, testShortMem + 1, 16);
        mkCode(1, 2, 3, savedPC, 0x63, PC, 0x04, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x63_WS_alpha();
        checkShortMem(0, 0x0102, 0x0304, 0x0506, 0x0708, 0x090A, 0x0B0C, 0x0D0E, 0x0F10, 0x1112, 0x1314, 0x4416, 0x1718);
        checkStack(11, 22);
    }
    [Fact] public void test_WS_alpha0_index0()
    {
        mkShortMem(STR_MEM); mkStack(11, 22, 0xAA55, testShortMem + 1, 0);
        mkCode(1, 2, 3, savedPC, 0x63, PC, 0x00, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x63_WS_alpha();
        checkShortMem(0, 0x5502, 0x0304, 0x0506, 0x0708, 0x090A, 0x0B0C, 0x0D0E, 0x0F10, 0x1112, 0x1314, 0x1516, 0x1718);
        checkStack(11, 22);
    }
    [Fact] public void test_WLS_alpha4_index3()
    {
        mkLongMem(STR_MEM); mkStack(11, 22, 0xAA55, testLongMemLow + 1, testLongMemHigh, 3);
        mkCode(1, 2, 3, savedPC, 0x64, PC, 0x04, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x64_WLS_alpha();
        checkLongMem(0, 0x0102, 0x0304, 0x0506, 0x0755, 0x090A, 0x0B0C, 0x0D0E, 0x0F10, 0x1112, 0x1314, 0x1516, 0x1718);
        checkStack(11, 22);
    }
    [Fact] public void test_WLS_alpha4_index16()
    {
        mkLongMem(STR_MEM); mkStack(11, 22, 0xAA66, testLongMemLow + 1, testLongMemHigh, 16);
        mkCode(1, 2, 3, savedPC, 0x64, PC, 0x04, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x64_WLS_alpha();
        checkLongMem(0, 0x0102, 0x0304, 0x0506, 0x0708, 0x090A, 0x0B0C, 0x0D0E, 0x0F10, 0x1112, 0x1314, 0x6616, 0x1718);
        checkStack(11, 22);
    }
    [Fact] public void test_WLS_alpha0_index0()
    {
        mkLongMem(STR_MEM); mkStack(11, 22, 0xAA77, testLongMemLow + 1, testLongMemHigh, 0);
        mkCode(1, 2, 3, savedPC, 0x64, PC, 0x00, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x64_WLS_alpha();
        checkLongMem(0, 0x7702, 0x0304, 0x0506, 0x0708, 0x090A, 0x0B0C, 0x0D0E, 0x0F10, 0x1112, 0x1314, 0x1516, 0x1718);
        checkStack(11, 22);
    }

    // ==================================================================
    // 7.5 Field Instructions
    // ==================================================================

    private static int mkFieldSpec(int pos, int width)
    {
        if (width > 16 || pos > 15)
        {
            Assert.Fail("invalid field spec");
        }
        int spec = (pos << 4) | ((width - 1) & 0x0F);
        return spec & 0xFF;
    }

    private static int mkFieldDesc(int offset, int fieldSpec) => (offset << 8) | fieldSpec;
    private static int mkFieldDesc(int offset, int pos, int width) => mkFieldDesc(offset, mkFieldSpec(pos, width));

    // ---- 7.5.1 Read Field ----

    [Fact] public void test_RF_a()
    {
        int desc = mkFieldDesc(2, 2, 4);
        mkShortMem(0x0000, 0x0000, 0b0011110000000000);
        mkStack(11, 22, testShortMem);
        mkCode(1, 2, 3, savedPC, 0x66, PC, desc >>> 8, desc & 0x00FF, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x66_RF_word(); checkStack(11, 22, 0x000F);
    }
    [Fact] public void test_RF_b()
    {
        int desc = mkFieldDesc(2, 2, 4);
        mkShortMem(0xFFFF, 0xFFFF, 0b1100001111111111, 0xFFFF, 0xFFFF);
        mkStack(11, 22, testShortMem);
        mkCode(1, 2, 3, savedPC, 0x66, PC, desc >>> 8, desc & 0x00FF, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x66_RF_word(); checkStack(11, 22, 0x0000);
    }
    [Fact] public void test_R0F_a()
    {
        int spec = mkFieldSpec(1, 9);
        mkShortMem(0x0000, 0x0000, 0b0111111111000000);
        mkStack(11, 22, testShortMem + 2);
        mkCode(1, 2, 3, savedPC, 0x66, PC, spec, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x65_R0F_alpha(); checkStack(11, 22, 0x01FF);
    }
    [Fact] public void test_R0F_b()
    {
        int spec = mkFieldSpec(1, 9);
        mkShortMem(0xFFFF, 0xFFFF, 0b1000000000111111, 0xFFFF, 0xFFFF);
        mkStack(11, 22, testShortMem + 2);
        mkCode(1, 2, 3, savedPC, 0x65, PC, spec, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x65_R0F_alpha(); checkStack(11, 22, 0x0000);
    }
    [Fact] public void test_RLF_a()
    {
        int desc = mkFieldDesc(2, 5, 5);
        mkLongMem(0x0000, 0x0000, 0b0000011111000000);
        mkStack(11, 22, testLongMemLow, testLongMemHigh);
        mkCode(1, 2, 3, savedPC, 0x68, PC, desc >>> 8, desc & 0x00FF, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x68_RLF_word(); checkStack(11, 22, 0x001F);
    }
    [Fact] public void test_RLF_b()
    {
        int desc = mkFieldDesc(2, 5, 5);
        mkLongMem(0xFFFF, 0xFFFF, 0b1111100000111111, 0xFFFF, 0xFFFF);
        mkStack(11, 22, testLongMemLow, testLongMemHigh);
        mkCode(1, 2, 3, savedPC, 0x68, PC, desc >>> 8, desc & 0x00FF, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x68_RLF_word(); checkStack(11, 22, 0x0000);
    }
    [Fact] public void test_RL0F_a()
    {
        int spec = mkFieldSpec(1, 15);
        mkLongMem(0x0000, 0x0000, 0b0111111111111111);
        mkStack(11, 22, testLongMemLow + 2, testLongMemHigh);
        mkCode(1, 2, 3, savedPC, 0x67, PC, spec, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x67_RL0F_alpha(); checkStack(11, 22, 0x7FFF);
    }
    [Fact] public void test_RL0F_b()
    {
        int spec = mkFieldSpec(1, 15);
        mkLongMem(0xFFFF, 0xFFFF, 0b1000000000000000, 0xFFFF, 0xFFFF);
        mkStack(11, 22, testLongMemLow + 2, testLongMemHigh);
        mkCode(1, 2, 3, savedPC, 0x67, PC, spec, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x67_RL0F_alpha(); checkStack(11, 22, 0x0000);
    }
    [Fact] public void test_RLFS_a()
    {
        int desc = mkFieldDesc(2, 0, 15);
        mkLongMem(0x0000, 0x0000, 0b1111111111111110);
        mkStack(11, 22, testLongMemLow, testLongMemHigh, desc);
        mkCode(1, 2, 3, savedPC, 0x67, PC, 0xFFFF, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x69_RLFS(); checkStack(11, 22, 0x7FFF);
    }
    [Fact] public void test_RLFS_b()
    {
        int desc = mkFieldDesc(2, 0, 15);
        mkLongMem(0xFFFF, 0xFFFF, 0b0000000000000001, 0xFFFF, 0xFFFF);
        mkStack(11, 22, testLongMemLow, testLongMemHigh, desc);
        mkCode(1, 2, 3, savedPC, 0x69, PC, 0xFFFF, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x69_RLFS(); checkStack(11, 22, 0x0000);
    }
    [Fact] public void test_RCFS_a()
    {
        int desc = mkFieldDesc(2, 11, 5);
        mkStack(11, 22, 4, desc); // 4 = *word* offset from CB
        mkCode(1, 2, 3, savedPC, 0x69, PC, 0xFF, 0xFF, 0xFF, 0xFF, 0, 0, 0, 0, 0b00000000, 0b00011111, 0, 0);
        Ch07_Assignment_Instructions.ESC_x1A_RCFS(); checkStack(11, 22, 0x001F);
    }
    [Fact] public void test_RCFS_b()
    {
        int desc = mkFieldDesc(2, 11, 5);
        mkStack(11, 22, 4, desc);
        mkCode(1, 2, 3, savedPC, 0x69, PC, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xff, 0b11111111, 0b11100000, 0xFF, 0xFF);
        Ch07_Assignment_Instructions.ESC_x1A_RCFS(); checkStack(11, 22, 0x0000);
    }
    [Fact] public void test_RLIPF_a()
    {
        int spec = mkFieldSpec(14, 1);
        mkShortMem(0x0000, 0x0000, 0x0000, 0b0000000000000010);
        mkLocalFrame(0x3344, 0x5566, 0x7788, 0x99AA, testShortMem);
        mkStack(11, 22);
        mkCode(1, 2, 3, savedPC, 0x6A, PC, 0x43, spec, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x6A_RLIPF_alphabeta(); checkStack(11, 22, 0x0001);
    }
    [Fact] public void test_RLIPF_b()
    {
        int spec = mkFieldSpec(14, 1);
        mkShortMem(0xFFFF, 0xFFFF, 0xFFFF, 0b1111111111111101, 0xFFFF);
        mkLocalFrame(0x3344, 0x5566, 0x7788, 0x99AA, testShortMem);
        mkStack(11, 22);
        mkCode(1, 2, 3, savedPC, 0x6A, PC, 0x43, spec, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x6A_RLIPF_alphabeta(); checkStack(11, 22, 0x0000);
    }
    [Fact] public void test_RLILPF_a()
    {
        int spec = mkFieldSpec(0, 1);
        mkLongMem(0x0000, 0x0000, 0x0000, 0b1000000000000000);
        mkLocalFrame(0x3344, 0x5566, 0x7788, 0x99AA, testLongMemLow, testLongMemHigh);
        mkStack(11, 22);
        mkCode(1, 2, 3, savedPC, 0x6B, PC, 0x43, spec, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x6B_RLILPF_alphabeta(); checkStack(11, 22, 0x0001);
    }
    [Fact] public void test_RLILPF_b()
    {
        int spec = mkFieldSpec(0, 1);
        mkLongMem(0xFFFF, 0xFFFF, 0xFFFF, 0b0111111111111111, 0xFFFF);
        mkLocalFrame(0x3344, 0x5566, 0x7788, 0x99AA, testLongMemLow, testLongMemHigh);
        mkStack(11, 22);
        mkCode(1, 2, 3, savedPC, 0x6B, PC, 0x43, spec, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x6B_RLILPF_alphabeta(); checkStack(11, 22, 0x0000);
    }

    // ---- 7.5.2 Write Field ----

    [Fact] public void test_WF_a()
    {
        int desc = mkFieldDesc(2, 2, 4);
        mkShortMem(0x0000, 0x0000, 0b0000000000000000, 0x0000);
        mkStack(11, 22, 0xFFFF, testShortMem);
        mkCode(1, 2, 3, savedPC, 0x6D, PC, desc >>> 8, desc & 0x00FF, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x6D_WF_word();
        checkStack(11, 22);
        checkShortMem(0x0000, 0x0000, 0b0011110000000000, 0x0000);
    }
    [Fact] public void test_WF_b()
    {
        int desc = mkFieldDesc(2, 2, 4);
        mkShortMem(0xFFFF, 0xFFFF, 0b1111111111111111, 0xFFFF);
        mkStack(11, 22, 0xFFE0, testShortMem);
        mkCode(1, 2, 3, savedPC, 0x6D, PC, desc >>> 8, desc & 0x00FF, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x6D_WF_word();
        checkStack(11, 22);
        checkShortMem(0xFFFF, 0xFFFF, 0b1100001111111111, 0xFFFF);
    }
    [Fact] public void test_W0F_a()
    {
        int spec = mkFieldSpec(1, 9);
        mkShortMem(0x0000, 0x0000, 0b0000000000000000, 0x0000);
        mkStack(11, 22, 0xFFFF, testShortMem + 2);
        mkCode(1, 2, 3, savedPC, 0x6C, PC, spec, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x6C_W0F_alpha();
        checkStack(11, 22);
        checkShortMem(0x0000, 0x0000, 0b0111111111000000, 0x0000);
    }
    [Fact] public void test_W0F_b()
    {
        int spec = mkFieldSpec(1, 9);
        mkShortMem(0xFFFF, 0xFFFF, 0b1111111111111111, 0xFFFF);
        mkStack(11, 22, 0xFC00, testShortMem + 2);
        mkCode(1, 2, 3, savedPC, 0x6C, PC, spec, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x6C_W0F_alpha();
        checkStack(11, 22);
        checkShortMem(0xFFFF, 0xFFFF, 0b1000000000111111, 0xFFFF);
    }
    [Fact] public void test_WLF_a()
    {
        int desc = mkFieldDesc(2, 13, 3);
        mkLongMem(0x0000, 0x0000, 0b0000000000000000, 0x0000);
        mkStack(11, 22, 0xFFFF, testLongMemLow, testLongMemHigh);
        mkCode(1, 2, 3, savedPC, 0x72, PC, desc >>> 8, desc & 0x00FF, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x72_WLF_word();
        checkStack(11, 22);
        checkLongMem(0x0000, 0x0000, 0b0000000000000111, 0x0000);
    }
    [Fact] public void test_WLF_b()
    {
        int desc = mkFieldDesc(2, 13, 3);
        mkLongMem(0xFFFF, 0xFFFF, 0b1111111111111111, 0xFFFF);
        mkStack(11, 22, 0xFFF0, testLongMemLow, testLongMemHigh);
        mkCode(1, 2, 3, savedPC, 0x72, PC, desc >>> 8, desc & 0x00FF, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x72_WLF_word();
        checkStack(11, 22);
        checkLongMem(0xFFFF, 0xFFFF, 0b1111111111111000, 0xFFFF);
    }
    [Fact] public void test_WL0F_a()
    {
        int spec = mkFieldSpec(0, 2);
        mkLongMem(0x0000, 0x0000, 0b0000000000000000, 0x0000);
        mkStack(11, 22, 0xFFFF, testLongMemLow + 2, testLongMemHigh);
        mkCode(1, 2, 3, savedPC, 0x71, PC, spec, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x71_WL0F_alpha();
        checkStack(11, 22);
        checkLongMem(0x0000, 0x0000, 0b1100000000000000, 0x0000);
    }
    [Fact] public void test_WL0F_b()
    {
        int spec = mkFieldSpec(0, 2);
        mkLongMem(0xFFFF, 0xFFFF, 0b1111111111111111, 0xFFFF);
        mkStack(11, 22, 0xFFF8, testLongMemLow + 2, testLongMemHigh);
        mkCode(1, 2, 3, savedPC, 0x71, PC, spec, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x71_WL0F_alpha();
        checkStack(11, 22);
        checkLongMem(0xFFFF, 0xFFFF, 0b0011111111111111, 0xFFFF);
    }
    [Fact] public void test_WLFS_a()
    {
        int desc = mkFieldDesc(2, 13, 3);
        mkLongMem(0x0000, 0x0000, 0b0000000000000000, 0x0000);
        mkStack(11, 22, 0xFFFF, testLongMemLow, testLongMemHigh, desc);
        mkCode(1, 2, 3, savedPC, 0x74, PC, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x74_WLFS();
        checkStack(11, 22);
        checkLongMem(0x0000, 0x0000, 0b0000000000000111, 0x0000);
    }
    [Fact] public void test_WLFS_b()
    {
        int desc = mkFieldDesc(2, 13, 3);
        mkLongMem(0xFFFF, 0xFFFF, 0b1111111111111111, 0xFFFF);
        mkStack(11, 22, 0xFFF0, testLongMemLow, testLongMemHigh, desc);
        mkCode(1, 2, 3, savedPC, 0x74, PC, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x74_WLFS();
        checkStack(11, 22);
        checkLongMem(0xFFFF, 0xFFFF, 0b1111111111111000, 0xFFFF);
    }
    [Fact] public void test_WS0F_a()
    {
        int spec = mkFieldSpec(4, 9);
        mkShortMem(0x0000, 0x0000, 0b0000000000000000, 0x0000);
        mkStack(11, 22, testShortMem + 2, 0xFFFF);
        mkCode(1, 2, 3, savedPC, 0x70, PC, spec, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x70_WS0F_alpha();
        checkStack(11, 22);
        checkShortMem(0x0000, 0x0000, 0b0000111111111000, 0x0000);
    }
    [Fact] public void test_WS0F_b()
    {
        int spec = mkFieldSpec(4, 9);
        mkShortMem(0xFFFF, 0xFFFF, 0b1111111111111111, 0xFFFF);
        mkStack(11, 22, testShortMem + 2, 0xFC00);
        mkCode(1, 2, 3, savedPC, 0x70, PC, spec, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x70_WS0F_alpha();
        checkStack(11, 22);
        checkShortMem(0xFFFF, 0xFFFF, 0b1111000000000111, 0xFFFF);
    }

    // ---- 7.5.3 Put Swapped Field ----

    [Fact] public void test_PS0F_a()
    {
        int spec = mkFieldSpec(4, 9);
        mkShortMem(0x0000, 0x0000, 0b0000000000000000, 0x0000);
        mkStack(11, 22, testShortMem + 2, 0xFFFF);
        mkCode(1, 2, 3, savedPC, 0x6F, PC, spec, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x6F_PS0F();
        checkStack(11, 22, testShortMem + 2);
        checkShortMem(0x0000, 0x0000, 0b0000111111111000, 0x0000);
    }
    [Fact] public void test_PS0F_b()
    {
        int spec = mkFieldSpec(4, 9);
        mkShortMem(0xFFFF, 0xFFFF, 0b1111111111111111, 0xFFFF);
        mkStack(11, 22, testShortMem + 2, 0xFC00);
        mkCode(1, 2, 3, savedPC, 0x6F, PC, spec, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x6F_PS0F();
        checkStack(11, 22, testShortMem + 2);
        checkShortMem(0xFFFF, 0xFFFF, 0b1111000000000111, 0xFFFF);
    }
    [Fact] public void test_PSF_a()
    {
        int desc = mkFieldDesc(2, 2, 4);
        mkShortMem(0x0000, 0x0000, 0b0000000000000000, 0x0000);
        mkStack(11, 22, testShortMem, 0xFFFF);
        mkCode(1, 2, 3, savedPC, 0x6E, PC, desc >>> 8, desc & 0x00FF, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x6E_PSF_word();
        checkStack(11, 22, testShortMem);
        checkShortMem(0x0000, 0x0000, 0b0011110000000000, 0x0000);
    }
    [Fact] public void test_PSF_b()
    {
        int desc = mkFieldDesc(2, 2, 4);
        mkShortMem(0xFFFF, 0xFFFF, 0b1111111111111111, 0xFFFF);
        mkStack(11, 22, testShortMem, 0xFFE0);
        mkCode(1, 2, 3, savedPC, 0x6E, PC, desc >>> 8, desc & 0x00FF, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x6E_PSF_word();
        checkStack(11, 22, testShortMem);
        checkShortMem(0xFFFF, 0xFFFF, 0b1100001111111111, 0xFFFF);
    }
    [Fact] public void test_PSLF_a()
    {
        int desc = mkFieldDesc(2, 12, 3);
        mkLongMem(0x0000, 0x0000, 0b0000000000000000, 0x0000);
        mkStack(11, 22, testLongMemLow, testLongMemHigh, 0xFFFF);
        mkCode(1, 2, 3, savedPC, 0x73, PC, desc >>> 8, desc & 0x00FF, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x73_PSLF_word();
        checkStack(11, 22, testLongMemLow, testLongMemHigh);
        checkLongMem(0x0000, 0x0000, 0b0000000000001110, 0x0000);
    }
    [Fact] public void test_WPSLF_b()
    {
        int desc = mkFieldDesc(2, 12, 3);
        mkLongMem(0xFFFF, 0xFFFF, 0b1111111111111111, 0xFFFF);
        mkStack(11, 22, testLongMemLow, testLongMemHigh, 0xFFF0);
        mkCode(1, 2, 3, savedPC, 0x73, PC, desc >>> 8, desc & 0x00FF, 5, 6, 7, 8);
        Ch07_Assignment_Instructions.OPC_x73_PSLF_word();
        checkStack(11, 22, testLongMemLow, testLongMemHigh);
        checkLongMem(0xFFFF, 0xFFFF, 0b1111111111110001, 0xFFFF);
    }
}
