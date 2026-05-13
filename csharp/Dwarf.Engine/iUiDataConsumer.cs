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

// Callbacks provided by the (device-) Agents of the mesa engine to the UI for
// asynchronously receiving UI events or initiating display updates.
//
// The lowercase 'i' prefix mirrors the Java name for traceability.
public interface iUiDataConsumer
{
    // Functional interface (delegate) for setting the cursor bitmap.
    //
    // bitmap   : bits for the cursor; each entry defines a 16-bit line of the
    //            cursor, the array should have a length of 16 entries
    // hotspotX : horizontal position of the hotspot in the cursor bitmap
    // hotspotY : vertical position of the hotspot in the cursor bitmap
    public delegate void PointerBitmapAcceptor(ushort[] bitmap, int hotspotX, int hotspotY);

    // Agent callback to inform the mesa engine that a keyboard key was
    // pressed or released.
    void acceptKeyboardKey(eLevelVKey key, bool isPressed);

    // Agent callback to inform the mesa engine that no keys are to be
    // considered pressed.
    void resetKeys();

    // Agent callback to inform the mesa engine about a change of the
    // mouse buttons depressions. key=1 left, 2 middle, 3 right.
    void acceptMouseKey(int key, bool isPressed);

    // Agent callback to inform the mesa engine about a new mouse pointer
    // position.
    void acceptMousePosition(int x, int y);

    // Register the callback to be used by the mesa engine to set a new shape
    // for the cursor bitmap. Registered once at UI initialization.
    void registerPointerBitmapAcceptor(PointerBitmapAcceptor acpt);

    // Register the callback to be used by the mesa machine to refresh the
    // visible UI state (display bitmap, MP code, statistics). Registered once
    // at UI initialization. Returns a callback returning the current color
    // table (may be null for B/W display; ints are 0x00rrggbb format).
    Func<int[]> registerUiDataRefresher(iMesaMachineDataAccessor refresher);
}
