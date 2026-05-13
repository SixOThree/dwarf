/*
Copyright (c) 2026, Matthew Dugal (C# .NET 10 port)
All rights reserved.

See csharp/BSD3-HEADER.txt for the full BSD-3 license terms.
*/

using Dwarf.Engine;
using Dwarf.Engine.Opcodes;

namespace Dwarf.Tests;

// Phase B smoke test for the dispatch pipeline: confirms that
// Opcodes.initializeInstructionsPrincOps40() fills the dispatch table from
// the chapter classes' RegisterAll methods, and that Opcodes.dispatch(byte)
// invokes the registered OpImpl.
//
// Ch03's own test class invokes ESC_x09_GMF() directly (bypassing dispatch);
// this test exercises the dispatch path itself.
public sealed class OpcodeDispatchSmokeTest : AbstractInstructionTest
{
    public OpcodeDispatchSmokeTest()
    {
        // Ensure the dispatch table is initialized (idempotent across tests).
        Opcodes.initializeInstructionsPrincOps40();
    }

    [Fact]
    public void Mode_is_V40_after_initializeInstructionsPrincOps40()
    {
        Assert.Equal(Opcodes.PrincOpsMode.V40, Opcodes.CurrentMode);
    }

    [Fact]
    public void Dispatch_LP_via_opcTable_lengthens_a_pointer()
    {
        // LP (opcode 0xF7): pop short pointer, push long pointer (MDS + ptr)
        Cpu.push((ushort)0x4321);
        Opcodes.dispatch(0xF7);
        int lengthened = Cpu.popLong();
        Assert.Equal(Cpu.MDS + 0x4321, lengthened);
    }

    [Fact]
    public void Dispatch_LP_with_zero_pushes_zero_long()
    {
        // LP convention: if the input is zero, the result is zero (not MDS).
        Cpu.push((ushort)0);
        Opcodes.dispatch(0xF7);
        int lengthened = Cpu.popLong();
        Assert.Equal(0, lengthened);
    }

    [Fact]
    public void Dispatch_ESC_route_invokes_RRMDS_via_esc_table()
    {
        // ESC_x79_RRMDS: pushes (MDS >> 16)
        // Path: opc[zESC] is the ESC dispatcher, which reads the next code byte
        // (0x79) and dispatches via escTable.

        // Set CB to a known location, place the ESC sub-opcode byte at PC=0
        // (in the high byte of word 0).
        Mem.writeWord(Cpu.CB, (ushort)(0x79 << 8)); // high byte = 0x79
        Cpu.PC = 0;

        Opcodes.dispatch(Opcodes.zESC);

        ushort top = Cpu.pop();
        Assert.Equal((ushort)(Cpu.MDS >>> PrincOpsDefs.WORD_BITS), top);
    }

    [Fact]
    public void Unregistered_opcode_falls_through_to_opcodeTrap()
    {
        // 0x00 is not registered by any chapter -> the prepare-table default
        // (Cpu.opcodeTrap) is still in place -> our ChkThrower should
        // intercept it.
        mesaException.expect_signalOpcodeTrap = true;
        Assert.Throws<MesaTrapOrFault>(() =>
        {
            Opcodes.dispatch(0x00);
        });
    }
}
