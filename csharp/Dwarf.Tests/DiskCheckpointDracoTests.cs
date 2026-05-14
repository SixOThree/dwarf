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

using System.IO.Compression;
using System.Text;
using Dwarf.Iop6085;

namespace Dwarf.Tests;

// Round-trip tests for the Draco .zdisk + .cscheck checkpoint format
// (Phase H-1..H-4). Mirrors the 7 Duchess scenarios in DiskCheckpointTests
// against HDisk.DiskFile.
//
// The .zdisk format is zlib-compressed, big-endian:
//   - 6-word header (signature1, heads, cyls, sectCount_hi, sectCount_lo, signature2)
//   - per sector: 2-word linear index + 266 words (10 label + 256 data)
// The .cscheck format is shared with Duchess except flag bit 0 = 1.
//
// Tests use the `internal` sectors/sectorsChanged/changed accessors to poke
// directly at in-memory state, bypassing the Mem-mediated writeSectorData /
// writeSectorLabel paths (which would require a full engine fixture).
public class DiskCheckpointDracoTests : IDisposable
{
    private readonly string tempDir;
    private const int MIN_CYLINDERS = 40;       // HDisk.DiskFile.minCylCount
    private const int HEADS = 12;               // typical 6085 disk
    private const int SECTORS_PER_TRACK = 16;   // HDisk.DiskFile.sectorsPerTrack
    private const int WORDS_PER_SECTOR = 266;   // 10 label + 256 data

    private const int SIGNATURE1 = 0xDAAD;
    private const int SIGNATURE2 = 0x5CC5;

