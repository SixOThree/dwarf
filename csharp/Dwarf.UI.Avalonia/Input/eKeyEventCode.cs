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

using System.Reflection;

namespace Dwarf.UI.Avalonia.Input;

// Java AWT KeyEvent VK_* constants ported to C#. The names and integer
// codes match Java AWT exactly so existing `keyboard-maps/*.map` files
// (which reference these by name and by hex code) work unchanged.
//
// Lookup helpers:
//   `valueOf(name)` returns the constant by its VK_* name (matches Java
//   `eKeyEventCode.valueOf(name)`)
//   `getName(code)` returns the VK_* name for a given AWT int code (matches
//   Java `eKeyEventCode.get(code).toString()`)
//
// Static-init order: all the public-static-readonly fields below are
// initialized in declaration order; the static constructor then walks
// them via reflection to build the name↔code dictionaries.
public sealed class eKeyEventCode
{
    public int Code { get; }
    private eKeyEventCode(int code) { Code = code; }

    public static readonly eKeyEventCode VK_0 = new(48);
    public static readonly eKeyEventCode VK_1 = new(49);
    public static readonly eKeyEventCode VK_2 = new(50);
    public static readonly eKeyEventCode VK_3 = new(51);
    public static readonly eKeyEventCode VK_4 = new(52);
    public static readonly eKeyEventCode VK_5 = new(53);
    public static readonly eKeyEventCode VK_6 = new(54);
    public static readonly eKeyEventCode VK_7 = new(55);
    public static readonly eKeyEventCode VK_8 = new(56);
    public static readonly eKeyEventCode VK_9 = new(57);
    public static readonly eKeyEventCode VK_A = new(65);
    public static readonly eKeyEventCode VK_ACCEPT = new(30);
    public static readonly eKeyEventCode VK_ADD = new(107);
    public static readonly eKeyEventCode VK_AGAIN = new(65481);
    public static readonly eKeyEventCode VK_ALL_CANDIDATES = new(256);
    public static readonly eKeyEventCode VK_ALPHANUMERIC = new(240);
    public static readonly eKeyEventCode VK_ALT = new(18);
    public static readonly eKeyEventCode VK_ALT_GRAPH = new(65406);
    public static readonly eKeyEventCode VK_AMPERSAND = new(150);
    public static readonly eKeyEventCode VK_ASTERISK = new(151);
    public static readonly eKeyEventCode VK_AT = new(512);
    public static readonly eKeyEventCode VK_B = new(66);
    public static readonly eKeyEventCode VK_BACK_QUOTE = new(192);
    public static readonly eKeyEventCode VK_BACK_SLASH = new(92);
    public static readonly eKeyEventCode VK_BACK_SPACE = new(8);
    public static readonly eKeyEventCode VK_BEGIN = new(65368);
    public static readonly eKeyEventCode VK_BRACELEFT = new(161);
    public static readonly eKeyEventCode VK_BRACERIGHT = new(162);
    public static readonly eKeyEventCode VK_C = new(67);
    public static readonly eKeyEventCode VK_CANCEL = new(3);
    public static readonly eKeyEventCode VK_CAPS_LOCK = new(20);
    public static readonly eKeyEventCode VK_CIRCUMFLEX = new(514);
    public static readonly eKeyEventCode VK_CLEAR = new(12);
    public static readonly eKeyEventCode VK_CLOSE_BRACKET = new(93);
    public static readonly eKeyEventCode VK_CODE_INPUT = new(258);
    public static readonly eKeyEventCode VK_COLON = new(513);
    public static readonly eKeyEventCode VK_COMMA = new(44);
    public static readonly eKeyEventCode VK_COMPOSE = new(65312);
    public static readonly eKeyEventCode VK_CONTEXT_MENU = new(525);
    public static readonly eKeyEventCode VK_CONTROL = new(17);
    public static readonly eKeyEventCode VK_CONVERT = new(28);
    public static readonly eKeyEventCode VK_COPY = new(65485);
    public static readonly eKeyEventCode VK_CUT = new(65489);
    public static readonly eKeyEventCode VK_D = new(68);
    public static readonly eKeyEventCode VK_DEAD_ABOVEDOT = new(134);
    public static readonly eKeyEventCode VK_DEAD_ABOVERING = new(136);
    public static readonly eKeyEventCode VK_DEAD_ACUTE = new(129);
    public static readonly eKeyEventCode VK_DEAD_BREVE = new(133);
    public static readonly eKeyEventCode VK_DEAD_CARON = new(138);
    public static readonly eKeyEventCode VK_DEAD_CEDILLA = new(139);
    public static readonly eKeyEventCode VK_DEAD_CIRCUMFLEX = new(130);
    public static readonly eKeyEventCode VK_DEAD_DIAERESIS = new(135);
    public static readonly eKeyEventCode VK_DEAD_DOUBLEACUTE = new(137);
    public static readonly eKeyEventCode VK_DEAD_GRAVE = new(128);
    public static readonly eKeyEventCode VK_DEAD_IOTA = new(141);
    public static readonly eKeyEventCode VK_DEAD_MACRON = new(132);
    public static readonly eKeyEventCode VK_DEAD_OGONEK = new(140);
    public static readonly eKeyEventCode VK_DEAD_SEMIVOICED_SOUND = new(143);
    public static readonly eKeyEventCode VK_DEAD_TILDE = new(131);
    public static readonly eKeyEventCode VK_DEAD_VOICED_SOUND = new(142);
    public static readonly eKeyEventCode VK_DECIMAL = new(110);
    public static readonly eKeyEventCode VK_DELETE = new(127);
    public static readonly eKeyEventCode VK_DIVIDE = new(111);
    public static readonly eKeyEventCode VK_DOLLAR = new(515);
    public static readonly eKeyEventCode VK_DOWN = new(40);
    public static readonly eKeyEventCode VK_E = new(69);
    public static readonly eKeyEventCode VK_END = new(35);
    public static readonly eKeyEventCode VK_ENTER = new(10);
    public static readonly eKeyEventCode VK_EQUALS = new(61);
    public static readonly eKeyEventCode VK_ESCAPE = new(27);
    public static readonly eKeyEventCode VK_EURO_SIGN = new(516);
    public static readonly eKeyEventCode VK_EXCLAMATION_MARK = new(517);
    public static readonly eKeyEventCode VK_F = new(70);
    public static readonly eKeyEventCode VK_F1 = new(112);
    public static readonly eKeyEventCode VK_F10 = new(121);
    public static readonly eKeyEventCode VK_F11 = new(122);
    public static readonly eKeyEventCode VK_F12 = new(123);
    public static readonly eKeyEventCode VK_F13 = new(61440);
    public static readonly eKeyEventCode VK_F14 = new(61441);
    public static readonly eKeyEventCode VK_F15 = new(61442);
    public static readonly eKeyEventCode VK_F16 = new(61443);
    public static readonly eKeyEventCode VK_F17 = new(61444);
    public static readonly eKeyEventCode VK_F18 = new(61445);
    public static readonly eKeyEventCode VK_F19 = new(61446);
    public static readonly eKeyEventCode VK_F2 = new(113);
    public static readonly eKeyEventCode VK_F20 = new(61447);
    public static readonly eKeyEventCode VK_F21 = new(61448);
    public static readonly eKeyEventCode VK_F22 = new(61449);
    public static readonly eKeyEventCode VK_F23 = new(61450);
    public static readonly eKeyEventCode VK_F24 = new(61451);
    public static readonly eKeyEventCode VK_F3 = new(114);
    public static readonly eKeyEventCode VK_F4 = new(115);
    public static readonly eKeyEventCode VK_F5 = new(116);
    public static readonly eKeyEventCode VK_F6 = new(117);
    public static readonly eKeyEventCode VK_F7 = new(118);
    public static readonly eKeyEventCode VK_F8 = new(119);
    public static readonly eKeyEventCode VK_F9 = new(120);
    public static readonly eKeyEventCode VK_FINAL = new(24);
    public static readonly eKeyEventCode VK_FIND = new(65488);
    public static readonly eKeyEventCode VK_FULL_WIDTH = new(243);
    public static readonly eKeyEventCode VK_G = new(71);
    public static readonly eKeyEventCode VK_GREATER = new(160);
    public static readonly eKeyEventCode VK_H = new(72);
    public static readonly eKeyEventCode VK_HALF_WIDTH = new(244);
    public static readonly eKeyEventCode VK_HELP = new(156);
    public static readonly eKeyEventCode VK_HIRAGANA = new(242);
    public static readonly eKeyEventCode VK_HOME = new(36);
    public static readonly eKeyEventCode VK_I = new(73);
    public static readonly eKeyEventCode VK_INPUT_METHOD_ON_OFF = new(263);
    public static readonly eKeyEventCode VK_INSERT = new(155);
    public static readonly eKeyEventCode VK_INVERTED_EXCLAMATION_MARK = new(518);
    public static readonly eKeyEventCode VK_J = new(74);
    public static readonly eKeyEventCode VK_JAPANESE_HIRAGANA = new(260);
    public static readonly eKeyEventCode VK_JAPANESE_KATAKANA = new(259);
    public static readonly eKeyEventCode VK_JAPANESE_ROMAN = new(261);
    public static readonly eKeyEventCode VK_K = new(75);
    public static readonly eKeyEventCode VK_KANA = new(21);
    public static readonly eKeyEventCode VK_KANA_LOCK = new(262);
    public static readonly eKeyEventCode VK_KANJI = new(25);
    public static readonly eKeyEventCode VK_KATAKANA = new(241);
    public static readonly eKeyEventCode VK_KP_DOWN = new(225);
    public static readonly eKeyEventCode VK_KP_LEFT = new(226);
    public static readonly eKeyEventCode VK_KP_RIGHT = new(227);
    public static readonly eKeyEventCode VK_KP_UP = new(224);
    public static readonly eKeyEventCode VK_L = new(76);
    public static readonly eKeyEventCode VK_LEFT = new(37);
    public static readonly eKeyEventCode VK_LEFT_PARENTHESIS = new(519);
    public static readonly eKeyEventCode VK_LESS = new(153);
    public static readonly eKeyEventCode VK_M = new(77);
    public static readonly eKeyEventCode VK_META = new(157);
    public static readonly eKeyEventCode VK_MINUS = new(45);
    public static readonly eKeyEventCode VK_MODECHANGE = new(31);
    public static readonly eKeyEventCode VK_MULTIPLY = new(106);
    public static readonly eKeyEventCode VK_N = new(78);
    public static readonly eKeyEventCode VK_NONCONVERT = new(29);
    public static readonly eKeyEventCode VK_NUM_LOCK = new(144);
    public static readonly eKeyEventCode VK_NUMBER_SIGN = new(520);
    public static readonly eKeyEventCode VK_NUMPAD0 = new(96);
    public static readonly eKeyEventCode VK_NUMPAD1 = new(97);
    public static readonly eKeyEventCode VK_NUMPAD2 = new(98);
    public static readonly eKeyEventCode VK_NUMPAD3 = new(99);
    public static readonly eKeyEventCode VK_NUMPAD4 = new(100);
    public static readonly eKeyEventCode VK_NUMPAD5 = new(101);
    public static readonly eKeyEventCode VK_NUMPAD6 = new(102);
    public static readonly eKeyEventCode VK_NUMPAD7 = new(103);
    public static readonly eKeyEventCode VK_NUMPAD8 = new(104);
    public static readonly eKeyEventCode VK_NUMPAD9 = new(105);
    public static readonly eKeyEventCode VK_O = new(79);
    public static readonly eKeyEventCode VK_OPEN_BRACKET = new(91);
    public static readonly eKeyEventCode VK_P = new(80);
    public static readonly eKeyEventCode VK_PAGE_DOWN = new(34);
    public static readonly eKeyEventCode VK_PAGE_UP = new(33);
    public static readonly eKeyEventCode VK_PASTE = new(65487);
    public static readonly eKeyEventCode VK_PAUSE = new(19);
    public static readonly eKeyEventCode VK_PERIOD = new(46);
    public static readonly eKeyEventCode VK_PLUS = new(521);
    public static readonly eKeyEventCode VK_PREVIOUS_CANDIDATE = new(257);
    public static readonly eKeyEventCode VK_PRINTSCREEN = new(154);
    public static readonly eKeyEventCode VK_PROPS = new(65482);
    public static readonly eKeyEventCode VK_Q = new(81);
    public static readonly eKeyEventCode VK_QUOTE = new(222);
    public static readonly eKeyEventCode VK_QUOTEDBL = new(152);
    public static readonly eKeyEventCode VK_R = new(82);
    public static readonly eKeyEventCode VK_RIGHT = new(39);
    public static readonly eKeyEventCode VK_RIGHT_PARENTHESIS = new(522);
    public static readonly eKeyEventCode VK_ROMAN_CHARACTERS = new(245);
    public static readonly eKeyEventCode VK_S = new(83);
    public static readonly eKeyEventCode VK_SCROLL_LOCK = new(145);
    public static readonly eKeyEventCode VK_SEMICOLON = new(59);
    public static readonly eKeyEventCode VK_SEPARATER = new(108);
    public static readonly eKeyEventCode VK_SEPARATOR = new(108);
    public static readonly eKeyEventCode VK_SHIFT = new(16);
    public static readonly eKeyEventCode VK_SLASH = new(47);
    public static readonly eKeyEventCode VK_SPACE = new(32);
    public static readonly eKeyEventCode VK_STOP = new(65480);
    public static readonly eKeyEventCode VK_SUBTRACT = new(109);
    public static readonly eKeyEventCode VK_T = new(84);
    public static readonly eKeyEventCode VK_TAB = new(9);
    public static readonly eKeyEventCode VK_U = new(85);
    public static readonly eKeyEventCode VK_UNDEFINED = new(0);
    public static readonly eKeyEventCode VK_UNDERSCORE = new(523);
    public static readonly eKeyEventCode VK_UNDO = new(65483);
    public static readonly eKeyEventCode VK_UP = new(38);
    public static readonly eKeyEventCode VK_V = new(86);
    public static readonly eKeyEventCode VK_W = new(87);
    public static readonly eKeyEventCode VK_WINDOWS = new(524);
    public static readonly eKeyEventCode VK_X = new(88);
    public static readonly eKeyEventCode VK_Y = new(89);
    public static readonly eKeyEventCode VK_Z = new(90);

