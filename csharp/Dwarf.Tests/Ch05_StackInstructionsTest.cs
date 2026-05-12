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

using Dwarf.Engine.Opcodes;

namespace Dwarf.Tests;

// Unittests for instructions implemented in class Ch05_Stack_Instructions.
public sealed class Ch05_StackInstructionsTest : AbstractInstructionTest
{
    // ==================================================================
    // 5.1 Stack Primitives
    // ==================================================================

    // REC
    [Fact] public void test_REC()
    {
        mkStack(1, 2, 3, SP, 4, 5);
        Ch05_Stack_Instructions.OPC_xA2_REC();
        checkStack(1, 2, 3, 4, SP, 5);
    }
    [Fact] public void test_REC_StackFull()
    {
        mkStack(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14);
        mesaException.expect_signalStackError = true;
        Assert.Throws<MesaTrapOrFault>(() => Ch05_Stack_Instructions.OPC_xA2_REC());
    }

    // REC2
    [Fact] public void test_REC2()
    {
        mkStack(1, 2, 3, SP, 4, 5, 6);
        Ch05_Stack_Instructions.OPC_xA3_REC2();
        checkStack(1, 2, 3, 4, 5, SP, 6);
    }
    [Fact] public void test_REC2_StackFull1()
    {
        mkStack(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14);
        mesaException.expect_signalStackError = true;
        Assert.Throws<MesaTrapOrFault>(() => Ch05_Stack_Instructions.OPC_xA3_REC2());
    }
    [Fact] public void test_REC2_StackFull2()
    {
        mkStack(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, SP, 14);
        mesaException.expect_signalStackError = true;
        Assert.Throws<MesaTrapOrFault>(() => Ch05_Stack_Instructions.OPC_xA3_REC2());
    }

    // DIS
    [Fact] public void test_DIS()
    {
        mkStack(1, 2, 3, SP, 4);
        Ch05_Stack_Instructions.OPC_xA4_DIS();
        checkStack(1, 2, SP, 3, 4);
    }
    [Fact] public void test_DIS_StackEmpty()
    {
        mkStack();
        mesaException.expect_signalStackError = true;
        Assert.Throws<MesaTrapOrFault>(() => Ch05_Stack_Instructions.OPC_xA4_DIS());
    }

    // DIS2
    [Fact] public void test_DIS2()
    {
        mkStack(1, 2, 3, SP, 4);
        Ch05_Stack_Instructions.OPC_xA5_DIS2();
        checkStack(1, SP, 2, 3, 4);
    }
    [Fact] public void test_DIS2_StackEmpty1()
    {
        mkStack();
        mesaException.expect_signalStackError = true;
        Assert.Throws<MesaTrapOrFault>(() => Ch05_Stack_Instructions.OPC_xA5_DIS2());
    }
    [Fact] public void test_DIS2_StackEmpty2()
    {
        mkStack(1);
        mesaException.expect_signalStackError = true;
        Assert.Throws<MesaTrapOrFault>(() => Ch05_Stack_Instructions.OPC_xA5_DIS2());
    }

    // EXCH
    [Fact] public void test_EXCH_1()
    {
        mkStack(1, 2, 3, 4);
        Ch05_Stack_Instructions.OPC_xA6_EXCH();
        checkStack(1, 2, 4, 3);
    }
    [Fact] public void test_EXCH_2()
    {
        mkStack(1, 2);
        Ch05_Stack_Instructions.OPC_xA6_EXCH();
        checkStack(2, 1);
    }
    [Fact] public void test_EXCH_StackEmpty1()
    {
        mkStack();
        mesaException.expect_signalStackError = true;
        Assert.Throws<MesaTrapOrFault>(() => Ch05_Stack_Instructions.OPC_xA6_EXCH());
    }
    [Fact] public void test_EXCH_StackEmpty2()
    {
        mkStack(1);
        mesaException.expect_signalStackError = true;
        Assert.Throws<MesaTrapOrFault>(() => Ch05_Stack_Instructions.OPC_xA6_EXCH());
    }

    // DEXCH
    [Fact] public void test_DEXCH()
    {
        mkStack(1, 2, 3, 4);
        Ch05_Stack_Instructions.OPC_xA7_DEXCH();
        checkStack(3, 4, 1, 2);
    }
    [Fact] public void test_DEXCH_StackEmpty()
    {
        mkStack(1, 2, 3);
        mesaException.expect_signalStackError = true;
        Assert.Throws<MesaTrapOrFault>(() => Ch05_Stack_Instructions.OPC_xA7_DEXCH());
    }

    // DUP
    [Fact] public void test_DUP_1()
    {
        mkStack(1, 2, 3);
        Ch05_Stack_Instructions.OPC_xA8_DUP();
        mkStack(1, 2, 3, 3); // upstream uses mkStack here (preserved verbatim)
    }
    [Fact] public void test_DUP_2()
    {
        mkStack(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13);
        Ch05_Stack_Instructions.OPC_xA8_DUP();
        mkStack(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 13);
    }
    [Fact] public void test_DUP_StackFull()
    {
        mkStack(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14);
        mesaException.expect_signalStackError = true;
        Assert.Throws<MesaTrapOrFault>(() => Ch05_Stack_Instructions.OPC_xA8_DUP());
    }

    // DDUP
    [Fact] public void test_DDUP()
    {
        mkStack(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12);
        Ch05_Stack_Instructions.OPC_xA9_DDUP();
        mkStack(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 11, 12);
    }
    [Fact] public void test_DDUP_StackFull()
    {
        mkStack(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13);
        mesaException.expect_signalStackError = true;
        Assert.Throws<MesaTrapOrFault>(() => Ch05_Stack_Instructions.OPC_xA9_DDUP());
    }