    public DiskCheckpointDracoTests()
    {
        this.tempDir = Path.Combine(Path.GetTempPath(), "dwarf-dracodisktest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this.tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(this.tempDir, recursive: true); } catch { /* ignore */ }
        GC.SuppressFinalize(this);
    }

    // Synthesizes a minimal Draco `.zdisk` file: 40 cylinders × 12 heads ×
    // 16 sectors = 7680 sectors of all-zero label+data, zlib-compressed,
    // big-endian. With all-zero sectors this compresses to under 10 KB.
    // Returns the file path.
    private string makeSyntheticDracoZdisk(int cylinders = MIN_CYLINDERS)
    {
        string diskPath = Path.Combine(this.tempDir, "synth.zdisk");
        int sectorCount = cylinders * HEADS * SECTORS_PER_TRACK;

        using (FileStream fs = new(diskPath, FileMode.Create, FileAccess.Write))
        using (ZLibStream zls = new(fs, CompressionLevel.Optimal))
        {
            // 6-word header (big-endian)
            writeBEWord(zls, SIGNATURE1);
            writeBEWord(zls, HEADS);
            writeBEWord(zls, cylinders);
            writeBEWord(zls, (sectorCount >> 16) & 0xFFFF);
            writeBEWord(zls, sectorCount & 0xFFFF);
            writeBEWord(zls, SIGNATURE2);

            // All sectors, in linear order, with their linear-index header
            for (int s = 0; s < sectorCount; s++)
            {
                writeBEWord(zls, (s >> 16) & 0xFFFF);
                writeBEWord(zls, s & 0xFFFF);
                for (int w = 0; w < WORDS_PER_SECTOR; w++)
                {
                    writeBEWord(zls, 0);
                }
            }
        }
        return diskPath;
    }

    private static void writeBEWord(Stream s, int v)
    {
        s.WriteByte((byte)((v >> 8) & 0xFF));
        s.WriteByte((byte)(v & 0xFF));
    }

    [Fact]
    public void draco_checkpoint_roundtrip_writes_and_reads_back()
    {
        string diskPath = this.makeSyntheticDracoZdisk();

        // Session 1: open, dirty two sectors, save
        {
            var df = new HDisk.DiskFile(diskPath, readonly_: false, deltasToKeep: 5);

            // Sector 5 — fill data with pattern A, label with marker
            ushort[] s5 = df.sectors[5];
            for (int w = 0; w < 10; w++) { s5[w] = (ushort)(0x5500 + w); }              // label
            for (int w = 10; w < WORDS_PER_SECTOR; w++) { s5[w] = (ushort)(0xA000 + w - 10); } // data
            df.sectorsChanged[5] = true;

            // Sector 137 — pattern B
            ushort[] s137 = df.sectors[137];
            for (int w = 0; w < 10; w++) { s137[w] = (ushort)(0x1370 + w); }
            for (int w = 10; w < WORDS_PER_SECTOR; w++) { s137[w] = (ushort)(0xB000 + w - 10); }
            df.sectorsChanged[137] = true;

            df.changed = true;
            var errors = new StringBuilder();
            Assert.True(df.saveDisk(errors), $"saveDisk should succeed; got errors: {errors}");
        }

        // Verify the .cscheck file was created
        string checkpointPath = diskPath + ".cscheck";
        Assert.True(File.Exists(checkpointPath), ".cscheck file should exist after saveDisk");

        // Session 2: re-open, verify sectors 5 and 137 carry the patterns
        {
            var df = new HDisk.DiskFile(diskPath, readonly_: false, deltasToKeep: 5);

            ushort[] s5 = df.sectors[5];
            for (int w = 0; w < 10; w++) { Assert.Equal((ushort)(0x5500 + w), s5[w]); }
            for (int w = 10; w < WORDS_PER_SECTOR; w++) { Assert.Equal((ushort)(0xA000 + w - 10), s5[w]); }

            ushort[] s137 = df.sectors[137];
            for (int w = 0; w < 10; w++) { Assert.Equal((ushort)(0x1370 + w), s137[w]); }
            for (int w = 10; w < WORDS_PER_SECTOR; w++) { Assert.Equal((ushort)(0xB000 + w - 10), s137[w]); }

            // Sectors from the checkpoint should be flagged dirty so cumulative
            // semantics carry forward to the next save.
            Assert.True(df.sectorsChanged[5]);
            Assert.True(df.sectorsChanged[137]);

            // Sectors that weren't in the checkpoint should still hold the
            // base content (zeros from the synthetic .zdisk).
            ushort[] s99 = df.sectors[99];
            for (int w = 0; w < WORDS_PER_SECTOR; w++) { Assert.Equal((ushort)0, s99[w]); }
            Assert.False(df.sectorsChanged[99]);
        }
    }

    [Fact]
    public void draco_no_dirty_sectors_skips_writing_checkpoint()
    {
        string diskPath = this.makeSyntheticDracoZdisk();
        var df = new HDisk.DiskFile(diskPath, readonly_: false, deltasToKeep: 5);

        // No writes; just save
        var errors = new StringBuilder();
        Assert.False(df.saveDisk(errors), "saveDisk should return false when nothing is dirty");

        // No .cscheck file should be created
        string checkpointPath = diskPath + ".cscheck";
        Assert.False(File.Exists(checkpointPath), "saveDisk with no dirty sectors should not create a .cscheck");
    }

    [Fact]
    public void draco_readonly_disk_returns_false_and_writes_nothing()
    {
        string diskPath = this.makeSyntheticDracoZdisk();
        var df = new HDisk.DiskFile(diskPath, readonly_: true, deltasToKeep: 5);

        // Dirty a sector (in normal use writeSectorData refuses to dirty a
        // readonly disk, but the persistence-side guard lives in saveDisk).
        df.sectors[5][10] = 0xDEAD;
        df.sectorsChanged[5] = true;
        df.changed = true;

        var errors = new StringBuilder();
        Assert.False(df.saveDisk(errors), "saveDisk on readonly disk should return false");

        // No .cscheck file should be created
        Assert.False(File.Exists(diskPath + ".cscheck"));
    }

    [Fact]
    public void draco_checkpoint_rotation_archives_prior_then_prunes()
    {
        string diskPath = this.makeSyntheticDracoZdisk();
        const int keepCount = 2;

        // Three save cycles → after the third, at most `keepCount` archived
        // checkpoints should remain (the oldest gets pruned).
        for (int session = 1; session <= 3; session++)
        {
            var df = new HDisk.DiskFile(diskPath, readonly_: false, deltasToKeep: keepCount);
            int sectorIdx = 5 + session;
            df.sectors[sectorIdx][10] = (ushort)(0xC000 + session);
            df.sectorsChanged[sectorIdx] = true;
            df.changed = true;

            var errors = new StringBuilder();
            Assert.True(df.saveDisk(errors), $"session {session}: saveDisk failed; errors: {errors}");

            // Sleep just enough for the timestamp suffix to advance
            // (yyyy.MM.dd_HH.mm.ss.fff has millisecond resolution).
            Thread.Sleep(5);
        }

        // After 3 saves: 1 current `.cscheck` + at most 2 archived `.cscheck-<ts>`
        Assert.True(File.Exists(diskPath + ".cscheck"));
        string[] archived = Directory.GetFiles(this.tempDir, "synth.zdisk.cscheck-*");
        Assert.True(archived.Length <= keepCount,
            $"expected at most {keepCount} archived checkpoints, found {archived.Length}");
    }

    [Fact]
    public void draco_cumulative_writes_across_sessions()
    {
        string diskPath = this.makeSyntheticDracoZdisk();

        // Session 1: write sector 5
        {
            var df = new HDisk.DiskFile(diskPath, readonly_: false, deltasToKeep: 5);
            df.sectors[5][10] = 0x5555;
            df.sectorsChanged[5] = true;
            df.changed = true;
            var errors = new StringBuilder();
            Assert.True(df.saveDisk(errors), $"session 1 save failed: {errors}");
        }

        // Session 2: write sector 9; sector 5 should still be there
        {
            var df = new HDisk.DiskFile(diskPath, readonly_: false, deltasToKeep: 5);
            Assert.Equal((ushort)0x5555, df.sectors[5][10]); // sector 5 carried forward

            df.sectors[9][10] = 0x9999;
            df.sectorsChanged[9] = true;
            df.changed = true;
            var errors = new StringBuilder();
            Assert.True(df.saveDisk(errors), $"session 2 save failed: {errors}");
        }

        // Session 3: both sectors should be present and flagged dirty (cumulative)
        {
            var df = new HDisk.DiskFile(diskPath, readonly_: false, deltasToKeep: 5);
            Assert.Equal((ushort)0x5555, df.sectors[5][10]);
            Assert.Equal((ushort)0x9999, df.sectors[9][10]);
            Assert.True(df.sectorsChanged[5]);
            Assert.True(df.sectorsChanged[9]);
        }
    }

    [Fact]
    public void draco_corrupted_checkpoint_is_skipped_disk_still_loads()
    {
        string diskPath = this.makeSyntheticDracoZdisk();

        // Write garbage at the .cscheck location
        string checkpointPath = diskPath + ".cscheck";
        File.WriteAllBytes(checkpointPath, new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04 });

        // Constructor should NOT throw — corrupted .cscheck is logged + skipped
        var df = new HDisk.DiskFile(diskPath, readonly_: false, deltasToKeep: 5);

        // Disk should be in its base-only state. No sectors dirty.
        for (int s = 0; s < df.sectorsChanged.Length; s++)
        {
            Assert.False(df.sectorsChanged[s], $"sector {s} should not be dirty after corrupted-checkpoint skip");
        }
        Assert.False(df.changed, "disk should not be flagged as changed after corrupted-checkpoint skip");
    }

