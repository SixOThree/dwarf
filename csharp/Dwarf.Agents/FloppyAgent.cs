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

namespace Dwarf.Agents;

// Agent for a floppy disk drive of a Dwarf machine.
//
// One floppy drive is supported (Pilot may or may not work with more than
// one). The agent runs synchronously: floppy I/O happens during `call()`
// (the CALLAGENT instruction), and the completion interrupt is enqueued at
// the end of the `call()` method.
//
// **Port scope (per RISKS R7)**: only the raw 1.44 MiB 3.5" floppy format
// is supported. Java upstream additionally parses legacy IMD/DMK floppy
// images (8" / 5.25" disks from XDE 4.0+ / ViewPoint 1.0+). The legacy
// formats are read-only and use a complicated re-implant onto a 3.5"
// template — that machinery is **deferred**. Users with IMD/DMK content
// should convert via the Java tool first, or wait for a future port pass.
//
// The .imd / .dmk extensions are rejected at insertFloppy time with a
// `NotSupportedException` pointing at the Java tool.
public class FloppyAgent : Agent
{
    /*
     * faked floppy properties for a 1.44MB, 3 1/2" disks
     */
    internal const int FLOPPY_HEADS = 2;
    internal const int FLOPPY_SECTORS = 18;
    internal const int FLOPPY_CYLS = 80; // nominal number of tracks for a 3 1/2" disk, gives 1440000 bytes
    internal const int FLOPPY_TOTAL_SECTORS = FLOPPY_HEADS * FLOPPY_SECTORS * FLOPPY_CYLS;

    /*
     * FloppyFCBType
     * FloppyDCBType (we support exactly one floppy drive, with a floppy inserted or not)
     */
    private const int fcb_lp_nextIOCB = 0;
    private const int fcb_w_interruptSelector = 2;
    private const int fcb_w_stopAgent = 3;
    private const int fcb_w_agentStopped = 4;
    private const int fcb_w_numberOfDCBs = 5;
    private const int dcb_w_deviceType = 6;
    private const int dcb_w_numberOfCylinders = 7;
    private const int dcb_w_numberOfHeads = 8;
    private const int dcb_w_sectorsPerTrack = 9;
    private const int dcb_w_ready = 10;
    private const int dcb_w_diskChanged = 11;
    private const int dcb_w_twoSided = 12;
    private const int dcb_w_suggestedTries = 13;
    private const int FCB_SIZE = 14;

    // status codes (FloppyDiskFace.Status)
    internal const ushort Status_inProgress       =  0;
    internal const ushort Status_goodCompletion   =  1;
    internal const ushort Status_diskChange       =  2;
    internal const ushort Status_notReady         =  3;
    internal const ushort Status_cylinderError    =  4;
    internal const ushort Status_deletedData      =  5;
    internal const ushort Status_recordNotFound   =  6;
    internal const ushort Status_headerError      =  7;
    internal const ushort Status_dataError        =  8;
    internal const ushort Status_dataLost         =  9;
    internal const ushort Status_writeFault       = 10;
    internal const ushort Status_memoryError      = 11;
    internal const ushort Status_invalidOperation = 12;
    internal const ushort Status_aborted          = 13;
    internal const ushort Status_otherError       = 14;

    internal const ushort Status_invalidCHS       = Status_recordNotFound; // TODO: better status code ??

    // operations
    private const int Op_nop = 0;
    private const int Op_readSector = 1;
    private const int Op_writeSector = 2;
    private const int Op_writeDeletedSector = 3;
    private const int Op_readId = 4;
    private const int Op_formatTrack = 5;
    private static readonly string[] OP_NAMES = {
        "nop", "readSector", "writeSector", "writeDeletedSector", "readId", "formatTrack",
    };

    // FloppyIOCBType
    private const int iocb_w_oper_device = 0;
    private const int iocb_w_oper_function = 1;
    private const int iocb_w_oper_address_cylinder = 2;
    private const int iocb_w_oper_adddress_sectorHead = 3; // head(0..7) , sector(8..15)
    private const int iocb_lp_oper_dataPtr = 4;
    private const int iocb_w_oper_incrementDataPtrAndRetries = 6; // incrementDataPointer(6:0..0) , tries(6:1..15)
    private const int iocb_w_oper_count = 7; // number of sectors to transfer OR number of tracks to format
    private const int iocb_w_density = 8;
    private const int iocb_w_sectorLength = 9;
    private const int iocb_w_sectorsPertrack = 10;
    private const int iocb_w_status = 11; // FloppyDiskFace.Status
    private const int iocb_lp_nextIocb = 12;
    private const int iocb_w_retries = 13;
    private const int iocb_w_logStatus = 14;

