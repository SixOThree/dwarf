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

namespace Dwarf.Engine;

// Implementation of common functionality for PrincOps chapter "9 Control Transfers":
// allocation/freeing local frames and the XFER primitive and related functions.
//
// The core XFER primitive is present in 2 implementations to support both
// PrincOps variants:
//   - "old" PrincOps up to version 4.0: all global frames live in the main data
//     space; uses a 16-bit GF register.
//   - "new" PrincOps after version 4.0 (see "Changed Chapters"): global frames
//     can live outside the main data space; uses a 32-bit GF register plus a
//     table of global frames (located at GFT). The overhead area for global
//     frames also changed.
//
// The variants are exposed through IXferImpl with one nested implementation per
// PrincOps variant. The active implementation is reachable via Xfer.impl, which
// starts as PrincOps 4.0 and must be explicitly switched to the post-4.0
// implementation via switchToNewPrincOps().
//
// Important fine point: PrincOps defines ControlLink as a 32-bit quantity,
// accessed either as LONG UNSPECIFIED or as MACHINE DEPENDENT RECORD to read
// subfields. Mesa uses a mixed-endian ordering, so word 0 in a MACHINE
// DEPENDENT RECORD is the lower word of the 32-bit quantity, whereas the
// natural (big-endian) interpretation in Java/C# treats word 0 as the upper
// word. ControlLinks here are read/reinterpreted in the natural way to yield a
// (Java/C#) 32-bit quantity, but words 0 and 1 swap during the
// reinterpretation as a MACHINE DEPENDENT ControlLink.
public static class Xfer
{
    /*
     * local frames
     */

    private const int AV = PrincOpsDefs.mALLOCATION_VECTOR;
    private const int AVITEM_TAGMASK = 0x0003;
    private const int FSINDEX_LAST = 255;

    private static int /* LocalFrameHandle */ AVFrame(int avItem)
    {
        if ((avItem & AVITEM_TAGMASK) != PrincOpsDefs.AVITEM_FRAME)
        {
            Cpu.ERROR("AVFrame :: not an AVITEM_FRAME");
        }
        return (short)(avItem & 0xFFFC);
    }

    private static int /* AVItem */ AVLink(int avItem)
    {
        if ((avItem & AVITEM_TAGMASK) != PrincOpsDefs.AVITEM_FRAME)
        {
            Cpu.ERROR("AVLink :: not an AVITEM_FRAME");
        }
        return (short)(avItem & 0xFFFC);
    }

    public static int /* pointer = local-frame */ alloc(/* FSIndex */ int fsi)
    {
        int item;
        int slot = fsi;
        while (true)
        {
            item = Mem.readMDSWord(AV, slot) & 0xFFFF;
            if ((item & AVITEM_TAGMASK) != PrincOpsDefs.AVITEM_INDIRECT)
            {
                break;
            }
            int itemData = item >>> 2;
            if (itemData > FSINDEX_LAST) { Cpu.ERROR("alloc :: itemData > FSINDEX_LAST (invalid 'next' frame size index)"); }
            slot = itemData;
        }
        if ((item & AVITEM_TAGMASK) == PrincOpsDefs.AVITEM_EMPTY)
        {
            Cpu.signalFrameFault(fsi);
        }
        // read the next frame item from the new frame and store it in the AV[slot]
        Mem.writeMDSWord(AV, slot, Mem.readMDSWord(AVLink(item)));
        // return the new frame
        return AVFrame(item);
    }

    public static void free(/* LocalFrameHandle */ int frame)
    {
        // get the fsi of this frame
        int word = Mem.readMDSWord(frame, PrincOpsDefs.LocalOverhead_word);
        int fsi = word & 0x00FF;

        // get the current value at AV[fsi]
        int item = Mem.readMDSWord(AV, fsi);
        Mem.writeMDSWord(frame, item);

        // put the frame (implicitly a FRAME-AVItem)
        Mem.writeMDSWord(AV, fsi, frame);
    }

    /*
     * link types
     */

    public enum LinkType
    {
        frame,
        oldProcedure,
        indirect,
        newProcedure,
    }

