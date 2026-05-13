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

// Implementation of instructions not documented in any available PrincOps
// document, reconstructed from the Dawn (Don Woodward) and Guam (Yasuhiro
// Hasegawa) sources.
public static class ChXX_Undocumented
{
    public enum MachineType
    {
        altoI     = 1,
        altoII    = 2,
        altoIIXM  = 3,
        dolphin   = 4,
        dorado    = 5,
        dandelion = 6,
        dicentra  = 7,
        daybreak  = 8,
        daisy     = 9,
        kiku      = 10,
        daylight  = 11,
        tridlion  = 12,
        dahlia    = 13,
    }

    public static MachineType machineType = MachineType.daybreak;

    // VERSION — Microcode Version (from 6085 microcode, not in PrincOps).
    //  Pushes two words:
    //    [0..3]   machineType
    //    [4..7]   majorVersion (incompatible-change counter)
    //    [8..13]  unused
    //    [14]     floatingPoint?
    //    [15]     cedar?
    //  Second word: releaseDate (days since January 1, 1901)
    public static readonly OpImpl ESC_x2F_VERSION = () =>
    {
        Cpu.push(((int)machineType << 12) | 0x0002); // type + floatingPoint(?)
        Cpu.push(0x8482);                            // Jan 1 1993 (?)
    };

    // STOPEMULATOR — Stop emulator.
    // Java upstream uses ProcessorAgent.getJavaTime to render `timeToRestart`
    // as a readable date; that's a Phase D agent. Phase B just logs the raw
    // value and throws MesaStopped.
    public static readonly OpImpl ESC_x8B_STOPEMULATOR = () =>
    {
        int timeToRestart = Cpu.popLong();
        string msg = "Mesa engine stopped by instruction STOPEMULATOR";
        Console.WriteLine($"\n**\n** {msg} [ timeToRestart: 0x{timeToRestart:X8} ]\n**\n");
        throw new Cpu.MesaStopped(msg);
    };

    // SUSPEND — Suspend emulator (treated like STOPEMULATOR since how the
    // processor is restarted is unknown).
    public static readonly OpImpl ESC_x8D_SUSPEND = () =>
    {
        string msg = "Mesa engine stopped by instruction SUSPEND";
        Console.WriteLine($"\n**\n** {msg}\n**\n");
        throw new Cpu.MesaStopped(msg);
    };

    // VMFIND — binary search over the VM database PRun array.
    // VM.interval : 4 words = struct { CARD32 page ; CARD32 count }
    private const int VMDataInternal_PRun_FIRST    = 0;
    private const int VMDataInternal_countRunPad   = 1;
    private const int VMDataInternal_Run_SIZE      = 14; // words
    private const int VMDataInternal_pRunFirst     = VMDataInternal_PRun_FIRST + (VMDataInternal_countRunPad * VMDataInternal_Run_SIZE);

    public static readonly OpImpl OPC_xBF_VMFIND = () =>
    {
        int pRunTop = Cpu.pop();
        int rBase = Cpu.popLong();
        int page = Cpu.popLong();

        ushort found = PrincOpsDefs.FALSE;
        int pRun = 0;

        int pageTop = 0 + Mem.getVirtualPagesSize();
        const int indexRunFirst = (VMDataInternal_pRunFirst - VMDataInternal_PRun_FIRST) / VMDataInternal_Run_SIZE;

        if (page >= pageTop) { Cpu.ERROR("VMFIND :: beyondVM (page >= pageTop)"); }

        int indexRunLow = indexRunFirst;
        int indexRunHigh = (pRunTop - VMDataInternal_PRun_FIRST) / VMDataInternal_Run_SIZE;

        while (true)
        {
            int indexRun = (indexRunLow + indexRunHigh) / 2;
            int pageComp = Mem.readDblWord(rBase + VMDataInternal_PRun_FIRST + (indexRun * VMDataInternal_Run_SIZE) + 0);

            if (page < pageComp)
            {
                indexRunHigh = indexRun - 1;
            }
            else if (pageComp < page)
            {
                indexRunLow = indexRun + 1;
            }
            else
            {
                pRun = 0 + indexRun * VMDataInternal_Run_SIZE;
                found = PrincOpsDefs.TRUE;
                break;
            }

            if (indexRunHigh < indexRunLow)
            {
                if (indexRunLow == indexRunFirst)
                {
                    pRun = VMDataInternal_pRunFirst;
                    found = PrincOpsDefs.FALSE;
                }
                else
                {
                    pRun = VMDataInternal_PRun_FIRST + (indexRunHigh * VMDataInternal_Run_SIZE);
                    int intervalPage  = Mem.readDblWord(rBase + pRun + 0);
                    int intervalCount = Mem.readDblWord(rBase + pRun + 2);
                    if (page < (intervalPage + intervalCount))
                    {
                        found = PrincOpsDefs.TRUE;
                    }
                    else
                    {
                        pRun += VMDataInternal_Run_SIZE;
                        found = PrincOpsDefs.FALSE;
                    }
                }
                break;
            }
        }

        Cpu.push(found);
        Cpu.push(pRun);
    };

    // Undocumented Fuji-Xerox instructions implemented in Yasuhiro Hasegawa's
    // Guam emulator. Behavior approximated by the upstream Java port.

    public static readonly OpImpl ESC_x8C_FujiXerox_undocumented_o214 = () =>
    {
        Cpu.popLong();
        Cpu.popLong();
        Cpu.popLong();
        Cpu.push((ushort)0);
    };

    public static readonly OpImpl ESC_xC5_FujiXerox_undocumented_o305 = () =>
    {
        // no-op (upstream)
    };

    public static readonly OpImpl ESC_xC6_FujiXerox_undocumented_o306 = () =>
    {
        Cpu.pop();
        Cpu.push((ushort)0);
    };

    public static void RegisterAll()
    {
        Opcodes.InstallEsc(0x2F, "VERSION",      ESC_x2F_VERSION);
        Opcodes.InstallEsc(0x8B, "STOPEMULATOR", ESC_x8B_STOPEMULATOR);
        Opcodes.InstallEsc(0x8D, "SUSPEND",      ESC_x8D_SUSPEND);
        Opcodes.Install(0xBF, "VMFIND",          OPC_xBF_VMFIND);
        Opcodes.InstallEsc(0x8C, "FX_o214",      ESC_x8C_FujiXerox_undocumented_o214);
        Opcodes.InstallEsc(0xC5, "FX_o305",      ESC_xC5_FujiXerox_undocumented_o305);
        Opcodes.InstallEsc(0xC6, "FX_o306",      ESC_xC6_FujiXerox_undocumented_o306);
    }
}
