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

// Implementation of the mesa engine real and virtual memory including display
// memory in mesa engine address space.
//
// Java's `short[]` (signed) becomes C# `ushort[]` (unsigned) for the underlying
// arrays — this matches Mesa's 16-bit word semantics and eliminates the
// `& 0xFFFF` masking ceremony that pervades the Java code. See DECISIONS.md §2.
//
// The IORegion-logging blocks in the Java accessors (inside Config.IOR_LOG_MEM_ACCESS
// branches) are intentionally omitted from this port — they require the IORegion
// infrastructure which lands in Phase F. Since `Config.IOR_LOG_MEM_ACCESS = false`
// is a const, dead-code elimination makes the omission a no-op.
public static class Mem
{
    // the real memory
    public static ushort[] mem = null!;

    // virtual memory part 1: map virtual-page => real-page-base address
    // (!= PrincOps: this holds the real page base address (to speed up things),
    //  not the real page no)
    public static int[] pageMap = null!;

    // virtual memory part 2:  map virtual-page => page-flags
    // (defined at package-level to allow Processes to access it for handling
    //  UI screen refresh)
    public static ushort[] pageFlags = null!;

    // number of address-bits in virtual and real addresses (externally configured)
    private static int addressBitsVirtual;
    private static int addressBitsReal;

    // memory limits resulting from 'addressBitsVirtual' and 'addressBitsReal'
    private static int lastVirtualAddress;
    private static int lastVirtualPage;
    private static int lastRealPage;

    // display memory characteristics
    private static int effectivePixelsPerLine; // pixels on a scan line in memory
    public static int displayPixelWidth;       // pixels on a scan line displayed
    public static int displayPixelHeight;      // number of vertical scan lines
    public static int displayFirstRealPage;
    public static int displayPageSize;         // total number of pages for the display memory
    internal static int displayFirstMappedVirtualPage; // (package-level for Processes)

    // Machine-Type GUAM: memory and virtual-memory-map setup at machine start

    // reserve the half of the first bank for IO devices
    private const int IOAREA_PAGECOUNT = PrincOpsDefs.PAGES_PER_SEGMENT / 2;

    // first virtual page of the ioArea
    private const int IOAREA_START_VPAGE = PrincOpsDefs.PAGES_PER_SEGMENT - IOAREA_PAGECOUNT;

    // virtual address of the ioArea-start (LONG POINTER)
    public const int ioArea = IOAREA_START_VPAGE * PrincOpsDefs.WORDS_PER_PAGE;

    public static void initializeMemoryGuam(int addrBitsVirtual, int addrBitsReal) =>
        initializeMemoryGuam(addrBitsVirtual, addrBitsReal, DisplayType.monochrome, 960, 720);

    public static void initializeMemoryGuam(
        int addrBitsVirtual,
        int addrBitsReal,
        DisplayType displayType,
        int displayWidth,
        int displayHeight)
    {
        if (mem != null)
        {
            throw new InvalidOperationException("MesaEngine memory already initialized");
        }
        if (addrBitsVirtual > PrincOpsDefs.MAX_VIRTUAL_ADDRESSBITS)
        {
            throw new ArgumentException("Requested virtual memory address bit count exceeds limit", nameof(addrBitsVirtual));
        }
        if ((displayWidth % 16) != 0)
        {
            throw new ArgumentException("Requested displayWidth not a multiple of 16", nameof(displayWidth));
        }
        if (displayType != DisplayType.monochrome && displayType != DisplayType.byteColor)
        {
            throw new ArgumentException("Unsupported 'displayType' = " + displayType, nameof(displayType));
        }

        // Real mem: min. 1024 pages .. max. 32768 pages (512 Kbyte .. 16 MByte)
        addressBitsReal = Math.Max(PrincOpsDefs.MIN_REAL_ADDRESSBITS, Math.Min(addrBitsReal, PrincOpsDefs.MAX_REAL_ADDRESSBITS));

        // Virtual mem: min. real-pages .. max. 131072 pages (real-mem .. 64 MByte)
        addressBitsVirtual = Math.Min(PrincOpsDefs.MAX_VIRTUAL_ADDRESSBITS, Math.Max(addrBitsVirtual, addressBitsReal));

        // Pilot/BWS seems to use multiple of 512 pixels per line for bytecolor
        // which is the number of pixels per memory page (2 pixels / word => 1 page == 512 pixels)
        effectivePixelsPerLine = (displayType == DisplayType.monochrome)
            ? displayWidth
            : ((displayWidth + 511) / 512) * 512;

        // additional real memory required for display
        int displayMemoryNeededWords
            = (((effectivePixelsPerLine * displayType.getBitDepth()) + PrincOpsDefs.WORD_BITS - 1)
                / PrincOpsDefs.WORD_BITS)
                * displayHeight;
        displayPageSize = (displayMemoryNeededWords + PrincOpsDefs.WORDS_PER_PAGE - 1) / PrincOpsDefs.WORDS_PER_PAGE;
        displayPixelWidth = displayWidth;
        displayPixelHeight = displayHeight;
        activeDisplayType = displayType;

        // allocate our memory and state arrays
        // -> mem is a contiguous word array containing
        //     - the real memory available to the mesa processor for mapping to virtual pages
        //     - plus the display memory appended to real memory
        // -> real pages available for mapping are [0..lastRealPage)
        // -> display memory real pages are [lastRealPage+1..lastRealPage+displayPageSize)
        int wordCount = PrincOpsDefs.WORDS_PER_PAGE << (addressBitsReal - PrincOpsDefs.ADDRESSBITS_IN_PAGE);
        int virtualPageCount = 1 << (addressBitsVirtual - PrincOpsDefs.ADDRESSBITS_IN_PAGE);
        int realPageCount = 1 << (addressBitsReal - PrincOpsDefs.ADDRESSBITS_IN_PAGE);
        mem = new ushort[wordCount + (displayPageSize * PrincOpsDefs.WORDS_PER_PAGE)];
        pageMap = new int[virtualPageCount + displayPageSize];
        pageFlags = new ushort[virtualPageCount + displayPageSize];
        lastVirtualAddress = (PrincOpsDefs.WORDS_PER_PAGE * virtualPageCount) - 1;
        lastVirtualPage = virtualPageCount - 1;
        lastRealPage = realPageCount - 1;
        displayFirstRealPage = lastRealPage + 1;

        // initialize virtual and display memory
        createInitialPageMappingGuam();
        if (activeDisplayType == DisplayType.monochrome)
        {
            initializeDisplayMemoryGuam();
        }
        else
        {
            initializeColorDisplayMemoryGuam();
        }
    }

