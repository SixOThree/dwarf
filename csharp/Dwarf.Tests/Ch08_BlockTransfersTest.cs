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
OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ANY THEORY OF LIABILITY,
WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR
OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System.Text;
using Dwarf.Engine;
using Dwarf.Engine.Opcodes;

namespace Dwarf.Tests;

// Unittests for instructions implemented in class Ch08_Block_Transfers.
// The COLORBLT test mutates the page map; restore at end via OnAfterTest.
public sealed class Ch08_BlockTransfersTest : AbstractInstructionTest
{
    protected override void OnAfterTest()
    {
        Mem.createInitialPageMappingGuam();
    }

    // ==================================================================
    // 8.1 Word Boundary Block Transfers
    // ==================================================================

    [Fact] public void test_BLT_a()
    {
        mkShortMem(
            0x0000, 0x1234, 0x2345, 0x3456, 0x4567, 0x5678, 0x6789, 0x7890,
            0x8901, 0x9012, 0xA123, 0xB234, 0xC345, 0xD456, 0xE567, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000);
        mkStack(22, 33, testShortMem + 1, 14, testShortMem + 24);
        Ch08_Block_Transfers.OPC_xF3_BLT();
        checkShortMem(
            0x0000, 0x1234, 0x2345, 0x3456, 0x4567, 0x5678, 0x6789, 0x7890,
            0x8901, 0x9012, 0xA123, 0xB234, 0xC345, 0xD456, 0xE567, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x1234, 0x2345, 0x3456, 0x4567, 0x5678, 0x6789, 0x7890, 0x8901,
            0x9012, 0xA123, 0xB234, 0xC345, 0xD456, 0xE567, 0x0000, 0x0000);
        checkStack(22, 33);
    }

    [Fact] public void test_BLT_b()
    {
        mkShortMem(
            0x0000, 0x1234, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000);
        mkStack(22, 33, testShortMem + 1, 17, testShortMem + 2);
        Ch08_Block_Transfers.OPC_xF3_BLT();
        checkShortMem(
            0x0000, 0x1234, 0x1234, 0x1234, 0x1234, 0x1234, 0x1234, 0x1234,
            0x1234, 0x1234, 0x1234, 0x1234, 0x1234, 0x1234, 0x1234, 0x1234,
            0x1234, 0x1234, 0x1234, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000);
        checkStack(22, 33);
    }

    [Fact] public void test_BLTL_a()
    {
        mkLongMem(
            0x0000, 0x1234, 0x2345, 0x3456, 0x4567, 0x5678, 0x6789, 0x7890,
            0x8901, 0x9012, 0xA123, 0xB234, 0xC345, 0xD456, 0xE567, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000);
        mkStack(22, 33, testLongMemLow + 1, testLongMemHigh, 14, testLongMemLow + 24, testLongMemHigh);
        Ch08_Block_Transfers.OPC_xF4_BLTL();
        checkLongMem(
            0x0000, 0x1234, 0x2345, 0x3456, 0x4567, 0x5678, 0x6789, 0x7890,
            0x8901, 0x9012, 0xA123, 0xB234, 0xC345, 0xD456, 0xE567, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x1234, 0x2345, 0x3456, 0x4567, 0x5678, 0x6789, 0x7890, 0x8901,
            0x9012, 0xA123, 0xB234, 0xC345, 0xD456, 0xE567, 0x0000, 0x0000);
        checkStack(22, 33);
    }

    [Fact] public void test_BLTL_b()
    {
        mkLongMem(
            0x0000, 0x1234, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000);
        mkStack(22, 33, testLongMemLow + 1, testLongMemHigh, 17, testLongMemLow + 2, testLongMemHigh);
        Ch08_Block_Transfers.OPC_xF4_BLTL();
        checkLongMem(
            0x0000, 0x1234, 0x1234, 0x1234, 0x1234, 0x1234, 0x1234, 0x1234,
            0x1234, 0x1234, 0x1234, 0x1234, 0x1234, 0x1234, 0x1234, 0x1234,
            0x1234, 0x1234, 0x1234, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000);
        checkStack(22, 33);
    }

    [Fact] public void test_BLTLR_a()
    {
        mkLongMem(
            0x0123, 0x1234, 0x2345, 0x3456, 0x4567, 0x5678, 0x6789, 0x7890,
            0x8901, 0x9012, 0xA123, 0xB234, 0xC345, 0xD456, 0xE567, 0xF678,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000);
        mkStack(22, 33, testLongMemLow + 1, testLongMemHigh, 14, testLongMemLow + 24, testLongMemHigh);
        Ch08_Block_Transfers.ESC_x27_BLTLR();
        checkLongMem(
            0x0123, 0x1234, 0x2345, 0x3456, 0x4567, 0x5678, 0x6789, 0x7890,
            0x8901, 0x9012, 0xA123, 0xB234, 0xC345, 0xD456, 0xE567, 0xF678,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x1234, 0x2345, 0x3456, 0x4567, 0x5678, 0x6789, 0x7890, 0x8901,
            0x9012, 0xA123, 0xB234, 0xC345, 0xD456, 0xE567, 0x0000, 0x0000);
        checkStack(22, 33);
    }

    [Fact] public void test_BLTLR_b()
    {
        mkLongMem(
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x1234, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000);
        mkStack(22, 33, testLongMemLow + 1, testLongMemHigh, 17, testLongMemLow, testLongMemHigh);
        Ch08_Block_Transfers.ESC_x27_BLTLR();
        checkLongMem(
            0x1234, 0x1234, 0x1234, 0x1234, 0x1234, 0x1234, 0x1234, 0x1234,
            0x1234, 0x1234, 0x1234, 0x1234, 0x1234, 0x1234, 0x1234, 0x1234,
            0x1234, 0x1234, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000);
        checkStack(22, 33);
    }

    [Fact] public void test_BLTC()
    {
        mkCode(
            1, 2, 3, savedPC, 0xF5, PC, 4, 5, 6, 7,
            0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0,
            0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88);
        mkStack(11, 22, 4, 8, testShortMem + 2);
        Ch08_Block_Transfers.OPC_xF5_BLTC();
        checkStack(11, 22);
        checkShortMem(
            0x0000, 0x0000, 0x1234, 0x5678, 0x9ABC, 0xDEF0, 0x1122, 0x3344,
            0x5566, 0x7788, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000);
    }

