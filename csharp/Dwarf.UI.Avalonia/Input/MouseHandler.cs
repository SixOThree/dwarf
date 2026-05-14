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
OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ANY THEORY OF LIABILITY,
WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR
OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using Avalonia.Input;
using Dwarf.Engine;
using Dwarf.UI.Avalonia.Controls;
using AvVisual = global::Avalonia.Visual;

namespace Dwarf.UI.Avalonia.Input;

// Avalonia pointer-event listener that forwards mouse movement and button
// state to the mesa engine via `iUiDataConsumer`.
//
// Wiring:
//   var mh = new MouseHandler(consumer, displayPixelWidth, displayPixelHeight);
//   mh.Attach(displayControl);
//
// Java upstream's `MouseHandler` also handled focus-grabbing on every
// mouse event ("Any mouse event will grab the input focus to the Dwarf
// window."). Avalonia raises focus implicitly on pointer interaction with
// focusable controls, so we don't need an explicit Focus() call — but if
// the host's `DisplayControl` is set `Focusable = true` this happens
// automatically.
//
// Avalonia mouse buttons map to AWT button ids:
//   1 = primary (left)
//   2 = middle
//   3 = secondary (right)
// (Java's `MouseEvent.getButton()` uses the same numbering.)
public sealed class MouseHandler
{
    private readonly iUiDataConsumer _mesaEngine;
    private readonly int _maxX;
    private readonly int _maxY;

    private int _lastX = int.MinValue;
    private int _lastY = int.MinValue;

    // Tracks which Avalonia pointer button maps to which Java/AWT button id.
    // Set on PointerPressed so PointerReleased can fire the right id.
    private int _lastPressedButton = 0;

    public MouseHandler(iUiDataConsumer consumer, int displayWidth, int displayHeight)
    {
        _mesaEngine = consumer;
        _maxX = displayWidth - 1;
        _maxY = displayHeight - 1;
    }

    public void Attach(InputElement target)
    {
        target.PointerMoved += OnPointerMoved;
        target.PointerPressed += OnPointerPressed;
        target.PointerReleased += OnPointerReleased;
        target.PointerEntered += OnPointerMoved; // also report position on enter (matches Java upstream)
        target.PointerExited += OnPointerMoved;  // and on exit
    }

    public void Detach(InputElement target)
    {
        target.PointerMoved -= OnPointerMoved;
        target.PointerPressed -= OnPointerPressed;
        target.PointerReleased -= OnPointerReleased;
        target.PointerEntered -= OnPointerMoved;
        target.PointerExited -= OnPointerMoved;
    }

    private void HandleNewMousePosition(PointerEventArgs e)
    {
        // MouseHandler is only ever attached to a DisplayControl (see
        // MainWindow). DisplayControl has no children, so e.Source IS the
        // control — cast directly so we can ask it for its image rect.
        var display = (DisplayControl)e.Source!;
        var pt = e.GetPosition(display);
        var imgRect = display.GetImageRect();

        // DisplayControl letterboxes the bitmap inside its bounds; translate
        // pointer DIPs → image-rect-local → mesa pixel coordinates.
        double localX = pt.X - imgRect.X;
        double localY = pt.Y - imgRect.Y;
        double scaleX = imgRect.Width > 0.0 ? (_maxX + 1.0) / imgRect.Width : 1.0;
        double scaleY = imgRect.Height > 0.0 ? (_maxY + 1.0) / imgRect.Height : 1.0;

        int newX = (int)Math.Min(Math.Max(0.0, localX * scaleX), _maxX);
        int newY = (int)Math.Min(Math.Max(0.0, localY * scaleY), _maxY);

        if (_lastX != newX || _lastY != newY)
        {
            _lastX = newX;
            _lastY = newY;
            _mesaEngine.acceptMousePosition(_lastX, _lastY);
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        HandleNewMousePosition(e);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        HandleNewMousePosition(e);
        int button = MapButton(e);
        if (button > 0)
        {
            _lastPressedButton = button;
            _mesaEngine.acceptMouseKey(button, true);
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        HandleNewMousePosition(e);
        int button = MapButton(e);
        // Avalonia's PointerReleased InitialPressMouseButton == ChangedButton
        // for the button being released — but on some platforms only the
        // original-press button is reliably reported. Use whichever the
        // event gives us; fall back to the last-pressed button.
        if (button == 0) { button = _lastPressedButton; }
        if (button > 0)
        {
            _mesaEngine.acceptMouseKey(button, false);
        }
    }

    // Map an Avalonia pointer event to the AWT/Java button id used by
    // iUiDataConsumer.acceptMouseKey:
    //   1 = primary (left)
    //   2 = middle
    //   3 = secondary (right)
    //
    // For PointerPressed, the event's ChangedButton tells us which button
    // changed. For PointerReleased, Avalonia 11 also exposes the
    // InitialPressMouseButton on PointerReleasedEventArgs.
    private static int MapButton(PointerEventArgs e)
    {
        // Both PointerPressedEventArgs and PointerReleasedEventArgs are
        // PointerEventArgs subclasses; the GetCurrentPoint(...) lets us
        // check the per-button state.
        var pt = e.GetCurrentPoint((AvVisual)e.Source!);
        var props = pt.Properties;

        if (e is PointerPressedEventArgs pp)
        {
            return pp.GetCurrentPoint((AvVisual)e.Source!).Properties.PointerUpdateKind switch
            {
                PointerUpdateKind.LeftButtonPressed => 1,
                PointerUpdateKind.MiddleButtonPressed => 2,
                PointerUpdateKind.RightButtonPressed => 3,
                _ => 0,
            };
        }
        else if (e is PointerReleasedEventArgs pr)
        {
            return pr.InitialPressMouseButton switch
            {
                MouseButton.Left => 1,
                MouseButton.Middle => 2,
                MouseButton.Right => 3,
                _ => 0,
            };
        }

        // Fallback for unexpected event subtypes — use the current properties.
        if (props.IsLeftButtonPressed) { return 1; }
        if (props.IsMiddleButtonPressed) { return 2; }
        if (props.IsRightButtonPressed) { return 3; }
        return 0;
    }
}