    // EXDIS
    [Fact] public void test_EXDIS()
    {
        mkStack(1, 2, 3);
        Ch05_Stack_Instructions.OPC_xAA_EXDIS();
        checkStack(1, 3, SP, 3);
    }

    // ==================================================================
    // 5.2 Check Instructions
    // ==================================================================

    // BNDCK
    [Fact] public void test_BNDCK_Ok()
    {
        mkStack(1, 2, 3); // 2 < 3
        Ch05_Stack_Instructions.OPC_x3C_BNDCK();
        checkStack(1, 2);
    }
    [Fact] public void test_BNDCK_Fail1()
    {
        mkStack(1, 2, 2); // 2 !< 2
        mesaException.expect_signalBoundsTrap = true;
        mesaException.beforeCheck = () => checkStack(1, 2);
        Assert.Throws<MesaTrapOrFault>(() => Ch05_Stack_Instructions.OPC_x3C_BNDCK());
    }
    [Fact] public void test_BNDCK_Fail2()
    {
        mkStack(1, 5, 4); // 5 !< 4
        mesaException.expect_signalBoundsTrap = true;
        mesaException.beforeCheck = () => checkStack(1, 5);
        Assert.Throws<MesaTrapOrFault>(() => Ch05_Stack_Instructions.OPC_x3C_BNDCK());
    }
    [Fact] public void test_BNDCK_StackEmpty()
    {
        mkStack(4); // missing index
        mesaException.expect_signalStackError = true;
        Assert.Throws<MesaTrapOrFault>(() => Ch05_Stack_Instructions.OPC_x3C_BNDCK());
    }

    // BNDCKL
    [Fact] public void test_BNDCKL_Ok1()
    {
        mkStack(1, 2, 3, 1, 4); // index 0x00030002 ; range 0x00040001
        Ch05_Stack_Instructions.ESC_x24_BNDCKL();
        checkStack(1, 2, 3);
    }
    [Fact] public void test_BNDCKL_Ok2()
    {
        mkStack(1, 1, 3, 2, 3); // index 0x00030001 ; range 0x00030002
        Ch05_Stack_Instructions.ESC_x24_BNDCKL();
        checkStack(1, 1, 3);
    }
    [Fact] public void test_BNDCKL_Fail()
    {
        mkStack(1, 2, 3, 2, 3); // index 0x00030002 ; range 0x00040002
        mesaException.expect_signalBoundsTrap = true;
        mesaException.beforeCheck = () => checkStack(1, 2, 3);
        Assert.Throws<MesaTrapOrFault>(() => Ch05_Stack_Instructions.ESC_x24_BNDCKL());
    }

    // NILCK
    [Fact] public void test_NILCK_Ok()
    {
        mkStack(1, 2);
        Ch05_Stack_Instructions.ESC_x25_NILCK();
        checkStack(1, 2);
    }
    [Fact] public void test_NILCK_Fail()
    {
        mkStack(1, 0);
        mesaException.expect_signalPointerTrap = true;
        mesaException.beforeCheck = () => checkStack(1, 0);
        Assert.Throws<MesaTrapOrFault>(() => Ch05_Stack_Instructions.ESC_x25_NILCK());
    }

    // NILCKL
    [Fact] public void test_NILCKL_Ok()
    {
        mkStack(1, 2, 0);
        Ch05_Stack_Instructions.ESC_x26_NILCKL();
        checkStack(1, 2, 0);
    }
    [Fact] public void test_NILCKL_Fail()
    {
        mkStack(1, 0, 0);
        mesaException.expect_signalPointerTrap = true;
        mesaException.beforeCheck = () => checkStack(1, 0, 0);
        Assert.Throws<MesaTrapOrFault>(() => Ch05_Stack_Instructions.ESC_x26_NILCKL());
    }

    // ==================================================================
    // 5.3 Unary Instructions
    // ==================================================================

    // NEG
    [Fact] public void test_NEG_1() { mkStack(32767);  Ch05_Stack_Instructions.OPC_xAB_NEG(); checkStack(-32767); }
    [Fact] public void test_NEG_2() { mkStack(-32767); Ch05_Stack_Instructions.OPC_xAB_NEG(); checkStack(32767); }
    [Fact] public void test_NEG_3() { mkStack(-32768); Ch05_Stack_Instructions.OPC_xAB_NEG(); checkStack(-32768); }

    // INC
    [Fact] public void test_INC_1() { mkStack(1, 2);     Ch05_Stack_Instructions.OPC_xAC_INC(); checkStack(1, 3); }
    [Fact] public void test_INC_2() { mkStack(1, 32767); Ch05_Stack_Instructions.OPC_xAC_INC(); checkStack(1, -32768); }

    // DINC
    [Fact] public void test_DINC_1() { mkStack(1, 2, 3);           Ch05_Stack_Instructions.OPC_xAE_DINC(); checkStack(1, 3, 3); }
    [Fact] public void test_DINC_2() { mkStack(1, 0xFFFF, 3);      Ch05_Stack_Instructions.OPC_xAE_DINC(); checkStack(1, 0, 4); }
    [Fact] public void test_DINC_3() { mkStack(1, 0xFFFF, 0xFFFF); Ch05_Stack_Instructions.OPC_xAE_DINC(); checkStack(1, 0, 0); }

