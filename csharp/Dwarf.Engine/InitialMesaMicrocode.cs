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

// Functionality implemented on real machines by a combination of the IOPs
// firmware and the initial microcode loaded from some source:
//
//   - load the germ file from some source into the boot area of virtual memory
//   - write the boot switches into the loaded germ
//   - write the boot request data into the loaded germ
public static class InitialMesaMicrocode
{
    /*
     * Germ loading
     */

    // virtual pages where to place the germ pages coming from boot source.
    // the germ has its MDS starting at page 0
    private const int Germ_page0 = 512;   // first germ source page is placed at this virtual page
    private const int Germ_page1 = 1;     // subsequent source pages are transferred starting at this virtual page
    private const int Germ_maxPages = 96; // germ extends maximally from page 1 to 95, so max 95 pages + page 0 => 96

    // locations in the germ's system data table (in MDS)
    public const int sFirstGermRequest = 208; // location of the boot request data in the germs system table data
    public const int sGermSwitchesOffset = 14; // offset in words of the boot switches to the germs entry system table data

    // Load a sequence of pages as germ into the memory of the mesa engine.
    //
    // pages           : sequence of 256-word pages containing the germ
    // firstPageIsGFT  : is this a Post-4.0 PrincOps germ (Global Frame Table in first page)?
    //
    // Returns true if the file loaded is plausible; false indicates the page
    // count was outside the allowed range (in which case the load was clamped
    // to the maximum allowed germ size).
    public static bool loadGerm(IReadOnlyList<ushort[]> pages, bool firstPageIsGFT)
    {
        bool plausible = true;
        bool savedDoLog = Mem.doLog;
        int currPageNo = 0;

        Mem.doLog = false;

        int germPages = pages.Count;
        if (germPages > Germ_maxPages)
        {
            Cpu.logWarning("loadGerm: inplausible page count for germ, using only first " + Germ_maxPages + " pages");
            plausible = false;
            germPages = Germ_maxPages;
        }

        // load the first source page to the special location if it is the GFT (Global Frame Table, MDS-relieved Pilot)
        if (firstPageIsGFT)
        {
            copyPage(pages[currPageNo++], Germ_page0);
            germPages--;
        }

        // load the rest of the germ into the germs MDS
        int targetPage = Germ_page1;
        while (germPages-- > 0)
        {
            copyPage(pages[currPageNo++], targetPage++);
        }

        Mem.doLog = savedDoLog;
        return plausible;
    }

    private static void copyPage(ushort[] pageContent, int targetPage)
    {
        ushort mapFlags = Mem.getVPageFlags(targetPage);
        int ptr = targetPage * PrincOpsDefs.WORDS_PER_PAGE;
        for (int i = 0; i < PrincOpsDefs.WORDS_PER_PAGE; i++)
        {
            Mem.writeWord(ptr++, pageContent[i]);
        }
        Mem.setVPageFlags(targetPage, mapFlags);
    }

    // Load a germ file from the filesystem into the memory of the mesa engine.
    //
    // filename        : the filename of the germ file
    // firstPageIsGFT  : is this a Post-4.0 PrincOps germ (Global Frame Table in first page)?
    public static bool loadGerm(string filename, bool firstPageIsGFT)
    {
        bool plausible = true;
        bool savedDoLog = Mem.doLog;
        try
        {
            using var fis = new FileStream(filename, FileMode.Open, FileAccess.Read);
            Mem.doLog = false;

            long germSize = fis.Length;
            if (germSize < PrincOpsDefs.BYTES_PER_PAGE)
            {
                throw new IOException("File '" + filename + "' too small for being a germ file");
            }
            if ((germSize % PrincOpsDefs.BYTES_PER_PAGE) != 0)
            {
                Cpu.logWarning("loadGerm: length of germ file '" + filename + "' not a multiple of page-size (256 words)");
                plausible = false;
            }

            long germPages = germSize / PrincOpsDefs.BYTES_PER_PAGE;
            if (germPages > Germ_maxPages)
            {
                Cpu.logWarning("loadGerm: inplausible page count for germ file '" + filename + "', using only first " + Germ_maxPages + " pages");
                plausible = false;
                germPages = Germ_maxPages;
            }

            // load the first source page to the special location if it is the GFT (Global Frame Table, MDS-relieved Pilot)
            if (firstPageIsGFT)
            {
                loadGermPage(fis, Germ_page0);
                germPages--;
            }

            // load the rest of the germ into the germs MDS
            int targetPage = Germ_page1;
            while (germPages-- > 0)
            {
                loadGermPage(fis, targetPage++);
            }
        }
        finally
        {
            Mem.doLog = savedDoLog;
        }
        return plausible;
    }

