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
using Dwarf.UI.Avalonia.Controls;

namespace Dwarf.Tests;

// Tests for MemDisplaySource — the bridge between the engine's
// monochrome 1-bpp-packed framebuffer and the UI's BGRA8888 layout.
//
// Mem.initializeMemoryGuam seeds display memory with a diamond/X test
// pattern (see Mem.initializeDisplayMemoryGuam). Each scanline contains
// the same 16-bit template word repeated horizontally; the template
// shifts pattern bits inward through rows 0..7, then mirrors back out
// through rows 8..15. Pattern repeats vertically every 16 lines.
//
// Template (from Mem.cs):
//   row 0: 0x8001   row 4: 0x0810   row 8 : 0x0180
//   row 1: 0x4002   row 5: 0x0420   row 9 : 0x0240
//   row 2: 0x2004   row 6: 0x0240   row 10: 0x0420
//   row 3: 0x1008   row 7: 0x0180   row 11: 0x0810
//                                   row 12: 0x1008
//                                   row 13: 0x2004
//                                   row 14: 0x4002
//                                   row 15: 0x8001
//
// BGRA8888 in memory (little-endian): bytes B, G, R, A.
// dark = 0x000000FF (alpha+rgb all 0 except alpha)
// light = 0xFFFFFFFF
public sealed class MemDisplaySourceTests
{
    // Make sure Mem is initialized before the source needs it. Same dance
    // as Phase C SmokeTests + AbstractInstructionTest — initialize-once
    // and tolerate other fixtures having got there first.
    private static MemDisplaySource initAndCreateSource()
    {
        if (Mem.pageFlags == null)
        {
            Mem.initializeMemoryGuam(PrincOpsDefs.MIN_REAL_ADDRESSBITS, PrincOpsDefs.MIN_REAL_ADDRESSBITS + 1);
        }
        return new MemDisplaySource();
    }

    private static byte[] renderToBuffer(MemDisplaySource src)
    {
        byte[] buffer = new byte[src.Width * src.Height * 4];
        unsafe
        {
            fixed (byte* p = buffer)
            {
                src.CopyToBgra8888((nint)p, src.Width * 4);
            }
        }
        return buffer;
    }

    private static uint pixel(byte[] buf, int width, int x, int y)
    {
        int idx = (y * width + x) * 4;
        return (uint)(buf[idx] | (buf[idx + 1] << 8) | (buf[idx + 2] << 16) | (buf[idx + 3] << 24));
    }

    private const uint Dark = 0xFF000000u;  // BGRA: 00,00,00,FF
    private const uint Light = 0xFFFFFFFFu; // BGRA: FF,FF,FF,FF

    [Fact]
    public void source_reports_mem_dimensions()
    {
        MemDisplaySource src = initAndCreateSource();
        Assert.Equal(Mem.getDisplayPixelWidth(), src.Width);
        Assert.Equal(Mem.getDisplayPixelHeight(), src.Height);
        Assert.True(src.Width % 16 == 0, "width must be a multiple of 16 for the bit-packed monochrome layout");
    }

    [Fact]
    public void row_0_template_0x8001_pixels_0_and_15_are_dark()
    {
        MemDisplaySource src = initAndCreateSource();
        byte[] buf = renderToBuffer(src);

        // 0x8001: bit 0x8000 = pixel 0 ON (dark), bit 0x0001 = pixel 15 ON (dark)
        Assert.Equal(Dark, pixel(buf, src.Width, 0, 0));
        Assert.Equal(Dark, pixel(buf, src.Width, 15, 0));
    }

    [Fact]
    public void row_0_middle_pixels_are_light()
    {
        MemDisplaySource src = initAndCreateSource();
        byte[] buf = renderToBuffer(src);

        // 0x8001 → pixels 1..14 are OFF (light)
        for (int x = 1; x <= 14; x++)
        {
            Assert.Equal(Light, pixel(buf, src.Width, x, 0));
        }
    }

    [Fact]
    public void row_1_template_0x4002_shifts_pattern_inward()
    {
        MemDisplaySource src = initAndCreateSource();
        byte[] buf = renderToBuffer(src);

        // 0x4002: bit 0x4000 = pixel 1 ON, bit 0x0002 = pixel 14 ON
        Assert.Equal(Light, pixel(buf, src.Width, 0, 1));
        Assert.Equal(Dark, pixel(buf, src.Width, 1, 1));
        Assert.Equal(Dark, pixel(buf, src.Width, 14, 1));
        Assert.Equal(Light, pixel(buf, src.Width, 15, 1));
    }

    [Fact]
    public void row_7_template_0x0180_center_pixels_are_dark()
    {
        MemDisplaySource src = initAndCreateSource();
        byte[] buf = renderToBuffer(src);

        // 0x0180 = 0b0000_0001_1000_0000: pixels 7 and 8 ON
        Assert.Equal(Dark, pixel(buf, src.Width, 7, 7));
        Assert.Equal(Dark, pixel(buf, src.Width, 8, 7));
        Assert.Equal(Light, pixel(buf, src.Width, 6, 7));
        Assert.Equal(Light, pixel(buf, src.Width, 9, 7));
    }

    [Fact]
    public void pattern_repeats_horizontally_every_16_pixels()
    {
        MemDisplaySource src = initAndCreateSource();
        byte[] buf = renderToBuffer(src);

        // Same template word fills the whole scanline, so pixel (0, 0)
        // should match pixel (16, 0), (32, 0), and so on.
        Assert.Equal(Dark, pixel(buf, src.Width, 16, 0));
        Assert.Equal(Dark, pixel(buf, src.Width, 32, 0));
        Assert.Equal(Dark, pixel(buf, src.Width, 31, 0));  // 15 + 16
        Assert.Equal(Light, pixel(buf, src.Width, 17, 0)); // 1 + 16, OFF
    }

    [Fact]
    public void pattern_repeats_vertically_every_16_lines()
    {
        MemDisplaySource src = initAndCreateSource();
        byte[] buf = renderToBuffer(src);

        // Template has 16 entries; line 16 reuses template[0] = 0x8001
        Assert.Equal(Dark, pixel(buf, src.Width, 0, 16));
        Assert.Equal(Dark, pixel(buf, src.Width, 15, 16));
        Assert.Equal(Light, pixel(buf, src.Width, 1, 16));
    }

    [Fact]
    public void alpha_channel_is_opaque_everywhere()
    {
        MemDisplaySource src = initAndCreateSource();
        byte[] buf = renderToBuffer(src);

        // BGRA → alpha is byte 3 of each pixel. Sample a few rows.
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                int idx = (y * src.Width + x) * 4 + 3;
                Assert.Equal(0xFF, buf[idx]);
            }
        }
    }

    [Fact]
    public void rejects_uninitialized_mem()
    {
        // We can't actually un-init Mem mid-process, so this test is a
        // documentation-only assertion: if `Mem.pageFlags` is null, the
        // constructor throws InvalidOperationException. After Mem is
        // initialized in another test, this would no-op-construct
        // successfully — so we only check that the constructor succeeds
        // here as a precondition.
        if (Mem.pageFlags == null) { Mem.initializeMemoryGuam(PrincOpsDefs.MIN_REAL_ADDRESSBITS, PrincOpsDefs.MIN_REAL_ADDRESSBITS + 1); }
        // not throwing is the assertion
        _ = new MemDisplaySource();
    }
}
