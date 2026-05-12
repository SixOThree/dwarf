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

using Dwarf.Engine;
using Dwarf.Engine.Opcodes;

namespace Dwarf.Tests;

// Phase C liveness tests: confirm that Xfer, Processes, and InitialMesaMicrocode
// are fully wired (no Phase B stubs throwing NotImplementedException), that the
// embedded base144.raw resource loads, and that the engine can be initialized
// end-to-end without exploding.
//
// These are *liveness* tests, not correctness tests — we don't have a real germ
// file in the repo to boot, so we don't try to execute Mesa code from base144.
// Instead, we verify each Phase C deliverable can be invoked.
public sealed class SmokeTests
{
    private static void initEngine()
    {
        // Mem.initializeMemoryGuam throws if already initialized. The Phase B
        // AbstractInstructionTest runs the same one-shot init in its fixture,
        // and xUnit makes no order guarantees, so just tolerate the second
        // attempt — the previous test left Mem in a usable shape.
        try
        {
            Mem.initializeMemoryGuam(PrincOpsDefs.MIN_REAL_ADDRESSBITS, PrincOpsDefs.MIN_REAL_ADDRESSBITS + 1);
        }
        catch (InvalidOperationException)
        {
            // Mem already initialized by another test fixture. Fine.
        }
        Cpu.resetRegisters();
        Opcodes.initializeInstructionsPrincOps40();
    }

    [Fact]
    public void embedded_base144_resource_loads()
    {
        byte[] germ = Resources.LoadGermImage();

        // 1.44 MiB raw floppy: 2880 sectors x 512 bytes = 1,474,560 bytes.
        Assert.Equal(1_474_560, germ.Length);
    }

    [Fact]
    public void mem_init_and_cpu_reset_succeed()
    {
        initEngine();

        Assert.Equal(0, Cpu.MDS);
        Assert.Equal(0, Cpu.PC);
        Assert.Equal(0, Cpu.LF);
        Assert.Equal(0, Cpu.SP);
        Assert.True(Cpu.running);
    }

    [Fact]
    public void xfer_is_no_longer_a_stub()
    {
        initEngine();

        // Xfer.alloc requires the allocation vector at PrincOpsDefs.mALLOCATION_VECTOR
        // (page 1 of MDS) to be set up. With MDS == 0, AV is at virtual address 0x0100.
        // For an empty slot the alloc path takes a frame fault — which is *not*
        // NotImplementedException. Catching MesaERROR or any engine fault is fine;
        // the point is we no longer hit "Phase C: Xfer.alloc".
        try
        {
            Xfer.alloc(1);
        }
        catch (NotImplementedException)
        {
            Assert.Fail("Xfer.alloc is still a stub");
        }
        catch
        {
            // Any other exception (frame-fault, MesaERROR, MesaAbort) is fine —
            // it proves the real code path ran.
        }
    }

    [Fact]
    public void xfer_impl_is_no_longer_a_stub()
    {
        initEngine();

        // The xfer primitive itself: passing zero values is guaranteed to take
        // an unboundTrap / controlTrap path, but again the point is to confirm
        // we are running the real implementation rather than the Phase B stub.
        try
        {
            Xfer.impl.xfer(0, 0, Xfer.XferType.xcall, false);
        }
        catch (NotImplementedException)
        {
            Assert.Fail("Xfer.impl.xfer is still a stub");
        }
        catch
        {
            // Expected — zero controlLink with default Mesa state will trap.
        }
    }

    [Fact]
    public void xfer_type_enum_values_match_princops()
    {
        // PrincOps fixes the numeric values used in writeMDSWord at LF[2] during
        // checkForXferTraps. They must match the Java source exactly.
        Assert.Equal(0, (int)Xfer.XferType.xreturn);
        Assert.Equal(1, (int)Xfer.XferType.xcall);
        Assert.Equal(2, (int)Xfer.XferType.xlocalCall);
        Assert.Equal(3, (int)Xfer.XferType.xport);
        Assert.Equal(4, (int)Xfer.XferType.xfer);
        Assert.Equal(5, (int)Xfer.XferType.xtrap);
        Assert.Equal(6, (int)Xfer.XferType.xprocessSwitch);
        Assert.Equal(7, (int)Xfer.XferType.xunused);
    }

