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

namespace Dwarf.UI.Avalonia.Controls;

// IDisplaySource that reads directly from the engine's display memory.
//
// Memory layout (monochrome):
//   - 1 bit per pixel, MSB-first within each 16-bit word
//   - displayWordsPerLine = Mem.displayPixelWidth / 16
//   - Bit `(0x8000 >> i)` of word `[y * wpl + x/16]` is pixel `(x, y)`
//   - Bit ON = pixel dark (BGRA 0x000000FF); bit OFF = pixel light (0xFFFFFFFF)
//
// Memory layout (8-bit color) — deferred to a follow-up:
//   - 1 byte per pixel
//   - effectivePixelsPerLine rounded up to multiples of 512
//   - color table lookup yields 0x00rrggbb (engine's DisplayAgent owns the LUT)
//
// **DECISIONS.md §5 / RISKS.md R2**: this is the perf-critical bit. Each
// `CopyToBgra8888` call expands `width * height` pixels into BGRA32 — at
// 1024×640 that's ~655k pixels × 4 bytes = ~2.5 MB of pixel writes per
// frame. At 50 Hz the rendering budget is 20 ms; we should comfortably
// land under 5 ms even on a modest CPU.
public sealed class MemDisplaySource : IDisplaySource
{
    public int Width { get; }
    public int Height { get; }

    // mem offset where the display memory begins (in ushort words)
    private readonly int _memWordOffset;

    // ushort words per scanline in the framebuffer layout
    private readonly int _wordsPerLine;

    public MemDisplaySource()
    {
        if (Mem.pageFlags == null)
        {
            throw new InvalidOperationException(
                "MemDisplaySource requires Mem to be initialized first via Mem.initializeMemoryGuam(...)");
        }
        if (Mem.getDisplayType() != DisplayType.monochrome)
        {
            throw new NotSupportedException(
                "MemDisplaySource only handles monochrome displays in this Phase E first cut. "
                + "Byte-color support follows when DisplayAgent's color table is wired in.");
        }

        Width = Mem.getDisplayPixelWidth();
        Height = Mem.getDisplayPixelHeight();
        _memWordOffset = Mem.getDisplayRealPage() * PrincOpsDefs.WORDS_PER_PAGE;
        _wordsPerLine = Width / PrincOpsDefs.WORD_BITS;
    }

    public void CopyToBgra8888(nint destination, int rowBytes)
    {
        ushort[] mem = Mem.getDisplayRealMemory();
        int memOff = _memWordOffset;
        int wpl = _wordsPerLine;
        int w = Width;
        int h = Height;

        unsafe
        {
            byte* p = (byte*)destination;

            // Pre-compute the two BGRA pixel values so the inner loop can
            // copy a 4-byte int rather than four bytes. Bit ON → dark
            // (black, opaque), bit OFF → light (white, opaque). Native
            // little-endian: BGRA bytes in memory are B,G,R,A; as an int
            // that's 0xAARRGGBB.
            uint dark = 0xFF000000u;  // A=FF, R=00, G=00, B=00
            uint light = 0xFFFFFFFFu; // A=FF, R=FF, G=FF, B=FF

            for (int y = 0; y < h; y++)
            {
                byte* rowBase = p + y * rowBytes;
                uint* pix = (uint*)rowBase;
                int wordRowStart = memOff + y * wpl;

                for (int wIdx = 0; wIdx < wpl; wIdx++)
                {
                    ushort word = mem[wordRowStart + wIdx];

                    // Unrolled 16-bit-MSB-first expansion. The compiler is
                    // welcome to vectorize this further; manually unrolling
                    // saves the branch overhead vs a bit-shift loop.
                    pix[0]  = (word & 0x8000) != 0 ? dark : light;
                    pix[1]  = (word & 0x4000) != 0 ? dark : light;
                    pix[2]  = (word & 0x2000) != 0 ? dark : light;
                    pix[3]  = (word & 0x1000) != 0 ? dark : light;
                    pix[4]  = (word & 0x0800) != 0 ? dark : light;
                    pix[5]  = (word & 0x0400) != 0 ? dark : light;
                    pix[6]  = (word & 0x0200) != 0 ? dark : light;
                    pix[7]  = (word & 0x0100) != 0 ? dark : light;
                    pix[8]  = (word & 0x0080) != 0 ? dark : light;
                    pix[9]  = (word & 0x0040) != 0 ? dark : light;
                    pix[10] = (word & 0x0020) != 0 ? dark : light;
                    pix[11] = (word & 0x0010) != 0 ? dark : light;
                    pix[12] = (word & 0x0008) != 0 ? dark : light;
                    pix[13] = (word & 0x0004) != 0 ? dark : light;
                    pix[14] = (word & 0x0002) != 0 ? dark : light;
                    pix[15] = (word & 0x0001) != 0 ? dark : light;

                    pix += 16;
                }
            }
        }
    }
}
