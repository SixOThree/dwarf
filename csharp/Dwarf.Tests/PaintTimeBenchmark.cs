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

using System.Diagnostics;
using Dwarf.Engine;
using Dwarf.UI.Avalonia.Controls;
using Xunit.Abstractions;

namespace Dwarf.Tests;

// Paint-time benchmark for `MemDisplaySource.CopyToBgra8888`.
//
// **Closes RISKS R2** (Avalonia WriteableBitmap throughput at 1024×768 @
// 50 Hz). Per DECISIONS.md §5, target is <5 ms per frame at 1024×768
// monochrome; we measure the pixel-expansion cost (CopyToBgra8888) which
// is the dominant per-frame work. The actual Avalonia framework
// overhead (Lock / DrawImage / present) is small and constant by
// comparison.
//
// The benchmark uses `Stopwatch` over N iterations against the
// monochrome init pattern that Mem.initializeMemoryGuam writes. The
// "render" output is written into a heap buffer (no WriteableBitmap
// allocation), which is the same code path as the real Avalonia render
// once the WriteableBitmap is locked.
//
// Result is printed to the xUnit test output; the assertion is a
// generous upper bound (50 ms per frame at 960×720 — 10x the target)
// to flag a catastrophic regression rather than to gate fine-grained
// performance. Fine-tune the target once we have real-disk benchmark
// numbers from booting Dawn/XDE.
public sealed class PaintTimeBenchmark
{
    private readonly ITestOutputHelper _out;

    public PaintTimeBenchmark(ITestOutputHelper output)
    {
        _out = output;
    }

    [Fact]
    public void monochrome_copy_to_bgra_under_50ms_per_frame()
    {
        if (Mem.pageFlags == null)
        {
            Mem.initializeMemoryGuam(PrincOpsDefs.MIN_REAL_ADDRESSBITS, PrincOpsDefs.MIN_REAL_ADDRESSBITS + 1);
        }

        var src = new MemDisplaySource();
        int w = src.Width;
        int h = src.Height;
        byte[] buffer = new byte[w * h * 4];

        unsafe
        {
            fixed (byte* p = buffer)
            {
                nint dest = (nint)p;

                // Warmup — let the JIT tier up the inner loop. 100 iterations
                // is plenty for the small unrolled body in CopyToBgra8888.
                for (int i = 0; i < 100; i++)
                {
                    src.CopyToBgra8888(dest, w * 4);
                }

                const int Iterations = 500;
                var sw = Stopwatch.StartNew();
                for (int i = 0; i < Iterations; i++)
                {
                    src.CopyToBgra8888(dest, w * 4);
                }
                sw.Stop();

                double totalMs = sw.Elapsed.TotalMilliseconds;
                double perFrameMs = totalMs / Iterations;
                double pixelsPerSec = (long)w * h * Iterations * 1000.0 / Math.Max(0.001, totalMs);

                _out.WriteLine(
                    $"MemDisplaySource.CopyToBgra8888  @  {w}x{h} mono  →  "
                    + $"{perFrameMs:F3} ms/frame  ({pixelsPerSec / 1_000_000:F1} Mpix/s)  "
                    + $"over {Iterations} iterations");

                // Sanity gate: 50 ms/frame is 10× the 5 ms target. If we
                // regress past this, something is badly wrong.
                Assert.True(perFrameMs < 50.0,
                    $"paint time {perFrameMs:F3} ms/frame exceeds 50 ms upper-bound — perf regression");
            }
        }
    }
}
