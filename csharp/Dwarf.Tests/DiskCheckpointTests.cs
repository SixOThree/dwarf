/*
Copyright (c) 2026, Matthew Dugal
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
    * Redistributions of source code must retain the above copyright
      notice, this list of conditions and the following disclaimer.
    * Redistributions in binary form must reproduce the above copyright
      notice, this list of conditions and the following disclaimer in the
      documentation and/or other materials provided with the distribution.
    * The name of the author may not be used to endorse or promote products
      derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDER "AS IS" AND ANY EXPRESS
OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS;
OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR
OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using Dwarf.Agents;
using Dwarf.Engine;

namespace Dwarf.Tests;

// Round-trip tests for the C#-native .cscheck disk checkpoint format
// (Phase H-1..H-4).
//
// Covers the Duchess `.dsk` variant (raw, big-endian, no compression).
// The Draco `.zdisk` variant shares the same .cscheck format with one
// flag bit different; end-to-end validation happens via interactive
// boot (user-confirmed with v2.1.0-csharp).
//
// Tests use the `internal` content/pagesChanged accessors to poke
// directly at the in-memory state, bypassing the Mem-mediated
// writePage/readPage path (which would require additional fixture).
public class DiskCheckpointTests : IDisposable
{
    private readonly string tempDir;
    private const int CYLINDERS = 1;          // smallest synthetic disk
    private const int HEADS = 12;             // DiskAgent fixed constant
    private const int SECTORS_PER_TRACK = 16; // DiskAgent fixed constant
    private const int WORDS_PER_PAGE = PrincOpsDefs.WORDS_PER_PAGE; // 256

    public DiskCheckpointTests()
    {
        this.tempDir = Path.Combine(Path.GetTempPath(), "dwarf-disktest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this.tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(this.tempDir, recursive: true); } catch { /* ignore */ }
        GC.SuppressFinalize(this);
    }

    // Synthesizes a minimal Duchess `.dsk` file (raw bytes, big-endian).
    // The first 2 bytes encode the physical seal (0xA28A in big-endian)
    // so `externalByteSwapped` defaults to false. Returns the file path.
    private string makeSyntheticDuchessDisk(int cylinders = CYLINDERS)
    {
        string diskPath = Path.Combine(this.tempDir, "synth.dsk");
        int byteLength = cylinders * HEADS * SECTORS_PER_TRACK * WORDS_PER_PAGE * 2;
        byte[] bytes = new byte[byteLength];
        // physical seal at offset 0: 0xA28A in big-endian wire order
        bytes[0] = 0xA2;
        bytes[1] = 0x8A;
        // Fill the rest with a recognizable pattern: byte i = (byte)(i & 0xFF).
        // This isn't valid Pilot data, but the test only checks round-trip
        // of writes, not Pilot's interpretation.
        for (int i = 2; i < byteLength; i++)
        {
            bytes[i] = (byte)(i & 0xFF);
        }
        File.WriteAllBytes(diskPath, bytes);
        return diskPath;
    }

    [Fact]
    public void duchess_checkpoint_roundtrip_writes_and_reads_back()
    {
        string diskPath = this.makeSyntheticDuchessDisk();

        // Session 1: open, dirty two pages, save
        {
            var df = new DiskAgent.DiskFile(diskPath, readonly_: false, deltasToKeep: 5);

            // Page 5 — fill with pattern A
            int p5Offset = 5 * WORDS_PER_PAGE;
            for (int w = 0; w < WORDS_PER_PAGE; w++) { df.content[p5Offset + w] = (ushort)(0xA000 + w); }
            df.pagesChanged[5] = true;

            // Page 17 — fill with pattern B
            int p17Offset = 17 * WORDS_PER_PAGE;
            for (int w = 0; w < WORDS_PER_PAGE; w++) { df.content[p17Offset + w] = (ushort)(0xB000 + w); }
            df.pagesChanged[17] = true;

            Assert.Equal(DiskState.OK, df.saveDisk());
        }

        // Verify the .cscheck file was created
        string checkpointPath = diskPath + ".cscheck";
        Assert.True(File.Exists(checkpointPath), ".cscheck file should exist after saveDisk");

        // Session 2: re-open, verify pages 5 and 17 carry the patterns
        {
            var df = new DiskAgent.DiskFile(diskPath, readonly_: false, deltasToKeep: 5);

            int p5Offset = 5 * WORDS_PER_PAGE;
            for (int w = 0; w < WORDS_PER_PAGE; w++)
            {
                Assert.Equal((ushort)(0xA000 + w), df.content[p5Offset + w]);
            }
            int p17Offset = 17 * WORDS_PER_PAGE;
            for (int w = 0; w < WORDS_PER_PAGE; w++)
            {
                Assert.Equal((ushort)(0xB000 + w), df.content[p17Offset + w]);
            }

            // Pages from the checkpoint should be flagged dirty so cumulative
            // semantics carry forward to the next save.
            Assert.True(df.pagesChanged[5]);
            Assert.True(df.pagesChanged[17]);

            // Pages that weren't in the checkpoint should still hold their
            // base content (sentinel pattern from makeSyntheticDuchessDisk).
            // Page 0 is partially overwritten by the physical seal; check page 1.
            int p1Offset = 1 * WORDS_PER_PAGE;
            ushort firstWordPage1 = df.content[p1Offset];
            int byteOffset = (1 * WORDS_PER_PAGE) * 2;
            ushort expectedFromSeed = (ushort)((byte)(byteOffset & 0xFF) << 8 | (byte)((byteOffset + 1) & 0xFF));
            Assert.Equal(expectedFromSeed, firstWordPage1);
            Assert.False(df.pagesChanged[1]);
        }
    }

    [Fact]
    public void duchess_no_dirty_pages_skips_writing_checkpoint()
    {
        string diskPath = this.makeSyntheticDuchessDisk();
        var df = new DiskAgent.DiskFile(diskPath, readonly_: false, deltasToKeep: 5);

        // No writes; just save
        Assert.Equal(DiskState.OK, df.saveDisk());

        // No .cscheck file should be created
        string checkpointPath = diskPath + ".cscheck";
        Assert.False(File.Exists(checkpointPath), "saveDisk with no dirty pages should not create a .cscheck");
    }

    [Fact]
    public void duchess_readonly_disk_returns_readonly_state()
    {
        string diskPath = this.makeSyntheticDuchessDisk();
        var df = new DiskAgent.DiskFile(diskPath, readonly_: true, deltasToKeep: 5);

        // Dirty a page (this would normally come via writePage, but the
        // readonly_ flag is about not persisting, not about preventing writes)
        df.content[5 * WORDS_PER_PAGE] = 0xDEAD;
        df.pagesChanged[5] = true;

        Assert.Equal(DiskState.ReadOnly, df.saveDisk());

        // No .cscheck file should be created
        Assert.False(File.Exists(diskPath + ".cscheck"));
    }

    [Fact]
    public void duchess_checkpoint_rotation_archives_prior_then_prunes()
    {
        string diskPath = this.makeSyntheticDuchessDisk();
        const int keepCount = 2;

        // Three save cycles → after the third, we should have 1 current + 2 archived.
        // The first save's archive should be pruned (since 3 > keepCount + 1 = 3 — wait,
        // we keep `keepCount` archives so 3 archives reduces to 2).
        for (int session = 1; session <= 3; session++)
        {
            var df = new DiskAgent.DiskFile(diskPath, readonly_: false, deltasToKeep: keepCount);
            int pageIdx = 5 + session;
            df.content[pageIdx * WORDS_PER_PAGE] = (ushort)(0xC000 + session);
            df.pagesChanged[pageIdx] = true;
            Assert.Equal(DiskState.OK, df.saveDisk());

            // Sleep just enough for the timestamp suffix to advance
            // (yyyy.MM.dd_HH.mm.ss.fff has millisecond resolution).
            Thread.Sleep(5);
        }

        // After 3 saves: 1 current `.cscheck` + at most 2 archived `.cscheck-<ts>`
        Assert.True(File.Exists(diskPath + ".cscheck"));
        string[] archived = Directory.GetFiles(this.tempDir, "synth.dsk.cscheck-*");
        Assert.True(archived.Length <= keepCount,
            $"expected at most {keepCount} archived checkpoints, found {archived.Length}");
    }

    [Fact]
    public void duchess_cumulative_writes_across_sessions()
    {
        string diskPath = this.makeSyntheticDuchessDisk();

        // Session 1: write page 5
        {
            var df = new DiskAgent.DiskFile(diskPath, readonly_: false, deltasToKeep: 5);
            df.content[5 * WORDS_PER_PAGE] = 0x5555;
            df.pagesChanged[5] = true;
            Assert.Equal(DiskState.OK, df.saveDisk());
        }

        // Session 2: write page 9; page 5 should still be there
        {
            var df = new DiskAgent.DiskFile(diskPath, readonly_: false, deltasToKeep: 5);
            Assert.Equal((ushort)0x5555, df.content[5 * WORDS_PER_PAGE]); // page 5 carried forward
            df.content[9 * WORDS_PER_PAGE] = 0x9999;
            df.pagesChanged[9] = true;
            Assert.Equal(DiskState.OK, df.saveDisk());
        }

        // Session 3: both pages should be present
        {
            var df = new DiskAgent.DiskFile(diskPath, readonly_: false, deltasToKeep: 5);
            Assert.Equal((ushort)0x5555, df.content[5 * WORDS_PER_PAGE]);
            Assert.Equal((ushort)0x9999, df.content[9 * WORDS_PER_PAGE]);
            // Both pages should be flagged dirty (cumulative)
            Assert.True(df.pagesChanged[5]);
            Assert.True(df.pagesChanged[9]);
        }
    }

    [Fact]
    public void duchess_corrupted_checkpoint_is_skipped_disk_still_loads()
    {
        string diskPath = this.makeSyntheticDuchessDisk();

        // Write garbage at the .cscheck location
        string checkpointPath = diskPath + ".cscheck";
        File.WriteAllBytes(checkpointPath, new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04 });

        // Constructor should NOT throw — corrupted .cscheck is logged + skipped
        var df = new DiskAgent.DiskFile(diskPath, readonly_: false, deltasToKeep: 5);

        // Disk should be in its base-only state. No pages dirty.
        for (int p = 0; p < df.pagesChanged.Length; p++)
        {
            Assert.False(df.pagesChanged[p], $"page {p} should not be dirty after corrupted-checkpoint skip");
        }
    }

    [Fact]
    public void duchess_merge_writes_new_base_and_archives_old_files()
    {
        string diskPath = this.makeSyntheticDuchessDisk();

        // First, dirty a page and save
        {
            var df = new DiskAgent.DiskFile(diskPath, readonly_: false, deltasToKeep: 5);
            df.content[7 * WORDS_PER_PAGE] = 0x7777;
            df.pagesChanged[7] = true;
            Assert.Equal(DiskState.OK, df.saveDisk());
        }
        Assert.True(File.Exists(diskPath + ".cscheck"));

        // Now merge
        {
            var df = new DiskAgent.DiskFile(diskPath, readonly_: false, deltasToKeep: 5);
            using StringWriter sw = new();
            df.mergeDelta(sw);
            string output = sw.ToString();
            Assert.Contains("merge: done", output);
        }

        // After merge:
        //   - .cscheck should be gone
        //   - the .dsk base should still exist
        //   - a .zip archive should exist (timestamped)
        Assert.False(File.Exists(diskPath + ".cscheck"), ".cscheck should be removed after merge");
        Assert.True(File.Exists(diskPath), "base .dsk should still exist after merge");
        string[] zips = Directory.GetFiles(this.tempDir, "synth.dsk-*.zip");
        Assert.Single(zips);

        // Re-open the merged disk and verify the page is now in the base
        {
            var df = new DiskAgent.DiskFile(diskPath, readonly_: false, deltasToKeep: 5);
            Assert.Equal((ushort)0x7777, df.content[7 * WORDS_PER_PAGE]);
            Assert.False(df.pagesChanged[7], "page should not be dirty after merge — it's part of the base now");
        }
    }
}
