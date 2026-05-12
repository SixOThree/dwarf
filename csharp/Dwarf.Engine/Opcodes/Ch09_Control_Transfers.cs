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

// Implementation of PrincOps 4.0 chapter 9: Control Transfers.
//
// Phase B note: no unit tests for this chapter (see unittest/package-info.java —
// these instructions need a real OS like Pilot to drive them). Implementations
// compile via Xfer/Processes stubs that throw `NotImplementedException("Phase C")`
// at runtime. Phase C wires up the real Xfer machinery; Phase D loads Pilot.
public static class Ch09_Control_Transfers
{
    // ==================================================================
    // 9.2.3 Frame Allocation
    // ==================================================================

    public static readonly OpImpl ESC_x0A_AF = () =>
    {
        int fsi = Cpu.pop();
        Cpu.push(Xfer.alloc(fsi));
    };

    public static readonly OpImpl ESC_x0B_FF = () =>
    {
        int frame = Cpu.pop();
        Xfer.free(frame);
    };

    // ==================================================================
    // 9.4.1 Local Function Calls
    // ==================================================================

    // LFC — Local Function Call (PrincOps <= 4.0 / old GF)
    public static readonly OpImpl OPCo_xED_LFC_word = () =>
    {
        // ATTENTION: deviation from PrincOps to keep return PC accurate —
        // PC must be saved BEFORE reading the next code word, otherwise the
        // return PC ends up 2 bytes short.
        int nPC = Mem.getNextCodeWord();
        Mem.writeMDSWord(Cpu.LF, PrincOpsDefs.LocalOverhead_pc, Cpu.PC);
        if (nPC == 0) { Cpu.unboundTrap(0); }

        ushort word = Mem.readCode(nPC / 2);
        int nFsi = ((nPC & 0x0001) == 0) ? word >>> 8 : word & 0xFF;
        int nLF = Xfer.alloc(nFsi);
        nPC++;

        Mem.writeMDSWord(nLF, PrincOpsDefs.LocalOverhead_globallink, Cpu.GF16);
        Mem.writeMDSWord(nLF, PrincOpsDefs.LocalOverhead_returnlink, Cpu.LF);

        Cpu.LF = nLF;
        Cpu.PC = nPC;

        Xfer.impl.checkForXferTraps(((Cpu.GF16 | 0x0001) << 16) | ((Cpu.PC - 1) & 0x0000FFFF), Xfer.XferType.xlocalCall);
    };

    // LFC — Local Function Call (PrincOps > 4.0 / new GF)
    public static readonly OpImpl OPCn_xED_LFC_word = () =>
    {
        int nPC = Mem.getNextCodeWord();
        Mem.writeMDSWord(Cpu.LF, PrincOpsDefs.LocalOverhead_pc, Cpu.PC);
        if (nPC == 0) { Cpu.unboundTrap(0); }

        ushort word = Mem.readCode(nPC / 2);
        int nLF = Xfer.alloc(((nPC & 0x0001) == 0) ? word >>> 8 : word & 0xFF);
        nPC++;

        Mem.writeMDSWord(nLF, PrincOpsDefs.LocalOverhead_globallink, Cpu.GFI);
        Mem.writeMDSWord(nLF, PrincOpsDefs.LocalOverhead_returnlink, Cpu.LF);

        Cpu.LF = nLF;
        Cpu.PC = nPC;

        Xfer.impl.checkForXferTraps((Cpu.GFI << 16) | ((Cpu.PC - 1) & 0x0000FFFF), Xfer.XferType.xlocalCall);
    };

    // ==================================================================
    // 9.4.2 External Function Calls
    // ==================================================================

    private static void call(int controlLink)
    {
        Mem.writeMDSWord(Cpu.LF, PrincOpsDefs.LocalOverhead_pc, Cpu.PC);
        Xfer.impl.xfer(controlLink, Cpu.LF, Xfer.XferType.xcall, false);
    }

    public static readonly OpImpl OPC_xDF_EFC0  = () => { call(Xfer.impl.fetchLink(0)); };
    public static readonly OpImpl OPC_xE0_EFC1  = () => { call(Xfer.impl.fetchLink(1)); };
    public static readonly OpImpl OPC_xE1_EFC2  = () => { call(Xfer.impl.fetchLink(2)); };
    public static readonly OpImpl OPC_xE2_EFC3  = () => { call(Xfer.impl.fetchLink(3)); };
    public static readonly OpImpl OPC_xE3_EFC4  = () => { call(Xfer.impl.fetchLink(4)); };
    public static readonly OpImpl OPC_xE4_EFC5  = () => { call(Xfer.impl.fetchLink(5)); };
    public static readonly OpImpl OPC_xE5_EFC6  = () => { call(Xfer.impl.fetchLink(6)); };
    public static readonly OpImpl OPC_xE6_EFC7  = () => { call(Xfer.impl.fetchLink(7)); };
    public static readonly OpImpl OPC_xE7_EFC8  = () => { call(Xfer.impl.fetchLink(8)); };
    public static readonly OpImpl OPC_xE8_EFC9  = () => { call(Xfer.impl.fetchLink(9)); };
    public static readonly OpImpl OPC_xE9_EFC10 = () => { call(Xfer.impl.fetchLink(10)); };
    public static readonly OpImpl OPC_xEA_EFC11 = () => { call(Xfer.impl.fetchLink(11)); };
    public static readonly OpImpl OPC_xEB_EFC12 = () => { call(Xfer.impl.fetchLink(12)); };