    [Fact] public void test_BLTCL()
    {
        mkCode(
            1, 2, 3, savedPC, 0xF5, PC, 4, 5, 6, 7,
            0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0,
            0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88);
        mkStack(11, 22, 4, 8, testLongMemLow + 2, testLongMemHigh);
        Ch08_Block_Transfers.OPC_xF6_BLTCL();
        checkStack(11, 22);
        checkLongMem(
            0x0000, 0x0000, 0x1234, 0x5678, 0x9ABC, 0xDEF0, 0x1122, 0x3344,
            0x5566, 0x7788, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000);
    }

    [Fact] public void test_CKSUM()
    {
        mkLongMem(
            0x0000, 0x0000,
            0x1759, 0x0028, 0x0004,
            0x0000, 0x0000, 0xffff, 0xffff, 0xffff, 0x0030,
            0x0000, 0x0000, 0x0800, 0x27d2, 0x976e, 0x0030,
            0x9135, 0x0599, 0x0008, 0x0000, 0x0000,
            0x1234, 0x2345);
        mkStack(11, 22, 0, 19, testLongMemLow + 3, testLongMemHigh);
        Ch08_Block_Transfers.ESC_x2A_CKSUM();
        checkStack(11, 22, 0x1759);
    }

    [Fact] public void test_BLEL_eq()
    {
        mkLongMem(
            0x0000, 0x0000, 0x1234, 0x5678, 0x9ABC, 0xDEF0, 0x1122, 0x3344,
            0x5566, 0x7788, 0x99AA, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x1234, 0x5678, 0x9ABC, 0xDEF0, 0x1122, 0x3344, 0x5566,
            0x7788, 0x99AA, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000);
        mkStack(11, 22, testLongMemLow + 17, testLongMemHigh, 9, testLongMemLow + 2, testLongMemHigh);
        Ch08_Block_Transfers.ESC_x28_BLEL();
        checkStack(11, 22, 1);
    }

    [Fact] public void test_BLEL_neq()
    {
        mkLongMem(
            0x0000, 0x0000, 0x1234, 0x5678, 0x9ABC, 0xDEF0, 0x1122, 0x3344,
            0x5566, 0x7788, 0x99AA, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x1234, 0x5678, 0x9ABC, 0xDEF0, 0x1122, 0x3344, 0x5566,
            0x7788, 0x990A, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000);
        mkStack(11, 22, testLongMemLow + 17, testLongMemHigh, 9, testLongMemLow + 2, testLongMemHigh);
        Ch08_Block_Transfers.ESC_x28_BLEL();
        checkStack(11, 22, 0);
    }

    [Fact] public void test_BLECL_eq()
    {
        mkLongMem(
            0x0000, 0x0000, 0x1234, 0x5678, 0x9ABC, 0xDEF0, 0x1122, 0x3344,
            0x5566, 0x7788, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000);
        mkCode(
            1, 2, 3, savedPC, 0xF5, PC, 4, 5, 6, 7,
            0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0,
            0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88,
            0x99, 0xAA);
        mkStack(11, 22, 4, 8, testLongMemLow + 2, testLongMemHigh);
        Ch08_Block_Transfers.ESC_x29_BLECL();
        checkStack(11, 22, 1);
    }

    [Fact] public void test_BLECL_neq()
    {
        mkLongMem(
            0x0000, 0x0000, 0x1234, 0x5678, 0x9ABC, 0xDEF0, 0x1122, 0x3344,
            0x5566, 0x7788, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000);
        mkCode(
            1, 2, 3, savedPC, 0xF5, PC, 4, 5, 6, 7,
            0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0,
            0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x08,
            0x99, 0xAA);
        mkStack(11, 22, 4, 8, testLongMemLow + 2, testLongMemHigh);
        Ch08_Block_Transfers.ESC_x29_BLECL();
        checkStack(11, 22, 0);
    }

    // ==================================================================
    // 8.3 Byte Boundary Block Transfers
    // ==================================================================

    private static readonly int[] BYTBLT_SRC_ODD = {
        0x0000, 0x0011, 0x2233, 0x4455, 0x6677, 0x8899, 0xAABB, 0xCCDD,
        0xEEFF, 0x1234, 0x5678, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000
    };
    private static readonly int[] BYTBLT_SRC_EVEN = {
        0x0000, 0x1122, 0x3344, 0x5566, 0x7788, 0x99AA, 0xBBCC, 0xDDEE,
        0xFF12, 0x3456, 0x7800, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000
    };

    [Fact] public void test_BYTBLT_a()
    {
        mkLongMem(BYTBLT_SRC_ODD);
        mkStack(11, 22, testLongMemLow + 23, testLongMemHigh, 5, 18, testLongMemLow, testLongMemHigh, 3);
        Ch08_Block_Transfers.ESC_x2D_BYTBLT();
        checkStack(11, 22);
        checkLongMem(
            0x0000, 0x0011, 0x2233, 0x4455, 0x6677, 0x8899, 0xAABB, 0xCCDD,
            0xEEFF, 0x1234, 0x5678, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0011, 0x2233, 0x4455, 0x6677, 0x8899, 0xAABB, 0xCCDD,
            0xEEFF, 0x1234, 0x5600, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000);
    }

    [Fact] public void test_BYTBLT_b()
    {
        mkLongMem(BYTBLT_SRC_ODD);
        mkStack(11, 22, testLongMemLow + 23, testLongMemHigh, 4, 18, testLongMemLow, testLongMemHigh, 3);
        Ch08_Block_Transfers.ESC_x2D_BYTBLT();
        checkStack(11, 22);
        checkLongMem(
            0x0000, 0x0011, 0x2233, 0x4455, 0x6677, 0x8899, 0xAABB, 0xCCDD,
            0xEEFF, 0x1234, 0x5678, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x1122, 0x3344, 0x5566, 0x7788, 0x99AA, 0xBBCC, 0xDDEE,
            0xFF12, 0x3456, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000);
    }