    // Load a germ from an in-memory byte buffer (e.g. the embedded base144.raw
    // image). Same plausibility checking as the file overload.
    public static bool loadGerm(byte[] germBytes, bool firstPageIsGFT)
    {
        bool plausible = true;
        bool savedDoLog = Mem.doLog;
        try
        {
            using var stream = new MemoryStream(germBytes, writable: false);
            Mem.doLog = false;

            long germSize = stream.Length;
            if (germSize < PrincOpsDefs.BYTES_PER_PAGE)
            {
                throw new IOException("Germ byte buffer too small (need at least 1 page = 512 bytes)");
            }
            if ((germSize % PrincOpsDefs.BYTES_PER_PAGE) != 0)
            {
                Cpu.logWarning("loadGerm: germ byte buffer length not a multiple of page-size (256 words)");
                plausible = false;
            }

            long germPages = germSize / PrincOpsDefs.BYTES_PER_PAGE;
            if (germPages > Germ_maxPages)
            {
                Cpu.logWarning("loadGerm: inplausible page count for germ byte buffer, using only first " + Germ_maxPages + " pages");
                plausible = false;
                germPages = Germ_maxPages;
            }

            if (firstPageIsGFT)
            {
                loadGermPage(stream, Germ_page0);
                germPages--;
            }

            int targetPage = Germ_page1;
            while (germPages-- > 0)
            {
                loadGermPage(stream, targetPage++);
            }
        }
        finally
        {
            Mem.doLog = savedDoLog;
        }
        return plausible;
    }

    //	private static void logf(String pattern, Object... args) {
    //		System.out.printf(pattern, args);
    //	}

    //	private static int germFilePage = 0;

    private static void loadGermPage(Stream fis, int targetPage)
    {
        ushort mapFlags = Mem.getVPageFlags(targetPage);
        int ptr = targetPage * PrincOpsDefs.WORDS_PER_PAGE;

        //		logf("\n** germ file page %d => targetPage %d => virtual mem start 0x%08X", germFilePage++, targetPage, ptr);

        for (int i = 0; i < PrincOpsDefs.WORDS_PER_PAGE; i++)
        {
            //			if ((i % 16) == 0) {
            //				logf("\n  0x%08X :", ptr);
            //			}
            int upper = fis.ReadByte() & 0xFF;
            int lower = fis.ReadByte() & 0xFF;
            ushort word = (ushort)(((upper << 8) | lower) & 0xFFFF);
            Mem.writeWord(ptr++, word);
            //			logf(" %04X", word);
        }
        Mem.setVPageFlags(targetPage, mapFlags);
        //		logf("\n-------------------\n");
    }

    /*
     * Boot switches
     */

    // Set the boot switches based on the characters in the passed string;
    // each character addresses one of the 256 switch positions through its
    // byte code.
    public static void setBootSwitches(string switches)
    {
        int switchesInMds = PrincOpsDefs.getSdMdsPtr(sFirstGermRequest) + sGermSwitchesOffset;
        ushort[] switchesBits = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

        // parse the switches string and accumulate them into the bit positions
        List<ushort> switchIndexes = parseSwitches(switches);
        foreach (ushort s in switchIndexes)
        {
            int w = s >>> 4;
            ushort bit = (ushort)(0x8000 >> (s & 0x0F));
            switchesBits[w] |= bit;
        }

        // implant the switch bits
        for (int i = 0; i < switchesBits.Length; i++)
        {
            Mem.writeMDSWord(switchesInMds, i, switchesBits[i]);
        }
    }

    private static List<ushort> parseSwitches(string switches)
    {
        var v = new List<ushort>();

        int len = switches.Length;
        int temp = 0;
        int inOctal = -1;
        for (int i = 0; i < len; i++)
        {
            char c = switches[i];
            if (inOctal > -1)
            {
                if (c >= '0' && c <= '7')
                {
                    temp = (temp << 3) | (c - '0');
                    inOctal++;
                    if (inOctal > 2)
                    {
                        // we had 3 octal digits -> store the value
                        inOctal = -1;
                        v.Add((ushort)(temp & 0x00FF));
                    }
                }
                else
                {
                    // not an expected octal digit -> abort parsing
                    inOctal = -1;
                    v.Add((ushort)((c - '\0') & 0x00FF));
                }
            }
            else
            {
                if (c == '\\')
                {
                    // start collecting octal digits
                    inOctal = 0;
                }
                else
                {
                    // plain character
                    v.Add((ushort)((c - '\0') & 0x00FF));
                }
            }
        }

        return v;
    }

    /*
     * Boot requests
     */

    // the following boot request offsets start in the germ's MDS at sFirstGermRequest
    // see Dawn :: PrincOps.h
    // see Guam :: Pilot.h

    private const int request_w_requestBasicVersion = 0;
    private const int request_w_action = 1;

    private const int request_w_location_deviceType = 2;
    private const int request_w_location_deviceOrdinal = 3;

    private const int request_dbl_location_disk_fileID = 4;
    private const int request_dbl_location_disk_firstPage = 9;
    private const int request_w_location_disk_da_cylinder = 11;
    private const int request_w_location_disk_da_sectorHead = 12;