    public static void createInitialPageMappingGuam()
    {
        // For some reasons not documented in PrincOps, the IO area in the first
        // (virtual) segment has to be mapped starting at real page/address 0
        // (germ? ; pilot?)

        int currRealAddress = 0;
        int currRealPage = 0;
        int currVirtualPage = IOAREA_START_VPAGE;

        // map IOArea (real pages starting at 0, virtual pages starting at IOAREA_START_VPAGE)
        while (currRealPage < IOAREA_PAGECOUNT)
        {
            pageMap[currVirtualPage] = currRealAddress;
            pageFlags[currVirtualPage] = PrincOpsDefs.MAPFLAGS_CLEAR;
            currRealPage++;
            currVirtualPage++;
            currRealAddress += PrincOpsDefs.WORDS_PER_PAGE;
        }

        // map the rest of the first segment (virtual pages starting at 0, real pages behind (real) io area)
        currVirtualPage = 0;
        while (currRealPage < PrincOpsDefs.PAGES_PER_SEGMENT)
        {
            pageMap[currVirtualPage] = currRealAddress;
            pageFlags[currVirtualPage] = PrincOpsDefs.MAPFLAGS_CLEAR;
            currRealPage++;
            currVirtualPage++;
            currRealAddress += PrincOpsDefs.WORDS_PER_PAGE;
        }

        // map the remaining real pages to the same virtual page addresses
        currRealAddress = PrincOpsDefs.PAGES_PER_SEGMENT * PrincOpsDefs.WORDS_PER_PAGE;
        for (int i = PrincOpsDefs.PAGES_PER_SEGMENT; i <= lastRealPage; i++)
        {
            pageMap[i] = currRealAddress;
            pageFlags[i] = PrincOpsDefs.MAPFLAGS_CLEAR;
            currRealAddress += PrincOpsDefs.WORDS_PER_PAGE;
        }

        // set the rest of virtual memory pages to "unmapped"
        for (int i = (lastRealPage + 1); i <= lastVirtualPage; i++)
        {
            pageMap[i] = 0;
            pageFlags[i] = PrincOpsDefs.MAPFLAGS_VACANT;
        }
    }

    // return the number of addressable virtual pages
    public static int getVirtualPagesSize() => lastVirtualPage + 1;

    // return the number of real pages in mem[] available for mapping to virtual pages
    public static int getRealPagesSize() => lastRealPage + 1;

    // put some pattern into display memory indicating that the display has still
    // not been initialized by the OS being booted (i.e. Pilot resp. its client XDE
    // or ViewPoint/GlobalView)
    private static void initializeDisplayMemoryGuam()
    {
        int displayWord = mem.Length - (displayPageSize * PrincOpsDefs.WORDS_PER_PAGE);
        int wordsPerLine = displayPixelWidth / PrincOpsDefs.WORD_BITS;
        ushort[] template =
        {
            0x8001, // 0b1000000000000001
            0x4002, // 0b0100000000000010
            0x2004, // 0b0010000000000100
            0x1008, // 0b0001000000001000
            0x0810, // 0b0000100000010000
            0x0420, // 0b0000010000100000
            0x0240, // 0b0000001001000000
            0x0180, // 0b0000000110000000
            0x0180,
            0x0240,
            0x0420,
            0x0810,
            0x1008,
            0x2004,
            0x4002,
            0x8001,
        };
        int ti = 0;
        for (int i = 0; i < displayPixelHeight; i++)
        {
            for (int j = 0; j < wordsPerLine; j++)
            {
                mem[displayWord++] = template[ti];
            }
            ti++;
            if (ti >= template.Length) { ti = 0; }
        }
    }

    private static void initializeColorDisplayMemoryGuam()
    {
        ushort[] template =
        {
            0x0100, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0001,
            0x0001, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0100,
            0x0000, 0x0100, 0x0000, 0x0000, 0x0000, 0x0000, 0x0001, 0x0000,
            0x0000, 0x0001, 0x0000, 0x0000, 0x0000, 0x0000, 0x0100, 0x0000,
            0x0000, 0x0000, 0x0100, 0x0000, 0x0000, 0x0001, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0001, 0x0000, 0x0000, 0x0100, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0100, 0x0001, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0001, 0x0100, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0001, 0x0100, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0000, 0x0100, 0x0001, 0x0000, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0001, 0x0000, 0x0000, 0x0100, 0x0000, 0x0000,
            0x0000, 0x0000, 0x0100, 0x0000, 0x0000, 0x0001, 0x0000, 0x0000,
            0x0000, 0x0001, 0x0000, 0x0000, 0x0000, 0x0000, 0x0100, 0x0000,
            0x0000, 0x0100, 0x0000, 0x0000, 0x0000, 0x0000, 0x0001, 0x0000,
            0x0001, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0100,
            0x0100, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0001,
        };
        int displayWord = mem.Length - (displayPageSize * PrincOpsDefs.WORDS_PER_PAGE);
        int wordsPerLine = effectivePixelsPerLine / 2;
        int tl = 0;
        for (int i = 0; i < displayPixelHeight; i++)
        {
            for (int j = 0; j < wordsPerLine; j++)
            {
                mem[displayWord++] = template[tl + (displayWord % 8)];
            }
            tl += 8;
            if (tl >= template.Length) { tl = 0; }
        }
    }