    private const int incrementDataPtrFlag = 0x00008000;
    private const int retriesMask = 0x00007FFF;

    internal interface FloppyDisk
    {
        bool isReadonly();
        bool isInvalidCHS(int cylinder, int head, int sector);
        int getLinearSector(int cylinder, int head, int sector);
        ushort readSector(int absSector, int memAddress, int sectorLength);
        ushort writeSector(int absSector, int memAddress, int sectorLength);
        ushort writeDeletedSector(int absSector);
        ushort formatTrack(int cylinder, int head);
        void storeSectorNoIntoIocb(int linearSector, int iocb);
        void saveFloppy(System.Text.StringBuilder? sb);
    }

    // Raw 1.44 MiB 3.5" floppy disk implementation. The on-disk format is a
    // straight stream of 1,474,560 bytes (FLOPPY_HEADS * FLOPPY_CYLS *
    // FLOPPY_SECTORS * BYTES_PER_PAGE), with optional byte-swapping detected
    // by signature bytes on sectors 4 and 6.
    internal sealed class FloppyDisk3dot5 : FloppyDisk
    {
        public const int WORD_SIZE = FLOPPY_HEADS * FLOPPY_CYLS * FLOPPY_SECTORS * PrincOpsDefs.WORDS_PER_PAGE;
        private const int BYTE_SIZE = WORD_SIZE * 2;

        // Java upstream had a class-level `ioBuffer` shared across all floppy
        // instances and used in `synchronized(ioBuffer)` blocks for I/O.
        // The C# port uses per-instance buffers + a static lock object; this
        // keeps the same exclusion guarantee while being clearer about
        // ownership.
        private static readonly object _ioLock = new();
        private readonly byte[] ioBuffer = new byte[4096];

        private readonly string filePath;
        private readonly bool swapBytes;
        private readonly bool readonly_;

        internal readonly ushort[] content;

        private bool changed = false;

        private void logf(string template, params object[] args)
        {
            if (Config.IO_LOG_FLOPPY)
            {
                Console.Write("FloppyFile[" + System.IO.Path.GetFileName(filePath) + "]: " + string.Format(template, args));
            }
        }

        public FloppyDisk3dot5(string filePath, bool readonly_)
        {
            this.content = new ushort[WORD_SIZE];
            this.filePath = filePath;
            if (!File.Exists(filePath))
            {
                logf("file not found\n");
                throw new IOException("Floppy file '" + System.IO.Path.GetFileName(filePath) + "' not found");
            }
            long fileLen = new FileInfo(filePath).Length;
            // Java upstream checks f.canWrite(); on Windows + .NET we use a
            // probe-file approach the same way DiskAgent does.
            bool writable = IsFileWritable(filePath);
            if (!writable)
            {
                this.readonly_ = true;
            }
            else
            {
                this.readonly_ = readonly_;
            }
            if (fileLen != BYTE_SIZE)
            {
                throw new IOException("Floppy file has wrong size " + fileLen + " bytes (instead of " + BYTE_SIZE + ")");
            }

            // load floppy content
            lock (_ioLock)
            {
                using var fis = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                this.swapBytes = this.loadRawContent(fis);
            }
        }

