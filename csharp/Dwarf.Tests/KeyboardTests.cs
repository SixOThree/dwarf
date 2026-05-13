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
using Dwarf.Engine;
using Dwarf.UI.Avalonia.Input;

namespace Dwarf.Tests;

// Keyboard infrastructure unit tests. Verifies eKeyEventCode lookup
// tables, AvaloniaKeyMap → AWT VK_* translation, and KeyboardMapper
// press/release semantics including the Ctrl-modifier behavior. A
// mock iUiDataConsumer captures the events emitted to the engine.
public sealed class KeyboardTests
{
    // Mock iUiDataConsumer that records every acceptKeyboardKey call.
    private sealed class MockConsumer : iUiDataConsumer
    {
        public List<(eLevelVKey key, bool pressed)> Events { get; } = new();

        public void acceptKeyboardKey(eLevelVKey key, bool isPressed)
        {
            Events.Add((key, isPressed));
        }

        // Unused by the keyboard tests; required by the interface.
        public void resetKeys() { }
        public void acceptMouseKey(int key, bool isPressed) { }
        public void acceptMousePosition(int x, int y) { }
        public void registerPointerBitmapAcceptor(iUiDataConsumer.PointerBitmapAcceptor acpt) { }
        public Func<int[]> registerUiDataRefresher(iMesaMachineDataAccessor refresher) => () => System.Array.Empty<int>();
    }

    /*
     * eKeyEventCode
     */

    [Fact]
    public void eKeyEventCode_valueOf_resolves_known_names()
    {
        Assert.Equal(65, eKeyEventCode.valueOf("VK_A")?.Code);
        Assert.Equal(48, eKeyEventCode.valueOf("VK_0")?.Code);
        Assert.Equal(17, eKeyEventCode.valueOf("VK_CONTROL")?.Code);
        Assert.Equal(112, eKeyEventCode.valueOf("VK_F1")?.Code);
        Assert.Equal(127, eKeyEventCode.valueOf("VK_DELETE")?.Code);
    }

    [Fact]
    public void eKeyEventCode_valueOf_returns_null_for_unknown_name()
    {
        Assert.Null(eKeyEventCode.valueOf("VK_NONSENSE"));
        Assert.Null(eKeyEventCode.valueOf(""));
        Assert.Null(eKeyEventCode.valueOf("vk_a")); // case-sensitive
    }

    [Fact]
    public void eKeyEventCode_getName_returns_known_names()
    {
        Assert.Equal("VK_A", eKeyEventCode.getName(65));
        Assert.Equal("VK_DELETE", eKeyEventCode.getName(127));
    }

    /*
     * AvaloniaKeyMap
     */

    [Fact]
    public void AvaloniaKeyMap_translates_letters()
    {
        Assert.Equal(65, AvaloniaKeyMap.ToVkCode(Key.A));
        Assert.Equal(90, AvaloniaKeyMap.ToVkCode(Key.Z));
        Assert.Equal(80, AvaloniaKeyMap.ToVkCode(Key.P));
    }

    [Fact]
    public void AvaloniaKeyMap_translates_digits_and_function_keys()
    {
        Assert.Equal(48, AvaloniaKeyMap.ToVkCode(Key.D0));
        Assert.Equal(57, AvaloniaKeyMap.ToVkCode(Key.D9));
        Assert.Equal(112, AvaloniaKeyMap.ToVkCode(Key.F1));
        Assert.Equal(123, AvaloniaKeyMap.ToVkCode(Key.F12));
    }

    [Fact]
    public void AvaloniaKeyMap_translates_modifiers_both_sides_same_code()
    {
        // Avalonia distinguishes left/right; AWT VK_SHIFT/CONTROL/ALT do not.
        Assert.Equal(16, AvaloniaKeyMap.ToVkCode(Key.LeftShift));
        Assert.Equal(16, AvaloniaKeyMap.ToVkCode(Key.RightShift));
        Assert.Equal(17, AvaloniaKeyMap.ToVkCode(Key.LeftCtrl));
        Assert.Equal(17, AvaloniaKeyMap.ToVkCode(Key.RightCtrl));
        Assert.Equal(18, AvaloniaKeyMap.ToVkCode(Key.LeftAlt));
        Assert.Equal(18, AvaloniaKeyMap.ToVkCode(Key.RightAlt));
    }

    [Fact]
    public void AvaloniaKeyMap_returns_null_for_unmapped_keys()
    {
        Assert.Null(AvaloniaKeyMap.ToVkCode(Key.None));
        // Avalonia has a few keys with no AWT equivalent; ImeProcessed is one.
        Assert.Null(AvaloniaKeyMap.ToVkCode(Key.ImeProcessed));
    }

    /*
     * KeyboardMapper — press/release semantics
     */

    [Fact]
    public void KeyboardMapper_basic_press_release()
    {
        var consumer = new MockConsumer();
        var km = new KeyboardMapper(consumer, eKeyEventCode.VK_CONTROL.Code, logKeypressed: false);
        km.mapDefaults_de_DE();

        km.pressed(65);  // VK_A
        km.released(65);

        Assert.Equal(2, consumer.Events.Count);
        Assert.Equal((eLevelVKey.A, true), consumer.Events[0]);
        Assert.Equal((eLevelVKey.A, false), consumer.Events[1]);
    }

