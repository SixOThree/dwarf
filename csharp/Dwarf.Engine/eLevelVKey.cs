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

// Enumeration of the keys on a (6085) Xerox keyboard, along with the bit
// position in the FCB of the keyboard agent used to inform the running
// program which key is pressed.
//
// Ported as a sealed class with static-readonly instances (the established
// "Java enum with constructor args" pattern in the port — see PilotDefs's
// DisplayType). Field names preserve Java identifiers verbatim, including
// the leading 'e' on the type and 'k' on the reserved-word slot ('null').
public sealed class eLevelVKey
{
    // fcb word[0]
    public static readonly eLevelVKey knull            = new(0); // "null" is a reserved word in Java/C# => "knull"
    public static readonly eLevelVKey Bullet           = new(1);
    public static readonly eLevelVKey SuperSub         = new(2);
    public static readonly eLevelVKey Case             = new(3);
    public static readonly eLevelVKey Strikeout        = new(4);
    public static readonly eLevelVKey KeypadTwo        = new(5);
    public static readonly eLevelVKey KeypadThree      = new(6);
    public static readonly eLevelVKey SingleQuote      = new(7);
    public static readonly eLevelVKey KeypadAdd        = new(8);
    public static readonly eLevelVKey KeypadSubtract   = new(9);
    public static readonly eLevelVKey KeypadMultiply   = new(10);
    public static readonly eLevelVKey KeypadDivide     = new(11);
    public static readonly eLevelVKey KeypadClear      = new(12);
    public static readonly eLevelVKey Point            = new(13);  // left mouse button
    public static readonly eLevelVKey Adjust           = new(14);  // right mouse button
    public static readonly eLevelVKey Menu             = new(15);  // middle mouse button

    // fcb word[1]
    public static readonly eLevelVKey Five             = new(16);
    public static readonly eLevelVKey Four             = new(17);
    public static readonly eLevelVKey Six              = new(18);
    public static readonly eLevelVKey E                = new(19);
    public static readonly eLevelVKey Seven            = new(20);
    public static readonly eLevelVKey D                = new(21);
    public static readonly eLevelVKey U                = new(22);
    public static readonly eLevelVKey V                = new(23);
    public static readonly eLevelVKey Zero             = new(24);
    public static readonly eLevelVKey K                = new(25);
    public static readonly eLevelVKey Dash             = new(26);
    public static readonly eLevelVKey P                = new(27);
    public static readonly eLevelVKey Slash            = new(28);
    public static readonly eLevelVKey Font             = new(29);
    public static readonly eLevelVKey Same             = new(30);
    public static readonly eLevelVKey BS               = new(31);

    // fcb word[2]
    public static readonly eLevelVKey Three            = new(32);
    public static readonly eLevelVKey Two              = new(33);
    public static readonly eLevelVKey W                = new(34);
    public static readonly eLevelVKey Q                = new(35);
    public static readonly eLevelVKey S                = new(36);
    public static readonly eLevelVKey A                = new(37);
    public static readonly eLevelVKey Nine             = new(38);
    public static readonly eLevelVKey I                = new(39);
    public static readonly eLevelVKey X                = new(40);
    public static readonly eLevelVKey O                = new(41);
    public static readonly eLevelVKey L                = new(42);
    public static readonly eLevelVKey Comma            = new(43);
    public static readonly eLevelVKey Quote            = new(44);
    public static readonly eLevelVKey RightBracket     = new(45);
    public static readonly eLevelVKey Open             = new(46);
    public static readonly eLevelVKey Special          = new(47);

    // fcb word[3]
    public static readonly eLevelVKey One              = new(48);
    public static readonly eLevelVKey Tab              = new(49);
    public static readonly eLevelVKey ParaTab          = new(50);
    public static readonly eLevelVKey F                = new(51);
    public static readonly eLevelVKey Props            = new(52);
    public static readonly eLevelVKey C                = new(53);
    public static readonly eLevelVKey J                = new(54);
    public static readonly eLevelVKey B                = new(55);
    public static readonly eLevelVKey Z                = new(56);
    public static readonly eLevelVKey LeftShift        = new(57);
    public static readonly eLevelVKey Period           = new(58);
    public static readonly eLevelVKey SemiColon        = new(59);
    public static readonly eLevelVKey NewPara          = new(60);
    public static readonly eLevelVKey OpenQuote        = new(61);
    public static readonly eLevelVKey Delete           = new(62);
    public static readonly eLevelVKey Next             = new(63);