    // Machine type Daybreak (Dove 6085): memory and virtual-memory-map setup at machine start

    public static void initializeMemoryDaybreak(bool largeScreen)
    {
        // fixed real and virtual sizes for this 6085 implementation
        addressBitsReal = 21;    // 13 bits for page address + 8 bits for word in page address == 8192 pages == 2 MWords == 4 MBytes
        addressBitsVirtual = 24; // 16 MWords == 32 MBytes == 65536 pages == 256 VMM pages

        // screen data
        if (largeScreen)
        {
            // "18 quadwords x 861 lines"
            displayPixelWidth = 1152;
            displayPixelHeight = 861;
            dBreak_displayType = 5;
        }
        else
        {
            // "13 quadwords x 633 lines"
            displayPixelWidth = 832;
            displayPixelHeight = 633;
            dBreak_displayType = 1;
        }
        displayPageSize = 256; // largescreen: 18 * 4 * 861 / 256 => 242.15625 pages
        activeDisplayType = DisplayType.monochrome;

        // allocate real memory and virtual memory map
        int realPageCount = 1 << (addressBitsReal - PrincOpsDefs.ADDRESSBITS_IN_PAGE);
        int virtualPageCount = 1 << (addressBitsVirtual - PrincOpsDefs.ADDRESSBITS_IN_PAGE);

        mem = new ushort[realPageCount * PrincOpsDefs.WORDS_PER_PAGE];
        pageMap = new int[virtualPageCount];
        pageFlags = new ushort[virtualPageCount];
        lastVirtualAddress = (PrincOpsDefs.WORDS_PER_PAGE * virtualPageCount) - 1;
        lastVirtualPage = virtualPageCount - 1;
        lastRealPage = realPageCount - 1;

        // initialize virtual and display memory
        createInitialPageMappingDBreak();
        initializeDisplayMemoryDBreak();
    }

    public const int IORegion_Real_StartPage = 32; // skip the first 8 KWord = 16 KByte (why-ever)
    public const int IORegion_PageCount = 64;      // = 16 KWord = 32 KByte (? sufficient space?)

    public const int IORegion_Virtual_PageAfterEnd = 256;
    public const int IORegion_Virtual_StartPage = IORegion_Virtual_PageAfterEnd - IORegion_PageCount;

    public const int IORegion_VM_StartAddress = IORegion_Virtual_StartPage * PrincOpsDefs.WORDS_PER_PAGE;
    public const int IORegion_VM_EndAddressPlusOne = IORegion_Virtual_PageAfterEnd * PrincOpsDefs.WORDS_PER_PAGE;

    public const int DBreak_Real_VMM_PageCount = 256;
    public const int DBreak_Real_DisplayMem_PageCount = 256;

    public static int dBreak_firstRealPageInVMM;
    public static int dBreak_lastRealPageInVMM;
    public static int dBreak_countRealPagesInVMM;

    public static int dBreak_Real_firstMapPage;
    public static int dBreak_Real_countMapPages;

    public static int dBreak_Real_firstDisplayBankPage;
    public static int dBreak_Real_countDisplayBankPages;
    public static int dBreak_displayType;

    private static void createInitialPageMappingDBreak()
    {
        // real memory (total 8192 pages):
        //   - 32 pages unused: why??
        //   - 64 pages IORegion
        //   - 7584 pages for virtual memory
        //   - 256 pages VM map
        //   - 256 pages display memory
        //
        // (initial) virtual memory mapping
        //   - 192 pages  => real[96 .. 256)     == free
        //   - 64 pages   => real[32 .. 96)      == IORegion
        //   - 7424 pages => real[256 .. 7680)   == free
        //   - rest       => unmapped

        int realPagesTotal = lastRealPage + 1;

        dBreak_firstRealPageInVMM = IORegion_Real_StartPage;
        dBreak_lastRealPageInVMM = realPagesTotal - DBreak_Real_VMM_PageCount - DBreak_Real_DisplayMem_PageCount - 1;
        dBreak_countRealPagesInVMM = dBreak_lastRealPageInVMM - dBreak_firstRealPageInVMM + 1;

        dBreak_Real_firstDisplayBankPage = realPagesTotal - DBreak_Real_DisplayMem_PageCount;
        dBreak_Real_countDisplayBankPages = DBreak_Real_DisplayMem_PageCount;

        dBreak_Real_firstMapPage = dBreak_Real_firstDisplayBankPage - DBreak_Real_VMM_PageCount;
        dBreak_Real_countMapPages = DBreak_Real_VMM_PageCount;

        int currVirtualPage = 0;

        // map real pages after the IORegion up to the VM start of the IORegion
        int currRealAddress = (IORegion_Real_StartPage + IORegion_PageCount) * PrincOpsDefs.WORDS_PER_PAGE;
        while (currVirtualPage < IORegion_Virtual_StartPage)
        {
            pageMap[currVirtualPage] = currRealAddress;
            pageFlags[currVirtualPage] = PrincOpsDefs.MAPFLAGS_CLEAR;
            currVirtualPage++;
            currRealAddress += PrincOpsDefs.WORDS_PER_PAGE;
        }
        int behindIORegionContinueRealAddress = currRealAddress;

        // map IORegion into the expected VM location (=> first VM bank is the mapped)
        currRealAddress = IORegion_Real_StartPage * PrincOpsDefs.WORDS_PER_PAGE;
        for (int i = 0; i < IORegion_PageCount; i++)
        {
            pageMap[currVirtualPage] = currRealAddress;
            pageFlags[currVirtualPage] = PrincOpsDefs.MAPFLAGS_CLEAR;
            currVirtualPage++;
            currRealAddress += PrincOpsDefs.WORDS_PER_PAGE;
        }

        // map remaining real pages up to the start of reserved real memory into VM
        currRealAddress = behindIORegionContinueRealAddress;
        int realAddressLimit = dBreak_lastRealPageInVMM * PrincOpsDefs.WORDS_PER_PAGE;
        while (currRealAddress <= realAddressLimit)
        {
            pageMap[currVirtualPage] = currRealAddress;
            pageFlags[currVirtualPage] = PrincOpsDefs.MAPFLAGS_CLEAR;
            currVirtualPage++;
            currRealAddress += PrincOpsDefs.WORDS_PER_PAGE;
        }

        // define the remaining pages in VM as unmapped
        while (currVirtualPage <= lastVirtualPage)
        {
            pageMap[currVirtualPage] = 0;
            pageFlags[currVirtualPage] = PrincOpsDefs.MAPFLAGS_VACANT;
            currVirtualPage++;
        }
    }