    public static LinkType getControlLinkType(int controlLink)
    {
        int tag = controlLink & 0x00000003;
        if (tag == 0) { return LinkType.frame; }
        if (tag == 0x00000001) { return LinkType.oldProcedure; }
        if (tag == 0x00000002) { return LinkType.indirect; }
        return LinkType.newProcedure;
    }

    public static int /* POINTER */ makeFrameLink(int controlLink)
    {
        if ((controlLink & 0x00000003) != 0) { Cpu.ERROR("makeFrameLink :: not a frame link"); }
        return controlLink & 0x0000FFFF;
        // not: return (controlLink >>> 16);
    }

    public static int /* POINTER TO ControlLink */ makeIndirectLink(int controlLink)
    {
        if ((controlLink & 0x00000003) != 0x00000002) { Cpu.ERROR("makeIndirectLink :: not an indirect link"); }
        return controlLink & 0x0000FFFF;
        // not: return (controlLink >>> 16);
    }

    public static int /* UNSPECIFIED */ makeProcDesc_taggedGF(int controlLink)
    {
        if ((controlLink & 0x00000003) != 0x00000001) { Cpu.ERROR("makeProcDesc_taggedGF :: not a procDesc"); }
        return controlLink & 0x0000FFFF;
        // not: return controlLink >>> 16;
    }

    public static int /* UNSPECIFIED */ makeProcDesc_pc(int controlLink)
    {
        if ((controlLink & 0x00000003) != 0x00000001) { Cpu.ERROR("makeProcDesc_pc :: not a procDesc"); }
        return controlLink >>> 16;
        // not: return controlLink & 0x0000FFFF;
    }


    /* ****************** */

    /*
     * 9.3 Control Transfer Primitive
     */

    public enum XferType
    {
        xreturn = 0,
        xcall = 1,
        xlocalCall = 2,
        xport = 3,
        xfer = 4,
        xtrap = 5,
        xprocessSwitch = 6,
        xunused = 7,
    }

    // Definition of XFER functionality that depends on the PrincOps version.
    public interface IXferImpl
    {
        // transfer primitive proper
        void xfer(/*ControlLink*/ int dst, /*ShortControlLink*/ int src, XferType xferType, bool free);

        // get control link from global frame or code segment
        int fetchLink(int offset /* offset: BYTE 0..255 */);

        // check if xfer traps are requested and possibly trap accordingly
        void checkForXferTraps(/*ControlLink*/ int dst, XferType xferType);
    }

    public static IXferImpl impl = new XfererPrincops40();

    public static void switchToNewPrincOps()
    {
        impl = new XfererPrincops4x();
    }

    /*
     *  XFER-functionality for PrincOps up to 4.0
     */

    private sealed class XfererPrincops40 : IXferImpl
    {
        /*
         * 9.3 Control Transfer Primitive
         */