        private static bool IsFileWritable(string filePath)
        {
            try
            {
                using var f = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool loadRawContent(Stream fis)
        {
            // read first 8 sectors
            int read = fis.Read(ioBuffer, 0, ioBuffer.Length);
            _ = read;

            // check signatures on sectors 4 and 6 (counting from 0) to see if bytes must be swapped.
            // A Xerox-formatted floppy has the following signatures:
            //   5th sector (offset 2048) starts with 0xC5D9 => swap if reversed
            //   7th sector (offset 3072) starts with 0xE5D6 => swap if reversed
            bool swapBytes
                 = ioBuffer[2048] == (byte)0xD9 && ioBuffer[2049] == (byte)0xC5
                && ioBuffer[3072] == (byte)0xD6 && ioBuffer[3073] == (byte)0xE5;

            // load file content ioBuffer-wise converting byte-pairs to shorts
            int bufferPos = 0;
            for (int i = 0; i < WORD_SIZE; i++)
            {
                if (bufferPos >= ioBuffer.Length)
                {
                    int r = fis.Read(ioBuffer, 0, ioBuffer.Length);
                    _ = r;
                    bufferPos = 0;
                }
                int b1 = (swapBytes ? ioBuffer[bufferPos + 1] : ioBuffer[bufferPos]) & 0xFF;
                int b2 = (swapBytes ? ioBuffer[bufferPos] : ioBuffer[bufferPos + 1]) & 0xFF;
                bufferPos += 2;
                ushort w = (ushort)((b1 << 8) | b2);
                this.content[i] = w;
            }

            return swapBytes;
        }

        public bool isReadonly() => this.readonly_;

        public void saveFloppy(System.Text.StringBuilder? sb)
        {
            if (this.readonly_) { return; }
            if (!this.changed) { return; }

            logf("saveFloppy(): writing floppy back back");
            lock (_ioLock)
            {
                try
                {
                    using var fos = new FileStream(this.filePath, FileMode.Create, FileAccess.Write);
                    int bufferPos = 0;
                    for (int i = 0; i < WORD_SIZE; i++)
                    {
                        int w = this.content[i] & 0xFFFF;
                        int b1 = this.swapBytes ? w & 0xFF : w >>> 8;
                        int b2 = this.swapBytes ? w >>> 8 : w & 0xFF;
                        ioBuffer[bufferPos++] = (byte)b1;
                        ioBuffer[bufferPos++] = (byte)b2;
                        if (bufferPos >= ioBuffer.Length)
                        {
                            fos.Write(ioBuffer, 0, ioBuffer.Length);
                            bufferPos = 0;
                        }
                    }
                    if (bufferPos > 0) // should not happen, just to be sure...
                    {
                        fos.Write(ioBuffer, 0, bufferPos);
                    }
                }
                catch (IOException e)
                {
                    if (sb != null)
                    {
                        if (sb.Length > 0) { sb.Append('\n'); }
                        sb.Append("Error writing floppy content back: ").Append(e.Message);
                    }
                }
            }
        }

        public bool isInvalidCHS(int cylinder, int head, int sector)
        {
            return cylinder < 0 || cylinder >= FLOPPY_CYLS
                || head < 0 || head >= FLOPPY_HEADS
                || sector < 1 || sector > FLOPPY_SECTORS; // sector counting starts with 1 ...??
        }

        public int getLinearSector(int cylinder, int head, int sector)
        {
            int absoluteSector = (cylinder * FLOPPY_HEADS + head) * FLOPPY_SECTORS + sector - 1; // sector counting starts with 1
            return absoluteSector;
        }

        public void storeSectorNoIntoIocb(int linearSector, int iocb)
        {
            int sector = (linearSector % FLOPPY_SECTORS) + 1;
            int track = linearSector / FLOPPY_SECTORS;
            int head = track % FLOPPY_HEADS;
            ushort cylinder = (ushort)(track / FLOPPY_HEADS);

            ushort sectorHead = (ushort)(((head << 8) | (sector & 0xFF)) & 0xFFFF);

            Mem.writeWord(iocb + iocb_w_oper_address_cylinder, cylinder);
            Mem.writeWord(iocb + iocb_w_oper_adddress_sectorHead, sectorHead);
        }

        public ushort readSector(int absSector, int memAddress, int sectorLength)
        {
            logf("    readSector ( absSector = {0} , sectorLength = {1} , memAddress = 0x{2:X8} => realPage = 0x{3:X6} )\n",
                absSector, sectorLength, memAddress, Mem.getVPageRealPage(memAddress >>> 8));
            if (!Mem.isWritable(memAddress) || !Mem.isWritable(memAddress + PrincOpsDefs.WORDS_PER_PAGE - 1))
            {
                logf(" *error* readSector : target memory not writable\n");
                return Status_memoryError;
            }
            int diskWordOffset = absSector * PrincOpsDefs.WORDS_PER_PAGE;
            if ((diskWordOffset + PrincOpsDefs.WORDS_PER_PAGE) > this.content.Length)
            {
                logf(" *error* readSector : absSector out of range\n");
                return Status_invalidCHS;
            }

            // copy sector content
            for (int i = 0; i < Math.Min(sectorLength, PrincOpsDefs.WORDS_PER_PAGE); i++)
            {
                ushort w = this.content[diskWordOffset++];
                Mem.writeWord(memAddress++, w);
            }

            return Status_goodCompletion;
        }

        public ushort writeSector(int absSector, int memAddress, int sectorLength)
        {
            logf("    writeSector ( absSector = {0} , sectorLength = {1} , memAddress = 0x{2:X8} => realPage = 0x{3:X6} )\n",
                absSector, sectorLength, memAddress, Mem.getVPageRealPage(memAddress >>> 8));
            if (this.readonly_)
            {
                logf(" *error* writeSector : floppy is readonly\n");
                return Status_writeFault;
            }
            if (!Mem.isReadable(memAddress) || !Mem.isReadable(memAddress + PrincOpsDefs.WORDS_PER_PAGE - 1))
            {
                logf(" *error* writeSector : target memory not readable\n");
                return Status_memoryError;
            }
            int diskWordOffset = absSector * PrincOpsDefs.WORDS_PER_PAGE;
            if ((diskWordOffset + PrincOpsDefs.WORDS_PER_PAGE) > this.content.Length)
            {
                logf(" *error* writeSector : absSector out of range\n");
                return Status_invalidCHS;
            }

            // copy sector content
            for (int i = 0; i < Math.Min(sectorLength, PrincOpsDefs.WORDS_PER_PAGE); i++)
            {
                ushort w = Mem.readWord(memAddress++);
                this.content[diskWordOffset++] = w;
            }

            this.changed = true;
            return Status_goodCompletion;
        }

        public ushort writeDeletedSector(int absSector)
        {
            logf("    writeDeletedSector ( absSector = {0} )\n", absSector);
            if (this.readonly_)
            {
                logf(" *error* writeDeletedSector : floppy is readonly\n");
                return Status_writeFault;
            }
            int diskWordOffset = absSector * PrincOpsDefs.WORDS_PER_PAGE;
            if ((diskWordOffset + PrincOpsDefs.WORDS_PER_PAGE) >= this.content.Length)
            {
                logf(" *error* writeDeletedSector : absSector out of range\n");
                return Status_invalidCHS;
            }

            // zero out sector content
            for (int i = 0; i < PrincOpsDefs.WORDS_PER_PAGE; i++)
            {
                this.content[diskWordOffset++] = 0;
            }

            this.changed = true;
            return Status_goodCompletion;
        }

        public ushort formatTrack(int cylinder, int head)
        {
            logf("    formatTrack ( ch = ( {0} , {1} )\n", cylinder, head);
            if (this.isInvalidCHS(cylinder, head, 1))
            {
                logf(" *error* formatTrack ch = ( {0} , {1}  ) out of range\n", cylinder, head);
                return Status_invalidCHS;
            }
            if (this.readonly_)
            {
                logf(" *error* formatTrack : floppy is readonly\n");
                return Status_writeFault;
            }

            // zero out track content (all sectors in one track)
            int diskWordOffset = this.getLinearSector(cylinder, head, 1) * PrincOpsDefs.WORDS_PER_PAGE;
            for (int i = 0; i < PrincOpsDefs.WORDS_PER_PAGE * FLOPPY_SECTORS; i++)
            {
                this.content[diskWordOffset++] = 0;
            }

            this.changed = true;
            return Status_goodCompletion;
        }
    }

    // Java upstream had nested LegacyFloppyDisk / IMDFloppyDisk / DMKFloppyDisk
    // classes for parsing 8" (.imd) and 5.25" (.dmk) legacy floppy images and
    // re-implanting their content on a 3.5" template. Deferred per RISKS R7;
    // see PROGRESS.md for the rationale.

    public FloppyAgent(int fcbAddress)
        : base(AgentDevice.floppyAgent, fcbAddress, FCB_SIZE)
    {
        this.enableLogging(Config.IO_LOG_FLOPPY);
    }

    public override void shutdown(System.Text.StringBuilder errMsgTarget)
    {
        // save current floppy if present
        if (this.currFloppy != null)
        {
            this.currFloppy.saveFloppy(errMsgTarget);
        }
    }

    // Exclusion lock between the UI thread (insertFloppy/ejectFloppy) and
    // the engine thread (refreshMesaMemory). Java relied on `synchronized`
    // on the instance for refreshMesaMemory only; the C# port uses an
    // explicit lock object and locks insertFloppy/ejectFloppy too for
    // visibility of writes to nextFloppy / nextEjected.
    private readonly object _lock = new();

    public override void refreshMesaMemory()
    {
        lock (_lock)
        {
            if (this.nextFloppy != null || this.nextEjected)
            {
                // save current floppy if present
                if (this.currFloppy != null)
                {
                    this.currFloppy.saveFloppy(null);
                }

                // load the floppy
                this.currFloppy = this.nextFloppy;
                this.floppyChanged = (this.currFloppy != null);

                // reset the "next" data
                this.nextFloppy = null;
                this.nextEjected = false;
            }
        }
    }

    private FloppyDisk? nextFloppy = null;
    private bool nextEjected = false;

    private FloppyDisk? currFloppy = null;
    private bool floppyChanged = false;

    // Insert a floppy image into the virtual drive. Per RISKS R7, only the
    // raw 1.44 MiB format is supported in the C# port. Files with .imd or
    // .dmk extensions are rejected at this entry point.
    //
    // Returns true if the floppy is effectively readonly (caller asked for
    // r/w but the underlying file isn't writable).
    public bool insertFloppy(string filePath, bool readonly_)
    {
        string fname = System.IO.Path.GetFileName(filePath);
        string ext = System.IO.Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();
        bool isImd = ext == "imd";
        bool isDmk = ext == "dmk";

        if (isImd || isDmk)
        {
            throw new NotSupportedException(
                $"Legacy floppy format '.{ext}' is not yet supported in the C# port (see RISKS.md R7). "
                + $"Convert '{fname}' to a 1.44 MiB raw 3.5\" image using the Java Dwarf tool first.");
        }

        lock (_lock)
        {
            this.nextFloppy = new FloppyDisk3dot5(filePath, readonly_);
            this.nextEjected = false;
            return this.nextFloppy.isReadonly();
        }
    }

    // Remove the current floppy. Since modifications are buffered in RAM,
    // the actual write-back to disk happens on the *next* refreshMesaMemory
    // tick (or on shutdown), not synchronously here.
    public void ejectFloppy()
    {
        lock (_lock)
        {
            this.nextFloppy = null;
            this.nextEjected = true;
        }
    }

    private int reads = 0;
    private int writes = 0;

    public int getReads() => this.reads;
    public int getWrites() => this.writes;

    public override void call()
    {
        // process the stopAgent flag
        bool stopAgent = (this.getFcbWord(fcb_w_stopAgent) != PrincOpsDefs.FALSE);
        logf("call() - stopAgent = {0}\n", stopAgent ? "true" : "false");
        if (stopAgent)
        {
            logf("call() - stop agent\n");
            this.setFcbWord(fcb_w_agentStopped, PrincOpsDefs.TRUE);
            return;
        }
        this.setFcbWord(fcb_w_agentStopped, PrincOpsDefs.FALSE);

        // get the (first) IOCB to process
        int iocb = this.getFcbDblWord(fcb_lp_nextIOCB);
        if (iocb == 0)
        {
            logf("call() - iocb == 0 (done, no iocb)\n");
            return; // no IOCB, nothing to be done
        }

        // get the interrupt to use when I/O is complete
        ushort interruptSelector = this.getFcbWord(fcb_w_interruptSelector);
        logf("call() - interruptSelector = 0x{0:X4}\n", interruptSelector & 0xFFFF);

        // process all IOCBs passed
        while (iocb != 0)
        {
            // fetch the IOCB data
            int deviceIndex = Mem.readWord(iocb + iocb_w_oper_device) & 0xFFFF;
            int function = Mem.readWord(iocb + iocb_w_oper_function) & 0xFFFF;
            int cylinder = Mem.readWord(iocb + iocb_w_oper_address_cylinder) & 0xFFFF;
            int sectorHead = Mem.readWord(iocb + iocb_w_oper_adddress_sectorHead) & 0xFFFF;
            int head = sectorHead >>> 8;
            int sector = sectorHead & 0xFF;
            int dataPtr = Mem.readDblWord(iocb + iocb_lp_oper_dataPtr);
            int incrementDataPtrAndRetries = Mem.readWord(iocb + iocb_w_oper_incrementDataPtrAndRetries) & 0xFFFF;
            bool incrementDataPtr = (incrementDataPtrAndRetries & incrementDataPtrFlag) != 0;
            int retries = incrementDataPtrAndRetries & retriesMask;
            int count = Mem.readWord(iocb + iocb_w_oper_count) & 0xFFFF;
            int density = Mem.readWord(iocb + iocb_w_density) & 0xFFFF;
            int sectorLength = Mem.readWord(iocb + iocb_w_sectorLength) & 0xFFFF;
            int sectorsPerTrack = Mem.readWord(iocb + iocb_w_sectorsPertrack) & 0xFFFF;
            ushort status = Mem.readWord(iocb + iocb_w_status);

            // log the next operation to perform
            string functionName = (function < OP_NAMES.Length) ? OP_NAMES[function] : "*invalid*";
            logf(
                "  iocb = 0x{0:X8} : function = {1} ({2})\n"
                + "       -> chs = ( {3} , {4} , {5} ) , dataPtr = 0x{6:X8}, incrDataPtr = {7}\n"
                + "       -> retries = {8} , count = {9} , density = {10} , sectorLength = {11} , sectorsPerTrack = {12}\n",
                iocb, function, functionName,
                cylinder, head, sector, dataPtr, incrementDataPtr ? "true" : "false",
                retries, count, density, sectorLength, sectorsPerTrack);

            // check if the only floppy drive is accessed and a floppy is inserted
            if (deviceIndex != 0)
            {
                Mem.writeWord(iocb + iocb_w_status, Status_otherError);
                iocb = Mem.readDblWord(iocb + iocb_lp_nextIocb);
                continue;
            }
            if (this.currFloppy == null)
            {
                Mem.writeWord(iocb + iocb_w_status, Status_notReady);
                iocb = Mem.readDblWord(iocb + iocb_lp_nextIocb);
                continue;
            }

            // do the operation requested in this IOCB
            switch (function)
            {
                case Op_nop:
                {
                    status = Status_goodCompletion;
                    break;
                }

                case Op_readSector:
                {
                    // check the specified sector address
                    if (this.currFloppy.isInvalidCHS(cylinder, head, sector))
                    {
                        status = Status_invalidCHS;
                        break;
                    }

                    // initialize and process the number of requested sectors
                    int linearSector = this.currFloppy.getLinearSector(cylinder, head, sector);
                    int memAddress = dataPtr;
                    status = Status_goodCompletion;
                    while (count > 0 && linearSector < FLOPPY_TOTAL_SECTORS)
                    {
                        // do it
                        status = this.currFloppy.readSector(linearSector, memAddress, sectorLength);
                        if (status != Status_goodCompletion) { break; }

                        // decrement the sector count and inform Pilot
                        count--;
                        Mem.writeWord(iocb + iocb_w_oper_count, (ushort)(count & 0xFFFF));

                        // go to the next sector and inform Pilot
                        linearSector++;
                        this.currFloppy.storeSectorNoIntoIocb(linearSector, iocb);

                        // go to next memory target block and inform Pilot (if requested)
                        if (incrementDataPtr)
                        {
                            memAddress += sectorLength;
                            Mem.writeDblWord(iocb + iocb_lp_oper_dataPtr, memAddress);
                        }

                        // update the stats
                        this.reads++;
                    }
                    break;
                }

                case Op_writeSector:
                {
                    // write a different disk without reading it is a problem
                    if (this.floppyChanged)
                    {
                        status = Status_diskChange;
                        break;
                    }
                    // check the specified sector address
                    if (this.currFloppy.isInvalidCHS(cylinder, head, sector))
                    {
                        status = Status_invalidCHS;
                        break;
                    }

                    // initialize and process the number of requested sectors
                    int linearSector = this.currFloppy.getLinearSector(cylinder, head, sector);
                    int memAddress = dataPtr;
                    status = Status_goodCompletion;
                    while (count > 0 && linearSector < FLOPPY_TOTAL_SECTORS)
                    {
                        // do it
                        status = this.currFloppy.writeSector(linearSector, memAddress, sectorLength);
                        if (status != Status_goodCompletion) { break; }

                        // decrement the sector count and inform Pilot
                        count--;
                        Mem.writeWord(iocb + iocb_w_oper_count, (ushort)(count & 0xFFFF));

                        // go to the next sector and inform Pilot
                        linearSector++;
                        this.currFloppy.storeSectorNoIntoIocb(linearSector, iocb);

                        // go to next memory source block and inform Pilot (if requested)
                        Mem.writeWord(iocb + iocb_w_oper_count, (ushort)(count & 0xFFFF));
                        if (incrementDataPtr)
                        {
                            memAddress += sectorLength;
                            Mem.writeDblWord(iocb + iocb_lp_oper_dataPtr, memAddress);
                        }

                        // update the stats
                        this.writes++;
                    }
                    break;
                }

                case Op_writeDeletedSector:
                {
                    // write a different disk without reading it is a problem
                    if (this.floppyChanged)
                    {
                        status = Status_diskChange;
                        break;
                    }
                    // check the specified sector address
                    if (this.currFloppy.isInvalidCHS(cylinder, head, sector))
                    {
                        status = Status_invalidCHS;
                        break;
                    }

                    // initialize and process the number of requested sectors
                    int linearSector = this.currFloppy.getLinearSector(cylinder, head, sector);
                    status = Status_goodCompletion;
                    while (count > 0 && linearSector < FLOPPY_TOTAL_SECTORS)
                    {
                        // do it
                        status = this.currFloppy.writeDeletedSector(linearSector);
                        if (status != Status_goodCompletion) { break; }

                        // decrement the sector count and inform Pilot
                        count--;
                        Mem.writeWord(iocb + iocb_w_oper_count, (ushort)(count & 0xFFFF));

                        // go to the next sector and inform Pilot
                        linearSector++;
                        this.currFloppy.storeSectorNoIntoIocb(linearSector, iocb);

                        // do the stats
                        this.writes++;
                    }
                    break;
                }

                case Op_readId:
                {
                    status = Status_invalidOperation;
                    break;
                }

                case Op_formatTrack:
                {
                    // write a different disk without reading it is a problem
                    if (this.floppyChanged)
                    {
                        status = Status_diskChange;
                        break;
                    }
                    // check the specified sector address
                    if (this.currFloppy.isInvalidCHS(cylinder, head, sector))
                    {
                        status = Status_invalidCHS;
                        break;
                    }

                    // format the requested number of tracks
                    while (count > 0)
                    {
                        // do it
                        status = this.currFloppy.formatTrack(cylinder, head);
                        if (status != Status_goodCompletion) { break; }

                        // decrement the track count and inform Pilot
                        count--;
                        Mem.writeWord(iocb + iocb_w_oper_count, (ushort)(count & 0xFFFF));

                        // switch to the next track
                        head++;
                        if (head >= FLOPPY_HEADS)
                        {
                            head = 0;
                            cylinder++;
                            if (cylinder > FLOPPY_CYLS) { break; }
                        }

                        // do the stats
                        this.writes++;
                    }
                    break;
                }

                default:
                    status = Status_notReady;
                    break;
            }

            logf("  iocb = 0x{0:X8}  => status = {1}\n", iocb, status);

            // put the status of this IOCB
            Mem.writeWord(iocb + iocb_w_status, status);

            // go to next IOCB
            iocb = Mem.readDblWord(iocb + iocb_lp_nextIocb);
        }

        // Pilot was already informed that the disk changed, so it is already used on the next operation
        this.floppyChanged = false;

        logf("call() - done\n");

        // unconditionally enqueue an interrupt
        Processes.requestMesaInterrupt(interruptSelector);
    }

    protected override void initializeFcb()
    {
        this.setFcbDblWord(fcb_lp_nextIOCB, 0);
        this.setFcbWord(fcb_w_interruptSelector, (ushort)0);
        this.setFcbWord(fcb_w_stopAgent, PrincOpsDefs.FALSE);
        this.setFcbWord(fcb_w_agentStopped, PrincOpsDefs.TRUE);
        this.setFcbWord(fcb_w_numberOfDCBs, (ushort)1);

        this.setFcbWord(dcb_w_deviceType, (ushort)PilotDefs.Device_microFloppy);
        this.setFcbWord(dcb_w_numberOfHeads, (ushort)FLOPPY_HEADS);
        this.setFcbWord(dcb_w_sectorsPerTrack, (ushort)FLOPPY_SECTORS);
        this.setFcbWord(dcb_w_numberOfCylinders, (ushort)FLOPPY_CYLS);
        this.setFcbWord(dcb_w_ready, PrincOpsDefs.FALSE);
        this.setFcbWord(dcb_w_diskChanged, PrincOpsDefs.TRUE);
        this.setFcbWord(dcb_w_twoSided, PrincOpsDefs.TRUE);
        this.setFcbWord(dcb_w_suggestedTries, (ushort)1);
    }
}