    // DEC
    [Fact] public void test_DEC_1() { mkStack(1, 1);      Ch05_Stack_Instructions.OPC_xAD_DEC(); checkStack(1, 0); }
    [Fact] public void test_DEC_2() { mkStack(1, 0);      Ch05_Stack_Instructions.OPC_xAD_DEC(); checkStack(1, 0xFFFF); }
    [Fact] public void test_DEC_3() { mkStack(1, -32768); Ch05_Stack_Instructions.OPC_xAD_DEC(); checkStack(1, 32767); }

    // ADDSB
    [Fact] public void test_ADDSB_1a()
    {
        mkCode(33, 44, PC, 0, 55, 66);
        mkStack(1, 88);
        Ch05_Stack_Instructions.OPC_xB4_ADDSB_alpha();
        checkStack(1, 88);
    }
    [Fact] public void test_ADDSB_1b()
    {
        mkCode(22, 33, 44, PC, 0, 55, 66);
        mkStack(1, 88);
        Ch05_Stack_Instructions.OPC_xB4_ADDSB_alpha();
        checkStack(1, 88);
    }
    [Fact] public void test_ADDSB_2a()
    {
        mkCode(33, 44, PC, 2, 55, 66);
        mkStack(1, 88);
        Ch05_Stack_Instructions.OPC_xB4_ADDSB_alpha();
        checkStack(1, 90);
    }
    [Fact] public void test_ADDSB_2b()
    {
        mkCode(22, 33, 44, PC, 2, 55, 66);
        mkStack(1, 88);
        Ch05_Stack_Instructions.OPC_xB4_ADDSB_alpha();
        checkStack(1, 90);
    }
    [Fact] public void test_ADDSB_3a()
    {
        mkCode(33, 44, PC, 254, 55, 66);
        mkStack(1, 88);
        Ch05_Stack_Instructions.OPC_xB4_ADDSB_alpha();
        checkStack(1, 86);
    }
    [Fact] public void test_ADDSB_3b()
    {
        mkCode(22, 33, 44, PC, 254, 55, 66); // -2
        mkStack(1, 88);
        Ch05_Stack_Instructions.OPC_xB4_ADDSB_alpha();
        checkStack(1, 86);
    }
    [Fact] public void test_ADDSB_4()
    {
        mkCode(22, 33, 44, PC, 0x23, 55, 66);
        mkStack(1, 0x0E00);
        Ch05_Stack_Instructions.OPC_xB4_ADDSB_alpha();
        checkStack(1, 0x0E23);
    }

    // DBL
    [Fact] public void test_DBL_1() { mkStack(1);     Ch05_Stack_Instructions.OPC_xAF_DBL(); checkStack(2); }
    [Fact] public void test_DBL_2() { mkStack(0);     Ch05_Stack_Instructions.OPC_xAF_DBL(); checkStack(0); }
    [Fact] public void test_DBL_3() { mkStack(40000); Ch05_Stack_Instructions.OPC_xAF_DBL(); checkStack(80000 % 65536); }

    // DDBL
    [Fact] public void test_DDBL_1() { mkStack(1, 0, 0);           Ch05_Stack_Instructions.OPC_xB0_DDBL(); checkStack(1, 0, 0); }
    [Fact] public void test_DDBL_2() { mkStack(1, 1, 1);           Ch05_Stack_Instructions.OPC_xB0_DDBL(); checkStack(1, 2, 2); }
    [Fact] public void test_DDBL_3() { mkStack(1, 1, 2);           Ch05_Stack_Instructions.OPC_xB0_DDBL(); checkStack(1, 2, 4); }
    [Fact] public void test_DDBL_4() { mkStack(1, 0x0001, 0x8000); Ch05_Stack_Instructions.OPC_xB0_DDBL(); checkStack(1, 0x0002, 0x0000); }

    // TRPL
    [Fact] public void test_TRPL_1() { mkStack(1);     Ch05_Stack_Instructions.OPC_xB1_TRPL(); checkStack(3); }
    [Fact] public void test_TRPL_2() { mkStack(0);     Ch05_Stack_Instructions.OPC_xB1_TRPL(); checkStack(0); }
    [Fact] public void test_TRPL_3() { mkStack(40000); Ch05_Stack_Instructions.OPC_xB1_TRPL(); checkStack(120000 % 65536); }

    // LINT
    [Fact] public void test_LINT_1() { mkStack(22, 33);    Ch05_Stack_Instructions.ESC_x18_LINT(); checkStack(22, 33, 0); }
    [Fact] public void test_LINT_2() { mkStack(22, 0);     Ch05_Stack_Instructions.ESC_x18_LINT(); checkStack(22, 0, 0); }
    [Fact] public void test_LINT_3() { mkStack(22, 32768); Ch05_Stack_Instructions.ESC_x18_LINT(); checkStack(22, 32768, 65535); }

