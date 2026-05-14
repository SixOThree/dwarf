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

// Agent for the harddisk of a Dwarf machine.
//
// Only one single disk is currently supported, but this could be easily
// extended if necessary — the disk itself is handled by an internal class.
//
// This implementation works with a fully cached disk content: the complete
// disk is loaded into memory at initialization and all changes (writes) to
// the disk are buffered, so no I/O operations to the external medium occur
// while the mesa engine is running and reads/writes to the disk.
//
// **Port simplification per DECISIONS.md §8**: the Java implementation
// writes a DEFLATE-compressed `.zdelta` file on shutdown and rotates old
// deltas; on next boot it overlays that delta on top of the base file. The
// C# port does *not* read those `.zdelta` files — users still run Java's
// `-merge` to fold them into a canonical base before switching.
//
// Phase H (2026-05-13): C#-native `.cscheck` checkpoint format added.
// On shutdown, dirty pages are written to `<name>.dsk.cscheck`. On boot,
// any existing `.cscheck` is applied on top of the base. Distinct from
// Java's `.zdelta` (different magic / compression / endianness) — the
// two formats can't be confused. See `Dwarf.Iop6085.HDisk.cs` for the
// Draco variant; same on-disk format with flag bit 0 = 0 for Duchess.
//
// The agent works synchronously: disk I/O occurs during `call()` (the
// CALLAGENT instruction). The interrupt signaling the end of operation is
// requested at the end of `call()`.
public class DiskAgent : Agent
{
    // Exception thrown if a delta file is invalid. Currently unused — the C#
    // port does not load deltas — but the class is preserved so that the
    // future C#-native checkpoint format can adopt the same error type.
    private sealed class DeltaCorrupted : Exception
    {
        public DeltaCorrupted() { }
        public DeltaCorrupted(string message) : base(message) { }
    }

    // Implementation of a single simulated hard disk providing the basic
    // read/write/verify operations of `DiskAgent`. Owns the full in-RAM
    // cache of the disk content; reads come from `content[]` and writes
    // mutate it in place (no persistence in the Phase D-2 port).
    public sealed class DiskFile
    {
        private readonly string filePath;          // the "full" file for the disk
        private readonly int cylinders;             // number of simulated cylinders (computed from file size)
        private readonly bool externalByteSwapped;  // true if pilot physical disk seal word is present AND has swapped nibbles

        // is the disk readonly?
        public readonly bool readonly_;

        // the cached disk content. `internal` so round-trip tests in
        // Dwarf.Tests can synthesize and verify content directly without
        // going through the Mem-mediated writePage/readPage path.
        internal readonly ushort[] content;

        // Phase H: per-page dirty tracking. Cumulative since the base
        // was loaded — includes pages restored from a `.cscheck` overlay
        // at boot. `saveDisk` writes every page whose flag is true.
        // `internal` for direct access from Dwarf.Tests round-trip tests.
        internal readonly bool[] pagesChanged;

        // .cscheck format constants — same shape as the Draco variant
        // (see csharp/Dwarf.Iop6085/HDisk.cs). The flags byte
        // distinguishes Duchess (= 0) from Draco (= 0x0001).
        private const string EXT_CHECKPOINT = ".cscheck";
        private const string EXT_CHECKPOINT_TEMP = ".cscheck.tmp";
        private const uint CHECKPOINT_MAGIC = 0x48435744; // "DWCH" little-endian
        private const ushort CHECKPOINT_VERSION = 1;
        private const ushort CHECKPOINT_FLAG_DRACO = 0x0001; // bit 0 set => Draco; clear => Duchess

        // Rotation count. Phase D-2 accepted-but-ignored; Phase H wires it in.
        private readonly int deltasToKeep;

        // local logging function
        private void logf(string template, params object[] args)
        {
            if (Config.IO_LOG_DISK)
            {
                Console.Write("DiskFile[" + System.IO.Path.GetFileName(filePath) + "]: " + string.Format(template, args));
            }
        }