    private static void initializeDisplayMemoryDBreak()
    {
        int displayWord = dBreak_Real_firstDisplayBankPage * PrincOpsDefs.WORDS_PER_PAGE;
        int wordsPerLine = displayPixelWidth / PrincOpsDefs.WORD_BITS;
        ushort[] template =
        {
            0xF00F, // 0b1111000000001111
            0x0C30, // 0b0000110000110000
            0x03C0, // 0b0000001111000000
            0x0C30, // 0b0000110000110000
            0x1008, // 0b0001000000001000
            0x1008, // 0b0001000000001000
            0x0810, // 0b0000100000010000
            0x0420, // 0b0000010000100000
            0x0240, // 0b0000001001000000
            0x0180, // 0b0000000110000000
            0x0240, // 0b0000001001000000
            0x0420, // 0b0000010000100000
            0x0810, // 0b0000100000010000
            0x1008, // 0b0001000000001000
            0x2004, // 0b0010000000000100
            0x4002, // 0b0100000000000010
        };
        int ti = 0;
        for (int i = 0; i < displayPixelHeight; i++)
        {
            for (int j = 0; j < wordsPerLine; j++)
            {
                mem[displayWord++] = template[ti];
            }
            ti++;
            if (ti >= template.Length) { ti = 0; }
        }

        displayFirstRealPage = dBreak_Real_firstDisplayBankPage;
        displayPageSize = ((wordsPerLine * displayPixelHeight) + PrincOpsDefs.WORDS_PER_PAGE - 1) / PrincOpsDefs.WORDS_PER_PAGE;
    }

    // instruction support

    public static bool isProtected(ushort flags) => (flags & PrincOpsDefs.MAPFLAGS_PROTECTED) != 0;
    public static bool isDirty(ushort flags)     => (flags & PrincOpsDefs.MAPFLAGS_DIRTY) != 0;
    public static bool isReferenced(ushort flags) => (flags & PrincOpsDefs.MAPFLAGS_REFERENCED) != 0;
    public static bool isVacant(ushort flags) => isProtected(flags) && isDirty(flags) && !isReferenced(flags);

    // for opcode SM - Set Map
    public static void setMap(string by, int virtualPageNo, int realPageNo, ushort flags) =>
        setMap(by, virtualPageNo, realPageNo, flags, false);

    public static void setMap(string by, int virtualPageNo, int realPageNo, ushort flags, bool isDisplayMem)
    {
        if (virtualPageNo < 0 || virtualPageNo > lastVirtualPage)
        {
            Cpu.ERROR("SM / setMap :: virtualPageNo out of range: " + virtualPageNo);
            return; // ERROR does not return, but the compiler can't see that
        }

        // clear real page caches
        int oldVirtualPageBase = virtualPageNo << 8;
        if (_lastLpVpageRead == oldVirtualPageBase) { _lastLpVpageRead = -1; }
        if (_lastLpVpageWritten == oldVirtualPageBase) { _lastLpVpageWritten = -1; }
        if (_lastMdsVpageRead == oldVirtualPageBase) { _lastMdsVpageRead = -1; }
        if (_lastMdsVpageWritten == oldVirtualPageBase) { _lastMdsVpageWritten = -1; }
        if (_lastCodeVpageRead == oldVirtualPageBase) { _lastCodeVpageRead = -1; }

        if (isVacant(flags))
        {
            pageMap[virtualPageNo] = 0;
        }
        else if (isDisplayMem && realPageNo >= lastRealPage && realPageNo <= (lastRealPage + displayPageSize))
        {
            pageMap[virtualPageNo] = realPageNo << PrincOpsDefs.ADDRESSBITS_IN_PAGE;
        }
        else if (realPageNo < 0 || realPageNo > lastRealPage)
        {
            pageFlags[virtualPageNo] = PrincOpsDefs.MAPFLAGS_VACANT;
            pageMap[virtualPageNo] = 0;
            Cpu.ERROR("SM / setMap :: realPageNo out of range: " + realPageNo);
            return;
        }
        else
        {
            pageMap[virtualPageNo] = realPageNo << PrincOpsDefs.ADDRESSBITS_IN_PAGE;
        }
        pageFlags[virtualPageNo] = flags;

        if (Config.LOG_OPCODES && (!"SMF".Equals(by) || isVacant(flags)))
        {
            Cpu.logf(
                "    {0} :: setMap() => map[ 0x{1:X6} ] -> rp = 0x{2:X8} , flags = 0x{3:X4}\n",
                by, virtualPageNo, pageMap[virtualPageNo], pageFlags[virtualPageNo]);
        }
    }