    [Fact]
    public void draco_merge_writes_new_base_and_archives_old_files()
    {
        string diskPath = this.makeSyntheticDracoZdisk();

        // First, dirty a sector and save
        {
            var df = new HDisk.DiskFile(diskPath, readonly_: false, deltasToKeep: 5);
            df.sectors[7][10] = 0x7777;
            df.sectorsChanged[7] = true;
            df.changed = true;
            var errors = new StringBuilder();
            Assert.True(df.saveDisk(errors), $"pre-merge save failed: {errors}");
        }
        Assert.True(File.Exists(diskPath + ".cscheck"));

        // Now merge
        {
            var df = new HDisk.DiskFile(diskPath, readonly_: false, deltasToKeep: 5);
            using StringWriter sw = new();
            df.mergeCheckpointToBase(sw);
            string output = sw.ToString();
            Assert.Contains("merge: done", output);
        }

        // After merge:
        //   - .cscheck should be gone
        //   - the .zdisk base should still exist
        //   - a .zip archive should exist (timestamped)
        Assert.False(File.Exists(diskPath + ".cscheck"), ".cscheck should be removed after merge");
        Assert.True(File.Exists(diskPath), "base .zdisk should still exist after merge");
        string[] zips = Directory.GetFiles(this.tempDir, "synth.zdisk-*.zip");
        Assert.Single(zips);

        // Re-open the merged disk and verify the sector is now in the base
        {
            var df = new HDisk.DiskFile(diskPath, readonly_: false, deltasToKeep: 5);
            Assert.Equal((ushort)0x7777, df.sectors[7][10]);
            Assert.False(df.sectorsChanged[7], "sector should not be dirty after merge — it's part of the base now");
            Assert.False(df.changed, "disk should not be flagged as changed after merge");
        }
    }
}
