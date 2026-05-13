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

using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Dwarf.Engine;
using Dwarf.UI.Avalonia.Controls;
using Dwarf.UI.Avalonia.Input;

namespace Dwarf.UI.Avalonia;

public partial class MainWindow : Window
{
    private UiRefresher? _refresher;
    private KeyHandler? _keyHandler;
    private MouseHandler? _mouseHandler;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);

        var display = this.FindControl<DisplayControl>("display")!;
        var session = GuiSession.Current;

        if (session != null)
        {
            // GUI Duchess mode: real engine running.
            display.Source = new MemDisplaySource();
            display.Focusable = true; // receive keyboard input

            this.Title = $"Dwarf / Duchess  —  {session.DisplayWidth}x{session.DisplayHeight} mono  (Avalonia)";

            // Wire input + refresh
            _keyHandler = new KeyHandler(session.KeyMapper);
            _keyHandler.Attach(this); // Window receives key events for the whole frame

            _mouseHandler = new MouseHandler(session.Consumer, session.DisplayWidth, session.DisplayHeight);
            _mouseHandler.Attach(display);

            _refresher = new UiRefresher(display);
            this.Opened += (_, _) => _refresher.Start();
            this.Closing += (_, _) =>
            {
                _refresher?.Stop();
                Processes.requestMesaEngineStop();
            };
        }
        else if (Mem.pageFlags != null && Mem.getDisplayType() == DisplayType.monochrome)
        {
            // Prototype mode: Mem initialized but engine not running. Show
            // whatever the init pattern left in display memory.
            display.Source = new MemDisplaySource();
            this.Title = $"Dwarf / Duchess  —  {Mem.getDisplayPixelWidth()}x{Mem.getDisplayPixelHeight()} mono (no engine)";

            // Refresh once so static pattern paints; no UiRefresher needed.
        }
        else
        {
            // Fallback: no Mem init either. Pure prototype window.
            display.Source = new DiagonalStripesSource(1024, 768);
            this.Title = "Dwarf / Duchess  —  test pattern (engine not initialized)";
        }
    }
}