    // for opcode GMF - Get Map Flags
    public static ushort getVPageFlags(int virtualPageNo)
    {
        if (virtualPageNo < 0 || virtualPageNo > lastVirtualPage) { return PrincOpsDefs.MAPFLAGS_VACANT; }
        return pageFlags[virtualPageNo];
    }

    public static int getVPageRealPage(int virtualPageNo)
    {
        if (virtualPageNo < 0 || virtualPageNo > lastVirtualPage) { return 0; }
        return (int)((uint)pageMap[virtualPageNo] >> PrincOpsDefs.ADDRESSBITS_IN_PAGE);
    }

    // for InitialMesaMicrocode.loadGermPage()
    public static ushort setVPageFlags(int virtualPageNo, ushort newFlags)
    {
        if (virtualPageNo < 0 || virtualPageNo > lastVirtualPage) { return PrincOpsDefs.MAPFLAGS_VACANT; }
        ushort currFlags = pageFlags[virtualPageNo];
        if (!isVacant(currFlags))
        {
            pageFlags[virtualPageNo] = newFlags;
        }
        return currFlags;
    }

    // for devices/agents
    public static bool isReadable(int lp) => !isVacant(getVPageFlags((int)((uint)lp >> PrincOpsDefs.ADDRESSBITS_IN_PAGE)));
    public static bool isWritable(int lp) => !isProtected(getVPageFlags((int)((uint)lp >> PrincOpsDefs.ADDRESSBITS_IN_PAGE)));

    // memory access support

    public static int getRealAddress(int longPointer, bool forWrite)
    {
        if (longPointer < 0 || longPointer > lastVirtualAddress)
        {
            Cpu.pointerTrap();
            return 0;
        }

        int pageNo = longPointer >> PrincOpsDefs.ADDRESSBITS_IN_PAGE;

        if (pageNo == 0)
        {
            Cpu.pointerTrap(); // nullPointer
            return 0;
        }

        ushort flags = pageFlags[pageNo];
        if (flags == PrincOpsDefs.MAPFLAGS_VACANT)
        {
            Cpu.signalPageFault(longPointer);
            return 0;
        }

        if (forWrite)
        {
            if (isProtected(flags))
            {
                Cpu.signalWriteProtectFault(longPointer);
                return 0;
            }
            flags |= PrincOpsDefs.MAPFLAGS_REFERENCED | PrincOpsDefs.MAPFLAGS_DIRTY;
        }
        else
        {
            flags |= PrincOpsDefs.MAPFLAGS_REFERENCED;
        }
        pageFlags[pageNo] = flags;

        int realBasePointer = pageMap[pageNo];
        if (Config.LOG_OPCODES && realBasePointer == 0 && (longPointer & 0xFFFFFF00) != 0x00008000)
        {
            Console.WriteLine($"\n**\n**** getRealAddress() :: using real page 0 for vLp = 0x{longPointer:X8}\n**\n");
            Cpu.logTrapOrFault("memory mapping problem\n");
        }

        return realBasePointer + (longPointer & 0x000000FF);
    }

    // special read function for debugging, returning 0xFFFF when unmapped
    public static ushort rawRead(int longPointer)
    {
        if (longPointer < 0 || longPointer > lastVirtualAddress) { return 0xFFFF; }

        int pageNo = longPointer >> PrincOpsDefs.ADDRESSBITS_IN_PAGE;
        ushort flags = pageFlags[pageNo];
        if (isVacant(flags)) { return 0xFFFF; }

        return mem[pageMap[pageNo] + (longPointer & 0x000000FF)];
    }

    // memory access logging

    public static bool doLog = true;

    private static void memLogf(string format, params object[] args)
    {
        if (Config.LOG_MEM_ACCESS && doLog) { Cpu.logf(format, args); }
    }

    // LONG POINTER access (with caching)

    private static int _lastLpVpageRead;
    private static int _lastLpRpageRead;

    private static ushort _readLpWord(int ptr)
    {
        int vPage = ptr & unchecked((int)0xFFFFFF00);
        if (vPage != _lastLpVpageRead)
        {
            _lastLpRpageRead = getRealAddress(vPage, false);
            _lastLpVpageRead = vPage;
        }
        // (IORegion logging block omitted — Phase F)
        return mem[_lastLpRpageRead | (ptr & 0x000000FF)];
    }

    private static int _lastLpVpageWritten;
    private static int _lastLpRpageWritten;

    private static void _writeLpWord(int ptr, ushort value)
    {
        int vPage = ptr & unchecked((int)0xFFFFFF00);
        if (vPage != _lastLpVpageWritten)
        {
            _lastLpRpageWritten = getRealAddress(vPage, true);
            _lastLpVpageWritten = vPage;
        }
        mem[_lastLpRpageWritten | (ptr & 0x000000FF)] = value;
        // (IORegion logging block omitted — Phase F)
    }

    public static ushort readWord(int longPointer)
    {
        ushort w = _readLpWord(longPointer);
        if (Config.LOG_MEM_ACCESS)
        {
            memLogf(".. readWord( lp = 0x{0:X8} )  -> 0x{1:X4}\n", longPointer, w);
        }
        return w;
    }

