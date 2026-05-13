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

using Dwarf.Engine;

namespace Dwarf.UI.Avalonia.Input;

// Mapper from host UI keystrokes (AWT-style integer codes) to mesa engine
// keys (`eLevelVKey`).
//
// Keys are mapped through one of two tables — `normalKeyMapping` when the
// Xerox Control modifier isn't pressed, `ctlKeyMapping` when it is. Press
// state tracking handles the special semantics around the Control key:
// pressing Ctrl latches a flag; subsequent presses go into `ctlPressed`;
// releasing Ctrl sends release events for everything in `ctlPressed` and
// transfers their state back to the normal pressed map so that the Java-
// side key-up arrives later doesn't drop again.
//
// Mappings come from one of three sources:
//   1. `mapDefaults_de_DE()` — hard-coded German keyboard, used if no
//      `.map` file is configured or it can't be opened
//   2. `loadConfigFile(filename)` — parses a `keyboard-maps/*.map` file
//      using the format documented in `kbd_linux_de_DE.map`
//   3. direct calls to `map` / `mapCtrl` / `unmap` (fluent API)
//
// The "Xerox Control" key is the host key chosen as the modifier for
// generating Xerox special keys (Copy/Move/Props/Find/Help/Undo/etc.).
// Defaults to AWT VK_CONTROL (17) in the configuration; tests can pass
// any code.
public sealed class KeyboardMapper
{
    // AWT key code of the Xerox-control key
    private readonly int CTL_KEY;

    // mapping from AWT-VK_* code → mesa eLevelVKey, without and with Ctrl-Key
    private readonly Dictionary<int, eLevelVKey> normalKeyMapping = new();
    private readonly Dictionary<int, eLevelVKey?> ctlKeyMapping = new();

    // is the Ctrl key currently pressed?
    private bool isCtlPressed = false;

    // keys pressed without or before the Ctrl key was pressed (resp. after
    // Ctrl was released). The value may be null to mark "still pressed on
    // the host side but Ctrl-up already released the mesa side".
    private readonly Dictionary<int, eLevelVKey?> normalPressed = new();

    // keys pressed while Ctrl was held
    private readonly Dictionary<int, eLevelVKey?> ctlPressed = new();

    // the consumer of keyboard events (the mesa side)
    private readonly iUiDataConsumer uiDataConsumer;

    private readonly bool logKeypressed;

    // Constructor.
    //
    // consumer            : the engine-side keyboard event sink
    // xeroxControlKeyCode : the AWT VK_* code for the host key chosen as
    //                       the Xerox Control modifier (typically
    //                       eKeyEventCode.VK_CONTROL.Code == 17)
    // logKeypressed       : write each press event to stdout (debug aid)
    public KeyboardMapper(iUiDataConsumer consumer, int xeroxControlKeyCode, bool logKeypressed)
    {
        this.uiDataConsumer = consumer;
        this.logKeypressed = logKeypressed;
        this.CTL_KEY = xeroxControlKeyCode;
    }

    private bool isPressed(int javaKey)
    {
        return this.normalPressed.ContainsKey(javaKey) || this.ctlPressed.ContainsKey(javaKey);
    }

    // Notify another key pressed on the host side.
    public void pressed(int key)
    {
        // Java upstream has a dev-only F1-toggle for BITBLT logging here,
        // guarded by `Config.LOG_BITBLT_INSNS` which is `false`. Dead
        // code in C# too — preserved for diff fidelity.
        if (Config.LOG_BITBLT_INSNS && key == 0x000070)
        {
            Config.dynLogBitblts = true;
            return;
        }

        // Ctrl going down latches the modifier flag; do nothing else.
        if (key == CTL_KEY)
        {
            this.isCtlPressed = true;
            return;
        }

        if (this.logKeypressed)
        {
            this.logKey(key);
        }

        if (this.isPressed(key)) { return; } // ignore auto-repeats

        // map the host key to mesa
        eLevelVKey? mesaKey;
        Dictionary<int, eLevelVKey?> keyStates;
        if (this.isCtlPressed)
        {
            this.ctlKeyMapping.TryGetValue(key, out mesaKey);
            keyStates = this.ctlPressed;
        }
        else
        {
            this.normalKeyMapping.TryGetValue(key, out eLevelVKey? n);
            mesaKey = n;
            keyStates = this.normalPressed;
        }
        if (mesaKey != null)
        {
            keyStates[key] = mesaKey;
            this.uiDataConsumer.acceptKeyboardKey(mesaKey, true);
        }
    }

    // Notify another key released on the host side.
    public void released(int key)
    {
        if (key == 0x000070)
        {
            // F1 dev toggle (see `pressed`).
            Config.dynLogBitblts = false;
            return;
        }

        // Ctrl going up: release all Xerox special keys it produced, but
        // continue tracking them under `normalPressed` (with null mesa-
        // value) so the eventual host-side key-up doesn't try to release
        // a mesa key that's already gone.
        if (key == CTL_KEY)
        {
            foreach (var (k, v) in this.ctlPressed)
            {
                if (v != null) { this.uiDataConsumer.acceptKeyboardKey(v, false); }
                this.normalPressed[k] = null;
            }
            this.isCtlPressed = false;
            this.ctlPressed.Clear();
            return;
        }

        // Map the host key to mesa and release (from whichever map holds
        // it).
        release(key, this.normalPressed);
        release(key, this.ctlPressed);
    }