    private static readonly Dictionary<string, eKeyEventCode> _byName;
    private static readonly Dictionary<int, string> _byCode;

    static eKeyEventCode()
    {
        _byName = new Dictionary<string, eKeyEventCode>(StringComparer.Ordinal);
        _byCode = new Dictionary<int, string>();
        foreach (FieldInfo fi in typeof(eKeyEventCode).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (fi.FieldType == typeof(eKeyEventCode) && fi.GetValue(null) is eKeyEventCode v)
            {
                _byName[fi.Name] = v;
                // first-name-for-this-code wins (VK_SEPARATER and VK_SEPARATOR both = 108;
                // alphabetic order preserves the original Java behavior)
                _byCode.TryAdd(v.Code, fi.Name);
            }
        }
    }

    // Lookup by name (e.g. "VK_A" → the VK_A instance). Returns null if
    // the name is unknown. Matches Java `eKeyEventCode.valueOf(name)`.
    public static eKeyEventCode? valueOf(string name) =>
        _byName.TryGetValue(name, out eKeyEventCode? v) ? v : null;

    // Lookup the name for a given AWT int code. Matches Java
    // `eKeyEventCode.get(code).toString()`.
    public static string? getName(int code) =>
        _byCode.TryGetValue(code, out string? n) ? n : null;
}