    public static void writeWord(int longPointer, ushort word)
    {
        _writeLpWord(longPointer, word);
        if (Config.LOG_MEM_ACCESS)
        {
            memLogf(".. writeWord( lp = 0x{0:X8} , 0x{1:X4} )\n", longPointer, word);
        }
    }

    public static int readDblWord(int longPointer)
    {
        ushort low = _readLpWord(longPointer);
        ushort high = _readLpWord(longPointer + 1);
        int dbl = (high << 16) | low;
        if (Config.LOG_MEM_ACCESS)
        {
            memLogf(".. readDblWord( lp = 0x{0:X8} )  -> 0x{1:X8}\n", longPointer, dbl);
        }
        return dbl;
    }

    public static void writeDblWord(int longPointer, int dblword)
    {
        _writeLpWord(longPointer, (ushort)(dblword & 0xFFFF));
        _writeLpWord(longPointer + 1, (ushort)((uint)dblword >> 16));
        if (Config.LOG_MEM_ACCESS)
        {
            memLogf(".. writeDblWord( lp = 0x{0:X8} , 0x{1:X8} )\n", longPointer, dblword);
        }
    }

    // MDS access (with caching)

    private static int _lastMdsVpageRead;
    private static int _lastMdsRpageRead;

    private static ushort _readLengthenedMDSWord(int ptr)
    {
        int vPage = ptr & unchecked((int)0xFFFFFF00);
        if (vPage != _lastMdsVpageRead)
        {
            _lastMdsRpageRead = getRealAddress(vPage, false);
            _lastMdsVpageRead = vPage;
        }
        // (IORegion logging block omitted — Phase F)
        return mem[_lastMdsRpageRead | (ptr & 0x000000FF)];
    }

    private static int _lastMdsVpageWritten;
    private static int _lastMdsRpageWritten;

    private static void _writeLengthenedMDSWord(int ptr, ushort value)
    {
        int vPage = ptr & unchecked((int)0xFFFFFF00);
        if (vPage != _lastMdsVpageWritten)
        {
            _lastMdsRpageWritten = getRealAddress(vPage, true);
            _lastMdsVpageWritten = vPage;
        }
        mem[_lastMdsRpageWritten | (ptr & 0x000000FF)] = value;
        // (IORegion logging block omitted — Phase F)
    }

    public static ushort readMDSWord(int pointer)
    {
        ushort w = _readLengthenedMDSWord(Cpu.lengthenPointer(pointer));
        if (Config.LOG_MEM_ACCESS)
        {
            memLogf(".. readMDSWord( p = 0x{0:X4} )  -> 0x{1:X4}\n", pointer, w);
        }
        return w;
    }

    public static ushort readMDSWord(int pointer, int offset)
    {
        ushort w = _readLengthenedMDSWord(Cpu.lengthenPointer(pointer + offset));
        if (Config.LOG_MEM_ACCESS)
        {
            memLogf(".. readMDSWord( p = 0x{0:X4} [0x{1:X4}+0x{2:X4}] )  -> 0x{3:X4}\n", pointer + offset, pointer, offset, w);
        }
        return w;
    }

    public static void writeMDSWord(int pointer, ushort value)
    {
        _writeLengthenedMDSWord(Cpu.lengthenPointer(pointer), value);
        if (Config.LOG_MEM_ACCESS)
        {
            memLogf(".. writeMDSWord( p = 0x{0:X4} , 0x{1:X4} )\n", pointer, value);
        }
    }

    public static void writeMDSWord(int pointer, int offset, ushort value)
    {
        _writeLengthenedMDSWord(Cpu.lengthenPointer(pointer + offset), value);
        if (Config.LOG_MEM_ACCESS)
        {
            memLogf(".. writeMDSWord( p = 0x{0:X4} [0x{1:X4}+0x{2:X4}] , 0x{3:X4} )\n", pointer + offset, pointer, offset, value);
        }
    }

    public static void writeMDSWord(int pointer, int value)
    {
        _writeLengthenedMDSWord(Cpu.lengthenPointer(pointer), (ushort)(value & 0xFFFF));
        if (Config.LOG_MEM_ACCESS)
        {
            memLogf(".. writeMDSWord( p = 0x{0:X4} , 0x{1:X4} )\n", pointer, value & 0xFFFF);
        }
    }

    public static void writeMDSWord(int pointer, int offset, int value)
    {
        _writeLengthenedMDSWord(Cpu.lengthenPointer(pointer + offset), (ushort)(value & 0xFFFF));
        if (Config.LOG_MEM_ACCESS)
        {
            memLogf(".. writeMDSWord( p = 0x{0:X4} [0x{1:X4}+0x{2:X4}] , 0x{3:X4} )\n", pointer + offset, pointer, offset, value & 0xFFFF);
        }
    }

    public static int readMDSDblWord(int pointer)
    {
        int ptr = Cpu.lengthenPointer(pointer);
        ushort low = _readLengthenedMDSWord(ptr);
        ushort high = _readLengthenedMDSWord(ptr + 1);
        int dbl = (high << 16) | low;
        if (Config.LOG_MEM_ACCESS)
        {
            memLogf(".. readMDSDblWord( p = 0x{0:X4} )  -> 0x{1:X8}\n", pointer, dbl);
        }
        return dbl;
    }