    [Fact]
    public void KeyboardMapper_ignores_auto_repeat()
    {
        var consumer = new MockConsumer();
        var km = new KeyboardMapper(consumer, eKeyEventCode.VK_CONTROL.Code, logKeypressed: false);
        km.mapDefaults_de_DE();

        km.pressed(65); // VK_A — first press
        km.pressed(65); // auto-repeat — ignored
        km.pressed(65); // auto-repeat — ignored
        km.released(65);

        // Only one press + one release in the event stream.
        Assert.Equal(2, consumer.Events.Count);
        Assert.Equal((eLevelVKey.A, true), consumer.Events[0]);
        Assert.Equal((eLevelVKey.A, false), consumer.Events[1]);
    }

    [Fact]
    public void KeyboardMapper_unmapped_keys_dont_emit_events()
    {
        var consumer = new MockConsumer();
        var km = new KeyboardMapper(consumer, eKeyEventCode.VK_CONTROL.Code, logKeypressed: false);
        km.mapDefaults_de_DE();

        // 0x99 is not in the German defaults
        km.pressed(0x99);
        km.released(0x99);

        Assert.Empty(consumer.Events);
    }

    [Fact]
    public void KeyboardMapper_ctrl_modifier_routes_to_ctlKey_mapping()
    {
        var consumer = new MockConsumer();
        var km = new KeyboardMapper(consumer, eKeyEventCode.VK_CONTROL.Code, logKeypressed: false);
        km.mapDefaults_de_DE();

        km.pressed(eKeyEventCode.VK_CONTROL.Code); // Ctrl down — silent
        km.pressed(eKeyEventCode.VK_C.Code);       // Ctrl-C → Copy
        km.released(eKeyEventCode.VK_C.Code);
        km.released(eKeyEventCode.VK_CONTROL.Code);

        // Expected event sequence:
        //   Copy press (from Ctrl-C down)
        //   Copy release (from Ctrl-C up)
        //   No event for Ctrl-up itself (Ctrl is silent — it's a modifier)
        Assert.Equal(2, consumer.Events.Count);
        Assert.Equal((eLevelVKey.Copy, true), consumer.Events[0]);
        Assert.Equal((eLevelVKey.Copy, false), consumer.Events[1]);
    }

    [Fact]
    public void KeyboardMapper_ctrl_up_releases_all_held_ctl_keys()
    {
        var consumer = new MockConsumer();
        var km = new KeyboardMapper(consumer, eKeyEventCode.VK_CONTROL.Code, logKeypressed: false);
        km.mapDefaults_de_DE();

        // Hold Ctrl, press multiple keys, release Ctrl while keys still down.
        km.pressed(eKeyEventCode.VK_CONTROL.Code);
        km.pressed(eKeyEventCode.VK_C.Code); // Copy
        km.pressed(eKeyEventCode.VK_M.Code); // Move
        km.released(eKeyEventCode.VK_CONTROL.Code);

        // Ctrl-up should release both Copy and Move (order: dictionary
        // iteration order, both must appear with pressed=false).
        Assert.Equal(4, consumer.Events.Count); // 2 presses + 2 releases
        Assert.Equal((eLevelVKey.Copy, true), consumer.Events[0]);
        Assert.Equal((eLevelVKey.Move, true), consumer.Events[1]);

        // The two release events follow. Order is implementation-defined,
        // but both keys must be released exactly once with isPressed=false.
        var releases = consumer.Events.GetRange(2, 2);
        Assert.Contains((eLevelVKey.Copy, false), releases);
        Assert.Contains((eLevelVKey.Move, false), releases);

        // Later host-side release of C / M should NOT produce another
        // release event for the mesa side (the mesa keys were already
        // released by Ctrl-up, but normalPressed remembers the host keys
        // are still "down" with a null mesa-value).
        km.released(eKeyEventCode.VK_C.Code);
        km.released(eKeyEventCode.VK_M.Code);
        Assert.Equal(4, consumer.Events.Count); // unchanged
    }

    [Fact]
    public void KeyboardMapper_loadConfigFile_reads_basic_mapping()
    {
        // Write a tiny .map file to a temp path.
        string tempPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempPath,
                "# tiny test map\n"
                + "VK_A : A\n"
                + "Ctrl!VK_M : Move\n"
                + "x000020 : Space\n");

            var consumer = new MockConsumer();
            var km = new KeyboardMapper(consumer, eKeyEventCode.VK_CONTROL.Code, logKeypressed: false);
            km.loadConfigFile(tempPath);

            // VK_A → A
            km.pressed(65);
            km.released(65);
            // Ctrl-M → Move
            km.pressed(eKeyEventCode.VK_CONTROL.Code);
            km.pressed(77);
            km.released(77);
            km.released(eKeyEventCode.VK_CONTROL.Code);
            // x000020 → Space
            km.pressed(0x20);
            km.released(0x20);

            Assert.Equal(6, consumer.Events.Count);
            Assert.Equal((eLevelVKey.A, true), consumer.Events[0]);
            Assert.Equal((eLevelVKey.A, false), consumer.Events[1]);
            Assert.Equal((eLevelVKey.Move, true), consumer.Events[2]);
            Assert.Equal((eLevelVKey.Move, false), consumer.Events[3]);
            Assert.Equal((eLevelVKey.Space, true), consumer.Events[4]);
            Assert.Equal((eLevelVKey.Space, false), consumer.Events[5]);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void KeyboardMapper_unmap_removes_existing_mapping()
    {
        var consumer = new MockConsumer();
        var km = new KeyboardMapper(consumer, eKeyEventCode.VK_CONTROL.Code, logKeypressed: false);
        km.mapDefaults_de_DE();

        km.unmap(65); // remove VK_A
        km.pressed(65);
        km.released(65);

        Assert.Empty(consumer.Events);
    }
}
