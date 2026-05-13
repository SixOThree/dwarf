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
using Avalonia.Interactivity;
using Dwarf.Engine;

namespace Dwarf.UI.Avalonia.Input;

// Avalonia KeyDown/KeyUp listener that forwards key events to a
// `KeyboardMapper` (which in turn talks to the mesa engine).
//
// Wiring:
//   var kh = new KeyHandler(keyboardMapper);
//   kh.Attach(mainWindow);
//
// Linux dead-key workaround (RISKS R6): on some Linux desktops the
// compositor pre-handles diacritic dead-key start characters (`, `^`,
// etc.) such that only the KeyUp arrives in the application — the
// KeyDown is swallowed. To still produce a press/release pair we detect
// a release for a key we never saw a press for, synthesize a press
// immediately, and schedule the release 50 ms later via `Task.Delay`.
//
// **Status**: ported faithfully from Java; test on Linux to confirm it
// behaves the same. The Java workaround keeps a List<Integer> currPressed;
// the C# port uses HashSet<int> for O(1) Contains/Add/Remove.
public sealed class KeyHandler
{
    private readonly KeyboardMapper _keyMapper;
    private readonly HashSet<int> _currPressed = new();
    private bool _dumpOnVkLess = false;

    public KeyHandler(KeyboardMapper keyMapper)
    {
        _keyMapper = keyMapper;
    }

    // Attach to an Avalonia visual (typically the main window or its root
    // content). Subscribes to KeyDown/KeyUp; detaches by `Detach`.
    public void Attach(InputElement target)
    {
        target.KeyDown += OnKeyDown;
        target.KeyUp += OnKeyUp;
    }

    public void Detach(InputElement target)
    {
        target.KeyDown -= OnKeyDown;
        target.KeyUp -= OnKeyUp;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Java upstream's VK_LESS-toggle for flight-recorder dump/start
        // is gated by `Config.USE_DEBUG_INTERPRETER` (false). Dead code
        // — preserved for diff fidelity. Avalonia.Key.OemComma maps to
        // VK_COMMA, not VK_LESS; the upstream code was using VK_LESS
        // for the `<` key on a German keyboard, which is a different
        // physical key. Skip the dev toggle in the C# port.
        if (Config.USE_DEBUG_INTERPRETER)
        {
            // intentionally unreachable
            if (_dumpOnVkLess)
            {
                Processes.requestFlightRecorderStopAndDump();
                _dumpOnVkLess = false;
            }
            else
            {
                Processes.requestFlightRecorderStart();
                _dumpOnVkLess = true;
            }
        }

        int? code = AvaloniaKeyMap.ToVkCode(e.Key);
        if (code == null) { return; }

        _keyMapper.pressed(code.Value);
        _currPressed.Add(code.Value);
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        int? code = AvaloniaKeyMap.ToVkCode(e.Key);
        if (code == null) { return; }

        int c = code.Value;
        _keyMapper.released(c);

        // HashSet.Remove returns true if the element was present —
        // single-lookup version of "Contains-then-Remove" (CA1868).
        if (!_currPressed.Remove(c))
        {
            // Dead-key workaround: we got a release for a key that was
            // never pressed. Synthesize a press now + delayed release
            // 50 ms later.
            _keyMapper.pressed(c);
            _ = Task.Run(async () =>
            {
                await Task.Delay(50).ConfigureAwait(false);
                _keyMapper.released(c);
            });
        }
    }
}