        // Constructor.
        //
        // filePath      : path to the raw disk content file. The filename is
        //                 also used to look up any future C#-native checkpoint.
        // readonly_     : is the disk to be readonly?
        // deltasToKeep  : number of old delta files to preserve after saving a
        //                 new delta. Currently unused (the C# port doesn't
        //                 write deltas); the parameter is kept for signature
        //                 compatibility with Java callers and for the future
        //                 C#-native checkpoint format.
        public DiskFile(string filePath, bool readonly_, int deltasToKeep)
        {
            long fileLength = new FileInfo(filePath).Length;
            int wordLength = ((int)(fileLength & 0xFFFFFFFF) + 1) / 2;

            this.filePath = filePath;
            this.cylinders = wordLength / (DISK_HEADS * DISK_SECTORS * PrincOpsDefs.WORDS_PER_PAGE);
            this.content = new ushort[wordLength];

            string? dir = System.IO.Path.GetDirectoryName(filePath);
            bool dirWritable = dir != null && IsDirectoryWritable(dir);
            if (!dirWritable)
            {
                this.readonly_ = true; // we won't be able to save a checkpoint
            }
            else
            {
                this.readonly_ = readonly_;
            }

            // Phase H: rotation count for .cscheck archives
            this.deltasToKeep = deltasToKeep;

            // Phase H: per-page dirty flags (one per WORDS_PER_PAGE block)
            int pageCount = wordLength / PrincOpsDefs.WORDS_PER_PAGE;
            this.pagesChanged = new bool[pageCount];

            logf("loading base file - byteLength = {0} => wordLength = {1} , cyls = {2} , heads = {3} , sects = {4}\n",
                fileLength, wordLength, this.cylinders, DISK_HEADS, DISK_SECTORS);

            // test the byte order we must use when reading the file content
            byte[] physicalSeal = new byte[2]; // 1st 2 bytes must be 121212B = 0xA28A
            using (var fis = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                int read = fis.Read(physicalSeal, 0, 2);
                _ = read;
                this.externalByteSwapped = (physicalSeal[0] == (byte)0x8A && physicalSeal[1] == (byte)0xA2);
            }

            // load the whole file content into the cache
            using (var fis = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                int offset = 0;
                for (int i = 0; i < cylinders; i++)
                {
                    if (this.externalByteSwapped)
                    {
                        offset = this.loadCylinderSwapped(fis, offset);
                    }
                    else
                    {
                        offset = this.loadCylinder(fis, offset);
                    }
                }
            }

            logf("done loading base file\n");

            // Phase H: apply C#-native .cscheck overlay if present
            this.tryApplyCheckpoint();
        }

        // Phase H: if a `<base>.cscheck` exists, read it and overlay its
        // pages on the in-memory content. Marks those pages as dirty so a
        // subsequent saveDisk preserves cumulative state.
        private void tryApplyCheckpoint()
        {
            string checkpointPath = this.filePath + EXT_CHECKPOINT;
            if (!File.Exists(checkpointPath)) { return; }

            int pagesLoaded;
            try
            {
                using FileStream fis = new(checkpointPath, FileMode.Open, FileAccess.Read);
                using System.IO.Compression.GZipStream gz = new(fis, System.IO.Compression.CompressionMode.Decompress);

                uint magic = readUInt32LE(gz);
                if (magic != CHECKPOINT_MAGIC)
                {
                    throw new IOException($".cscheck magic mismatch: expected 0x{CHECKPOINT_MAGIC:X8}, got 0x{magic:X8}");
                }
                ushort version = readUInt16LE(gz);
                if (version != CHECKPOINT_VERSION)
                {
                    throw new IOException($".cscheck version unsupported: {version}");
                }
                ushort flags = readUInt16LE(gz);
                if ((flags & CHECKPOINT_FLAG_DRACO) != 0)
                {
                    throw new IOException(".cscheck flags indicate Draco format but loaded into Duchess DiskAgent");
                }
                uint headerPageCount = readUInt32LE(gz);
                if (headerPageCount != (uint)this.pagesChanged.Length)
                {
                    throw new IOException($".cscheck pageCount mismatch: header={headerPageCount}, base={this.pagesChanged.Length}");
                }
                uint changedCount = readUInt32LE(gz);

                pagesLoaded = 0;
                for (uint p = 0; p < changedCount; p++)
                {
                    uint pageIdx = readUInt32LE(gz);
                    if (pageIdx >= (uint)this.pagesChanged.Length)
                    {
                        throw new IOException($".cscheck page index out of range: {pageIdx}");
                    }
                    int wordOffset = (int)pageIdx * PrincOpsDefs.WORDS_PER_PAGE;
                    for (int w = 0; w < PrincOpsDefs.WORDS_PER_PAGE; w++)
                    {
                        this.content[wordOffset + w] = readUInt16LE(gz);
                    }
                    this.pagesChanged[pageIdx] = true;
                    pagesLoaded++;
                }
            }
            catch (Exception ex) when (ex is IOException || ex is System.IO.InvalidDataException)
            {
                // Corrupted file or wrong format — log + skip, run from base only.
                // `InvalidDataException` covers GZipStream malformed input.
                Console.Write($"** .cscheck error: {ex.Message}\n");
                Console.Write("** Skipping checkpoint application; running from base disk only.\n");
                return;
            }

            Console.Write($"applied .cscheck: {pagesLoaded} pages restored from {System.IO.Path.GetFileName(checkpointPath)}\n");
        }