    // SHIFTSB
    [Fact] public void test_SHIFTSB_1()
    {
        mkCode(22, 33, 44, PC, 2, 55, 66);
        mkStack(22, 0x04);
        Ch05_Stack_Instructions.OPC_x7C_SHIFTSB_alpha();
        checkStack(22, 0x10);
    }
    [Fact] public void test_SHIFTSB_2()
    {
        mkCode(22, 33, 44, PC, -2, 55, 66);
        mkStack(22, 0x04);
        Ch05_Stack_Instructions.OPC_x7C_SHIFTSB_alpha();
        checkStack(22, 0x1);
    }
    [Fact] public void test_SHIFTSB_3()
    {
        mkCode(22, 33, 44, PC, 0, 55, 66);
        mkStack(22, 0x04);
        Ch05_Stack_Instructions.OPC_x7C_SHIFTSB_alpha();
        checkStack(22, 0x4);
    }
    [Fact] public void test_SHIFTSB_4()
    {
        mkCode(22, 33, 44, PC, 16, 55, 66);
        mkStack(22, 0x04);
        mesaException.expect_ERROR = true;
        Assert.Throws<MesaTrapOrFault>(() => Ch05_Stack_Instructions.OPC_x7C_SHIFTSB_alpha());
    }
    [Fact] public void test_SHIFTSB_5()
    {
        mkCode(22, 33, 44, PC, -16, 55, 66);
        mkStack(22, 0x04);
        mesaException.expect_ERROR = true;
        Assert.Throws<MesaTrapOrFault>(() => Ch05_Stack_Instructions.OPC_x7C_SHIFTSB_alpha());
    }

    // ==================================================================
    // 5.4 Logical Instructions
    // ==================================================================

    // AND
    [Fact] public void test_AND_1() { mkStack(33, 0xFF0F, 0xF0FF); Ch05_Stack_Instructions.OPC_xB2_AND(); checkStack(33, 0xF00F); }
    [Fact] public void test_AND_2() { mkStack(33, 0x0F0F, 0xF0F0); Ch05_Stack_Instructions.OPC_xB2_AND(); checkStack(33, 0x0000); }

    // DAND
    [Fact] public void test_DAND_1() { mkStack(33, 0xFF0F, 0xFF0F, 0xF0FF, 0x0FF0); Ch05_Stack_Instructions.ESC_x13_DAND(); checkStack(33, 0xF00F, 0x0F00); }
    [Fact] public void test_DAND_2() { mkStack(33, 0xFF00, 0x00FF, 0x00FF, 0xFF00); Ch05_Stack_Instructions.ESC_x13_DAND(); checkStack(33, 0x0000, 0x0000); }

    // IOR
    [Fact] public void test_IOR_1() { mkStack(33, 0xFF0F, 0xF0FF); Ch05_Stack_Instructions.OPC_xB3_IOR(); checkStack(33, 0xFFFF); }
    [Fact] public void test_IOR_2() { mkStack(33, 0x0F0F, 0xF0F0); Ch05_Stack_Instructions.OPC_xB3_IOR(); checkStack(33, 0xFFFF); }

    // DIOR
    [Fact] public void test_DIOR_1() { mkStack(33, 0xFF0F, 0xFF0F, 0xF0FF, 0x0FF0); Ch05_Stack_Instructions.ESC_x14_DIOR(); checkStack(33, 0xFFFF, 0xFFFF); }
    [Fact] public void test_DIOR_2() { mkStack(33, 0xFF00, 0x00FF, 0x0000, 0x00FF); Ch05_Stack_Instructions.ESC_x14_DIOR(); checkStack(33, 0xFF00, 0x00FF); }

    // XOR
    [Fact] public void test_XOR_1() { mkStack(33, 0x0F0F, 0x00FF); Ch05_Stack_Instructions.ESC_x12_XOR(); checkStack(33, 0x0FF0); }
    [Fact] public void test_XOR_2() { mkStack(33, 0x0F0F, 0xF0F0); Ch05_Stack_Instructions.ESC_x12_XOR(); checkStack(33, 0xFFFF); }

    // DXOR
    [Fact] public void test_DXOR_1() { mkStack(33, 0xFF0F, 0xFF00, 0xF0FF, 0x0FF0); Ch05_Stack_Instructions.ESC_x15_DXOR(); checkStack(33, 0x0FF0, 0xF0F0); }
    [Fact] public void test_DXOR_2() { mkStack(33, 0xFF00, 0x00FF, 0x0000, 0xFFFF); Ch05_Stack_Instructions.ESC_x15_DXOR(); checkStack(33, 0xFF00, 0xFF00); }

    // SHIFT
    [Fact] public void test_SHIFT_1() { mkStack(33, 0x00F0, 4);   Ch05_Stack_Instructions.OPC_x7B_SHIFT(); checkStack(33, 0x0F00); }
    [Fact] public void test_SHIFT_2() { mkStack(33, 0x00F0, -4);  Ch05_Stack_Instructions.OPC_x7B_SHIFT(); checkStack(33, 0x000F); }
    [Fact] public void test_SHIFT_3() { mkStack(33, 0x00F0, 0);   Ch05_Stack_Instructions.OPC_x7B_SHIFT(); checkStack(33, 0x00F0); }
    [Fact] public void test_SHIFT_4() { mkStack(33, 0x00F0, 11);  Ch05_Stack_Instructions.OPC_x7B_SHIFT(); checkStack(33, 0x8000); }
    [Fact] public void test_SHIFT_5() { mkStack(33, 0x00F0, 12);  Ch05_Stack_Instructions.OPC_x7B_SHIFT(); checkStack(33, 0x0000); }
    [Fact] public void test_SHIFT_6() { mkStack(33, 0x00F0, 100); Ch05_Stack_Instructions.OPC_x7B_SHIFT(); checkStack(33, 0x0000); }
    [Fact] public void test_SHIFT_7() { mkStack(33, 0x00F0, -5);  Ch05_Stack_Instructions.OPC_x7B_SHIFT(); checkStack(33, 0x0007); }
    [Fact] public void test_SHIFT_8() { mkStack(33, 0x00F0, -8);  Ch05_Stack_Instructions.OPC_x7B_SHIFT(); checkStack(33, 0x0000); }

