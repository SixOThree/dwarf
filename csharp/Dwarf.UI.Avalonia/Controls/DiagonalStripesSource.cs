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

// Test pattern source for the Phase E DisplayControl prototype. Emits
// 16-pixel-wide diagonal stripes alternating black and white. Used to
// verify the rendering pipeline before the engine-driven source lands.
public sealed class DiagonalStripesSource : IDisplaySource
{
    public int Width { get; }
    public int Height { get; }

    public DiagonalStripesSource(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public void CopyToBgra8888(nint destination, int rowBytes)
    {
        unsafe
        {
            byte* p = (byte*)destination;
            for (int y = 0; y < Height; y++)
            {
                byte* row = p + y * rowBytes;
                for (int x = 0; x < Width; x++)
                {
                    bool dark = (((x + y) / 16) & 1) == 0;
                    int idx = x * 4;
                    byte v = dark ? (byte)0 : (byte)255;
                    row[idx + 0] = v;    // B
                    row[idx + 1] = v;    // G
                    row[idx + 2] = v;    // R
                    row[idx + 3] = 0xFF; // A
                }
            }
        }
    }
}