        private static bool IsDirectoryWritable(string dir)
        {
            try
            {
                // Try to create + delete a probe file to verify write access.
                // Cheap and reliable across platforms.
                string probe = System.IO.Path.Combine(dir, $".dwarf-write-probe-{Guid.NewGuid():N}");
                using (File.Create(probe)) { }
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // functionality for loading the raw disk

        private readonly byte[] cylBuffer = new byte[DISK_HEADS * DISK_SECTORS * PrincOpsDefs.BYTES_PER_PAGE];

        private int loadCylinder(FileStream fis, int wordOffset)
        {
            int bytesRead = fis.Read(this.cylBuffer, 0, this.cylBuffer.Length);
            if (bytesRead != this.cylBuffer.Length)
            {
                throw new IOException("short read: got " + bytesRead + " instead of " + this.cylBuffer.Length + " bytes");
            }
            int b = 0;
            int rest = DISK_HEADS * DISK_SECTORS * PrincOpsDefs.WORDS_PER_PAGE;
            while (rest > 0)
            {
                int upper = this.cylBuffer[b++] & 0xFF;
                int lower = this.cylBuffer[b++] & 0xFF;
                ushort word = (ushort)(((upper << 8) | lower) & 0xFFFF);
                this.content[wordOffset++] = word;
                rest--;
            }
            return wordOffset;
        }

        private int loadCylinderSwapped(FileStream fis, int wordOffset)
        {
            int bytesRead = fis.Read(this.cylBuffer, 0, this.cylBuffer.Length);
            if (bytesRead != this.cylBuffer.Length)
            {
                throw new IOException("short read: got " + bytesRead + " instead of " + this.cylBuffer.Length + " bytes");
            }
            int b = 0;
            int rest = DISK_HEADS * DISK_SECTORS * PrincOpsDefs.WORDS_PER_PAGE;
            while (rest > 0)
            {
                int lower = this.cylBuffer[b++] & 0xFF;
                int upper = this.cylBuffer[b++] & 0xFF;
                ushort word = (ushort)(((upper << 8) | lower) & 0xFFFF);
                this.content[wordOffset++] = word;
                rest--;
            }
            return wordOffset;
        }

        // Save a checkpoint for the disk. Writes a `.cscheck` overlay file
        // alongside the base if any pages are dirty. Returns the disk state
        // for the caller:
        //   - ReadOnly: disk was opened readonly; nothing written
        //   - OK:       nothing to do (no dirty pages) OR checkpoint written successfully
        //   - Corrupted: write failed for an I/O reason
        //
        // Phase H (2026-05-13): replaces the Phase D-2 stub.
        public DiskState saveDisk()
        {
            if (this.readonly_)
            {
                return DiskState.ReadOnly;
            }

            int dirtyCount = 0;
            for (int i = 0; i < this.pagesChanged.Length; i++)
            {
                if (this.pagesChanged[i]) { dirtyCount++; }
            }
            if (dirtyCount == 0)
            {
                logf("disk is not changed, no checkpoint written\n");
                return DiskState.OK;
            }

            string checkpointPath = this.filePath + EXT_CHECKPOINT;
            string tempPath = this.filePath + EXT_CHECKPOINT_TEMP;

            try
            {
                if (File.Exists(tempPath)) { File.Delete(tempPath); }
                int pagesWritten = this.writeCheckpoint(tempPath, dirtyCount);
                logf("wrote {0} dirty pages to {1}\n", pagesWritten, System.IO.Path.GetFileName(tempPath));
            }
            catch (Exception e)
            {
                logf("failed to write checkpoint: {0}\n", e.Message);
                try { if (File.Exists(tempPath)) { File.Delete(tempPath); } } catch { /* swallow */ }
                return DiskState.Corrupted;
            }

            // Rotate: archive an existing checkpoint with a timestamp suffix
            if (File.Exists(checkpointPath))
            {
                string ts = DateTime.Now.ToString("yyyy.MM.dd_HH.mm.ss.fff");
                string archivedPath = checkpointPath + "-" + ts;
                try
                {
                    File.Move(checkpointPath, archivedPath);
                }
                catch (Exception e)
                {
                    logf("failed to archive prior checkpoint: {0}\n", e.Message);
                    try { File.Delete(checkpointPath); } catch { /* swallow */ }
                }
            }
            try
            {
                File.Move(tempPath, checkpointPath);
            }
            catch (Exception e)
            {
                logf("failed to finalize checkpoint: {0}\n", e.Message);
                return DiskState.Corrupted;
            }

            this.pruneOldCheckpoints();
            return DiskState.OK;
        }

        // Write the dirty pages to a `.cscheck` file at `path`. Returns the
        // number of pages written. Caller handles atomic rename.
        private int writeCheckpoint(string path, int expectedDirtyCount)
        {
            int pagesWritten = 0;
            using (FileStream fos = new(path, FileMode.Create, FileAccess.Write))
            using (System.IO.Compression.GZipStream gz = new(fos, System.IO.Compression.CompressionLevel.Optimal))
            {
                writeUInt32LE(gz, CHECKPOINT_MAGIC);
                writeUInt16LE(gz, CHECKPOINT_VERSION);
                writeUInt16LE(gz, 0); // flags: 0 == Duchess
                writeUInt32LE(gz, (uint)this.pagesChanged.Length);
                writeUInt32LE(gz, (uint)expectedDirtyCount);

                for (int p = 0; p < this.pagesChanged.Length; p++)
                {
                    if (!this.pagesChanged[p]) { continue; }
                    writeUInt32LE(gz, (uint)p);
                    int wordOffset = p * PrincOpsDefs.WORDS_PER_PAGE;
                    for (int w = 0; w < PrincOpsDefs.WORDS_PER_PAGE; w++)
                    {
                        writeUInt16LE(gz, this.content[wordOffset + w]);
                    }
                    pagesWritten++;
                }
                gz.Flush();
            }
            return pagesWritten;
        }

        // Delete archived checkpoints beyond `deltasToKeep`.
        private void pruneOldCheckpoints()
        {
            string? dir = System.IO.Path.GetDirectoryName(this.filePath);
            if (dir == null) { return; }
            string baseName = System.IO.Path.GetFileName(this.filePath) + EXT_CHECKPOINT + "-";
            string[] archived;
            try
            {
                archived = Directory.GetFiles(dir, baseName + "*");
            }
            catch (IOException)
            {
                return;
            }
            Array.Sort(archived, (a, b) => string.Compare(b, a, StringComparison.Ordinal));
            for (int i = this.deltasToKeep; i < archived.Length; i++)
            {
                try { File.Delete(archived[i]); }
                catch (IOException) { /* swallow */ }
            }
        }

        // Little-endian I/O helpers for the .cscheck format
        private static void writeUInt16LE(Stream s, ushort v)
        {
            s.WriteByte((byte)(v & 0xFF));
            s.WriteByte((byte)((v >> 8) & 0xFF));
        }

        private static void writeUInt32LE(Stream s, uint v)
        {
            s.WriteByte((byte)(v & 0xFF));
            s.WriteByte((byte)((v >> 8) & 0xFF));
            s.WriteByte((byte)((v >> 16) & 0xFF));
            s.WriteByte((byte)((v >> 24) & 0xFF));
        }

        private static ushort readUInt16LE(Stream s)
        {
            int b0 = s.ReadByte();
            int b1 = s.ReadByte();
            if (b0 < 0 || b1 < 0) { throw new IOException(".cscheck truncated"); }
            return (ushort)(b0 | (b1 << 8));
        }

        private static uint readUInt32LE(Stream s)
        {
            int b0 = s.ReadByte();
            int b1 = s.ReadByte();
            int b2 = s.ReadByte();
            int b3 = s.ReadByte();
            if (b0 < 0 || b1 < 0 || b2 < 0 || b3 < 0) { throw new IOException(".cscheck truncated"); }
            return (uint)b0 | ((uint)b1 << 8) | ((uint)b2 << 16) | ((uint)b3 << 24);
        }

        // Phase H: -merge mode. Writes the in-memory state out as a new raw
        // .dsk base (preserving the byte order of the original file), then
        // archives the prior base + all `.cscheck` files into a timestamped
        // zip alongside the disk. After this runs, the C# port boots from a
        // clean base on next start.
        public void mergeDelta(TextWriter messages)
        {
            if (this.readonly_)
            {
                messages.WriteLine($"merge: skipping read-only disk: {System.IO.Path.GetFileName(this.filePath)}");
                return;
            }

            string? dir = System.IO.Path.GetDirectoryName(this.filePath);
            if (dir == null)
            {
                messages.WriteLine($"merge: cannot resolve directory for {System.IO.Path.GetFileName(this.filePath)}");
                return;
            }

            // Build the list of files to archive: current base + current
            // checkpoint + all rotated checkpoints.
            var toArchive = new List<string> { this.filePath };
            string checkpointPath = this.filePath + EXT_CHECKPOINT;
            if (File.Exists(checkpointPath)) { toArchive.Add(checkpointPath); }
            string baseName = System.IO.Path.GetFileName(this.filePath) + EXT_CHECKPOINT + "-";
            string[] rotated;
            try
            {
                rotated = Directory.GetFiles(dir, baseName + "*");
                Array.Sort(rotated, StringComparer.Ordinal);
                toArchive.AddRange(rotated);
            }
            catch (IOException)
            {
                rotated = Array.Empty<string>();
            }

            // 1. Write new base to a temp file
            string newBaseTemp = this.filePath + ".new";
            try { if (File.Exists(newBaseTemp)) { File.Delete(newBaseTemp); } } catch { /* swallow */ }

            messages.WriteLine($"merge: writing new base for {System.IO.Path.GetFileName(this.filePath)} ...");
            this.writeFullBase(newBaseTemp);

            // 2. Archive the old files
            string ts = DateTime.Now.ToString("yyyy.MM.dd_HH.mm.ss.fff");
            string zipPath = this.filePath + "-" + ts + ".zip";
            messages.WriteLine($"merge: archiving prior files into {System.IO.Path.GetFileName(zipPath)} ...");
            using (FileStream zipFs = new(zipPath, FileMode.Create, FileAccess.Write))
            using (var zip = new System.IO.Compression.ZipArchive(zipFs, System.IO.Compression.ZipArchiveMode.Create))
            {
                foreach (string path in toArchive)
                {
                    if (!File.Exists(path)) { continue; }
                    var entry = zip.CreateEntry(System.IO.Path.GetFileName(path), System.IO.Compression.CompressionLevel.Optimal);
                    using Stream entryStream = entry.Open();
                    using FileStream fileStream = new(path, FileMode.Open, FileAccess.Read);
                    fileStream.CopyTo(entryStream);
                }
            }

            // 3. Replace old base with the new one
            try { File.Delete(this.filePath); }
            catch (IOException e) { messages.WriteLine($"!! couldn't remove old base: {e.Message}"); throw; }
            File.Move(newBaseTemp, this.filePath);

            // 4. Delete .cscheck and rotated archives — they're in the zip now
            if (File.Exists(checkpointPath))
            {
                try { File.Delete(checkpointPath); } catch (IOException) { /* swallow */ }
            }
            foreach (string r in rotated)
            {
                try { File.Delete(r); } catch (IOException) { /* swallow */ }
            }

            // 5. Reset dirty flags — the in-memory state IS the new base
            for (int i = 0; i < this.pagesChanged.Length; i++) { this.pagesChanged[i] = false; }

            messages.WriteLine($"merge: done. New base is {System.IO.Path.GetFileName(this.filePath)}; backup is {System.IO.Path.GetFileName(zipPath)}");
        }

        // Write the full in-memory state out as a new raw .dsk file,
        // preserving the byte order of the original (externalByteSwapped
        // means the file uses Intel order, so each word is written as
        // lower-byte then upper-byte; otherwise big-endian).
        private void writeFullBase(string path)
        {
            using FileStream fos = new(path, FileMode.Create, FileAccess.Write);
            byte[] buffer = new byte[this.content.Length * 2];
            int b = 0;
            if (this.externalByteSwapped)
            {
                for (int w = 0; w < this.content.Length; w++)
                {
                    ushort word = this.content[w];
                    buffer[b++] = (byte)(word & 0xFF);
                    buffer[b++] = (byte)((word >> 8) & 0xFF);
                }
            }
            else
            {
                for (int w = 0; w < this.content.Length; w++)
                {
                    ushort word = this.content[w];
                    buffer[b++] = (byte)((word >> 8) & 0xFF);
                    buffer[b++] = (byte)(word & 0xFF);
                }
            }
            fos.Write(buffer, 0, buffer.Length);
            fos.Flush();
        }

        // The number of simulated cylinders for the disk.
        public int getCylinders() => this.cylinders;

        // Read a single page (sector) from the disk into mesa memory.
        //
        // diskWordOffset : linear (word-)offset of the page on the disk
        // memAddress     : mesa virtual memory address to copy the page into
        // returns        : the dcb-status for this disk operation
        public ushort readPage(int diskWordOffset, int memAddress)
        {
            // log and basic plausibility checks
            logf("readpage ( diskWordOffset = 0x{0:X8} , memAddress = 0x{1:X8} => realPage = 0x{2:X6} )\n",
                diskWordOffset, memAddress, Mem.getVPageRealPage(memAddress >>> 8));
            if (diskWordOffset < 0 || (diskWordOffset + PrincOpsDefs.WORDS_PER_PAGE) >= this.content.Length)
            {
                logf(" *error* diskWordOffset[+WORDS_PER_PAGE] out of range\n");
                return Status_seekTimeout; // TODO: better status code
            }
            if (!Mem.isWritable(memAddress) || !Mem.isWritable(memAddress + PrincOpsDefs.WORDS_PER_PAGE - 1))
            {
                logf(" *error* target memory not writable\n");
                return Status_memoryFault;
            }

            // copy page content
            for (int i = 0; i < PrincOpsDefs.WORDS_PER_PAGE; i++)
            {
                ushort w = this.content[diskWordOffset++];
                Mem.writeWord(memAddress++, w);
            }

            return Status_goodCompletion;
        }

        // Write a single page (sector) from mesa memory to the disk.
        public ushort writePage(int diskWordOffset, int memAddress)
        {
            logf("writepage ( diskWordOffset = 0x{0:X8} , memAddress = 0x{1:X8} )\n", diskWordOffset, memAddress);
            if (diskWordOffset < 0 || (diskWordOffset + PrincOpsDefs.WORDS_PER_PAGE) >= this.content.Length)
            {
                logf(" *error* diskWordOffset[+WORDS_PER_PAGE] out of range\n");
                return Status_seekTimeout; // TODO: better status code
            }
            if (!Mem.isReadable(memAddress) || !Mem.isReadable(memAddress + PrincOpsDefs.WORDS_PER_PAGE - 1))
            {
                logf(" *error* target memory not readable\n");
                return Status_memoryFault;
            }

            // Phase H: mark the page dirty for the next .cscheck write.
            int pageIdx = diskWordOffset / PrincOpsDefs.WORDS_PER_PAGE;
            this.pagesChanged[pageIdx] = true;

            // copy page content
            for (int i = 0; i < PrincOpsDefs.WORDS_PER_PAGE; i++)
            {
                this.content[diskWordOffset++] = Mem.readWord(memAddress++);
            }

            return Status_goodCompletion;
        }

        // Verify that the disk page and the memory page have identical content.
        public ushort verifyPage(int diskWordOffset, int memAddress)
        {
            logf("verifypage ( diskWordOffset = 0x{0:X8} , memAddress = 0x{1:X8} )\n", diskWordOffset, memAddress);
            if (diskWordOffset < 0 || (diskWordOffset + PrincOpsDefs.WORDS_PER_PAGE) >= this.content.Length)
            {
                logf(" *error* diskWordOffset[+WORDS_PER_PAGE] out of range\n");
                return Status_seekTimeout; // TODO: better status code
            }
            if (!Mem.isReadable(memAddress) || !Mem.isReadable(memAddress + PrincOpsDefs.WORDS_PER_PAGE - 1))
            {
                logf(" *error* target memory not readable\n");
                return Status_memoryFault;
            }

            // verify page content
            for (int i = 0; i < PrincOpsDefs.WORDS_PER_PAGE; i++)
            {
                if (this.content[diskWordOffset++] != Mem.readWord(memAddress++))
                {
                    return Status_dataVerifyError;
                }
            }

            return Status_goodCompletion;
        }
    }

    // the list of attached disks (however only one is used/supported)
    private static readonly List<DiskFile> diskFiles = new();

    // Add a harddisk to the mesa engine.
    public static DiskState addFile(string filePath, bool readonly_, int deltasToKeep)
    {
        if (diskFiles.Count > 0)
        {
            Cpu.logWarning("DiskAgent.addFile :: only 1 disk currently supported, ignored disk file: " + filePath);
            return DiskState.Other;
        }

        if (!File.Exists(filePath))
        {
            Cpu.ERROR("DiskAgent.addFile :: file not found: " + filePath);
        }
        long diskSizeInBytes = new FileInfo(filePath).Length;
        if (diskSizeInBytes < DISK_MINIMAL_BYTE_SIZE)
        {
            Cpu.ERROR("DiskAgent.addFile :: smaller than minimal size: " + diskSizeInBytes + " bytes (" + filePath + ")");
        }

        try
        {
            if (Config.IO_LOG_DISK)
            {
                Cpu.logInfo("DiskAgent.addFile :: adding file '" + filePath + "'");
            }
            DiskFile diskfile = new DiskFile(filePath, readonly_, deltasToKeep);
            diskFiles.Add(diskfile);
            return (!readonly_ && diskfile.readonly_) ? DiskState.ReadOnly : DiskState.OK;
        }
        catch (IOException e)
        {
            Cpu.ERROR(
                "DiskAgent.addFile :: unable to initialize with file: " + filePath
                + "\n"
                + e.GetType() + ": " + e.Message);
            return DiskState.Other; // keep the compiler happy; Cpu.ERROR does not return
        }
    }

    // Java's `-merge` migration utility; not supported in C#. Emits a hint
    // pointing at the Java implementation.
    public static void mergeDisks(TextWriter messages)
    {
        foreach (DiskFile df in diskFiles)
        {
            df.mergeDelta(messages);
        }
    }

    /*
     * faked disk characteristics
     */
    private const int DISK_HEADS = 2;
    private const int DISK_SECTORS = 16;
    private const long DISK_MINIMAL_BYTE_SIZE = DISK_HEADS * DISK_SECTORS * PrincOpsDefs.BYTES_PER_PAGE; // at least 1 cylinder!

    /*
     * DiskFCBType
     * DiskDCBType x 1 (we support exactly one disk!)
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
    private const int dcb_w6_agentDeviceData = 10;
    private const int FCB_with_DCB_SIZE = 16;

    private const int Command_noOp = 0;
    private const int Command_read = 1;
    private const int Command_write = 2;
    private const int Command_verify = 3;
    private const int Command_format = 4;
    private const int Command_readHeader = 5;
    private const int Command_readHeaderAndData = 6;
    private const int Command_makeBootable = 7;
    private const int Command_makeUnbootable = 8;
    private const int Command_getBootLocation = 9;
    // not defined: reserved10 .. reserved19

    private const ushort Status_inProgress      = 0;
    private const ushort Status_goodCompletion  = 1;
    private const ushort Status_notReady        = 2;
    private const ushort Status_recalibrateError = 3;
    private const ushort Status_seekTimeout     = 4;
    private const ushort Status_headerCRCError  = 5;
    private const ushort Status_reserved6       = 6;
    private const ushort Status_dataCRCError    = 7;
    private const ushort Status_headerNotFound  = 8;
    private const ushort Status_reserved9       = 9;
    private const ushort Status_dataVerifyError = 10;
    private const ushort Status_overrunError    = 11;
    private const ushort Status_writeFault      = 12;
    private const ushort Status_memoryError     = 13;
    private const ushort Status_memoryFault     = 14;
    private const ushort Status_clientError     = 15;
    private const ushort Status_operationReset  = 16;
    private const ushort Status_otherError      = 17;

    private const int iocb_w_op_clientHeader_cylinder = 0;
    private const int iocb_w_op_clientHeader_sectorHead = 1; // high-nibble: sector, low-nibble: head
    private const int iocb_dbl_op_reserved1 = 2;
    private const int iocb_lp_op_dataPtr = 4; // page(!)-aligned data address
    private const int iocb_w_op_ctl = 6; // tries(8), command(6), enableTrackBuffer(1), incrementDataPtr(1)
    private const int iocb_w_op_pageCount = 7;
    private const int iocb_dbl_op_deviceStatus = 8;
    private const int iocb_dbl_op_diskHeader = 10; // target for read header
    private const int iocb_w_op_device = 12;
    private const int iocb_w_deviceIndex = 13;
    private const int iocb_w_diskAddress_cylinder = 14;
    private const int iocb_w_diskAddress_sectorHead = 15; // high-nibble: sector, low-nibble: head
    private const int iocb_lp_dataPtr = 16;
    private const int iocb_w_incrementDataPtr = 18;
    private const int iocb_w_command = 19;
    private const int iocb_w_pageCount = 20;
    private const int iocb_w_status = 21;
    private const int iocb_lp_nextIocb = 22;
    private const int iocb_w10_agentOperationData = 24;

    // Constructor.
    public DiskAgent(int fcbAddress)
        : base(AgentDevice.diskAgent, fcbAddress, FCB_with_DCB_SIZE)
    {
        this.enableLogging(Config.IO_LOG_DISK);
    }

    public override void shutdown(System.Text.StringBuilder errMsgTarget)
    {
        logf("shutdown\n");
        foreach (DiskFile f in diskFiles)
        {
            f.saveDisk();
        }
    }

    public override void refreshMesaMemory()
    {
        // disk agent works synchronously, so currently nothing to transfer
        // to mesa memory
    }

    /*
     * statistical data
     */

    private int reads = 0;
    private int writes = 0;

    public int getReads() => this.reads;
    public int getWrites() => this.writes;

    /*
     * implementation of the public agent methods
     */

    protected override void initializeFcb()
    {
        if (diskFiles.Count != 1)
        {
            Cpu.ERROR("DiskAgent.initializeFcb :: disk count not 1 (actual: " + diskFiles.Count + ")");
        }

        this.setFcbDblWord(fcb_lp_nextIOCB, 0);
        this.setFcbWord(fcb_w_interruptSelector, (ushort)0);
        this.setFcbWord(fcb_w_stopAgent, PrincOpsDefs.FALSE);
        this.setFcbWord(fcb_w_agentStopped, PrincOpsDefs.TRUE);
        this.setFcbWord(fcb_w_numberOfDCBs, (ushort)1);

        this.setFcbWord(dcb_w_deviceType, (ushort)PilotDefs.Device_anyPilotDisk);
        this.setFcbWord(dcb_w_numberOfHeads, (ushort)DISK_HEADS);
        this.setFcbWord(dcb_w_sectorsPerTrack, (ushort)DISK_SECTORS);
        this.setFcbWord(dcb_w_numberOfCylinders, (ushort)diskFiles[0].getCylinders());
        for (int i = 0; i < 6; i++)
        {
            this.setFcbWord(dcb_w6_agentDeviceData + i, (ushort)0);
        }
    }

    public override void call()
    {
        int iocb = this.getFcbDblWord(fcb_lp_nextIOCB);
        ushort interruptSelector = this.getFcbWord(fcb_w_interruptSelector);

        // check for stopping the agent
        bool stopAgent = (this.getFcbWord(fcb_w_stopAgent) != PrincOpsDefs.FALSE);
        logf("call() - stopAgent = {0}\n", stopAgent ? "true" : "false");
        if (stopAgent)
        {
            logf("call() - stop agent\n");
            this.setFcbWord(fcb_w_agentStopped, PrincOpsDefs.TRUE);
            return;
        }
        this.setFcbWord(fcb_w_agentStopped, PrincOpsDefs.FALSE);

        // is there something to do?
        if (iocb == 0)
        {
            logf("call() - iocb == 0 (done, no iocb)\n");
            return; // no IOCB, nothing to be done
        }

        logf("call() - interruptSelector = 0x{0:X4}\n", interruptSelector & 0xFFFF);

        // process all IOCBs
        while (iocb != 0)
        {
            logf("call() - processing IOCB 0x{0:X8}\n", iocb);

            int diskIndex = Mem.readWord(iocb + iocb_w_deviceIndex);
            int cylinder = Mem.readWord(iocb + iocb_w_diskAddress_cylinder);
            int sectorHead = Mem.readWord(iocb + iocb_w_diskAddress_sectorHead);
            int diskWordOffset = this.getDiskWordOffset(cylinder, sectorHead);

            logf("call() -    diskIndex = {0} , cylinder = 0x{1:X4} , sectorHead = 0x{2:X4} , diskWordOffset = 0x{3:X8}\n",
                diskIndex, cylinder, sectorHead, diskWordOffset);

            int dataPtr = Mem.readDblWord(iocb + iocb_lp_dataPtr);
            bool incrementDataPtr = (Mem.readWord(iocb + iocb_w_incrementDataPtr) != PrincOpsDefs.FALSE);
            int command = Mem.readWord(iocb + iocb_w_command) & 0xFFFF;
            int pageCount = Mem.readWord(iocb + iocb_w_pageCount) & 0xFFFF;

            logf("call() -    command = {0} , dataPtr = 0x{1:X8} , pageCount = {2} , incrementDataPtr = {3}\n",
                command, dataPtr, pageCount, incrementDataPtr ? "true" : "false");

            int currIocb = iocb;
            ushort currIocbStatus = Status_goodCompletion;

            iocb = Mem.readDblWord(iocb + iocb_lp_nextIocb);

            if (diskIndex >= diskFiles.Count)
            {
                Mem.writeWord(currIocb + iocb_w_status, Status_clientError);
                continue;
            }
            DiskFile? disk = diskFiles[diskIndex];
            if (disk == null)
            {
                Mem.writeWord(currIocb + iocb_w_status, Status_notReady);
                continue;
            }

            if (dataPtr == 0)
            {
                Mem.writeWord(currIocb + iocb_w_status, Status_memoryError);
                continue;
            }

            while (pageCount > 0)
            {
                // execute requested disk operation, if implemented
                switch (command)
                {
                    case Command_noOp:
                        currIocbStatus = Status_goodCompletion; // just for completeness
                        break;
                    case Command_read:
                        currIocbStatus = disk.readPage(diskWordOffset, dataPtr);
                        this.reads++;
                        break;
                    case Command_write:
                        currIocbStatus = disk.writePage(diskWordOffset, dataPtr);
                        this.writes++;
                        break;
                    case Command_verify:
                        currIocbStatus = disk.verifyPage(diskWordOffset, dataPtr);
                        break;
                    default:
                        // some unsupported disk operation
                        currIocbStatus = Status_otherError;
                        break;
                }
                if (currIocbStatus != Status_goodCompletion)
                {
                    break;
                }

                // done with this page
                pageCount--;

                // prepare for next sector
                diskWordOffset += PrincOpsDefs.WORDS_PER_PAGE;
                dataPtr += PrincOpsDefs.WORDS_PER_PAGE;

                // update iocb data
                Mem.writeWord(currIocb + iocb_w_pageCount, (ushort)(pageCount & 0xFFFF));
                if (incrementDataPtr)
                {
                    Mem.writeDblWord(currIocb + iocb_lp_dataPtr, dataPtr);
                }
            }

            // save state for this IOCB
            Mem.writeWord(currIocb + iocb_w_status, currIocbStatus);

            logf("call() - done processing IOCB 0x{0:X8} => dataPtr = 0x{1:X8} , pageCount = {2} , status = {3}\n",
                currIocb,
                Mem.readDblWord(currIocb + iocb_lp_dataPtr),
                Mem.readWord(currIocb + iocb_w_pageCount),
                Mem.readWord(currIocb + iocb_w_status));
        }

        // raise interrupt after having processed all IOCBs
        Processes.requestMesaInterrupt(interruptSelector);
    }

    // compute the (word) offset for the given cylinder/sector/head
    private int getDiskWordOffset(int cylinder, int sectorHead)
    {
        int head = (sectorHead >> 8) & 0x00FF;
        int sector = sectorHead & 0x00FF;

        int absoluteSector
                = (cylinder * DISK_HEADS * DISK_SECTORS)
                + (head * DISK_SECTORS)
                + sector;

        logf("call() -    cyl = {0} - head = {1} - sect = {2} => abs-sector = {3}\n",
            cylinder, head, sector, absoluteSector);

        return absoluteSector * PrincOpsDefs.WORDS_PER_PAGE;
    }
}