    private void release(int javaKey, Dictionary<int, eLevelVKey?> keyStates)
    {
        if (!keyStates.TryGetValue(javaKey, out eLevelVKey? mesaKey)) { return; }
        if (mesaKey != null) { this.uiDataConsumer.acceptKeyboardKey(mesaKey, false); }
        keyStates.Remove(javaKey);
    }

    /*
     * definition of the mapping
     */

    // Add a mapping for both the plain key and the Ctrl-modified key.
    // Pass null for either to leave that variant unmapped.
    public KeyboardMapper map(int key, eLevelVKey? normalKey, eLevelVKey? ctlKey)
    {
        if (normalKey != null) { this.normalKeyMapping[key] = normalKey; }
        if (ctlKey != null) { this.ctlKeyMapping[key] = ctlKey; }
        return this;
    }

    // Add a plain-key-only mapping.
    public KeyboardMapper map(int key, eLevelVKey normalKey) => this.map(key, normalKey, null);

    // Add a Ctrl-modified mapping only.
    public KeyboardMapper mapCtrl(int key, eLevelVKey ctlKey) => this.map(key, null, ctlKey);

    // Remove all mappings for the given host key code.
    public KeyboardMapper unmap(int key)
    {
        this.normalKeyMapping.Remove(key);
        this.ctlKeyMapping.Remove(key);
        return this;
    }

    // Default mapping for a German keyboard. Used when no .map file is
    // configured or the configured file can't be read.
    public KeyboardMapper mapDefaults_de_DE()
    {
        return this
            // first row: digits etc.
            .map(0x000031, eLevelVKey.One)
            // ^ = 'becomes' and ° = 'dereference' — uncovered
            .map(0x00000082, eLevelVKey.Bullet)
            .map(0x000032, eLevelVKey.Two)
            .map(0x000033, eLevelVKey.Three)
            .map(0x000034, eLevelVKey.Four)
            .map(0x000035, eLevelVKey.Five)
            .map(0x000036, eLevelVKey.Six)
            .map(0x000037, eLevelVKey.Seven)
            .map(0x000038, eLevelVKey.Eight)
            .map(0x000039, eLevelVKey.Nine)
            .map(0x000030, eLevelVKey.Zero)
            .map(0x010000DF, eLevelVKey.Dash)
            .map(0x000080, eLevelVKey.Equal) // `
            .map(0x000081, eLevelVKey.Equal) // '
            .map(0x0000007F, eLevelVKey.Delete)

            // second row: qwertz etc.
            .map(0x000051, eLevelVKey.Q)
            .map(0x000057, eLevelVKey.W)
            .map(0x000045, eLevelVKey.E)
            .map(0x000052, eLevelVKey.R)
            .map(0x000054, eLevelVKey.T)
            .map(0x00005A, eLevelVKey.Z)
            .map(0x000055, eLevelVKey.U)
            .map(0x000049, eLevelVKey.I)
            .map(0x00004F, eLevelVKey.O)
            .map(0x000050, eLevelVKey.P)
            .map(0x010000FC, eLevelVKey.LeftBracket)
            .map(0x00000209, eLevelVKey.RightBracket)

            // third row: asdf etc.
            .map(0x000041, eLevelVKey.A)
            .map(0x000053, eLevelVKey.S)
            .map(0x000044, eLevelVKey.D)
            .map(0x000046, eLevelVKey.F)
            .map(0x000047, eLevelVKey.G)
            .map(0x000048, eLevelVKey.H)
            .map(0x00004A, eLevelVKey.J)
            .map(0x00004B, eLevelVKey.K)
            .map(0x00004C, eLevelVKey.L)
            .map(0x010000D6, eLevelVKey.SemiColon)
            .map(0x010000C4, eLevelVKey.Quote)
            .map(0x00000208, eLevelVKey.DoubleQuote)

            // fourth row: <yxcv etc.
            .map(0x000059, eLevelVKey.Y)
            .map(0x000058, eLevelVKey.X)
            .map(0x000043, eLevelVKey.C)
            .map(0x000056, eLevelVKey.V)
            .map(0x000042, eLevelVKey.B)
            .map(0x00004E, eLevelVKey.N)
            .map(0x00004D, eLevelVKey.M)
            .map(0x0000002C, eLevelVKey.Comma)
            .map(0x0000002E, eLevelVKey.Period)
            .map(0x0000002D, eLevelVKey.Slash)

            // others
            .map(0x000020, eLevelVKey.Space)
            .map(0x000010, eLevelVKey.LeftShift)
            .map(0x000009, eLevelVKey.ParaTab)
            .map(0x00000A, eLevelVKey.NewPara)
            .map(0x000008, eLevelVKey.BS)
            .map(0x000014, eLevelVKey.Lock)

            // xerox special keys via Ctrl
            .mapCtrl(0x00001B, eLevelVKey.Stop) // ESC
            .mapCtrl(0x00004D, eLevelVKey.Move) // Ctrl-M
            .mapCtrl(0x000043, eLevelVKey.Copy) // Ctrl-C
            .mapCtrl(0x000053, eLevelVKey.Same) // Ctrl-S
            .mapCtrl(0x00004F, eLevelVKey.Open) // Ctrl-O
            .mapCtrl(0x000050, eLevelVKey.Props) // Ctrl-P
            .mapCtrl(0x000046, eLevelVKey.Find) // Ctrl-F
            .mapCtrl(0x000048, eLevelVKey.Help) // Ctrl-H
            .mapCtrl(0x000055, eLevelVKey.Undo) // Ctrl-U
            .mapCtrl(0x000041, eLevelVKey.Again) // Ctrl-A
            .mapCtrl(0x00004E, eLevelVKey.Next); // Ctrl-N
    }

