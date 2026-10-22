// Key settings: the names the window writes, the values it reads, and a file
// the game changed while the window was open.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Hd2ModSettingGui;
using Hd2ModSettingGui.Core;
using Xunit;

namespace Hd2ModSettingGui.Tests
{
    public class KeyTests
    {
        // The game's own keyboard names (Keyboard.button_name 0..511, as Super
        // Holo Kit logged them in game, 2026-10); the mouse's buttons are in
        // it too, with " **" after them.
        static readonly HashSet<string> GameNames = new HashSet<string>(
            ("0|1|2|3|4|5|6|7|8|9|a|accept|app1|app2|attn|auto|ax|b|back tab|backslash|backspace|backtick|break|c|caps lock|" +
             "clear|close bracket|comma|context menu|convert|copy|cr sel|cu sel|d|delete|down|e|end|enlw|enter|equal|er eof|" +
             "escape|ex sel|execute|f|f1|f10|f11|f12|f13|f14|f15|f16|f17|f18|f19|f2|f20|f21|f22|f23|f24|f3|f4|f5|f6|f7|f8|f9|" +
             "final|finish|g|h|help|home|i|ico00 *|icoclr|icohlp|insert|j|jisho|jump|junja|k|kana|kanji|l|left|left alt|" +
             "left button **|left ctrl|left shift|left system|loya|m|mail|mashu|media|middle button **|minus|mode change|n|" +
             "next track|non convert|noname|num lock|numpad *|numpad +|numpad -|numpad .|numpad /|numpad 0|numpad 1|numpad 2|" +
             "numpad 3|numpad 4|numpad 5|numpad 6|numpad 7|numpad 8|numpad 9|numpad enter|o|oem attn|oem_102 (> <)|oem_8 (?!)|" +
             "oemclr|oempa1|oempa2|oempa3|open bracket|p|pa1|packet|page down|page up|pause|period|play|play / pause|" +
             "previous track|print|print screen|process|q|quote|r|reset|right|right alt|right button **|right ctrl|right shift|" +
             "right system|roya|s|scroll lock|select|semicolon|separator|shift|slash|sleep|space|stop|t|tab|touroku|u|up|v|" +
             "volume down|volume mute|volume up|w|wsctrl|x|x button 1 **|x button 2 **|y|z|zoom").Split('|'));

        [Fact]
        public void Every_key_name_the_window_writes_is_one_the_game_knows()
        {
            var written = Enum.GetValues(typeof(Key)).Cast<Key>().Select(KeyNames.FromKey).Where(n => n != null).Distinct().ToList();
            Assert.True(written.Count > 90);                         // letters, digits, F keys, numpad, the rest
            Assert.Empty(written.Where(n => !GameNames.Contains(n)));
            Assert.Equal("q", KeyNames.FromKey(Key.Q));
            Assert.Equal("numpad 7", KeyNames.FromKey(Key.NumPad7));
            Assert.Equal("f12", KeyNames.FromKey(Key.F12));
            Assert.Null(KeyNames.FromKey(Key.Escape));               // Esc leaves the key as it was
        }

        [Fact]
        public void The_middle_and_side_mouse_buttons_but_not_left_or_right()
        {
            Assert.Equal("mouse 3", KeyNames.FromMouse(MouseButton.Middle));
            Assert.Equal("mouse 4", KeyNames.FromMouse(MouseButton.XButton1));
            Assert.Equal("mouse 5", KeyNames.FromMouse(MouseButton.XButton2));
            Assert.Null(KeyNames.FromMouse(MouseButton.Left));
            Assert.Null(KeyNames.FromMouse(MouseButton.Right));
        }

        [Fact]
        public void A_key_value_is_its_name_and_none_is_empty()
        {
            var key = new OptionSpec { Kind = Kind.Key, Default = "F6" };
            Assert.Equal("f6", key.DefaultValue);
            Assert.Equal("page up", Protocol.Parse(key, " Page Up "));
            Assert.Equal("", Protocol.Parse(key, ""));
            Assert.Equal("", new OptionSpec { Kind = Kind.Key }.DefaultValue);
            Assert.Equal(Kind.Key, Protocol.KindOf("key"));
        }

        [Fact]
        public void A_file_the_game_changed_is_taken_and_a_half_written_one_waits()
        {
            using (var f = new Folder(false))
            {
                var list = Path.Combine(f.Dir, "demo_options.json");
                File.WriteAllText(list, "{\"title\":\"Demo\",\"settings\":\"demo_settings.txt\",\"options\":[" +
                    "{\"key\":\"menu\",\"type\":\"key\",\"default\":\"\"},{\"key\":\"size\",\"type\":\"slider\",\"min\":0,\"max\":1,\"step\":0.1,\"default\":0.5}]}");
                var settings = Path.Combine(f.Dir, "demo_settings.txt");
                File.WriteAllText(settings, "menu=\nsize=0.5\n#end\n");
                var s = new ModState(ModList.Load(list));
                Assert.False(s.TakeOutside());                      // nothing changed
                s.Set("size", 0.7);
                s.Save();
                Assert.False(s.TakeOutside());                      // our own write
                File.WriteAllText(settings, "menu=mouse 4\nsize=0.7\n");
                Assert.False(s.TakeOutside());                      // no #end yet
                Assert.Equal("", s.Value("menu"));
                File.WriteAllText(settings, "menu=mouse 4\nsize=0.7\n#end\n");
                Assert.True(s.TakeOutside());
                Assert.Equal("mouse 4", s.Value("menu"));
                Assert.True(s.CanUndo);                             // ours (size) still undoable; the game's not added
                Assert.True(s.Undo());
                Assert.False(s.CanUndo);
            }
        }
    }
}