    [Fact] public void test_BYTBLT_c()
    {
        mkLongMem(BYTBLT_SRC_EVEN);
        mkStack(11, 22, testLongMemLow + 23, testLongMemHigh, 5, 18, testLongMemLow + 1, testLongMemHigh, 0);
        Ch08_Block_Transfers.ESC_x2D_BYTBLT();
        checkStack(11, 22);
        checkLongMem(
            0x0000, 0x1122, 0x3344, 0x5566, 0x7788, 0x99AA, 0xBBCC, 0xDDEE,
            0xFF12, 0x3456, 0x7800, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0011, 0x2233, 0x4455, 0x6677, 0x8899, 0xAABB, 0xCCDD,
            0xEEFF, 0x1234, 0x5600, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000);
    }

    [Fact] public void test_BYTBLT_d()
    {
        mkLongMem(BYTBLT_SRC_EVEN);
        mkStack(11, 22, testLongMemLow + 23, testLongMemHigh, 5, 19, testLongMemLow + 1, testLongMemHigh, 0);
        Ch08_Block_Transfers.ESC_x2D_BYTBLT();
        checkStack(11, 22);
        checkLongMem(
            0x0000, 0x1122, 0x3344, 0x5566, 0x7788, 0x99AA, 0xBBCC, 0xDDEE,
            0xFF12, 0x3456, 0x7800, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0011, 0x2233, 0x4455, 0x6677, 0x8899, 0xAABB, 0xCCDD,
            0xEEFF, 0x1234, 0x5678, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000);
    }

    [Fact] public void test_BYTBLT_e()
    {
        mkLongMem(BYTBLT_SRC_EVEN);
        mkStack(11, 22, testLongMemLow + 23, testLongMemHigh, 4, 18, testLongMemLow + 1, testLongMemHigh, 0);
        Ch08_Block_Transfers.ESC_x2D_BYTBLT();
        checkStack(11, 22);
        checkLongMem(
            0x0000, 0x1122, 0x3344, 0x5566, 0x7788, 0x99AA, 0xBBCC, 0xDDEE,
            0xFF12, 0x3456, 0x7800, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x1122, 0x3344, 0x5566, 0x7788, 0x99AA, 0xBBCC, 0xDDEE,
            0xFF12, 0x3456, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000);
    }

    [Fact] public void test_BYTBLTR_a()
    {
        mkLongMem(BYTBLT_SRC_ODD);
        mkStack(11, 22, testLongMemLow + 23, testLongMemHigh, 5, 18, testLongMemLow, testLongMemHigh, 3);
        Ch08_Block_Transfers.ESC_x2E_BYTBLTR();
        checkStack(11, 22);
        checkLongMem(
            0x0000, 0x0011, 0x2233, 0x4455, 0x6677, 0x8899, 0xAABB, 0xCCDD,
            0xEEFF, 0x1234, 0x5678, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0011, 0x2233, 0x4455, 0x6677, 0x8899, 0xAABB, 0xCCDD,
            0xEEFF, 0x1234, 0x5600, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000);
    }

    [Fact] public void test_BYTBLTR_b()
    {
        mkLongMem(BYTBLT_SRC_ODD);
        mkStack(11, 22, testLongMemLow + 23, testLongMemHigh, 4, 18, testLongMemLow, testLongMemHigh, 3);
        Ch08_Block_Transfers.ESC_x2E_BYTBLTR();
        checkStack(11, 22);
        checkLongMem(
            0x0000, 0x0011, 0x2233, 0x4455, 0x6677, 0x8899, 0xAABB, 0xCCDD,
            0xEEFF, 0x1234, 0x5678, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x1122, 0x3344, 0x5566, 0x7788, 0x99AA, 0xBBCC, 0xDDEE,
            0xFF12, 0x3456, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000);
    }

    [Fact] public void test_BYTBLTR_c()
    {
        mkLongMem(BYTBLT_SRC_EVEN);
        mkStack(11, 22, testLongMemLow + 23, testLongMemHigh, 5, 18, testLongMemLow + 1, testLongMemHigh, 0);
        Ch08_Block_Transfers.ESC_x2E_BYTBLTR();
        checkStack(11, 22);
        checkLongMem(
            0x0000, 0x1122, 0x3344, 0x5566, 0x7788, 0x99AA, 0xBBCC, 0xDDEE,
            0xFF12, 0x3456, 0x7800, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0011, 0x2233, 0x4455, 0x6677, 0x8899, 0xAABB, 0xCCDD,
            0xEEFF, 0x1234, 0x5600, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000);
    }

    [Fact] public void test_BYTBLTR_d()
    {
        mkLongMem(BYTBLT_SRC_EVEN);
        mkStack(11, 22, testLongMemLow + 23, testLongMemHigh, 5, 19, testLongMemLow + 1, testLongMemHigh, 0);
        Ch08_Block_Transfers.ESC_x2E_BYTBLTR();
        checkStack(11, 22);
        checkLongMem(
            0x0000, 0x1122, 0x3344, 0x5566, 0x7788, 0x99AA, 0xBBCC, 0xDDEE,
            0xFF12, 0x3456, 0x7800, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0011, 0x2233, 0x4455, 0x6677, 0x8899, 0xAABB, 0xCCDD,
            0xEEFF, 0x1234, 0x5678, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000);
    }

    [Fact] public void test_BYTBLTR_e()
    {
        mkLongMem(BYTBLT_SRC_EVEN);
        mkStack(11, 22, testLongMemLow + 23, testLongMemHigh, 4, 18, testLongMemLow + 1, testLongMemHigh, 0);
        Ch08_Block_Transfers.ESC_x2E_BYTBLTR();
        checkStack(11, 22);
        checkLongMem(
            0x0000, 0x1122, 0x3344, 0x5566, 0x7788, 0x99AA, 0xBBCC, 0xDDEE,
            0xFF12, 0x3456, 0x7800, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
            0x0000, 0x1122, 0x3344, 0x5566, 0x7788, 0x99AA, 0xBBCC, 0xDDEE,
            0xFF12, 0x3456, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000);
    }