    // DSHIFT
    [Fact] public void test_DSHIFT_1()  { mkStack(33, 0x00F0, 0x0000, 4);   Ch05_Stack_Instructions.ESC_x17_DSHIFT(); checkStack(33, 0x0F00, 0x0000); }
    [Fact] public void test_DSHIFT_2()  { mkStack(33, 0x00F0, 0x0000, 12);  Ch05_Stack_Instructions.ESC_x17_DSHIFT(); checkStack(33, 0x0000, 0x000F); }
    [Fact] public void test_DSHIFT_3()  { mkStack(33, 0x00F0, 0x0000, 20);  Ch05_Stack_Instructions.ESC_x17_DSHIFT(); checkStack(33, 0x0000, 0x0F00); }
    [Fact] public void test_DSHIFT_4()  { mkStack(33, 0x00F0, 0x0000, 24);  Ch05_Stack_Instructions.ESC_x17_DSHIFT(); checkStack(33, 0x0000, 0xF000); }
    [Fact] public void test_DSHIFT_5()  { mkStack(33, 0x00F0, 0x0000, 25);  Ch05_Stack_Instructions.ESC_x17_DSHIFT(); checkStack(33, 0x0000, 0xE000); }
    [Fact] public void test_DSHIFT_6()  { mkStack(33, 0x00F0, 0x0000, 26);  Ch05_Stack_Instructions.ESC_x17_DSHIFT(); checkStack(33, 0x0000, 0xC000); }
    [Fact] public void test_DSHIFT_7()  { mkStack(33, 0x00F0, 0x0000, 27);  Ch05_Stack_Instructions.ESC_x17_DSHIFT(); checkStack(33, 0x0000, 0x8000); }
    [Fact] public void test_DSHIFT_8()  { mkStack(33, 0x00F0, 0x0000, 28);  Ch05_Stack_Instructions.ESC_x17_DSHIFT(); checkStack(33, 0x0000, 0x0000); }
    [Fact] public void test_DSHIFT_9()  { mkStack(33, 0x00F0, 0x0000, 9);   Ch05_Stack_Instructions.ESC_x17_DSHIFT(); checkStack(33, 0xE000, 0x0001); }
    [Fact] public void test_DSHIFT_10() { mkStack(33, 0x00F0, 0xF000, -5);  Ch05_Stack_Instructions.ESC_x17_DSHIFT(); checkStack(33, 0x0007, 0x0780); }
    [Fact] public void test_DSHIFT_11() { mkStack(33, 0x00F0, 0xF000, -8);  Ch05_Stack_Instructions.ESC_x17_DSHIFT(); checkStack(33, 0x0000, 0x00F0); }

    // ROTATE
    [Fact] public void test_ROTATE_1() { mkStack(33, 0x00F0, 9);   Ch05_Stack_Instructions.ESC_x16_ROTATE(); checkStack(33, 0xE001); }
    [Fact] public void test_ROTATE_2() { mkStack(33, 0x00F0, 25);  Ch05_Stack_Instructions.ESC_x16_ROTATE(); checkStack(33, 0xE001); }
    [Fact] public void test_ROTATE_3() { mkStack(33, 0x00F0, -5);  Ch05_Stack_Instructions.ESC_x16_ROTATE(); checkStack(33, 0x8007); }
    [Fact] public void test_ROTATE_4() { mkStack(33, 0x00F0, -21); Ch05_Stack_Instructions.ESC_x16_ROTATE(); checkStack(33, 0x8007); }
    [Fact] public void test_ROTATE_5() { mkStack(33, 0x00F0, 0);   Ch05_Stack_Instructions.ESC_x16_ROTATE(); checkStack(33, 0x00F0); }
    [Fact] public void test_ROTATE_6() { mkStack(33, 0x00F0, -4);  Ch05_Stack_Instructions.ESC_x16_ROTATE(); checkStack(33, 0x000F); }
    [Fact] public void test_ROTATE_7() { mkStack(33, 0x00F0, 4);   Ch05_Stack_Instructions.ESC_x16_ROTATE(); checkStack(33, 0x0F00); }

    // ==================================================================
    // 5.5 Arithmetic Instructions
    // ==================================================================

    // ADD
    [Fact] public void test_ADD_1() { mkStack(33, 22, 33);         Ch05_Stack_Instructions.OPC_xB5_ADD(); checkStack(33, 55); }
    [Fact] public void test_ADD_2() { mkStack(33, 33, 22);         Ch05_Stack_Instructions.OPC_xB5_ADD(); checkStack(33, 55); }
    [Fact] public void test_ADD_3() { mkStack(33, 22, 0);          Ch05_Stack_Instructions.OPC_xB5_ADD(); checkStack(33, 22); }
    [Fact] public void test_ADD_4() { mkStack(33, 0xFFF0, 0x000F); Ch05_Stack_Instructions.OPC_xB5_ADD(); checkStack(33, 0xFFFF); }
    [Fact] public void test_ADD_5() { mkStack(33, 0xFFF0, 0x0010); Ch05_Stack_Instructions.OPC_xB5_ADD(); checkStack(33, 0x0000); }

