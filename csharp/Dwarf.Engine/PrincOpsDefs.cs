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

// Constants defined in the Mesa Processor Principles of Operation Version 4.0
// (May85) document and the "Changed chapters" document, as required for the
// implementation of the mesa engine.
//
// Identifier names are preserved verbatim from the Java port for traceability.
public static class PrincOpsDefs
{
    // basic sizes of a word, a page etc.
    public const int WORD_BITS = 16;

    public const int ADDRESSBITS_IN_PAGE = 8;
    public const int WORDS_PER_PAGE = 256;
    public const int BYTES_PER_PAGE = 512;

    public const int PAGES_PER_SEGMENT = 256; // giving 64K words per segment

    // basic values (Mesa BOOLEAN type, 16-bit word)
    public const ushort FALSE = 0;
    public const ushort TRUE = 1;

    // cSS : Mesa evaluation stack depth
    // fixed by PrincOps (probably: 16 less first/last as guards for StackError)
    public const int cSTACK_LENGTH = 14;

    // cSV : State vector size
    // SIZE[StateVector] + MAX[SIZE[Control Link], SIZE[FSIndex], SIZE[LONG POINTER]]
    public const int cSTATE_VECTOR_SIZE = 18;

    // cWM : wake-up mask  (?? processor dependent... ??)
    public const int cWAKEUP_MASK = 10;

    // cWDC : maximum wake-up disable counter
    public const int cWAKEUP_DISABLE_COUNTER = 7; // minimal value fixed by PrincOps
    public const int WdcMax = 64; // any value > cWAKEUP_DISABLE_COUNTER will do

    // cTickMin/cTickMax : minimum / maximum tick size in milliseconds
    public const int cTICK_MIN = 15;
    public const int cTICK_MAX = 60;

    // TYPE CodeSegment
    public const int cCODESEGMENT_SIZE = 4; // 4 words available at the begin

    // Constant memory locations

    // mPDA : process data area (LONG POINTER, absolute)
    public const int mPROCESS_DATA_AREA = 0x00010000; // 0200000B

    // mGFT : global frame table (LONG POINTER, absolute) (PrincOps post-4.0)
    public const int mGLOBAL_FRAME_TABLE = 0x00020000; // 0400000B

    // mAV : allocation vector (POINTER, MDS-relative)
    public const int mALLOCATION_VECTOR = 0x0100; // 0400B = start of page one in MDS

    // mSD : system data table
    // (POINTER, MDS-relative, table element: ControlLink = LONG UNSPECIFIED, count: 256 elements)
    private const int mSYSTEM_DATA_TBL = 0x0200; // 01000B = start of page two in MDS

    public static int getSdMdsPtr(int index) => mSYSTEM_DATA_TBL + ((index & 0xFF) * 2);

    // mETT : ESC trap table
    // (POINTER, MDS-relative, table element: ControlLink = LONG UNSPECIFIED, count: 256 elements)
    private const int mESC_TRAP_TBL = 0x0400; // 02000B = start of page three in MDS, four pages long

    public static int getEttMdsPtr(int index) => mESC_TRAP_TBL + ((index & 0xFF) * 2);

    // fault queue indexes (3 of 8 possible entries)

    public const int qFRAME_FAULT = 0;
    public const int qPAGE_FAULT = 1;
    public const int qWRITE_PROTECT_FAULT = 2;

    // system data table indexes

    public const int sBoot            =  1; //  1B
    public const int sBoundsTrap      = 14; // 16B
    public const int sBreakTrap       =  0; //  0B
    public const int sCodeTrap        =  7; //  7B
    public const int sControlTrap     =  6; //  6B
    public const int sDivCheckTrap    = 11; // 13B
    public const int sDivZeroTrap     = 10; // 12B
    public const int sInterruptError  = 12; // 14B
    public const int sHardwareError   =  8; // 10B
    public const int sOpcodeTrap      =  5; //  5B
    public const int sPointerTrap     = 15; // 17B
    public const int sProcessTrap     = 13; // 15B
    public const int sRescheduleError =  3; //  3B
    public const int sStackError      =  2; //  2B
    public const int sUnboundTrap     =  9; // 11B
    public const int sXferTrap        =  4; //  4B

    // VM mapping flags
    // PrincOps defines the lower 3 bits; upper bits are free for use.

    public const ushort MAPFLAGS_MASK       = 0x0007;
    public const ushort MAPFLAGS_CLEAR      = 0x0000;
    public const ushort MAPFLAGS_PROTECTED  = 0x0004;
    public const ushort MAPFLAGS_DIRTY      = 0x0002;
    public const ushort MAPFLAGS_REFERENCED = 0x0001;
    public const ushort MAPFLAGS_VACANT     = 0x0006; // PROTECTED and DIRTY but not REFERENCED

    // AVItem-flag-values

    public const int AVITEM_FRAME    = 0;
    public const int AVITEM_EMPTY    = 1;
    public const int AVITEM_INDIRECT = 2;
    public const int AVITEM_UNUSED   = 3;

    // Global frame overhead for PrincOps up to 4.0:
    // - global frames are always located in an MDS
    // - register GF is a POINTER (16 bit)
    // - the global frame overhead also holds the code base reference

    public const int GLOBALOVERHEAD40_SIZE = 4;
    public const int GlobalOverhead40_available = -4; // unspecified
    public const int GlobalOverhead40_word = -3;      // flags: 0x0002 = trapxfers ; 0x0001 = codelinks
    public const int GlobalOverhead40_codebase = -2;  // long pointer to CodeSegment

    // Global frame overhead for PrincOps > 4.0:
    // - global frames are outside an MDS (or not necessary in an MDS)
    // - register GF is a LONG POINTER (32 bit)
    // - the global frame overhead lacks the code base reference

    public const int GLOBALOVERHEAD4x_SIZE = 2;
    public const int GlobalOverhead4x_available = -2; // unspecified
    public const int GlobalOverhead4x_word = -1;      // flags: 0x0002 = trapxfers ; 0x0001 = codelinks

    // Flags in global frame overhead word (common to all PrincOps versions)

    public const int GlobalLinkage_CodeLinks = 0x0001;
    public const int GlobalLinkage_TrapXfers = 0x0002;

    // Local frame overhead

    public const int LOCALOVERHEAD_SIZE = 4;
    public const int LocalOverhead_word = -4;       // available: byte , fsi: FSIndex
    public const int LocalOverhead_returnlink = -3; // ShortControlLink
    public const int LocalOverhead_globallink = -2; // GlobalFrameHandle
    public const int LocalOverhead_pc = -1;         // cardinal

    // Offsets in global frame table entries

    public const int GFTItem_SIZE = 4;        // 4 words long
    public const int GFTItem_globalFrame = 0; // LONG POINTER (GlobalFrameHandle)
    public const int GFTItem_codebase = 2;    // LONG POINTER (TO CodeSegment)

    // Offsets in a PortLink

    public const int Port_inport = 0;
    public const int Port_outport = 2;

    // Not part of PrincOps proper — limits of this specific Dwarf engine implementation.

    public const int MIN_REAL_ADDRESSBITS = 18;    // 18 bits =>   1024 pages =>   256 kwords =  512 KByte real memory
    public const int MAX_REAL_ADDRESSBITS = 23;    // 23 bits =>  32768 pages =>  8192 kwords =   16 MByte real memory

    public const int MAX_VIRTUAL_ADDRESSBITS = 25; // 25 bits => 131072 pages => 32768 kwords =   64 MByte virtual memory
}
