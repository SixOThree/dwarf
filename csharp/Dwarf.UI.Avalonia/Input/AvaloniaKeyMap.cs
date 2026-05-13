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

using Avalonia.Input;

namespace Dwarf.UI.Avalonia.Input;

// Bridge between Avalonia's `Key` enum (the actual UI input value) and
// Java AWT VK_* integer codes (which existing `keyboard-maps/*.map`
// files reference by name).
//
// Avalonia.Key has different integer values from AWT VK_*, so this is a
// translator — a `switch` returning the AWT VK_* code for each known
// Avalonia key. Unknown keys return null and the KeyboardMapper drops
// them.
//
// Coverage: ASCII letters, digits, common modifiers (Shift/Ctrl/Alt),
// navigation (arrows, Home/End/Page), function keys F1-F24, numpad,
// common punctuation. Dead keys + uncommon international keys are
// platform-specific in Avalonia and not mapped here — RISKS R6 says
// "test on Linux during Phase E"; the workaround in `KeyHandler` should
// cover Linux dead-key behavior when it surfaces.
public static class AvaloniaKeyMap
{
    // Avalonia.Key → AWT VK_* integer code (or null if no mapping).
    public static int? ToVkCode(Key key) => key switch
    {
        // letters
        Key.A => eKeyEventCode.VK_A.Code,
        Key.B => eKeyEventCode.VK_B.Code,
        Key.C => eKeyEventCode.VK_C.Code,
        Key.D => eKeyEventCode.VK_D.Code,
        Key.E => eKeyEventCode.VK_E.Code,
        Key.F => eKeyEventCode.VK_F.Code,
        Key.G => eKeyEventCode.VK_G.Code,
        Key.H => eKeyEventCode.VK_H.Code,
        Key.I => eKeyEventCode.VK_I.Code,
        Key.J => eKeyEventCode.VK_J.Code,
        Key.K => eKeyEventCode.VK_K.Code,
        Key.L => eKeyEventCode.VK_L.Code,
        Key.M => eKeyEventCode.VK_M.Code,
        Key.N => eKeyEventCode.VK_N.Code,
        Key.O => eKeyEventCode.VK_O.Code,
        Key.P => eKeyEventCode.VK_P.Code,
        Key.Q => eKeyEventCode.VK_Q.Code,
        Key.R => eKeyEventCode.VK_R.Code,
        Key.S => eKeyEventCode.VK_S.Code,
        Key.T => eKeyEventCode.VK_T.Code,
        Key.U => eKeyEventCode.VK_U.Code,
        Key.V => eKeyEventCode.VK_V.Code,
        Key.W => eKeyEventCode.VK_W.Code,
        Key.X => eKeyEventCode.VK_X.Code,
        Key.Y => eKeyEventCode.VK_Y.Code,
        Key.Z => eKeyEventCode.VK_Z.Code,

        // top-row digits
        Key.D0 => eKeyEventCode.VK_0.Code,
        Key.D1 => eKeyEventCode.VK_1.Code,
        Key.D2 => eKeyEventCode.VK_2.Code,
        Key.D3 => eKeyEventCode.VK_3.Code,
        Key.D4 => eKeyEventCode.VK_4.Code,
        Key.D5 => eKeyEventCode.VK_5.Code,
        Key.D6 => eKeyEventCode.VK_6.Code,
        Key.D7 => eKeyEventCode.VK_7.Code,
        Key.D8 => eKeyEventCode.VK_8.Code,
        Key.D9 => eKeyEventCode.VK_9.Code,

        // numpad digits
        Key.NumPad0 => eKeyEventCode.VK_NUMPAD0.Code,
        Key.NumPad1 => eKeyEventCode.VK_NUMPAD1.Code,
        Key.NumPad2 => eKeyEventCode.VK_NUMPAD2.Code,
        Key.NumPad3 => eKeyEventCode.VK_NUMPAD3.Code,
        Key.NumPad4 => eKeyEventCode.VK_NUMPAD4.Code,
        Key.NumPad5 => eKeyEventCode.VK_NUMPAD5.Code,
        Key.NumPad6 => eKeyEventCode.VK_NUMPAD6.Code,
        Key.NumPad7 => eKeyEventCode.VK_NUMPAD7.Code,
        Key.NumPad8 => eKeyEventCode.VK_NUMPAD8.Code,
        Key.NumPad9 => eKeyEventCode.VK_NUMPAD9.Code,
        Key.Add => eKeyEventCode.VK_ADD.Code,
        Key.Subtract => eKeyEventCode.VK_SUBTRACT.Code,
        Key.Multiply => eKeyEventCode.VK_MULTIPLY.Code,
        Key.Divide => eKeyEventCode.VK_DIVIDE.Code,
        Key.Decimal => eKeyEventCode.VK_DECIMAL.Code,
        Key.Separator => eKeyEventCode.VK_SEPARATOR.Code,

        // function keys
        Key.F1 => eKeyEventCode.VK_F1.Code,
        Key.F2 => eKeyEventCode.VK_F2.Code,
        Key.F3 => eKeyEventCode.VK_F3.Code,
        Key.F4 => eKeyEventCode.VK_F4.Code,
        Key.F5 => eKeyEventCode.VK_F5.Code,
        Key.F6 => eKeyEventCode.VK_F6.Code,
        Key.F7 => eKeyEventCode.VK_F7.Code,
        Key.F8 => eKeyEventCode.VK_F8.Code,
        Key.F9 => eKeyEventCode.VK_F9.Code,
        Key.F10 => eKeyEventCode.VK_F10.Code,
        Key.F11 => eKeyEventCode.VK_F11.Code,
        Key.F12 => eKeyEventCode.VK_F12.Code,
        Key.F13 => eKeyEventCode.VK_F13.Code,
        Key.F14 => eKeyEventCode.VK_F14.Code,
        Key.F15 => eKeyEventCode.VK_F15.Code,
        Key.F16 => eKeyEventCode.VK_F16.Code,
        Key.F17 => eKeyEventCode.VK_F17.Code,
        Key.F18 => eKeyEventCode.VK_F18.Code,
        Key.F19 => eKeyEventCode.VK_F19.Code,
        Key.F20 => eKeyEventCode.VK_F20.Code,
        Key.F21 => eKeyEventCode.VK_F21.Code,
        Key.F22 => eKeyEventCode.VK_F22.Code,
        Key.F23 => eKeyEventCode.VK_F23.Code,
        Key.F24 => eKeyEventCode.VK_F24.Code,

        // modifiers — Avalonia distinguishes left/right; Java AWT VK_*
        // doesn't (modifiers are reported as a single VK_SHIFT/CONTROL/ALT
        // regardless of which side). Map both sides to the same VK_*.
        Key.LeftShift => eKeyEventCode.VK_SHIFT.Code,
        Key.RightShift => eKeyEventCode.VK_SHIFT.Code,
        Key.LeftCtrl => eKeyEventCode.VK_CONTROL.Code,
        Key.RightCtrl => eKeyEventCode.VK_CONTROL.Code,
        Key.LeftAlt => eKeyEventCode.VK_ALT.Code,
        Key.RightAlt => eKeyEventCode.VK_ALT.Code, // some layouts treat right-Alt as AltGr; .map files can re-bind
        Key.LWin => eKeyEventCode.VK_WINDOWS.Code,
        Key.RWin => eKeyEventCode.VK_WINDOWS.Code,
        Key.CapsLock => eKeyEventCode.VK_CAPS_LOCK.Code,
        Key.NumLock => eKeyEventCode.VK_NUM_LOCK.Code,
        Key.Scroll => eKeyEventCode.VK_SCROLL_LOCK.Code,

        // whitespace / control
        Key.Space => eKeyEventCode.VK_SPACE.Code,
        Key.Tab => eKeyEventCode.VK_TAB.Code,
        Key.Enter => eKeyEventCode.VK_ENTER.Code, // Avalonia: Return == Enter (Key.Return alias)
        Key.Back => eKeyEventCode.VK_BACK_SPACE.Code,
        Key.Escape => eKeyEventCode.VK_ESCAPE.Code,
        Key.Delete => eKeyEventCode.VK_DELETE.Code,
        Key.Insert => eKeyEventCode.VK_INSERT.Code,

        // navigation
        Key.Up => eKeyEventCode.VK_UP.Code,
        Key.Down => eKeyEventCode.VK_DOWN.Code,
        Key.Left => eKeyEventCode.VK_LEFT.Code,
        Key.Right => eKeyEventCode.VK_RIGHT.Code,
        Key.Home => eKeyEventCode.VK_HOME.Code,
        Key.End => eKeyEventCode.VK_END.Code,
        Key.PageUp => eKeyEventCode.VK_PAGE_UP.Code,
        Key.PageDown => eKeyEventCode.VK_PAGE_DOWN.Code,
        Key.PrintScreen => eKeyEventCode.VK_PRINTSCREEN.Code,
        Key.Pause => eKeyEventCode.VK_PAUSE.Code,
        Key.Help => eKeyEventCode.VK_HELP.Code,
        Key.Clear => eKeyEventCode.VK_CLEAR.Code,
        Key.Cancel => eKeyEventCode.VK_CANCEL.Code,

        // common OEM punctuation (US layout assumptions; non-US layouts
        // map physical keys to different chars but the VK_* code is the
        // same since AWT keys are positional, not character-based)
        Key.OemComma => eKeyEventCode.VK_COMMA.Code,
        Key.OemPeriod => eKeyEventCode.VK_PERIOD.Code,
        Key.OemMinus => eKeyEventCode.VK_MINUS.Code,
        Key.OemPlus => eKeyEventCode.VK_EQUALS.Code, // physically the = key on US
        Key.OemQuestion => eKeyEventCode.VK_SLASH.Code,
        Key.OemSemicolon => eKeyEventCode.VK_SEMICOLON.Code,
        Key.OemQuotes => eKeyEventCode.VK_QUOTE.Code,
        Key.OemTilde => eKeyEventCode.VK_BACK_QUOTE.Code,
        Key.OemOpenBrackets => eKeyEventCode.VK_OPEN_BRACKET.Code,
        Key.OemCloseBrackets => eKeyEventCode.VK_CLOSE_BRACKET.Code,
        Key.OemPipe => eKeyEventCode.VK_BACK_SLASH.Code,
        Key.OemBackslash => eKeyEventCode.VK_BACK_SLASH.Code,

        // anything else: unmapped
        _ => null,
    };
}