    private const int request_w_location_ether_bfn1 = 4; // boot file number as HostNumber (3 words)
    private const int request_w_location_ether_bfn2 = 5;
    private const int request_w_location_ether_bfn3 = 6;
    private const int request_w_location_ether_networkNumber1 = 7;
    private const int request_w_location_ether_networkNumber2 = 8;
    private const int request_w_location_ether_hostNumber1 = 9;
    private const int request_w_location_ether_hostNumber2 = 10;
    private const int request_w_location_ether_hostNumber3 = 11;
    private const int request_w_location_ether_socket = 12;

    private const int request_w_location_any_a = 4;
    private const int request_w_location_any_b = 5;
    private const int request_w_location_any_c = 6;
    private const int request_w_location_any_d = 7;
    private const int request_w_location_any_e = 8;
    private const int request_w_location_any_f = 9;
    private const int request_w_location_any_g = 10;
    private const int request_w_location_any_h = 11;

    private const int request_w_requestExtensionVersion = 13;

    // switches start here at offset 14 of sFirstGermRequest => sGermSwitches
    private const int request_w16_switches = 14; // length: 16 words = 256 bits

    private const int request_w_inLoadMode = 30;
    private const int request_w_session = 31;

    private const int request_SIZE = 32;

    // constants for some of the above fields

    private const ushort CurrentRequestBasicVersion     = 1838; // octal 03456
    private const ushort CurrentRequestExtensionVersion = 4012; // octal 07654

    private const ushort Action_inLoad             = 0;
    private const ushort Action_outLoad            = 1;
    private const ushort Action_bootPhysicalVolume = 2;
    private const ushort Action_teledebug          = 3;
    private const ushort Action_noOp               = 4;

    private const ushort DeviceType_ethernet       = 6;
    private const ushort DeviceType_anyPilotDisk   = 64;
    private const ushort DeviceType_anyFloppy      = 17;
    private const ushort DeviceType_stream         = 4000;

    private const ushort Session_continuingAfterOutLoad = 0;
    private const ushort Session_newSession             = 1;

    private static void putRequestWord(int offset, ushort value)
    {
        Mem.writeMDSWord(PrincOpsDefs.getSdMdsPtr(sFirstGermRequest), offset, value);
    }

    private static void clearRequest()
    {
        ushort zero = 0;
        for (int i = 0; i < request_SIZE; i++)
        {
            putRequestWord(i, zero);
        }
        putRequestWord(request_w_requestBasicVersion, CurrentRequestBasicVersion);
        putRequestWord(request_w_requestExtensionVersion, CurrentRequestExtensionVersion); // necesssary ?
    }

    public static void setBootRequestDisk(ushort deviceOrdinal)
    {
        clearRequest();

        putRequestWord(request_w_action, Action_bootPhysicalVolume);
        putRequestWord(request_w_location_deviceType, DeviceType_anyPilotDisk);
        putRequestWord(request_w_location_deviceOrdinal, deviceOrdinal);
    }

    public static void setBootRequestFloppy(ushort deviceOrdinal)
    {
        clearRequest();

        putRequestWord(request_w_action, Action_inLoad);
        putRequestWord(request_w_location_deviceType, DeviceType_anyFloppy);
        putRequestWord(request_w_location_deviceOrdinal, deviceOrdinal);
    }

    // Java upstream uses octal literals (0_25200004037L). C# has no octal literal
    // syntax, so the decimal-equivalent values are used below with the original
    // octal in a comment for traceability.
    public const long BFN_Daybreak_Germ          = 0xAA00081FL; // octal 025200004037
    public const long BFN_Daybreak_SimpleNetExec = 0xAA000820L; // octal 025200004040
    public const long BFN_Daybreak_Installer     = 0xAA000827L; // octal 025200004047

    public static void setBootRequestEthernet(ushort deviceOrdinal, long bfn)
    {
        clearRequest();

        putRequestWord(request_w_action, Action_inLoad);
        putRequestWord(request_w_location_deviceType, DeviceType_ethernet);
        putRequestWord(request_w_location_deviceOrdinal, deviceOrdinal);

        // boot file number to load (some magic number)
        putRequestWord(request_w_location_ether_bfn1, (ushort)((bfn >> 32) & 0xFFFF));
        putRequestWord(request_w_location_ether_bfn2, (ushort)((bfn >> 16) & 0xFFFF));
        putRequestWord(request_w_location_ether_bfn3, (ushort)(bfn & 0xFFFF));

        // source: unknown/default network, broadcast address, boot socket
        putRequestWord(request_w_location_ether_networkNumber1, 0x0000); // unknown/default network
        putRequestWord(request_w_location_ether_networkNumber2, 0x0000);
        putRequestWord(request_w_location_ether_hostNumber1, 0xFFFF);    // broadcast address
        putRequestWord(request_w_location_ether_hostNumber2, 0xFFFF);
        putRequestWord(request_w_location_ether_hostNumber3, 0xFFFF);
        putRequestWord(request_w_location_ether_socket, 0x000A);         // boot socket
    }

    public static void setBootRequestStream()
    {
        clearRequest();

        putRequestWord(request_w_action, Action_inLoad);
        putRequestWord(request_w_location_deviceType, DeviceType_stream);
        putRequestWord(request_w_location_deviceOrdinal, 0);
    }
}
