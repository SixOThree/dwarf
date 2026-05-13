/*
Copyright (c) 2026, Matthew Dugal (C# .NET 10 port)
All rights reserved.

See csharp/BSD3-HEADER.txt for the full BSD-3 license terms.
*/

using Dwarf.Engine;

namespace Dwarf.Tests;

// Phase A smoke test: confirms that AbstractInstructionTest's fixture wires up
// Mem + Cpu + the ChkThrower correctly. No opcode dispatch is exercised — that
// arrives in Phase B with the Ch0X tests.
public sealed class FixtureSmokeTest : AbstractInstructionTest
{
    [Fact]
    public void Fixture_initializes_memory_and_registers()
    {
        // memory is allocated
        Assert.NotNull(Mem.mem);
        Assert.True(Mem.mem.Length > 0);

        // MDS / CB set to test-fixture values
        Assert.Equal(128 * 1024, Cpu.MDS);
        Assert.NotEqual(0, Cpu.CB);

        // LF points into the AV-table-allocated frame for fsi=14
        Assert.NotEqual(0, Cpu.LF);

        // stack is empty
        Assert.Equal(0, Cpu.SP);
        Assert.Equal(0, Cpu.savedSP);

        // thrower is our ChkThrower
        Assert.Same(mesaException, Cpu.thrower);
    }

    [Fact]
    public void Mem_readWord_writeWord_round_trip_in_MDS_zone()
    {
        int lp = Cpu.MDS + 0x1000; // safe address in MDS
        Mem.writeWord(lp, 0xABCD);
        ushort got = Mem.readWord(lp);
        Assert.Equal((ushort)0xABCD, got);
    }

    [Fact]
    public void Stack_push_pop_round_trip()
    {
        Cpu.push((ushort)0x1234);
        Cpu.push((ushort)0x5678);
        Assert.Equal(2, Cpu.SP);
        Assert.Equal((ushort)0x5678, Cpu.pop());
        Assert.Equal((ushort)0x1234, Cpu.pop());
        Assert.Equal(0, Cpu.SP);
    }

    [Fact]
    public void Unexpected_pointerTrap_fails_the_test_via_ChkThrower()
    {
        // The default thrower (installed by the fixture) is a ChkThrower with all
        // expect_* flags false. Calling a trap method must therefore signal it as
        // "unexpected" before throwing MesaTrapOrFault. We catch the inner
        // Assert.Fail() exception and translate it.
        Assert.Throws<Xunit.Sdk.FailException>(() =>
        {
            // Without setting expect_signalPointerTrap = true, this is unexpected.
            Cpu.pointerTrap();
        });
    }

    [Fact]
    public void Expected_pointerTrap_throws_MesaTrapOrFault()
    {
        mesaException.expect_signalPointerTrap = true;
        Assert.Throws<MesaTrapOrFault>(() =>
        {
            Cpu.pointerTrap();
        });
    }
}