    // SUB
    [Fact] public void test_SUB_1() { mkStack(33, 33, 22);         Ch05_Stack_Instructions.OPC_xB6_SUB(); checkStack(33, 11); }
    [Fact] public void test_SUB_2() { mkStack(33, 0xFFFF, 0x000F); Ch05_Stack_Instructions.OPC_xB6_SUB(); checkStack(33, 0xFFF0); }
    [Fact] public void test_SUB_3() { mkStack(33, 0, 1);           Ch05_Stack_Instructions.OPC_xB6_SUB(); checkStack(33, 0xFFFF); }
    [Fact] public void test_SUB_4() { mkStack(33, 1, 0);           Ch05_Stack_Instructions.OPC_xB6_SUB(); checkStack(33, 1); }
    [Fact] public void test_SUB_5() { mkStack(33, 10, 10);         Ch05_Stack_Instructions.OPC_xB6_SUB(); checkStack(33, 0); }

    // DADD
    [Fact] public void test_DADD_1() { mkStack(33, 22, 0, 33, 0);             Ch05_Stack_Instructions.OPC_xB7_DADD(); checkStack(33, 55, 0); }
    [Fact] public void test_DADD_2() { mkStack(33, 0xFFFF, 0, 1, 0);          Ch05_Stack_Instructions.OPC_xB7_DADD(); checkStack(33, 0, 1); }
    [Fact] public void test_DADD_3() { mkStack(33, 0xFFFF, 0xFFFF, 1, 0);     Ch05_Stack_Instructions.OPC_xB7_DADD(); checkStack(33, 0, 0); }
    [Fact] public void test_DADD_4() { mkStack(33, 0xFFFF, 0xFFFF, 0, 1);     Ch05_Stack_Instructions.OPC_xB7_DADD(); checkStack(33, 0xFFFF, 0); }
    [Fact] public void test_DADD_5() { mkStack(33, 22, 33, 44, 66);           Ch05_Stack_Instructions.OPC_xB7_DADD(); checkStack(33, 66, 99); }
    [Fact] public void test_DADD_6() { mkStack(33, 22, 33, 0, 0);             Ch05_Stack_Instructions.OPC_xB7_DADD(); checkStack(33, 22, 33); }

    // DSUB
    [Fact] public void test_DSUB_1() { mkStack(33, 33, 0, 22, 0);   Ch05_Stack_Instructions.OPC_xB8_DSUB(); checkStack(33, 11, 0); }
    [Fact] public void test_DSUB_2() { mkStack(33, 22, 0, 33, 0);   Ch05_Stack_Instructions.OPC_xB8_DSUB(); checkStack(33, -11, 0xFFFF); }
    [Fact] public void test_DSUB_3() { mkStack(33, 55, 66, 33, 55); Ch05_Stack_Instructions.OPC_xB8_DSUB(); checkStack(33, 22, 11); }
    [Fact] public void test_DSUB_4() { mkStack(33, 0, 0, 1, 0);     Ch05_Stack_Instructions.OPC_xB8_DSUB(); checkStack(33, 0xFFFF, 0xFFFF); }
    [Fact] public void test_DSUB_5() { mkStack(33, 0, 0, 0, 1);     Ch05_Stack_Instructions.OPC_xB8_DSUB(); checkStack(33, 0x0000, 0xFFFF); }

    // ADC
    [Fact] public void test_ADC_1() { mkStack(33, 1, 2, 3);           Ch05_Stack_Instructions.OPC_xB9_ADC(); checkStack(33, 4, 2); }
    [Fact] public void test_ADC_2() { mkStack(33, 1, 2, 0xFFFF);      Ch05_Stack_Instructions.OPC_xB9_ADC(); checkStack(33, 0, 3); }
    [Fact] public void test_ADC_3() { mkStack(33, 1, 0xFFFF, 0xFFFF); Ch05_Stack_Instructions.OPC_xB9_ADC(); checkStack(33, 0, 0); }

    // ACD
    [Fact] public void test_ACD_1() { mkStack(33, 1, 2, 3);           Ch05_Stack_Instructions.OPC_xBA_ACD(); checkStack(33, 3, 3); }
    [Fact] public void test_ACD_2() { mkStack(33, 0xFFFE, 2, 3);      Ch05_Stack_Instructions.OPC_xBA_ACD(); checkStack(33, 0, 4); }
    [Fact] public void test_ACD_3() { mkStack(33, 0xFFFE, 2, 0xFFFF); Ch05_Stack_Instructions.OPC_xBA_ACD(); checkStack(33, 0, 0); }

    // MUL
    [Fact] public void test_MUL_1a() { mkStack(33, 2, 3, SP, 4);      Ch05_Stack_Instructions.OPC_xBC_MUL(); checkStack(33, 6, SP, 0, 4); }
    [Fact] public void test_MUL_1b() { mkStack(33, 3, 2, SP, 4);      Ch05_Stack_Instructions.OPC_xBC_MUL(); checkStack(33, 6, SP, 0, 4); }
    [Fact] public void test_MUL_2a() { mkStack(33, 2, 0x8002, SP, 4); Ch05_Stack_Instructions.OPC_xBC_MUL(); checkStack(33, 4, SP, 1, 4); }
    [Fact] public void test_MUL_2b() { mkStack(33, 0x8002, 2, SP, 4); Ch05_Stack_Instructions.OPC_xBC_MUL(); checkStack(33, 4, SP, 1, 4); }
    [Fact] public void test_MUL_3()  { mkStack(33, 0xFFFF, 0xFFFF, SP, 4); Ch05_Stack_Instructions.OPC_xBC_MUL(); checkStack(33, 0x0001, SP, 0xFFFE, 4); }
    [Fact] public void test_MUL_4a() { mkStack(33, 0xFFFF, 0, SP, 4);      Ch05_Stack_Instructions.OPC_xBC_MUL(); checkStack(33, 0, SP, 0, 4); }
    [Fact] public void test_MUL_4b() { mkStack(33, 0, 0xFFFF, SP, 4);      Ch05_Stack_Instructions.OPC_xBC_MUL(); checkStack(33, 0, SP, 0, 4); }