    public static readonly OpImpl OPC_xEC_EFCB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        call(Xfer.impl.fetchLink(alpha));
    };

    public static readonly OpImpl OPC_xEE_SFC = () =>
    {
        int controlLink = Cpu.popLong();
        call(controlLink);
    };

    public static readonly OpImpl OPC_xF0_KFCB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        int controlLink = Mem.readMDSDblWord(PrincOpsDefs.getSdMdsPtr(alpha));
        call(controlLink);
    };

    // ==================================================================
    // 9.4.3 Nested Function Calls
    // ==================================================================

    public static readonly OpImpl OPC_x7A_LKB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        Cpu.recover();
        int shortControlLink = Cpu.pop();
        Mem.writeMDSWord(Cpu.LF, shortControlLink - alpha);
    };

    // ==================================================================
    // 9.4.4 Returns
    // ==================================================================

    public static readonly OpImpl OPC_xEF_RET = () =>
    {
        int controlLink = Mem.readMDSWord(Cpu.LF, PrincOpsDefs.LocalOverhead_returnlink);
        Xfer.impl.xfer(controlLink, 0, Xfer.XferType.xreturn, true);
    };

    // ==================================================================
    // 9.4.5 Coroutine Transfers
    // ==================================================================

    public static readonly OpImpl ESC_x0D_PO = () =>
    {
        _ = Cpu.pop(); // reserved
        int portLink = Cpu.pop();
        Mem.writeMDSWord(Cpu.LF, PrincOpsDefs.LocalOverhead_pc, Cpu.PC);
        Mem.writeMDSWord(portLink, PrincOpsDefs.Port_inport, Cpu.LF);
        int outport = Mem.readMDSDblWord(portLink, PrincOpsDefs.Port_outport);
        Xfer.impl.xfer(outport, portLink, Xfer.XferType.xport, false);
    };

    public static readonly OpImpl ESC_x0E_POR = () =>
    {
        ESC_x0D_PO();
    };

    public static readonly OpImpl ESC_x0C_PI = () =>
    {
        Cpu.recover();
        Cpu.recover();
        int src = Cpu.pop();
        int port = Cpu.pop();
        Mem.writeMDSWord(port, PrincOpsDefs.Port_inport, 0);
        if (src != 0)
        {
            Mem.writeMDSWord(port, PrincOpsDefs.Port_outport, src);
        }
    };

    // ==================================================================
    // 9.4.6 Link Instructions
    // ==================================================================

    private static int controlLinkAsLongPointer(int controlLink) => controlLink;

    public static readonly OpImpl OPC_x77_LLKB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        Cpu.pushLong(Xfer.impl.fetchLink(alpha));
    };

    public static readonly OpImpl OPC_x78_RKIB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        int longPointer = controlLinkAsLongPointer(Xfer.impl.fetchLink(alpha));
        Cpu.push(Mem.readWord(longPointer));
    };

    public static readonly OpImpl OPC_x79_RKDIB_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        int longPointer = controlLinkAsLongPointer(Xfer.impl.fetchLink(alpha));
        Cpu.push(Mem.readWord(longPointer));
        Cpu.push(Mem.readWord(longPointer + 1));
    };

    // ==================================================================
    // 9.5.3 Trap Handlers
    // ==================================================================

    public static readonly OpImpl ESC_x20_DSK_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        int state = Cpu.LF + alpha;
        Cpu.saveStack(Cpu.lengthenPointer(state));
    };

    public static readonly OpImpl ESC_x23_LSK_alpha = () =>
    {
        int alpha = Mem.getNextCodeByte();
        int state = Cpu.LF + alpha;
        Cpu.loadStack(Cpu.lengthenPointer(state));
    };

    public static readonly OpImpl ESC_x22_XF_alpha = () =>
    {
        int ptr = Cpu.LF + Mem.getNextCodeByte();
        Xfer.impl.xfer(
            Mem.readMDSDblWord(ptr, Cpu.TransferDescriptor_dst),
            Mem.readMDSWord(ptr, Cpu.TransferDescriptor_src),
            Xfer.XferType.xfer,
            true);
    };

    public static readonly OpImpl ESC_x21_XE_alpha = () =>
    {
        // BEGIN ENABLE Abort => ERROR
        try
        {
            int ptr = Cpu.LF + Mem.getNextCodeByte();
            Mem.writeMDSWord(Cpu.LF, PrincOpsDefs.LocalOverhead_pc, Cpu.PC);
            Xfer.impl.xfer(
                Mem.readMDSDblWord(ptr, Cpu.TransferDescriptor_dst),
                Mem.readMDSWord(ptr, Cpu.TransferDescriptor_src),
                Xfer.XferType.xfer,
                false);
            Processes.enableInterrupts();
        }
        catch (Cpu.MesaAbort)
        {
            Cpu.ERROR("saveProcess :: received Abort-exception");
        }
    };

    // ==================================================================
    // 9.5.4 Breakpoints
    // ==================================================================

    public static readonly OpImpl OPC_x3D_BRK = () =>
    {
        if (Cpu.breakByte == 0)
        {
            Cpu.breakTrap();
        }
        else
        {
            Opcodes.dispatch(Cpu.breakByte);
            Cpu.breakByte = 0;
        }
    };

    // ==================================================================
    // (changed chapters) 9.1.4.2 Descriptor Instruction
    // ==================================================================

    public static readonly OpImpl OPCn_xFD_DESC_word = () =>
    {
        int word = Mem.getNextCodeWord();
        Cpu.push(Cpu.GFI | 0x0003);
        Cpu.push(word);
    };

    // ==================================================================
    // Registration
    // ==================================================================

    public static void RegisterAll()
    {
        // 9.2.3 Frame Allocation
        Opcodes.InstallEsc(0x0A, "AF", ESC_x0A_AF);
        Opcodes.InstallEsc(0x0B, "FF", ESC_x0B_FF);

        // 9.4.1 Local Function Calls — old/new GF
        Opcodes.InstallOld(0xED, "LFC", OPCo_xED_LFC_word);
        Opcodes.InstallNew(0xED, "LFC", OPCn_xED_LFC_word);

        // 9.4.2 External Function Calls
        Opcodes.Install(0xDF, "EFC0",  OPC_xDF_EFC0);
        Opcodes.Install(0xE0, "EFC1",  OPC_xE0_EFC1);
        Opcodes.Install(0xE1, "EFC2",  OPC_xE1_EFC2);
        Opcodes.Install(0xE2, "EFC3",  OPC_xE2_EFC3);
        Opcodes.Install(0xE3, "EFC4",  OPC_xE3_EFC4);
        Opcodes.Install(0xE4, "EFC5",  OPC_xE4_EFC5);
        Opcodes.Install(0xE5, "EFC6",  OPC_xE5_EFC6);
        Opcodes.Install(0xE6, "EFC7",  OPC_xE6_EFC7);
        Opcodes.Install(0xE7, "EFC8",  OPC_xE7_EFC8);
        Opcodes.Install(0xE8, "EFC9",  OPC_xE8_EFC9);
        Opcodes.Install(0xE9, "EFC10", OPC_xE9_EFC10);
        Opcodes.Install(0xEA, "EFC11", OPC_xEA_EFC11);
        Opcodes.Install(0xEB, "EFC12", OPC_xEB_EFC12);
        Opcodes.Install(0xEC, "EFCB",  OPC_xEC_EFCB_alpha);
        Opcodes.Install(0xEE, "SFC",   OPC_xEE_SFC);
        Opcodes.Install(0xF0, "KFCB",  OPC_xF0_KFCB_alpha);

        // 9.4.3 Nested Function Calls
        Opcodes.Install(0x7A, "LKB", OPC_x7A_LKB_alpha);

        // 9.4.4 Returns
        Opcodes.Install(0xEF, "RET", OPC_xEF_RET);

        // 9.4.5 Coroutine Transfers
        Opcodes.InstallEsc(0x0C, "PI",  ESC_x0C_PI);
        Opcodes.InstallEsc(0x0D, "PO",  ESC_x0D_PO);
        Opcodes.InstallEsc(0x0E, "POR", ESC_x0E_POR);

        // 9.4.6 Link Instructions
        Opcodes.Install(0x77, "LLKB", OPC_x77_LLKB_alpha);
        Opcodes.Install(0x78, "RKIB", OPC_x78_RKIB_alpha);
        Opcodes.Install(0x79, "RKDIB", OPC_x79_RKDIB_alpha);

        // 9.5.3 Trap Handlers
        Opcodes.InstallEsc(0x20, "DSK", ESC_x20_DSK_alpha);
        Opcodes.InstallEsc(0x21, "XE",  ESC_x21_XE_alpha);
        Opcodes.InstallEsc(0x22, "XF",  ESC_x22_XF_alpha);
        Opcodes.InstallEsc(0x23, "LSK", ESC_x23_LSK_alpha);

        // 9.5.4 Breakpoints
        Opcodes.Install(0x3D, "BRK", OPC_x3D_BRK);

        // 9.1.4.2 Descriptor (new GF only)
        Opcodes.InstallNew(0xFD, "DESC", OPCn_xFD_DESC_word);
    }
}
