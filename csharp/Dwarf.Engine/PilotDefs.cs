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
OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR
OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

namespace Dwarf.Engine;

// Constants specific to Pilot.
public static class PilotDefs
{
    // AV / fsi configuration

    public const int AVHeapSize = 32; // 00B
    public const int LastAVHeapSlot = AVHeapSize - 2; // the last entry is unused??

    // The local frame sizes used by Pilot (data area only) at the fsi slots.
    public static readonly int[] FRAME_SIZE_MAP =
    {
           8,   12,   16,   20,   24,
          28,   32,   40,   48,   56,
          68,   80,   96,  112,  128,
         148,  168,  192,  224,  252,
         508,  764, 1020, 1276, 1532,
        1788, 2044, 2556, 3068, 3580, 4092,
    };

    // Number of pre-allocated local frames for each slot.
    public static readonly int[] FRAME_WEIGHT_MAP =
    {
        20, 26, 15, 16, 16,
        12,  8,  8,  5,  5,
         7,  2,  2,  1,  1,
         1,  1,  1,  1,  0,
         0,  0,  0,  0,  0,
         0,  0,  0,  0,  0, 0,
    };

    // devices

    public const int Device_anyFloppy = 17;
    public const int Device_microFloppy = 23; // 1.44MB, 3 1/2" disks
    public const int Device_anyPilotDisk = 64;
}

// Java enum DisplayType { monochrome(0, 1), fourBitPlaneColor(1, 4), byteColor(2, 8) }
// — Java enums carry constructor arguments; we use a sealed class with static instances
// so callers can write `DisplayType.monochrome.bitDepth` identically to the Java code.
public sealed class DisplayType
{
    public static readonly DisplayType monochrome        = new(0, 1);
    public static readonly DisplayType fourBitPlaneColor = new(1, 4);
    public static readonly DisplayType byteColor         = new(2, 8);

    public int type { get; }
    public int bitDepth { get; }

    private DisplayType(int type, int bitDepth)
    {
        this.type = type;
        this.bitDepth = bitDepth;
    }

    public int getType() => type;
    public int getBitDepth() => bitDepth;
}