        public void xfer(int dst, int src, XferType xferType, bool free)
        {
            int nPC = 0; // new PC
            int nLF = 0; // new LF
            bool push = false;
            int nDst = dst; // controlLink

            if (xferType == XferType.xtrap && free) { Cpu.ERROR("xfer :: xferType = trap && free = true"); }

            while (getControlLinkType(nDst) == LinkType.indirect)
            {
                int link = makeIndirectLink(nDst);
                if (xferType == XferType.xtrap) { Cpu.ERROR("xfer :: xferType = trap in indirect link type upchain"); }
                nDst = Mem.readMDSDblWord(link);
                push = true;
            }

            switch (getControlLinkType(nDst))
            {
                case LinkType.oldProcedure:
                case LinkType.newProcedure:
                    // fields of "proc: ProcDesc"
                    // not: int proc_taggedGF = nDst >>> 16;
                    // not: int proc_pc = nDst & 0x0000FFFF;
                    int proc_taggedGF = nDst & 0x0000FFFF;
                    int proc_pc = nDst >>> 16;

                    // set global frame
                    Cpu.GF16 = proc_taggedGF & 0xFFFE;
                    Cpu.GF32 = Cpu.MDS + Cpu.GF16;
                    if (Cpu.GF16 == 0) { Cpu.unboundTrap(dst); }

                    // set code base
                    Cpu.CB = Mem.readMDSDblWord(Cpu.GF16 + PrincOpsDefs.GlobalOverhead40_codebase);
                    if ((Cpu.CB & 0x00000001) != 0) { Cpu.codeTrap(Cpu.GF16); }

                    // check new pc
                    nPC = proc_pc;
                    if (nPC == 0) { Cpu.unboundTrap(dst); }

                    // get the local frame index and allocate the new local frame
                    int word = Mem.readCode(nPC / 2);
                    nLF = alloc(((nPC & 0x0001) == 0) ? word >>> 8 : word & 0xFF);
                    nPC++;

                    // setup the new local frame (global frame link, return link)
                    Mem.writeMDSWord(nLF, PrincOpsDefs.LocalOverhead_globallink, Cpu.GF16);
                    Mem.writeMDSWord(nLF, PrincOpsDefs.LocalOverhead_returnlink, src);

                    break;

                case LinkType.frame:
                    int frame = makeFrameLink(nDst);
                    if (frame == 0) { Cpu.controlTrap(src); }

                    nLF = frame;

                    Cpu.GF16 = Mem.readMDSWord(nLF, PrincOpsDefs.LocalOverhead_globallink) & 0xFFFF;
                    Cpu.GF32 = Cpu.MDS + Cpu.GF16;
                    if (Cpu.GF16 == 0) { Cpu.unboundTrap(nDst); }

                    Cpu.CB = Mem.readMDSDblWord(Cpu.GF16, PrincOpsDefs.GlobalOverhead40_codebase);
                    if ((Cpu.CB & 0x00000001) != 0) { Cpu.codeTrap(Cpu.GF16); }

                    nPC = Mem.readMDSWord(nLF, PrincOpsDefs.LocalOverhead_pc) & 0xFFFF;
                    if (nPC == 0) { Cpu.unboundTrap(dst); }

                    if (xferType == XferType.xtrap)
                    {
                        Mem.writeMDSWord(nLF, PrincOpsDefs.LocalOverhead_returnlink, src);
                        Processes.disableInterrupts();
                    }

                    break;

                default:
                    // this cannot happen, as indirect links were already processed
                    break;
            }

            if (push)
            {
                Cpu.push(dst & 0x0000FFFF);
                // not: Cpu.push(dst >>> 16);
                Cpu.push(src);
                Cpu.discard();
                Cpu.discard();
            }

            if (free)
            {
                Xfer.free(Cpu.LF);
            }

            Cpu.LF = nLF;
            Cpu.PC = nPC;

            checkForXferTraps(dst, xferType);
        }

        /*
         * 9.4.2 External Function Calls
         */

        public int fetchLink(int offset)
        {
            int globalWord = Mem.readMDSWord(Cpu.GF16, PrincOpsDefs.GlobalOverhead40_word);
            if ((globalWord & PrincOpsDefs.GlobalLinkage_CodeLinks) == PrincOpsDefs.GlobalLinkage_CodeLinks)
            {
                return Mem.readDblWord(Cpu.CB - ((offset + 1) * 2));
            }
            else
            {
                return Mem.readMDSDblWord(Cpu.GF16 - PrincOpsDefs.GLOBALOVERHEAD40_SIZE - ((offset + 1) * 2));
            }
        }

        /*
         * 9.5.5 Xfer Traps
         */

        public void checkForXferTraps(int dst, XferType xferType)
        {
            if ((Cpu.XTS & 0x0001) != 0)
            {
                int globalWord = Mem.readMDSWord(Cpu.GF16 + PrincOpsDefs.GlobalOverhead40_word);
                if ((globalWord & PrincOpsDefs.GlobalLinkage_TrapXfers) != 0)
                {
                    Cpu.XTS = Cpu.XTS >>> 1;
                    Cpu.nakedTrap(PrincOpsDefs.sXferTrap); // TODO: ?? ! Abort => ERROR
                    Mem.writeMDSWord(Cpu.LF, 0, (ushort)(dst & 0xFFFF));
                    Mem.writeMDSWord(Cpu.LF, 1, (ushort)(dst >>> 16));
                    Mem.writeMDSWord(Cpu.LF, 2, (ushort)xferType);
                    throw new Cpu.MesaAbort();
                }
            }
            else
            {
                Cpu.XTS = Cpu.XTS >>> 1;
            }
        }
    }

