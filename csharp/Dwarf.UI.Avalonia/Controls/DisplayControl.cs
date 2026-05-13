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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Dwarf.UI.Avalonia.Controls;

// Phase E prototype display control. Holds a WriteableBitmap sized to the
// IDisplaySource's pixel dimensions, copies the source frame into the
// backbuffer on each render, and draws it stretched to the control bounds.
//
// **DECISIONS.md §5 / RISKS.md R2**: this is the perf-critical bit. Target
// is < 5 ms per frame at 1024x768 monochrome. If too slow, fall back to a
// SkiaSharp custom-rendered control.
//
// The first prototype uses Bgra8888 (32 bits per pixel) for everything —
// mono and color sources both expand into BGRA on the fly. Once perf is
// measured we can decide whether to switch monochrome to Gray8.
public sealed class DisplayControl : Control
{
    private WriteableBitmap? _bitmap;

    public IDisplaySource? Source { get; set; }

    public override void Render(DrawingContext context)
    {
        IDisplaySource? src = Source;
        if (src == null) { return; }

        int w = src.Width;
        int h = src.Height;
        if (w <= 0 || h <= 0) { return; }

        // (Re)allocate the bitmap if the source dimensions changed.
        if (_bitmap == null
            || _bitmap.PixelSize.Width != w
            || _bitmap.PixelSize.Height != h)
        {
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(
                new PixelSize(w, h),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Opaque);
        }

        // Copy the current frame into the bitmap backbuffer.
        using (ILockedFramebuffer fb = _bitmap.Lock())
        {
            src.CopyToBgra8888(fb.Address, fb.RowBytes);
        }

        // Stretch-blit into the control's bounds. For a 1:1 display the
        // bounds will match the bitmap pixel size, but if the user resizes
        // the window we'll scale.
        context.DrawImage(_bitmap, new Rect(Bounds.Size));
    }

    // The control's intrinsic size matches the source so Avalonia's layout
    // gives us a 1:1 pixel mapping unless the parent constrains us.
    protected override Size MeasureOverride(Size availableSize)
    {
        if (Source != null)
        {
            return new Size(Source.Width, Source.Height);
        }
        return base.MeasureOverride(availableSize);
    }
}