    // ==================================================================
    // 8.4 Bit Boundary Block Transfers (BITBLT)
    // ==================================================================

    private static void dumpWord(StringBuilder sb, int w)
    {
        int mask = 0x8000;
        for (int i = 0; i < 16; i++)
        {
            sb.Append(((w & mask) != 0) ? 'X' : ' ');
            mask >>= 1;
        }
    }

    private static StringBuilder expandBitmap(StringBuilder? sb, int at, int lineWords, int pixelHeight)
    {
        sb ??= new StringBuilder();
        sb.Append('+');
        for (int i = 0; i < lineWords; i++) { sb.Append("----------------"); }
        sb.Append("+\n");
        for (int j = 0; j < pixelHeight; j++)
        {
            sb.Append('|');
            for (int i = 0; i < lineWords; i++) { dumpWord(sb, Mem.readWord(at++)); }
            sb.Append("|\n");
        }
        sb.Append('+');
        for (int i = 0; i < lineWords; i++) { sb.Append("----------------"); }
        sb.Append("+\n");
        return sb;
    }

    private static void setupStartBitmap()
    {
        Mem.writeWord(testLongMem + 7,  (ushort)0xFFFF); Mem.writeWord(testLongMem + 8,  (ushort)0xFFFF);
        Mem.writeWord(testLongMem + 13, (ushort)0x8000); Mem.writeWord(testLongMem + 14, (ushort)0x0001);
        Mem.writeWord(testLongMem + 19, (ushort)0x8000); Mem.writeWord(testLongMem + 20, (ushort)0x0001);
        Mem.writeWord(testLongMem + 25, (ushort)0x8000); Mem.writeWord(testLongMem + 26, (ushort)0x0001);
        Mem.writeWord(testLongMem + 31, (ushort)0xFFFF); Mem.writeWord(testLongMem + 32, (ushort)0xFFFF);
    }

    private static void setupStartBitmapWithBlackZone()
    {
        setupStartBitmap();
        int at = testLongMem + 42;
        for (int i = 0; i < 12 * 6; i++) { Mem.writeWord(at++, (ushort)0xFFFF); }
    }

    private static void checkBitmap(string intro, int at, int wordWidth, params string[] lines)
    {
        int curr = at;
        bool ok = true;
        foreach (string line in lines)
        {
            byte[] bits = Encoding.UTF8.GetBytes(line);
            int word = 0;
            int cnt = 0;
            foreach (byte b in bits)
            {
                word <<= 1;
                if (b != ' ') { word |= 1; }
                cnt++;
                if (cnt == 16)
                {
                    int actual = Mem.readWord(curr++);
                    if (actual != word) { ok = false; goto report; }
                    cnt = 0;
                    word = 0;
                }
            }
            if (cnt != 0)
            {
                int actual = Mem.readWord(curr++);
                if (actual != word) { ok = false; goto report; }
            }
        }
        return;

        report:
        StringBuilder sbExp = new();
        sbExp.Append('+');
        for (int i = 0; i < wordWidth; i++) { sbExp.Append("----------------"); }
        string hsep = sbExp.Append('+').ToString();

        sbExp.Clear();
        sbExp.Append(hsep).Append('\n');
        foreach (string l in lines) { sbExp.Append('|').Append(l).Append("|\n"); }
        sbExp.Append(hsep);

        StringBuilder sbAct = expandBitmap(null, at, wordWidth, lines.Length);
        Assert.True(ok, $"{intro}\nExpected:\n{sbExp}\nActual:\n{sbAct}");
    }

    private const int flg_forward         = 0x0000;
    private const int flg_backward        = 0x8000;
    private const int flg_overlap         = 0x0000;
    private const int flg_disjoint        = 0x4000;
    private const int flg_overlapItems    = 0x0000;
    private const int flg_disjointItems   = 0x2000;
    private const int flg_bitmap          = 0x0000;
    private const int flg_gray            = 0x1000;
    private const int flg_srcFuncNull       = 0x0000;
    private const int flg_srcFuncComplement = 0x0800;
    private const int flg_dstFuncNull = 0x0000;
    private const int flg_dstFuncAnd  = 0x0200;
    private const int flg_dstFuncOr   = 0x0400;
    private const int flg_dstFuncXor  = 0x0600;

    private static void mkBitBltArg(int shortTarget,
        int dstWordLp, int dstBit, int dstBpl,
        int srcWordLp, int srcBit, int srcBpl,
        int width, int height, params int[] flagBits)
    {
        int flags = 0;
        foreach (int f in flagBits) { flags |= f; }

        Mem.writeMDSWord(shortTarget, 0, dstWordLp & 0xFFFF);
        Mem.writeMDSWord(shortTarget, 1, (int)((uint)dstWordLp >> 16));
        Mem.writeMDSWord(shortTarget, 2, dstBit);
        Mem.writeMDSWord(shortTarget, 3, dstBpl);
        Mem.writeMDSWord(shortTarget, 4, srcWordLp & 0xFFFF);
        Mem.writeMDSWord(shortTarget, 5, (int)((uint)srcWordLp >> 16));
        Mem.writeMDSWord(shortTarget, 6, srcBit);
        Mem.writeMDSWord(shortTarget, 7, srcBpl);
        Mem.writeMDSWord(shortTarget, 8, width);
        Mem.writeMDSWord(shortTarget, 9, height);
        Mem.writeMDSWord(shortTarget, 10, flags);
        Mem.writeMDSWord(shortTarget, 11, 0);
    }

    [Fact] public void test_BITBLT_forward_null_null_intoWhite()
    {
        setupStartBitmap();
        mkBitBltArg(testShortMem,
            testLongMem + 48, 2, 6 * 16,
            testLongMem + 7, 0, 6 * 16,
            32, 5,
            flg_forward, flg_disjoint, flg_disjointItems, flg_srcFuncNull, flg_dstFuncNull);
        mkStack(testShortMem);
        Ch08_Block_Transfers.ESC_x2B_BITBLT();
        checkStack();

        checkBitmap("BITBLT_forward_null_null_intoWhite", testLongMem, 6,
            "                                                                                                ",
            "                XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX                                                ",
            "                X                              X                                                ",
            "                X                              X                                                ",
            "                X                              X                                                ",
            "                XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX                                                ",
            "                                                                                                ",
            "                                                                                                ",
            "  XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX                                                              ",
            "  X                              X                                                              ",
            "  X                              X                                                              ",
            "  X                              X                                                              ",
            "  XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX                                                              ",
            "                                                                                                ");
    }