    /*
     * XFER-functionality for PrincOps after 4.0 ("MDS-relieved")
     */

    private sealed class XfererPrincops4x : IXferImpl
    {
        /*
         * 9.3 Control Transfer Primitive
         */

        // TODO: in the ChangedChapters and possibly the implementations there seems
        //       to be some mixing of GlobalFrameIndex and GlobalFrameHandle:
        //        -> GlobalFrameIndex is the index in the GFT (1-step jumps over 4-word entries)
        //        -> GlobalFrameHandle is the relative POINTER to the start of an entry (4-step jumps)

        public void xfer(int dst, int src, XferType xferType, bool free)
        {
            int nPC = 0; // new PC
            int nLF = 0; // new LF
            bool push = false;
            int nDst = dst; // controlLink

            if (Config.LOG_OPCODES)
            {
                Cpu.logf(
                      "   xfer dst=0x{0:X8} src=0x{1:X4} xferType={2} free={3}\n"
                    + "     currGFI=0x{4:X4}, currGF32=0x{5:X8}, currCB=0x{6:X8}, currPC=0x{7:X4}, currLF=0x{8:X4}\n",
                    dst, src, xferType.ToString(), free ? "true" : "false",
                    Cpu.GFI, Cpu.GF32, Cpu.CB, Cpu.PC, Cpu.LF
                );
            }

            if (xferType == XferType.xtrap && free) { Cpu.ERROR("xfer :: xferType = trap && free = true"); }

            while (getControlLinkType(nDst) == LinkType.indirect)
            {
                int link = makeIndirectLink(nDst);
                if (xferType == XferType.xtrap) { Cpu.ERROR("xfer :: xferType = trap in indirect link type upchain"); }
                nDst = Mem.readMDSDblWord(link);
                if (Config.LOG_OPCODES) { Cpu.logf("     -> indirect-link => new dst=0x{0:X8}\n", nDst); }
                push = true;
            }

            switch (getControlLinkType(nDst))
            {
                case LinkType.oldProcedure:
                {
                    // fields of "proc: ProcDesc"
                    // not: int proc_taggedGF = nDst >>> 16;
                    // not: int proc_pc = nDst & 0x0000FFFF;
                    int proc_taggedGF = nDst & 0x0000FFFF;
                    int proc_pc = nDst >>> 16;

                    // get global frame index
                    // (gfi seems to be hidden in the overhead 'word' in the (up to 4.0)
                    // unused bits above the linkage flags in a pseudo global frame in MDS)
                    // unsure: use <= 4.0 or > 4.0 offsets in overhead area?
                    int gf = proc_taggedGF & 0xFFFC;
                    if (gf == 0) { Cpu.unboundTrap(dst); }
                    Cpu.GFI = Mem.readMDSWord(gf, PrincOpsDefs.GlobalOverhead4x_word) & 0xFFFC;
                    if (Cpu.GFI == 0) { Cpu.unboundTrap(dst); }
                    // int gftItemPtr = Cpu.GFT + (Cpu.GFI * PrincOpsDefs.GFTItem_SIZE); -- does not work...
                    int gftItemPtr = Cpu.GFT + Cpu.GFI; // GFI is already left-shifted by 2, so it is the word offset of the gftItem in GFT

                    // set global frame
                    Cpu.GF32 = Mem.readDblWord(gftItemPtr + PrincOpsDefs.GFTItem_globalFrame);

                    // set code base
                    Cpu.CB = Mem.readDblWord(gftItemPtr + PrincOpsDefs.GFTItem_codebase);
                    if ((Cpu.CB & 0x00000001) != 0) { Cpu.codeTrap(Cpu.GFI); }

                    // check new pc
                    nPC = proc_pc;
                    if (nPC == 0) { Cpu.unboundTrap(dst); }

                    // get the local frame index and allocate the new local frame
                    int word = Mem.readCode(nPC / 2) & 0xFFFF;
                    int nFsi = ((nPC & 0x0001) == 0) ? word >>> 8 : word & 0xFF;
                    nLF = alloc(nFsi);
                    nPC++;

                    // setup the new local frame (global frame link, return link)
                    Mem.writeMDSWord(nLF, PrincOpsDefs.LocalOverhead_globallink, Cpu.GFI);
                    Mem.writeMDSWord(nLF, PrincOpsDefs.LocalOverhead_returnlink, src);

                    if (Config.LOG_OPCODES)
                    {
                        Cpu.logf(
                              "     -> oldProcedure-xfer\n"
                            + "     -> GFI=0x{0:X4} GF32=0x{1:X8} CB=0x{2:X8} newPC=0x{3:X4} newFsi={4}\n"
                            + "     -> newLF=0x{5:X4} newLF.globalLink=0x{6:X4} newLF.returnLink=0x{7:X4}\n",
                            Cpu.GFI, Cpu.GF32, Cpu.CB, nPC, nFsi,
                            nLF, Mem.readMDSWord(nLF, PrincOpsDefs.LocalOverhead_globallink), Mem.readMDSWord(nLF, PrincOpsDefs.LocalOverhead_returnlink)
                        );
                    }
                }
                break;

                case LinkType.newProcedure:
                {
                    // fields of "proc: ProcDesc"
                    // not: int proc_taggedGF = nDst >>> 16;
                    // not: int proc_pc = nDst & 0x0000FFFF;
                    int proc_taggedGF = nDst & 0x0000FFFF;
                    int proc_pc = nDst >>> 16;

                    // get global frame index
                    Cpu.GFI = proc_taggedGF & 0xFFFC;
                    if (Cpu.GFI == 0) { Cpu.unboundTrap(dst); }
                    // int gftItemPtr = Cpu.GFT + (Cpu.GFI * PrincOpsDefs.GFTItem_SIZE); -- does not work...
                    int gftItemPtr = Cpu.GFT + Cpu.GFI; // GFI is already left-shifted by 2, so it is the word offset of the gftItem in GFT

                    // set global frame
                    Cpu.GF32 = Mem.readDblWord(gftItemPtr + PrincOpsDefs.GFTItem_globalFrame);

                    // set code base
                    Cpu.CB = Mem.readDblWord(gftItemPtr + PrincOpsDefs.GFTItem_codebase);
                    if ((Cpu.CB & 0x00000001) != 0) { Cpu.codeTrap(Cpu.GFI); }

                    // check new pc
                    nPC = proc_pc;
                    if (nPC == 0) { Cpu.unboundTrap(dst); }

                    // get the local frame index and allocate the new local frame
                    int word = Mem.readCode(nPC / 2);
                    int nFsi = ((nPC & 0x0001) == 0) ? word >>> 8 : word & 0xFF;
                    nLF = alloc(nFsi);
                    nPC++;

                    // setup the new local frame (global frame link, return link)
                    Mem.writeMDSWord(nLF, PrincOpsDefs.LocalOverhead_globallink, Cpu.GFI);
                    Mem.writeMDSWord(nLF, PrincOpsDefs.LocalOverhead_returnlink, src);

                    if (Config.LOG_OPCODES)
                    {
                        Cpu.logf(
                              "     -> newProcedure-xfer\n"
                            + "     -> GFI=0x{0:X4} GF32=0x{1:X8} CB=0x{2:X8} newPC=0x{3:X4} newFsi={4}\n"
                            + "     -> newLF=0x{5:X4} newLF.globalLink=0x{6:X4} newLF.returnLink=0x{7:X4}\n",
                            Cpu.GFI, Cpu.GF32, Cpu.CB, nPC, nFsi,
                            nLF, Mem.readMDSWord(nLF, PrincOpsDefs.LocalOverhead_globallink), Mem.readMDSWord(nLF, PrincOpsDefs.LocalOverhead_returnlink)
                        );
                    }
                }
                break;

                case LinkType.frame:
                {
                    int frame = makeFrameLink(nDst);
                    if (frame == 0) { Cpu.controlTrap(src); }

                    nLF = frame;

                    Cpu.GFI = Mem.readMDSWord(nLF, PrincOpsDefs.LocalOverhead_globallink);
                    if (Cpu.GFI == 0) { Cpu.unboundTrap(nDst); }
                    // int gftItemPtr = Cpu.GFT + (Cpu.GFI * PrincOpsDefs.GFTItem_SIZE); -- does not work...
                    int gftItemPtr = Cpu.GFT + Cpu.GFI; // GFI is already left-shifted by 2, so it is the word offset of the gftItem in GFT

                    Cpu.GF32 = Mem.readDblWord(gftItemPtr + PrincOpsDefs.GFTItem_globalFrame);

                    Cpu.CB = Mem.readDblWord(gftItemPtr + PrincOpsDefs.GFTItem_codebase);
                    if ((Cpu.CB & 0x00000001) != 0) { Cpu.codeTrap(Cpu.GFI); }

                    nPC = Mem.readMDSWord(nLF, PrincOpsDefs.LocalOverhead_pc) & 0xFFFF;
                    if (nPC == 0) { Cpu.unboundTrap(dst); }

                    if (xferType == XferType.xtrap)
                    {
                        Mem.writeMDSWord(nLF, PrincOpsDefs.LocalOverhead_returnlink, src);
                        Processes.disableInterrupts();
                    }

                    if (Config.LOG_OPCODES)
                    {
                        Cpu.logf(
                              "     -> frame-xfer\n"
                            + "     -> GFI=0x{0:X4} GF32=0x{1:X8} CB=0x{2:X8} newPC=0x{3:X4}\n"
                            + "     -> newLF=0x{4:X4} newLF.globalLink=0x{5:X4} newLF.returnLink=0x{6:X4}\n",
                            Cpu.GFI, Cpu.GF32, Cpu.CB, nPC,
                            nLF, Mem.readMDSWord(nLF, PrincOpsDefs.LocalOverhead_globallink), Mem.readMDSWord(nLF, PrincOpsDefs.LocalOverhead_returnlink)
                        );
                    }
                }
                break;

                default:
                    // this cannot happen, as indirect links were already processed
                    break;
            }

            if (push)
            {
                Cpu.push(dst & 0x0000FFFF);
                // not: Cpu.push(dst >>> 16);
                Cpu.push(src);
                Cpu.discard();
                Cpu.discard();
            }

            if (free)
            {
                Xfer.free(Cpu.LF);
            }

            Cpu.LF = nLF;
            Cpu.PC = nPC;

            checkForXferTraps(dst, xferType);
        }