    public static int readMDSDblWord(int pointer, int offset)
    {
        int ptr = Cpu.lengthenPointer(pointer + offset);
        ushort low = _readLengthenedMDSWord(ptr);
        ushort high = _readLengthenedMDSWord(ptr + 1);
        int dbl = (high << 16) | low;
        if (Config.LOG_MEM_ACCESS)
        {
            memLogf(".. readMDSDblWord( p = 0x{0:X4} [0x{1:X4}+0x{2:X4}] )  -> 0x{3:X8}\n", pointer + offset, pointer, offset, dbl);
        }
        return dbl;
    }

    public static void writeMDSDblWord(int pointer, int value)
    {
        int ptr = Cpu.lengthenPointer(pointer);
        _writeLengthenedMDSWord(ptr, (ushort)(value & 0xFFFF));
        _writeLengthenedMDSWord(ptr + 1, (ushort)((uint)value >> 16));
        if (Config.LOG_MEM_ACCESS)
        {
            memLogf(".. writeMDSDblWord( p = 0x{0:X4} , 0x{1:X8} )\n", pointer, value);
        }
    }

    public static void writeMDSDblWord(int pointer, int offset, int value)
    {
        int ptr = Cpu.lengthenPointer(pointer + offset);
        _writeLengthenedMDSWord(ptr, (ushort)(value & 0xFFFF));
        _writeLengthenedMDSWord(ptr + 1, (ushort)((uint)value >> 16));
        if (Config.LOG_MEM_ACCESS)
        {
            memLogf(".. writeMDSDblWord( p = 0x{0:X4} [0x{1:X4}+0x{2:X4}] , 0x{3:X8} )\n", pointer + offset, pointer, offset, value);
        }
    }

    // code access (with caching)

    public static int getCodeByte(int cb, int pc)
    {
        int vPtr = cb + (pc >> 1);
        int vPage = vPtr & unchecked((int)0xFFFFFF00);
        int rPtr = getRealAddress(vPage, false) | (vPtr & 0x000000FF);
        bool isHighByte = (pc & 0x0001) == 0;

        int codeWord = mem[rPtr];
        return isHighByte ? (codeWord >> 8) & 0x00FF : codeWord & 0x00FF;
    }

    public static void patchCodeByte(int cb, int pc, int codeByte)
    {
        int vPtr = cb + (pc >> 1);
        int vPage = vPtr & unchecked((int)0xFFFFFF00);
        int rPtr = getRealAddress(vPage, false) | (vPtr & 0x000000FF);
        bool isHighByte = (pc & 0x0001) == 0;

        codeByte &= 0x00FF;
        int codeWord = mem[rPtr];
        codeWord = isHighByte
            ? (codeByte << 8) | (codeWord & 0x00FF)
            : (codeWord & 0xFF00) | codeByte;
        mem[rPtr] = (ushort)codeWord;
    }

    private static int _lastCodeVpageRead;
    private static int _lastCodeRpageRead;

    private static ushort _readLengthenedCodeWord(int ptr)
    {
        int vPage = ptr & unchecked((int)0xFFFFFF00);
        if (vPage != _lastCodeVpageRead)
        {
            _lastCodeRpageRead = getRealAddress(vPage, false);
            _lastCodeVpageRead = vPage;
        }
        return mem[_lastCodeRpageRead | (ptr & 0x000000FF)];
    }

    // returns 0..255
    public static int getNextCodeByte()
    {
        int codeWord = _readLengthenedCodeWord(Cpu.CB + (Cpu.PC >> 1));
        bool getHighByte = (Cpu.PC & 0x0001) == 0;
        Cpu.PC++;
        return getHighByte ? (int)((uint)codeWord >> 8) : codeWord & 0x00FF;
    }

    // returns 0..65535
    public static int getNextCodeWord()
    {
        int b1 = getNextCodeByte();
        int b2 = getNextCodeByte();
        return (b1 << 8) | b2;
    }

    public static int peekNextCodeByte()
    {
        int currPC = Cpu.PC;
        int value = getNextCodeByte();
        Cpu.PC = currPC;
        return value;
    }

    public static int peekNextCodeWord()
    {
        int currPC = Cpu.PC;
        int value = getNextCodeWord();
        Cpu.PC = currPC;
        return value;
    }

    public static ushort readCode(short offset) => _readLengthenedCodeWord(Cpu.CB + (offset & 0xFFFF));
    public static ushort readCode(int offset)   => _readLengthenedCodeWord(Cpu.CB + (offset & 0xFFFF));

    // String access

    public static ushort fetchByte(int longPointer, int offset /* LONG CARDINAL */)
    {
        ushort word = readWord(longPointer + ((offset & 0x7FFFFFFF) / 2));
        return ((offset & 1) == 0)
            ? (ushort)((word >> 8) & 0x00FF)
            : (ushort)(word & 0x00FF);
    }

    public static ushort fetchByte(int longPointer, short offset) => fetchByte(longPointer, offset & 0xFFFF);

    public static void storeByte(int longPointer, int offset /* LONG CARDINAL */, ushort b)
    {
        int addr = longPointer + ((offset & 0x7FFFFFFF) / 2);
        ushort word = readWord(addr);
        word = ((offset & 1) == 0)
            ? (ushort)(((b << 8) & 0xFF00) | (word & 0x00FF))
            : (ushort)((word & 0xFF00) | (b & 0x00FF));
        writeWord(addr, word);
    }

    // Intra-word field access

    private static readonly ushort[] FIELD_MASKS =
    {
        0x0001, 0x0003, 0x0007, 0x000f, 0x001f, 0x003f, 0x007f, 0x00ff,
        0x01ff, 0x03ff, 0x07ff, 0x0fff, 0x1fff, 0x3fff, 0x7fff, 0xffff,
    };

