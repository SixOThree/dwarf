/*
Copyright (c) 2017, Dr. Hans-Walter Latz (original Java implementation pattern)
Copyright (c) 2026, Matthew Dugal (C# .NET 10 port — minimal Avalonia variant)
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

using Avalonia.Threading;
using AvVisual = global::Avalonia.Visual;

namespace Dwarf.UI.Avalonia;

// Minimum-viable Avalonia UI refresher: a `DispatcherTimer` ticking every
// 20 ms that calls `Visual.InvalidateVisual()` on the target control.
// That triggers Avalonia to call `Control.Render(DrawingContext)` on the
// DisplayControl, which in turn calls `IDisplaySource.CopyToBgra8888`
// to refresh the WriteableBitmap with the engine's current display
// memory.
//
// **Scope note**: Java upstream's `UiRefresher` (~270 LOC) also handled
// MP-code display, statistics formatting, status-line alternation, and
// mouse-cursor shape updates. Those are polish items for later — the
// minimum to get an interactive boot is just the display refresh
// trigger. Status line + MP display can be added when MainWindow grows
// a status bar.
public sealed class UiRefresher
{
    private const int RefreshIntervalMs = 20; // ~50 Hz
    private readonly AvVisual _target;
    private readonly DispatcherTimer _timer;
    private bool _paused = false;

    public UiRefresher(AvVisual target)
    {
        _target = target;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(RefreshIntervalMs),
        };
        _timer.Tick += OnTick;
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    // Pause/resume — for window-minimized / focus-lost situations where
    // there's no point repainting until the user comes back. Cheaper than
    // Stop/Start because we keep the timer object alive.
    public bool Paused
    {
        get => _paused;
        set => _paused = value;
    }

    public bool IsRunning => _timer.IsEnabled;

    private void OnTick(object? sender, EventArgs e)
    {
        if (_paused) { return; }
        _target.InvalidateVisual();
    }
}