    [Fact]
    public void processes_helpers_are_no_longer_stubs()
    {
        initEngine();

        // These were Phase B stubs that threw NotImplementedException.
        // Now they should be pure bit-twiddling functions that return.
        ushort flags = 0;
        flags = Processes.setPsbFlagsWaiting(flags);
        Assert.True(Processes.isPsbFlagsWaiting(flags));
        flags = Processes.unsetPsbFlagsWaiting(flags);
        Assert.False(Processes.isPsbFlagsWaiting(flags));

        ushort link = 0;
        link = Processes.setPsbLink_priority(link, 5);
        Assert.Equal(5, Processes.getPsbLink_priority(link));

        ushort mon = 0;
        Assert.False(Processes.isMonitorLocked(mon));
        mon = Processes.setMonitorLocked(mon);
        Assert.True(Processes.isMonitorLocked(mon));

        // disableInterrupts / enableInterrupts are no longer stubs either.
        ushort wdcBefore = Cpu.WDC;
        Processes.disableInterrupts();
        Assert.Equal((ushort)(wdcBefore + 1), Cpu.WDC);
        Processes.enableInterrupts();
        Assert.Equal(wdcBefore, Cpu.WDC);
    }

    [Fact]
    public void processes_psbStart_constant_is_correct()
    {
        // Phase B placeholder had PDA_LP_header_ready = 0. Phase C must compute
        // it from the real PDA base address.
        Assert.Equal(Cpu.PDA, Processes.PDA_LP_header_ready);
        Assert.Equal(Cpu.PDA + 1, Processes.PDA_LP_header_count);

        // PsbStart depends on the header size; verify it lands at a small
        // positive value consistent with the PrincOps layout.
        Assert.Equal(8, Processes.PsbStart);
    }

    [Fact]
    public void initial_mesa_microcode_constants_match_pilot_header()
    {
        // Boot file numbers from Dawn :: PrincOps.h, octal-encoded in the Java
        // source. The C# port converted them to hex. Verify the numerical
        // identity is preserved.
        // octal 0_25200004037L
        Assert.Equal(2_852_128_799L, InitialMesaMicrocode.BFN_Daybreak_Germ);
        // octal 0_25200004040L = previous + 1 (since 037 + 1 = 040 in octal)
        Assert.Equal(2_852_128_800L, InitialMesaMicrocode.BFN_Daybreak_SimpleNetExec);
        // octal 0_25200004047L = base + 8 (010 octal)
        Assert.Equal(2_852_128_807L, InitialMesaMicrocode.BFN_Daybreak_Installer);

        // Boot request offsets/positions documented in Pilot.h.
        Assert.Equal(208, InitialMesaMicrocode.sFirstGermRequest);
        Assert.Equal(14, InitialMesaMicrocode.sGermSwitchesOffset);
    }

    [Fact]
    public void initial_mesa_microcode_loads_germ_bytes()
    {
        initEngine();

        // base144.raw is a floppy template, not a germ — 2880 pages exceeds
        // Germ_maxPages (96). loadGerm should clamp to 96 pages, log a warning,
        // and return false (plausibility check failed).
        byte[] germ = Resources.LoadGermImage();
        bool plausible = InitialMesaMicrocode.loadGerm(germ, firstPageIsGFT: false);

        Assert.False(plausible);

        // First page of base144 starts with the standard 1.44 MiB floppy boot
        // sector header. Pull byte 0 from virtual page 1 — should not be 0
        // (or should match the base144 byte ordering).
        // We can't easily assert exact content, but we *can* assert the
        // engine didn't crash during the load.
        Assert.True(Cpu.running, "engine still running after loadGerm");
    }

    [Fact]
    public void boot_request_setup_does_not_throw()
    {
        initEngine();

        // Each of these mutates the SD-table boot-request area. With a freshly
        // initialized MDS (== 0), the writes land at MDS-relative addresses that
        // resolve to real memory pages mapped in createInitialPageMappingGuam.
        InitialMesaMicrocode.setBootRequestDisk(0);
        InitialMesaMicrocode.setBootRequestFloppy(0);
        InitialMesaMicrocode.setBootRequestStream();
        InitialMesaMicrocode.setBootRequestEthernet(0, InitialMesaMicrocode.BFN_Daybreak_Germ);

        // Switches with octal-escape syntax — exercise the parser path.
        InitialMesaMicrocode.setBootSwitches("8Wy{|}\\346\\347\\350\\377");
    }

    [Fact]
    public void switch_to_new_princops_swaps_the_xfer_impl()
    {
        initEngine();

        // Default: 4.0 implementation.
        var implBefore = Xfer.impl;

        try
        {
            Xfer.switchToNewPrincOps();
            var implAfter = Xfer.impl;
            Assert.NotSame(implBefore, implAfter);
        }
        finally
        {
            // Restore for subsequent tests (xUnit gives no order guarantee).
            Xfer.impl = implBefore;
        }
    }
}
