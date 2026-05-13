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

// Unittests for instructions implemented in class Ch03_Memory_Organization.
public sealed class Ch03_MemoryOrganizationTest : AbstractInstructionTest
{
    private static int getRpForVp(int vp)
    {
        Cpu.pushLong(vp);

        Ch03_Memory_Organization.ESC_x09_GMF();

        int rp = Cpu.popLong();
        _ = Cpu.pop(); // drop mf

        return rp;
    }

    private static ushort getMfForVp(int vp)
    {
        Cpu.pushLong(vp);

        Ch03_Memory_Organization.ESC_x09_GMF();

        _ = Cpu.popLong(); // drop rp
        ushort mf = Cpu.pop();

        return mf;
    }

    private static void setMfForVp(int vp, ushort newMf)
    {
        Cpu.pushLong(vp);
        Cpu.push(newMf);

        Ch03_Memory_Organization.ESC_x08_SMF();
        _ = Cpu.popLong(); // drop rp
        _ = Cpu.pop();     // drop mf
    }

    private static void setMap(int vp, ushort mf, int rp)
    {
        Cpu.pushLong(vp);
        Cpu.pushLong(rp);
        Cpu.push(mf);

        Ch03_Memory_Organization.ESC_x07_SM();
    }

    private const ushort MF_CLEAN   = 0x0000;
    private const ushort MF_READ    = 0x0001;
    private const ushort MF_WRITTEN = 0x0003;
    private const ushort MF_VACANT  = 0x0006;

    // Java @After — restore the initial page mapping after every test that
    // potentially mutated it.
    protected override void OnAfterTest()
    {
        Mem.createInitialPageMappingGuam();
    }

    [Fact]
    public void testVpToRealMapping()
    {
        int rp = 0x00000100;
        int vp = 0;
        int lp = 0x00000011;
        ushort data = 0x1234;

        // implant the start value
        Mem.writeWord((rp << 8) | lp, data);

        // place the real page in virtual page 0 to start testing
        setMfForVp(rp, MF_VACANT); // unmap the test rp, located where vp == rp
        Assert.True(MF_VACANT == getMfForVp(rp), $"getMfForVp(rp) after unmapping rp: expected MF_VACANT, got 0x{getMfForVp(rp):X4}");
        setMap(vp, MF_CLEAN, rp);

        while (vp < firstUnmappedPage)
        {
            // unmap the real page
            setMfForVp(vp, MF_VACANT);

            // map the real page to the next virtual page to test
            vp++;
            lp += 256;
            setMap(vp, MF_CLEAN, rp);

            // check initial state
            ushort actualMf = getMfForVp(vp);
            Assert.True(MF_CLEAN == actualMf, $"mapFlags for mapped vp (vp={vp}): expected MF_CLEAN, got 0x{actualMf:X4}");
            int actualRp = getRpForVp(vp);
            Assert.True(rp == actualRp, $"rp for mapped vp (vp={vp}): expected 0x{rp:X8}, got 0x{actualRp:X8}");

            // read from the page
            ushort memValue = Mem.readWord(lp);
            Assert.True(data == memValue, $"value at mapped virtual page address (vp={vp}): expected 0x{data:X4}, got 0x{memValue:X4}");
            actualMf = getMfForVp(vp);
            Assert.True(MF_READ == actualMf, $"mapFlags for vp after read (vp={vp}): expected MF_READ, got 0x{actualMf:X4}");
            setMfForVp(vp, MF_CLEAN);

            // change data and write it to page
            data = (ushort)((data + 17) & 0xFFFF);
            Mem.writeWord(lp, data);
            actualMf = getMfForVp(vp);
            Assert.True(MF_WRITTEN == actualMf, $"mapFlags for vp after write (vp={vp}): expected MF_WRITTEN, got 0x{actualMf:X4}");
        }
    }
}