    [Fact] public void test_BITBLT_forward_null_null_intoBlack()
    {
        setupStartBitmapWithBlackZone();
        mkBitBltArg(testShortMem,
            testLongMem + 42, 1, 6 * 16,
            testLongMem, 15, 6 * 16,
            34, 7,
            flg_forward, flg_disjoint, flg_disjointItems, flg_srcFuncNull, flg_dstFuncNull);
        mkStack(testShortMem);
        Ch08_Block_Transfers.ESC_x2B_BITBLT();
        checkStack();

        checkBitmap("BITBLT_forward_null_null_intoBlack", testLongMem, 6,
            "                                                                                                ",
            "                XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX                                                ",
            "                X                              X                                                ",
            "                X                              X                                                ",
            "                X                              X                                                ",
            "                XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX                                                ",
            "                                                                                                ",
            "X                                  XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "X XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "X X                              X XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "X X                              X XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "X X                              X XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "X XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "X                                  XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX");
    }

    [Fact] public void test_BITBLT_forward_null_or()
    {
        setupStartBitmap();
        mkBitBltArg(testShortMem,
            testLongMem + 48, 2, 6 * 16,
            testLongMem + 7, 0, 6 * 16,
            32, 5,
            flg_forward, flg_disjoint, flg_disjointItems, flg_srcFuncNull, flg_dstFuncOr);
        mkStack(testShortMem);
        Ch08_Block_Transfers.ESC_x2B_BITBLT();
        checkStack();

        checkBitmap("BITBLT_forward_null_or", testLongMem, 6,
            "                                                                                                ",
            "                XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX                                                ",
            "                X                              X                                                ",
            "                X                              X                                                ",
            "                X                              X                                                ",
            "                XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX                                                ",
            "                                                                                                ",
            "                                                                                                ",
            "  XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX                                                              ",
            "  X                              X                                                              ",
            "  X                              X                                                              ",
            "  X                              X                                                              ",
            "  XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX                                                              ",
            "                                                                                                ");
    }

    [Fact] public void test_BITBLT_forward_null_xor_intoWhite()
    {
        setupStartBitmap();
        mkBitBltArg(testShortMem,
            testLongMem + 48, 2, 6 * 16,
            testLongMem + 7, 0, 6 * 16,
            32, 5,
            flg_forward, flg_disjoint, flg_disjointItems, flg_srcFuncNull, flg_dstFuncXor);
        mkStack(testShortMem);
        Ch08_Block_Transfers.ESC_x2B_BITBLT();
        checkStack();

        checkBitmap("BITBLT_forward_null_xor_intoWhite", testLongMem, 6,
            "                                                                                                ",
            "                XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX                                                ",
            "                X                              X                                                ",
            "                X                              X                                                ",
            "                X                              X                                                ",
            "                XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX                                                ",
            "                                                                                                ",
            "                                                                                                ",
            "  XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX                                                              ",
            "  X                              X                                                              ",
            "  X                              X                                                              ",
            "  X                              X                                                              ",
            "  XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX                                                              ",
            "                                                                                                ");
    }

