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

using System.Runtime.InteropServices;
using Dwarf.UI.Avalonia.Controls;

namespace Dwarf.Tests;

// Phase E first-step tests. Verifies the IDisplaySource → byte-buffer
// rendering path without needing Avalonia.Headless. Run the full
// `dotnet run -- -gui` for an interactive visual check.
public sealed class DisplayPipelineTests
{
    [Fact]
    public void DiagonalStripesSource_reports_expected_dimensions()
    {
        var src = new DiagonalStripesSource(1024, 768);
        Assert.Equal(1024, src.Width);
        Assert.Equal(768, src.Height);
    }

    [Fact]
    public void DiagonalStripesSource_writes_bgra8888_pattern()
    {
        const int w = 64;
        const int h = 32;
        var src = new DiagonalStripesSource(w, h);

        byte[] buffer = new byte[w * h * 4];
        unsafe
        {
            fixed (byte* p = buffer)
            {
                src.CopyToBgra8888((nint)p, w * 4);
            }
        }

        // pixel (0,0): (0+0)/16 = 0 → dark → BGRA = 00,00,00,FF
        Assert.Equal(0x00, buffer[0]); // B
        Assert.Equal(0x00, buffer[1]); // G
        Assert.Equal(0x00, buffer[2]); // R
        Assert.Equal(0xFF, buffer[3]); // A

        // pixel (16,0): (16+0)/16 = 1 → light → BGRA = FF,FF,FF,FF
        int idx16 = 16 * 4;
        Assert.Equal(0xFF, buffer[idx16 + 0]);
        Assert.Equal(0xFF, buffer[idx16 + 1]);
        Assert.Equal(0xFF, buffer[idx16 + 2]);
        Assert.Equal(0xFF, buffer[idx16 + 3]);

        // pixel (32,0): (32+0)/16 = 2 → dark again
        int idx32 = 32 * 4;
        Assert.Equal(0x00, buffer[idx32 + 0]);
        Assert.Equal(0x00, buffer[idx32 + 1]);
        Assert.Equal(0x00, buffer[idx32 + 2]);
        Assert.Equal(0xFF, buffer[idx32 + 3]);

        // alpha is opaque everywhere
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int idx = (y * w + x) * 4 + 3;
                Assert.Equal(0xFF, buffer[idx]);
            }
        }
    }

    [Fact]
    public void DiagonalStripesSource_respects_row_stride()
    {
        const int w = 8;
        const int h = 4;
        const int stride = (w * 4) + 8; // 8 bytes of padding per row
        var src = new DiagonalStripesSource(w, h);

        byte[] buffer = new byte[stride * h];
        // Fill padding with a sentinel so we can detect over-write.
        Array.Fill(buffer, (byte)0xAB);

        unsafe
        {
            fixed (byte* p = buffer)
            {
                src.CopyToBgra8888((nint)p, stride);
            }
        }

        // The padding bytes at the end of each row should be untouched.
        for (int y = 0; y < h; y++)
        {
            for (int b = w * 4; b < stride; b++)
            {
                Assert.Equal(0xAB, buffer[y * stride + b]);
            }
        }
    }
}