    // fcb word[4]
    public static readonly eLevelVKey R                = new(64);
    public static readonly eLevelVKey T                = new(65);
    public static readonly eLevelVKey G                = new(66);
    public static readonly eLevelVKey Y                = new(67);
    public static readonly eLevelVKey H                = new(68);
    public static readonly eLevelVKey Eight            = new(69);
    public static readonly eLevelVKey N                = new(70);
    public static readonly eLevelVKey M                = new(71);
    public static readonly eLevelVKey Lock             = new(72);
    public static readonly eLevelVKey Space            = new(73);
    public static readonly eLevelVKey LeftBracket      = new(74);
    public static readonly eLevelVKey Equal            = new(75);
    public static readonly eLevelVKey RightShift       = new(76);
    public static readonly eLevelVKey Stop             = new(77);
    public static readonly eLevelVKey Move             = new(78);
    public static readonly eLevelVKey Undo             = new(79);

    // fcb word[5]
    public static readonly eLevelVKey Margins          = new(80);
    public static readonly eLevelVKey KeypadSeven      = new(81);
    public static readonly eLevelVKey KeypadEight      = new(82);
    public static readonly eLevelVKey KeypadNine       = new(83);
    public static readonly eLevelVKey KeypadFour       = new(84);
    public static readonly eLevelVKey KeypadFive       = new(85);
    public static readonly eLevelVKey English          = new(86);
    public static readonly eLevelVKey KeypadSix        = new(87);
    public static readonly eLevelVKey Katakana         = new(88);
    public static readonly eLevelVKey Copy             = new(89);
    public static readonly eLevelVKey Find             = new(90);
    public static readonly eLevelVKey Again            = new(91);
    public static readonly eLevelVKey Help             = new(92);
    public static readonly eLevelVKey Expand           = new(93);
    public static readonly eLevelVKey KeypadOne        = new(94);
    public static readonly eLevelVKey DiagnosticBitTwo = new(95);

    // fcb word[6]
    public static readonly eLevelVKey DiagnosticBitOne = new(96);
    public static readonly eLevelVKey Center           = new(97);
    public static readonly eLevelVKey KeypadZero       = new(98);
    public static readonly eLevelVKey Bold             = new(99);
    public static readonly eLevelVKey Italic           = new(100);
    public static readonly eLevelVKey Underline        = new(101);
    public static readonly eLevelVKey Superscript      = new(102);
    public static readonly eLevelVKey Subscript        = new(103);
    public static readonly eLevelVKey Smaller          = new(104);
    public static readonly eLevelVKey KeypadPeriod     = new(105);
    public static readonly eLevelVKey KeypadComma      = new(106);
    public static readonly eLevelVKey LeftShiftAlt     = new(107);
    public static readonly eLevelVKey DoubleQuote      = new(108);
    public static readonly eLevelVKey Defaults         = new(109);
    public static readonly eLevelVKey Hiragana         = new(110);
    public static readonly eLevelVKey RightShiftAlt    = new(111);

    private readonly int idx;
    private readonly int word_;
    private readonly ushort bit_;
    private readonly ushort mask_;

    private eLevelVKey(int i)
    {
        this.idx = i;
        this.word_ = i / PrincOpsDefs.WORD_BITS;
        this.bit_ = (ushort)(1 << (PrincOpsDefs.WORD_BITS - 1 - (i % PrincOpsDefs.WORD_BITS)));
        this.mask_ = (ushort)~this.bit_;
    }

    public int getAbsoluteBit() => this.idx;
    public int getWord() => this.word_;
    public ushort getBit() => this.bit_;
    public ushort getMask() => this.mask_;

    public void setPressed(ushort[] kbdWords)
    {
        kbdWords[this.word_] &= this.mask_;
    }

    public void setReleased(ushort[] kbdWords)
    {
        kbdWords[this.word_] |= this.bit_;
    }
}