    [Fact] public void test_BITBLT_forward_null_xor_intoBlack()
    {
        setupStartBitmapWithBlackZone();
        mkBitBltArg(testShortMem,
            testLongMem + 48, 2, 6 * 16,
            testLongMem + 7, 0, 6 * 16,
            32, 5,
            flg_forward, flg_disjoint, flg_disjointItems, flg_srcFuncNull, flg_dstFuncXor);
        mkStack(testShortMem);
        Ch08_Block_Transfers.ESC_x2B_BITBLT();
        checkStack();

        checkBitmap("BITBLT_forward_null_xor_intoBlack", testLongMem, 6,
            "                                                                                                ",
            "                XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX                                                ",
            "                X                              X                                                ",
            "                X                              X                                                ",
            "                X                              X                                                ",
            "                XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX                                                ",
            "                                                                                                ",
            "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "XX                                XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "XX XXXXXXXXXXXXXXXXXXXXXXXXXXXXXX XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "XX XXXXXXXXXXXXXXXXXXXXXXXXXXXXXX XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "XX XXXXXXXXXXXXXXXXXXXXXXXXXXXXXX XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "XX                                XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX");
    }

    [Fact] public void test_BITBLT_forward_complement_or()
    {
        setupStartBitmap();
        mkBitBltArg(testShortMem,
            testLongMem + 42, 1, 6 * 16,
            testLongMem, 15, 6 * 16,
            34, 7,
            flg_forward, flg_disjoint, flg_disjointItems, flg_srcFuncComplement, flg_dstFuncOr);
        mkStack(testShortMem);
        Ch08_Block_Transfers.ESC_x2B_BITBLT();
        checkStack();

        checkBitmap("BITBLT_forward_complement_or", testLongMem, 6,
            "                                                                                                ",
            "                XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX                                                ",
            "                X                              X                                                ",
            "                X                              X                                                ",
            "                X                              X                                                ",
            "                XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX                                                ",
            "                                                                                                ",
            " XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX                                                             ",
            " X                                X                                                             ",
            " X XXXXXXXXXXXXXXXXXXXXXXXXXXXXXX X                                                             ",
            " X XXXXXXXXXXXXXXXXXXXXXXXXXXXXXX X                                                             ",
            " X XXXXXXXXXXXXXXXXXXXXXXXXXXXXXX X                                                             ",
            " X                                X                                                             ",
            " XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX                                                             ",
            "                                                                                                ");
    }

    [Fact] public void test_BITBLT_forward_null_and_intoWhite()
    {
        setupStartBitmap();
        mkBitBltArg(testShortMem,
            testLongMem + 42, 1, 6 * 16,
            testLongMem, 15, 6 * 16,
            34, 7,
            flg_forward, flg_disjoint, flg_disjointItems, flg_srcFuncNull, flg_dstFuncAnd);
        mkStack(testShortMem);
        Ch08_Block_Transfers.ESC_x2B_BITBLT();
        checkStack();

        checkBitmap("BITBLT_forward_null_and_intoWhite", testLongMem, 6,
            "                                                                                                ",
            "                XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX                                                ",
            "                X                              X                                                ",
            "                X                              X                                                ",
            "                X                              X                                                ",
            "                XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX                                                ",
            "                                                                                                ",
            "                                                                                                ",
            "                                                                                                ",
            "                                                                                                ",
            "                                                                                                ",
            "                                                                                                ",
            "                                                                                                ",
            "                                                                                                ",
            "                                                                                                ");
    }

    [Fact] public void test_BITBLT_forward_null_and_intoBlack()
    {
        setupStartBitmapWithBlackZone();
        mkBitBltArg(testShortMem,
            testLongMem + 42, 1, 6 * 16,
            testLongMem, 15, 6 * 16,
            34, 7,
            flg_forward, flg_disjoint, flg_disjointItems, flg_srcFuncNull, flg_dstFuncAnd);
        mkStack(testShortMem);
        Ch08_Block_Transfers.ESC_x2B_BITBLT();
        checkStack();

        checkBitmap("BITBLT_forward_null_and_intoBlack", testLongMem, 6,
            "                                                                                                ",
            "                XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX                                                ",
            "                X                              X                                                ",
            "                X                              X                                                ",
            "                X                              X                                                ",
            "                XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX                                                ",
            "                                                                                                ",
            "X                                  XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "X XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "X X                              X XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "X X                              X XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "X X                              X XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "X XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "X                                  XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX");
    }

    [Fact] public void test_BITBLT_forward_complement_and_intoBlack()
    {
        setupStartBitmapWithBlackZone();
        mkBitBltArg(testShortMem,
            testLongMem + 48, 2, 6 * 16,
            testLongMem + 7, 0, 6 * 16,
            32, 5,
            flg_forward, flg_disjoint, flg_disjointItems, flg_srcFuncComplement, flg_dstFuncAnd);
        mkStack(testShortMem);
        Ch08_Block_Transfers.ESC_x2B_BITBLT();
        checkStack();

        checkBitmap("BITBLT_forward_complement_and_intoBlack", testLongMem, 6,
            "                                                                                                ",
            "                XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX                                                ",
            "                X                              X                                                ",
            "                X                              X                                                ",
            "                X                              X                                                ",
            "                XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX                                                ",
            "                                                                                                ",
            "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "XX                                XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "XX XXXXXXXXXXXXXXXXXXXXXXXXXXXXXX XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "XX XXXXXXXXXXXXXXXXXXXXXXXXXXXXXX XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "XX XXXXXXXXXXXXXXXXXXXXXXXXXXXXXX XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "XX                                XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX");
    }

    private static void innertest_BITBLT_backward_null_or_intoWhite(int dstBit)
    {
        setupStartBitmap();
        mkBitBltArg(testShortMem,
            testLongMem + 72, dstBit, -6 * 16,
            testLongMem + 31, 0, -6 * 16,
            32, 5,
            flg_backward, flg_disjoint, flg_disjointItems, flg_srcFuncNull, flg_dstFuncOr);
        mkStack(testShortMem);
        Ch08_Block_Transfers.ESC_x2B_BITBLT();
        checkStack();

        string shift = "                ".Substring(0, dstBit);

        checkBitmap($"BITBLT_backward_null_or_intoWhite, dstBit={dstBit}", testLongMem, 6,
            "                                                                                                ",
            "                XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX                                                ",
            "                X                              X                                                ",
            "                X                              X                                                ",
            "                X                              X                                                ",
            "                XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX                                                ",
            "                                                                                                ",
            "                                                                                                ",
            (shift + "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX                                                                ").Substring(0, 96),
            (shift + "X                              X                                                                ").Substring(0, 96),
            (shift + "X                              X                                                                ").Substring(0, 96),
            (shift + "X                              X                                                                ").Substring(0, 96),
            (shift + "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX                                                                ").Substring(0, 96),
            "                                                                                                ",
            "                                                                                                ");
    }

    [Fact] public void test_BITBLT_backward_null_or_intoWhite_dstBit00() { innertest_BITBLT_backward_null_or_intoWhite(0); }
    [Fact] public void test_BITBLT_backward_null_or_intoWhite_dstBit01() { innertest_BITBLT_backward_null_or_intoWhite(1); }
    [Fact] public void test_BITBLT_backward_null_or_intoWhite_dstBit02() { innertest_BITBLT_backward_null_or_intoWhite(2); }
    [Fact] public void test_BITBLT_backward_null_or_intoWhite_dstBit03() { innertest_BITBLT_backward_null_or_intoWhite(3); }
    [Fact] public void test_BITBLT_backward_null_or_intoWhite_dstBit04() { innertest_BITBLT_backward_null_or_intoWhite(4); }
    [Fact] public void test_BITBLT_backward_null_or_intoWhite_dstBit05() { innertest_BITBLT_backward_null_or_intoWhite(5); }
    [Fact] public void test_BITBLT_backward_null_or_intoWhite_dstBit06() { innertest_BITBLT_backward_null_or_intoWhite(6); }
    [Fact] public void test_BITBLT_backward_null_or_intoWhite_dstBit07() { innertest_BITBLT_backward_null_or_intoWhite(7); }
    [Fact] public void test_BITBLT_backward_null_or_intoWhite_dstBit08() { innertest_BITBLT_backward_null_or_intoWhite(8); }
    [Fact] public void test_BITBLT_backward_null_or_intoWhite_dstBit09() { innertest_BITBLT_backward_null_or_intoWhite(9); }
    [Fact] public void test_BITBLT_backward_null_or_intoWhite_dstBit10() { innertest_BITBLT_backward_null_or_intoWhite(10); }
    [Fact] public void test_BITBLT_backward_null_or_intoWhite_dstBit11() { innertest_BITBLT_backward_null_or_intoWhite(11); }
    [Fact] public void test_BITBLT_backward_null_or_intoWhite_dstBit12() { innertest_BITBLT_backward_null_or_intoWhite(12); }
    [Fact] public void test_BITBLT_backward_null_or_intoWhite_dstBit13() { innertest_BITBLT_backward_null_or_intoWhite(13); }
    [Fact] public void test_BITBLT_backward_null_or_intoWhite_dstBit14() { innertest_BITBLT_backward_null_or_intoWhite(14); }
    [Fact] public void test_BITBLT_backward_null_or_intoWhite_dstBit15() { innertest_BITBLT_backward_null_or_intoWhite(15); }

    [Fact] public void test_BITBLT_backward_complement_and_intoBlack()
    {
        setupStartBitmapWithBlackZone();
        mkBitBltArg(testShortMem,
            testLongMem + 72, 2, -6 * 16,
            testLongMem + 31, 0, -6 * 16,
            32, 5,
            flg_backward, flg_disjoint, flg_disjointItems, flg_srcFuncComplement, flg_dstFuncAnd);
        mkStack(testShortMem);
        Ch08_Block_Transfers.ESC_x2B_BITBLT();
        checkStack();

        checkBitmap("BITBLT_backward_complement_and_intoBlack", testLongMem, 6,
            "                                                                                                ",
            "                XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX                                                ",
            "                X                              X                                                ",
            "                X                              X                                                ",
            "                X                              X                                                ",
            "                XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX                                                ",
            "                                                                                                ",
            "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "XX                                XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "XX XXXXXXXXXXXXXXXXXXXXXXXXXXXXXX XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "XX XXXXXXXXXXXXXXXXXXXXXXXXXXXXXX XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "XX XXXXXXXXXXXXXXXXXXXXXXXXXXXXXX XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "XX                                XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX");
    }

    private static void mkBrick(int at, params int[] words)
    {
        for (int i = 0; i < words.Length; i++)
        {
            Mem.writeWord(at + i, (ushort)words[i]);
        }
    }

    private static int mkGrayParm(int yOffset, int widthMinusOne, int heightMinusOne)
        => ((yOffset & 0x0F) << 8) | ((widthMinusOne & 0x0F) << 4) | (heightMinusOne & 0x0F);

    [Fact] public void test_BITBLT_brick_none_none()
    {
        setupStartBitmap();
        int brickAt = testLongMem + 4096;
        mkBrick(brickAt,
            0b0011001100110011,
            0b0011001100110011,
            0b1100110011001100,
            0b1100110011001100);

        mkBitBltArg(testShortMem,
            testLongMem + 42, 2, 6 * 16,
            brickAt + 1, 3, mkGrayParm(1, 0, 3),
            34, 9,
            flg_gray, flg_srcFuncNull, flg_dstFuncNull);
        mkStack(testShortMem);
        Ch08_Block_Transfers.ESC_x2B_BITBLT();
        checkStack();

        checkBitmap("BITBLT_brick_none_none", testLongMem, 6,
            "                                                                                                ",
            "                XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX                                                ",
            "                X                              X                                                ",
            "                X                              X                                                ",
            "                X                              X                                                ",
            "                XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX                                                ",
            "                                                                                                ",
            "  X  XX  XX  XX  XX  XX  XX  XX  XX                                                             ",
            "   XX  XX  XX  XX  XX  XX  XX  XX  X                                                            ",
            "   XX  XX  XX  XX  XX  XX  XX  XX  X                                                            ",
            "  X  XX  XX  XX  XX  XX  XX  XX  XX                                                             ",
            "  X  XX  XX  XX  XX  XX  XX  XX  XX                                                             ",
            "   XX  XX  XX  XX  XX  XX  XX  XX  X                                                            ",
            "   XX  XX  XX  XX  XX  XX  XX  XX  X                                                            ",
            "  X  XX  XX  XX  XX  XX  XX  XX  XX                                                             ",
            "  X  XX  XX  XX  XX  XX  XX  XX  XX                                                             ");
    }

    [Fact] public void test_BITBLT_brick_complement_none()
    {
        setupStartBitmap();
        int brickAt = testLongMem + 4096;
        mkBrick(brickAt,
            0b0011001100110011,
            0b0011001100110011,
            0b1100110011001100,
            0b1100110011001100);

        mkBitBltArg(testShortMem,
            testLongMem + 42, 2, 6 * 16,
            brickAt + 1, 3, mkGrayParm(1, 0, 3),
            34, 9,
            flg_gray, flg_srcFuncComplement, flg_dstFuncNull);
        mkStack(testShortMem);
        Ch08_Block_Transfers.ESC_x2B_BITBLT();
        checkStack();

        checkBitmap("BITBLT_brick_complement_none", testLongMem, 6,
            "                                                                                                ",
            "                XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX                                                ",
            "                X                              X                                                ",
            "                X                              X                                                ",
            "                X                              X                                                ",
            "                XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX                                                ",
            "                                                                                                ",
            "   XX  XX  XX  XX  XX  XX  XX  XX  X                                                            ",
            "  X  XX  XX  XX  XX  XX  XX  XX  XX                                                             ",
            "  X  XX  XX  XX  XX  XX  XX  XX  XX                                                             ",
            "   XX  XX  XX  XX  XX  XX  XX  XX  X                                                            ",
            "   XX  XX  XX  XX  XX  XX  XX  XX  X                                                            ",
            "  X  XX  XX  XX  XX  XX  XX  XX  XX                                                             ",
            "  X  XX  XX  XX  XX  XX  XX  XX  XX                                                             ",
            "   XX  XX  XX  XX  XX  XX  XX  XX  X                                                            ",
            "   XX  XX  XX  XX  XX  XX  XX  XX  X                                                            ");
    }

    [Fact] public void test_COLORBLT_BW_1024x640_pattern()
    {
        int bitmapStart = testLongMem;
        int bitmapEndPlusOne = bitmapStart + (1024 * 640 / 16);
        int bitmapStartPage = (int)((uint)bitmapStart >> 8);
        int bitmapEndPagePlusOne = (int)((uint)bitmapEndPlusOne >> 8);
        int rPageStartMinusOne = Mem.getVPageRealPage(bitmapStartPage - 1);
        int rPageEndPlusOne = Mem.getVPageRealPage(bitmapEndPagePlusOne);

        mkShortMem(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        mkLocalFrame(
            0x1234,
            testLongMemLow, testLongMemHigh,
            0x0000,
            1024,
            testShortMem, (int)((uint)Cpu.MDS >> 16),
            0x0000,
            4096,
            1024,
            640,
            0x7000,
            0, 0);

        mkStack(Cpu.LF + 1);

        try
        {
            Mem.setMap("test_COLORBLT_BW_1024x640_pattern", bitmapStartPage - 1, 0, PrincOpsDefs.MAPFLAGS_VACANT);
            Mem.setMap("test_COLORBLT_BW_1024x640_pattern", bitmapEndPagePlusOne, 0, PrincOpsDefs.MAPFLAGS_VACANT);

            mkStack(Cpu.LF + 1);
            Ch08_Block_Transfers.ESC_xC0_COLORBLT();
        }
        finally
        {
            Mem.setMap("test_COLORBLT_BW_1024x640_pattern", bitmapStartPage - 1, rPageStartMinusOne, PrincOpsDefs.MAPFLAGS_CLEAR);
            Mem.setMap("test_COLORBLT_BW_1024x640_pattern", bitmapEndPagePlusOne, rPageEndPlusOne, PrincOpsDefs.MAPFLAGS_CLEAR);
        }
    }

    [Fact] public void test_BITBLT_brick_none_none_ppl9()
    {
        setupStartBitmap();
        int brickAt = testLongMem + 4096;
        mkBrick(brickAt, 0xFFFF);

        mkBitBltArg(testShortMem,
            testLongMem, 0, 9,
            brickAt, 0, mkGrayParm(0, 0, 0),
            9, 16,
            flg_gray, flg_srcFuncNull, flg_dstFuncNull);
        mkStack(testShortMem);
        Ch08_Block_Transfers.ESC_x2B_BITBLT();
        checkStack();

        checkBitmap("BITBLT_brick_none_none_ppl9", testLongMem, 1,
            "XXXXXXXXXXXXXXXX",
            "XXXXXXXXXXXXXXXX",
            "XXXXXXXXXXXXXXXX",
            "XXXXXXXXXXXXXXXX",
            "XXXXXXXXXXXXXXXX",
            "XXXXXXXXXXXXXXXX",
            "XXXXXXXXXXXXXXXX",
            "XXXXXXXXXXXXXXXX",
            "XXXXXXXXXXXXXXXX",
            "                ",
            "                ");
    }

    private static int ch(char first, char second)
        => ((first << 8) & 0xFF00) | (second & 0xFF);

    private static string getLongMemStringEquiv(int wordCount)
    {
        StringBuilder sb = new();
        for (int i = 0; i < wordCount; i++)
        {
            int w = Mem.readWord(testLongMem + i);
            int c = (int)((uint)w >> 8);
            sb.Append((c >= 0x20 && c < 0x7F) ? (char)c : 'µ');
            c = w & 0xFF;
            sb.Append((c >= 0x20 && c < 0x7F) ? (char)c : 'µ');
        }
        return sb.ToString();
    }

    [Fact] public void test_BITBLT_as_BYTBLTR_misuse_backwrd()
    {
        mkLongMem(
            ch('q', 'w'), ch('e', 'r'), ch('t', 'z'), ch('u', 'i'), ch('o', 'p'),
            ch('\n', 'a'), ch('s', 'd'), ch('f', 'g'), ch('h', 'j'), ch('k', 'l'),
            ch('\n', 'y'), ch('x', 'c'), ch('v', 'b'), ch('n', 'm'), ch('%', '%'), ch('+', '+'));

        mkBitBltArg(testShortMem,
            testLongMem + 0x0E, 0x0000, -8,
            testLongMem + 0x0D, 0x0008, -8,
            8, 23,
            flg_backward);

        mkStack(testShortMem);
        Ch08_Block_Transfers.ESC_x2B_BITBLT();
        checkStack();

        string expected = "qwertzzuiopµasdfghjklµyxcvbnm%++";
        string result = getLongMemStringEquiv(16);
        Assert.Equal(expected, result);

        checkLongMem(
            ch('q', 'w'), ch('e', 'r'), ch('t', 'z'), ch('z', 'u'), ch('i', 'o'), ch('p', '\n'),
            ch('a', 's'), ch('d', 'f'), ch('g', 'h'), ch('j', 'k'), ch('l', '\n'),
            ch('y', 'x'), ch('c', 'v'), ch('b', 'n'), ch('m', '%'), ch('+', '+'));
    }

    [Fact] public void test_BITBLT_as_BYTBLT_misuse_forward()
    {
        mkLongMem(
            ch('q', 'w'), ch('e', 'r'), ch('t', 'z'), ch('u', 'i'), ch('o', 'p'),
            ch('\n', 'a'), ch('s', 'd'), ch('f', 'g'), ch('h', 'j'), ch('k', 'l'),
            ch('\n', 'y'), ch('x', 'c'), ch('v', 'b'), ch('n', 'm'), ch('%', '%'), ch('+', '+'));

        mkBitBltArg(testShortMem,
            testLongMem + 0x02, 0x0008, 8,
            testLongMem + 0x03, 0x0000, 8,
            8, 22,
            flg_forward);

        mkStack(testShortMem);
        Ch08_Block_Transfers.ESC_x2B_BITBLT();
        checkStack();

        string expected = "qwertuiopµasdfghjklµyxcvbnmm%%++";
        string result = getLongMemStringEquiv(16);
        Assert.Equal(expected, result);

        checkLongMem(
            ch('q', 'w'), ch('e', 'r'), ch('t', 'u'), ch('i', 'o'), ch('p', '\n'),
            ch('a', 's'), ch('d', 'f'), ch('g', 'h'), ch('j', 'k'), ch('l', '\n'),
            ch('y', 'x'), ch('c', 'v'), ch('b', 'n'), ch('m', 'm'), ch('%', '%'), ch('+', '+'));
    }
}
