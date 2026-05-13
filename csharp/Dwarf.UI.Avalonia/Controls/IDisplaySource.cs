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

namespace Dwarf.UI.Avalonia.Controls;

// Abstraction over the engine's display memory for the Avalonia
// DisplayControl. A source publishes width/height and copies its current
// frame into the WriteableBitmap's locked backbuffer in BGRA8888 layout
// (4 bytes per pixel, little-endian B,G,R,A).
//
// For Phase E prototype: DiagonalStripesSource generates a static pattern.
// Later: a MemDisplaySource reads from Mem.getDisplayRealMemory() (mono or
// 8-bit color) and expands into BGRA on the fly.
public interface IDisplaySource
{
    int Width { get; }
    int Height { get; }

    // Copy the current frame into the buffer at `destination`, in BGRA8888
    // pixel layout, using `rowBytes` for stride (which may be larger than
    // Width * 4 due to alignment).
    void CopyToBgra8888(nint destination, int rowBytes);
}