    private void logKey(int javaCode)
    {
        string? name = eKeyEventCode.getName(javaCode);
        Console.WriteLine($"key: x{javaCode:x8}{(name != null ? " = " : "")}{name ?? ""}");
    }

    private const string CTRL_MARK = "Ctrl!";

    // Load the keyboard mapping from the specified file. Lines have one of:
    //   #...               comment
    //   key : MesaKeyName  plain mapping
    //   Ctrl!key : MesaKeyName   Ctrl-modified mapping
    // where `key` is either an AWT-style `VK_NAME` or a hex code `xHHHHHHHH`.
    // Falls back to mapDefaults_de_DE() if the file cannot be opened or
    // reading fails partway through.
    public void loadConfigFile(string filename)
    {
        StreamReader reader;
        try
        {
            reader = new StreamReader(filename);
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine($"Unable to access keyboard map file '{filename}'");
            this.mapDefaults_de_DE();
            return;
        }
        catch (DirectoryNotFoundException)
        {
            Console.WriteLine($"Unable to access keyboard map file '{filename}'");
            this.mapDefaults_de_DE();
            return;
        }

        try
        {
            using (reader)
            {
                int lineNo = 0;
                string? l;
                while ((l = reader.ReadLine()) != null)
                {
                    lineNo++;
                    string line = l.Trim();
                    if (line.Length == 0) { continue; }
                    if (line.StartsWith('#')) { continue; }

                    string[] mapping = line.Split(':');
                    if (mapping.Length != 2)
                    {
                        Console.WriteLine($"Invalid keyboard mapping line[{lineNo}]: {l}");
                        continue;
                    }

                    string xeroxSel = mapping[1].Trim();
                    eLevelVKey? xeroxKey = getLevelVKey(xeroxSel);
                    if (xeroxKey == null)
                    {
                        Console.WriteLine($"Invalid Xerox-key in keyboard mapping line[{lineNo}]: {l}");
                        continue;
                    }

                    string javaSel = mapping[0].Trim();
                    bool hasCtrlMod = false;
                    int javaCode;

                    if (javaSel.StartsWith(CTRL_MARK, StringComparison.Ordinal))
                    {
                        hasCtrlMod = true;
                        javaSel = javaSel.Substring(CTRL_MARK.Length).Trim();
                    }

                    if (javaSel.StartsWith('x'))
                    {
                        if (!int.TryParse(javaSel.AsSpan(1), System.Globalization.NumberStyles.HexNumber,
                            System.Globalization.CultureInfo.InvariantCulture, out javaCode))
                        {
                            Console.WriteLine($"Invalid hex-code for java-key in keyboard mapping line[{lineNo}]: {l}");
                            continue;
                        }
                    }
                    else
                    {
                        eKeyEventCode? jk = eKeyEventCode.valueOf(javaSel);
                        if (jk == null)
                        {
                            Console.WriteLine($"Invalid key-name for java-key in keyboard mapping line[{lineNo}]: {l}");
                            continue;
                        }
                        javaCode = jk.Code;
                    }

                    if (hasCtrlMod)
                    {
                        this.mapCtrl(javaCode, xeroxKey);
                    }
                    else
                    {
                        this.map(javaCode, xeroxKey);
                    }
                }
            }
        }
        catch (IOException e)
        {
            Console.WriteLine($"Error reading keyboard map file '{filename}': {e.Message}");
            this.mapDefaults_de_DE();
        }
    }

    private static eLevelVKey? getLevelVKey(string name)
    {
        // eLevelVKey is a sealed class with named static fields; do a
        // reflective lookup that mirrors Java's `enum.valueOf(name)`.
        var fi = typeof(eLevelVKey).GetField(name,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        return fi?.GetValue(null) as eLevelVKey;
    }
}