        /*
         * 9.4.2 External Function Calls
         */

        public int fetchLink(int offset)
        {
            int globalWord = Mem.readWord(Cpu.GF32 + PrincOpsDefs.GlobalOverhead4x_word);
            if ((globalWord & PrincOpsDefs.GlobalLinkage_CodeLinks) == PrincOpsDefs.GlobalLinkage_CodeLinks)
            {
                return Mem.readDblWord(Cpu.CB - ((offset + 1) * 2));
            }
            else
            {
                return Mem.readDblWord(Cpu.GF32 - PrincOpsDefs.GLOBALOVERHEAD4x_SIZE - ((offset + 1) * 2));
            }
        }

        /*
         * 9.5.5 Xfer Traps
         */

        public void checkForXferTraps(int dst, XferType xferType)
        {
            if ((Cpu.XTS & 0x0001) != 0)
            {
                int globalWord = Mem.readWord(Cpu.GF32 + PrincOpsDefs.GlobalOverhead4x_word);
                if ((globalWord & PrincOpsDefs.GlobalLinkage_TrapXfers) != 0)
                {
                    Cpu.XTS = Cpu.XTS >>> 1;
                    Cpu.nakedTrap(PrincOpsDefs.sXferTrap); // TODO: ?? ! Abort => ERROR
                    Mem.writeMDSWord(Cpu.LF, 0, (ushort)(dst & 0xFFFF));
                    Mem.writeMDSWord(Cpu.LF, 1, (ushort)(dst >>> 16));
                    Mem.writeMDSWord(Cpu.LF, 2, (ushort)xferType);
                    throw new Cpu.MesaAbort();
                }
            }
            else
            {
                Cpu.XTS = Cpu.XTS >>> 1;
            }
        }
    }
}