    public static ushort readField(ushort sourceWord, int spec8)
    {
        int pos = (spec8 >> 4) & 0x0F;
        int len = spec8 & 0x0F;
        int totalLen = pos + len + 1;
        if (totalLen > PrincOpsDefs.WORD_BITS)
        {
            Cpu.ERROR("readField :: fieldSpec[ pos + len + 1 ] > PrincOpsDefs.WORD_BITS");
        }
        int shiftBy = PrincOpsDefs.WORD_BITS - totalLen;
        return (ushort)((sourceWord >> shiftBy) & FIELD_MASKS[len]);
    }

    public static ushort writeField(ushort sourceWord, int spec8, ushort data)
    {
        int pos = (spec8 >> 4) & 0x0F;
        int len = spec8 & 0x0F;
        int totalLen = pos + len + 1;
        if (totalLen > PrincOpsDefs.WORD_BITS)
        {
            Cpu.ERROR("writeField :: fieldSpec[ pos + len + 1 ] > PrincOpsDefs.WORD_BITS");
        }
        int shiftBy = PrincOpsDefs.WORD_BITS - totalLen;
        ushort mask = FIELD_MASKS[len];
        data &= mask;
        data = (ushort)(data << shiftBy);
        mask = (ushort)(mask << shiftBy);
        return (ushort)((sourceWord & (~mask & 0xFFFF)) | data);
    }

    // Display mapping and access

    private static int vDisplayFrom;
    private static int vDisplayTo;
    private static int pixelsPerWord = 1;
    private static int displayWordsPerLine = 1;

    public static void mapDisplayMemory(int toVirtualPage)
    {
        int realDispMem = lastRealPage + 1;
        Cpu.logTrapOrFault(
            $" ## mapDisplayMemory( toVirtualPage = 0x{toVirtualPage:X8} ) :: realDispMem = 0x{realDispMem:X8} , displayPageSize ={displayPageSize}\n");
        for (int i = 0; i < displayPageSize; i++)
        {
            setMap("mapDisplayMemory", toVirtualPage + i, realDispMem + i, PrincOpsDefs.MAPFLAGS_CLEAR, true);
        }
        displayFirstMappedVirtualPage = toVirtualPage;

        vDisplayFrom = displayFirstMappedVirtualPage * PrincOpsDefs.WORDS_PER_PAGE;
        vDisplayTo = (displayFirstMappedVirtualPage + displayPageSize) * PrincOpsDefs.WORDS_PER_PAGE;
        pixelsPerWord = (activeDisplayType == DisplayType.byteColor) ? 2 : 16;
        displayWordsPerLine = effectivePixelsPerLine / pixelsPerWord;
    }

    public static bool isInDisplayMemory(int vAddr) => vAddr >= vDisplayFrom && vAddr < vDisplayTo;

    public static int getDisplayY(int vAddr, int pixelOffset)
    {
        if (!isInDisplayMemory(vAddr)) { return -1; }
        int wordOffset = (vAddr - vDisplayFrom) + (pixelOffset / pixelsPerWord);
        return wordOffset / displayWordsPerLine;
    }

    public static int getDisplayX(int vAddr, int pixelOffset)
    {
        if (!isInDisplayMemory(vAddr)) { return -1; }
        int wordOffset = (vAddr - vDisplayFrom) + (pixelOffset / pixelsPerWord);
        wordOffset -= (wordOffset / displayWordsPerLine) * displayWordsPerLine;
        return (wordOffset * pixelsPerWord) + (pixelOffset % pixelsPerWord);
    }

    public static ushort[] getDisplayRealMemory() => mem;
    public static int getDisplayVirtualPage() => displayFirstMappedVirtualPage;
    public static int getDisplayRealPage() => displayFirstRealPage;
    public static int getDisplayPageSize() => displayPageSize;

    private static DisplayType activeDisplayType = DisplayType.monochrome;

    public static DisplayType getDisplayType() => activeDisplayType;
    public static int getDisplayPixelWidth() => displayPixelWidth;
    public static int getDisplayPixelHeight() => displayPixelHeight;

    public static void setDisplayMemoryDirty()
    {
        if (displayFirstMappedVirtualPage == 0) { return; }
        for (int i = 0; i < displayPageSize; i++)
        {
            setVPageFlags(displayFirstMappedVirtualPage + i, PrincOpsDefs.MAPFLAGS_DIRTY);
        }
    }

    public static void resetDisplayPagesFlags()
    {
        if (displayFirstMappedVirtualPage == 0) { return; }
        int currAddr = displayFirstMappedVirtualPage;
        int currPage = displayFirstMappedVirtualPage;
        for (int i = 0; i < displayPageSize; i++)
        {
            if (currAddr != _lastLpVpageWritten)
            {
                pageFlags[currPage] = PrincOpsDefs.MAPFLAGS_CLEAR;
            }
            currAddr += PrincOpsDefs.WORDS_PER_PAGE;
            currPage++;
        }
    }

    public static bool locateRealDisplayMemoryInVMMap()
    {
        int displayMemBaseAddress = displayFirstRealPage * PrincOpsDefs.WORDS_PER_PAGE;
        displayFirstMappedVirtualPage = 0;
        for (int page = 0; page < pageMap.Length; page++)
        {
            if (pageMap[page] == displayMemBaseAddress)
            {
                // we found the location of the display in virtual memory
                displayFirstMappedVirtualPage = page;
                // make sure the screen is refreshed (e.g. after display is turned on after a world-swap)
                for (int i = 0; i < displayPageSize; i++)
                {
                    pageFlags[displayFirstMappedVirtualPage + i] |= PrincOpsDefs.MAPFLAGS_DIRTY;
                }
                return true;
            }
        }
        return false;
    }
}