    // DMUL
    [Fact] public void test_DMUL_1()  { mkStack(33, 4, 0, 4, 0);                 Ch05_Stack_Instructions.ESC_x30_DMUL(); checkStack(33, 0x0010, 0); }
    [Fact] public void test_DMUL_2a() { mkStack(33, 1, 1, 5, 0);                 Ch05_Stack_Instructions.ESC_x30_DMUL(); checkStack(33, 5, 5); }
    [Fact] public void test_DMUL_2b() { mkStack(33, 5, 0, 1, 1);                 Ch05_Stack_Instructions.ESC_x30_DMUL(); checkStack(33, 5, 5); }
    [Fact] public void test_DMUL_3a() { mkStack(33, 0xFFFF, 0xFFFF, 2, 0);       Ch05_Stack_Instructions.ESC_x30_DMUL(); checkStack(33, 0xFFFE, 0xFFFF); }
    [Fact] public void test_DMUL_3b() { mkStack(33, 2, 0, 0xFFFF, 0xFFFF);       Ch05_Stack_Instructions.ESC_x30_DMUL(); checkStack(33, 0xFFFE, 0xFFFF); }
    [Fact] public void test_DMUL_4a() { mkStack(33, 0, 0, 0xFFFF, 0xFFFF);       Ch05_Stack_Instructions.ESC_x30_DMUL(); checkStack(33, 0, 0); }
    [Fact] public void test_DMUL_4b() { mkStack(33, 0xFFFF, 0xFFFF, 0, 0);       Ch05_Stack_Instructions.ESC_x30_DMUL(); checkStack(33, 0, 0); }

    // SDIV
    [Fact] public void test_SDIV_1a() { mkStack(33, 11, 2);       Ch05_Stack_Instructions.ESC_x31_SDIV(); checkStack(33, 5,  SP, 1); }
    [Fact] public void test_SDIV_1b() { mkStack(33, -11, -2);     Ch05_Stack_Instructions.ESC_x31_SDIV(); checkStack(33, 5,  SP, -1); }
    [Fact] public void test_SDIV_2a() { mkStack(33, -11, 2);      Ch05_Stack_Instructions.ESC_x31_SDIV(); checkStack(33, -5, SP, -1); }
    [Fact] public void test_SDIV_2b() { mkStack(33, 11, -2);      Ch05_Stack_Instructions.ESC_x31_SDIV(); checkStack(33, -5, SP, 1); }
    [Fact] public void test_SDIV_3()
    {
        mkStack(33, 10, 0);
        mesaException.expect_signalDivideZeroTrap = true;
        Assert.Throws<MesaTrapOrFault>(() => Ch05_Stack_Instructions.ESC_x31_SDIV());
    }
    [Fact] public void test_SDIV_4a() { mkStack(33, 0x8000, 2);   Ch05_Stack_Instructions.ESC_x31_SDIV(); checkStack(33, -16384, SP, 0); }
    [Fact] public void test_SDIV_4b() { mkStack(33, 0x7FFF, 2);   Ch05_Stack_Instructions.ESC_x31_SDIV(); checkStack(33, 16383,  SP, 1); }

    // UDIV
    [Fact] public void test_UDIV_1() { mkStack(33, 11, 2);     Ch05_Stack_Instructions.ESC_x1C_UDIV(); checkStack(33, 5, SP, 1); }
    [Fact] public void test_UDIV_2() { mkStack(33, 0xFFFF, 2); Ch05_Stack_Instructions.ESC_x1C_UDIV(); checkStack(33, 0x7FFF, SP, 1); }
    [Fact] public void test_UDIV_3()
    {
        mkStack(33, 22, 0);
        mesaException.expect_signalDivideZeroTrap = true;
        Assert.Throws<MesaTrapOrFault>(() => Ch05_Stack_Instructions.ESC_x1C_UDIV());
    }

    // LUDIV
    [Fact] public void test_LUDIV_1() { mkStack(33, 11, 0, 2); Ch05_Stack_Instructions.ESC_x1D_LUDIV(); checkStack(33, 5, SP, 1); }
    [Fact] public void test_LUDIV_2() { mkStack(33, 11, 1, 2); Ch05_Stack_Instructions.ESC_x1D_LUDIV(); checkStack(33, 32773, SP, 1); }
    [Fact] public void test_LUDIV_3()
    {
        mkStack(33, 11, 2, 2);
        mesaException.expect_signalDivideCheckTrap = true;
        Assert.Throws<MesaTrapOrFault>(() => Ch05_Stack_Instructions.ESC_x1D_LUDIV());
    }
    [Fact] public void test_LUDIV_4()
    {
        mkStack(33, 22, 0, 0);
        mesaException.expect_signalDivideZeroTrap = true;
        Assert.Throws<MesaTrapOrFault>(() => Ch05_Stack_Instructions.ESC_x1D_LUDIV());
    }

