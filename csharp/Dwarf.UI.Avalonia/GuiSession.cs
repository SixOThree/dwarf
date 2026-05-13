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
using Dwarf.UI.Avalonia.Input;

namespace Dwarf.UI.Avalonia;

// State the Avalonia UI needs to know about the engine when running in
// GUI mode. The host process (Dwarf.Duchess.RunGui) constructs an
// instance and publishes it via `GuiSession.Current` before launching
// Avalonia; the `MainWindow` constructor reads `Current` to wire
// `KeyHandler` / `MouseHandler` / `UiRefresher`.
//
// Lives in `Dwarf.UI.Avalonia` rather than `Dwarf.Duchess` so the
// MainWindow doesn't need a reference to the host project — only the
// host references the UI library, which keeps the project graph acyclic.
public sealed class GuiSession
{
    public iUiDataConsumer Consumer { get; }
    public KeyboardMapper KeyMapper { get; }
    public int DisplayWidth { get; }
    public int DisplayHeight { get; }

    public GuiSession(iUiDataConsumer consumer, KeyboardMapper keyMapper, int displayWidth, int displayHeight)
    {
        Consumer = consumer;
        KeyMapper = keyMapper;
        DisplayWidth = displayWidth;
        DisplayHeight = displayHeight;
    }

    // Set by the host before launching Avalonia. MainWindow reads it on
    // construction. Null in the no-engine prototype path.
    public static GuiSession? Current { get; set; }
}