    // SDDIV
    private static void innerTestSDDIV(int dividend, int divisor)
    {
        int quotient = dividend / divisor;
        int remainder = dividend % divisor;
        mkStack(33, dividend & 0xFFFF, dividend >>> 16, divisor & 0xFFFF, divisor >>> 16);
        Ch05_Stack_Instructions.ESC_x32_SDDIV();
        checkStack(33, quotient & 0xFFFF, quotient >>> 16, SP, remainder & 0xFFFF, remainder >>> 16);
    }
    [Fact] public void test_SDDIV_1() { innerTestSDDIV( 12345675,  456789); }
    [Fact] public void test_SDDIV_2() { innerTestSDDIV(-12345675,  456789); }
    [Fact] public void test_SDDIV_3() { innerTestSDDIV(-12345675, -456789); }
    [Fact] public void test_SDDIV_4() { innerTestSDDIV( 12345675, -456789); }
    [Fact] public void test_SDDIV_5()
    {
        mkStack(33, 22, 33, 0, 0);
        mesaException.expect_signalDivideZeroTrap = true;
        Assert.Throws<MesaTrapOrFault>(() => Ch05_Stack_Instructions.ESC_x32_SDDIV());
    }

    // UDDIV
    private const long LONG_CARDINAL_MASK = 0x00FFFFFFFFL;
    private static void innerTestUDDIV(long dividend, long divisor)
    {
        dividend &= LONG_CARDINAL_MASK;
        divisor  &= LONG_CARDINAL_MASK;
        long quotient  = (dividend / divisor) & LONG_CARDINAL_MASK;
        long remainder = (dividend % divisor) & LONG_CARDINAL_MASK;
        mkStack(33, (int)(dividend & 0xFFFF), (int)(dividend >>> 16), (int)(divisor & 0xFFFF), (int)(divisor >>> 16));
        Ch05_Stack_Instructions.ESC_x33_UDDIV();
        checkStack(33, (int)(quotient & 0xFFFF), (int)(quotient >>> 16), SP, (int)(remainder & 0xFFFF), (int)(remainder >>> 16));
    }
    [Fact] public void test_UDDIV_1() { innerTestUDDIV(12345675,    456789); }
    [Fact] public void test_UDDIV_2() { innerTestUDDIV(12345675,    3456789); }
    [Fact] public void test_UDDIV_3() { innerTestUDDIV(4, 3); }
    [Fact] public void test_UDDIV_4() { innerTestUDDIV(0xFFFFFFFF, 0xF0F0F0F0); }
    [Fact] public void test_UDDIV_5()
    {
        mkStack(33, 22, 33, 0, 0);
        mesaException.expect_signalDivideZeroTrap = true;
        Assert.Throws<MesaTrapOrFault>(() => Ch05_Stack_Instructions.ESC_x33_UDDIV());
    }

    // DCMP
    private static void innerTestDCMP(int j, int k)
    {
        mkStack(33, j & 0xFFFF, j >>> 16, k & 0xFFFF, k >>> 16);
        Ch05_Stack_Instructions.OPC_xBD_DCMP();
        int expectedResult = 0;
        if (j > k) { expectedResult = +1; }
        if (j < k) { expectedResult = -1; }
        checkStack(33, expectedResult);
    }
    [Fact] public void test_DCMP_1a() { innerTestDCMP(1, 1); }
    [Fact] public void test_DCMP_1b() { innerTestDCMP(-1, -1); }
    [Fact] public void test_DCMP_2a() { innerTestDCMP(1, -1); }
    [Fact] public void test_DCMP_2b() { innerTestDCMP(-1, 1); }
    [Fact] public void test_DCMP_3a() { innerTestDCMP(123456789, 123456789); }
    [Fact] public void test_DCMP_3b() { innerTestDCMP(123456788, 123456789); }
    [Fact] public void test_DCMP_3c() { innerTestDCMP(123456789, 123456788); }
    [Fact] public void test_DCMP_4a() { innerTestDCMP(-123456789, -123456789); }
    [Fact] public void test_DCMP_4b() { innerTestDCMP(-123456788, -123456789); }
    [Fact] public void test_DCMP_4c() { innerTestDCMP(-123456789, -123456788); }
    [Fact] public void test_DCMP_5a() { innerTestDCMP(1, 0); }
    [Fact] public void test_DCMP_5b() { innerTestDCMP(-1, 0); }
    [Fact] public void test_DCMP_5c() { innerTestDCMP(0, 1); }
    [Fact] public void test_DCMP_5d() { innerTestDCMP(0, -1); }

    // UDCMP
    private static void innerTestUDCMP(long j, long k)
    {
        j &= LONG_CARDINAL_MASK;
        k &= LONG_CARDINAL_MASK;
        mkStack(33, (int)(j & 0xFFFF), (int)(j >>> 16), (int)(k & 0xFFFF), (int)(k >>> 16));
        Ch05_Stack_Instructions.OPC_xBE_UDCMP();
        int expectedResult = 0;
        if (j > k) { expectedResult = +1; }
        if (j < k) { expectedResult = -1; }
        checkStack(33, expectedResult);
    }
    [Fact] public void test_UDCMP_1a() { innerTestUDCMP(0, 0); }
    [Fact] public void test_UDCMP_1b() { innerTestUDCMP(0, 1); }
    [Fact] public void test_UDCMP_1c() { innerTestUDCMP(1, 0); }
    [Fact] public void test_UDCMP_1d() { innerTestUDCMP(1, 1); }
    [Fact] public void test_UDCMP_2a() { innerTestUDCMP(3456789012L, 3456789012L); }
    [Fact] public void test_UDCMP_2b() { innerTestUDCMP(3456789011L, 3456789012L); }
    [Fact] public void test_UDCMP_2c() { innerTestUDCMP(3456789012L, 3456789011L); }
}
