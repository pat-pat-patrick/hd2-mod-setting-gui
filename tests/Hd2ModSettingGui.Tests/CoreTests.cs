// The window's core on the settings list Super Holo HUD really writes
// (fixtures/super_holo_hud_options.json, made by the kit's own code).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Hd2ModSettingGui.Core;
using Xunit;

namespace Hd2ModSettingGui.Tests
{
    public sealed class Folder : IDisposable
    {
        public readonly string Dir = Path.Combine(Path.GetTempPath(), "hd2msg_" + Guid.NewGuid().ToString("N"));
        public static string Fixture => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures", "super_holo_hud_options.json");
        public Folder(bool withList = true)
        {
            Directory.CreateDirectory(Dir);
            if (withList) File.Copy(Fixture, Path.Combine(Dir, "super_holo_hud_options.json"));
        }
        public string Settings => Path.Combine(Dir, "super_holo_hud_settings.txt");
        public string List => Path.Combine(Dir, "super_holo_hud_options.json");
        public void Dispose() { try { Directory.Delete(Dir, true); } catch (IOException) { } }

        /// <summary>The file as the kit reads it: string.gmatch "([%w_]+)=([^\r\n]*)".</summary>
        public Dictionary<string, string> LuaRead() =>
            Regex.Matches(File.ReadAllText(Settings), @"([0-9A-Za-z_]+)=([^\r\n]*)").Cast<Match>()
                 .GroupBy(m => m.Groups[1].Value).ToDictionary(g => g.Key, g => g.Last().Groups[2].Value);
    }

    public class ProtocolTests
    {
        [Fact]
        public void Numbers_as_lua_writes_them()
        {
            Assert.Equal(new[] { "1", "0.8", "0.05", "4", "0.3", "-20" },
                new[] { 1.0, 0.8, 0.05, 4, 0.30000000000000004, -20.0 }.Select(Protocol.Number).ToArray());
        }

        [Fact]
        public void Written_whole_with_its_end_mark()
        {
            using (var f = new Folder(false))
            {
                Protocol.WriteValues(f.Settings, new Dictionary<string, object> { ["size"] = 0.5, ["flicker"] = false, ["glow3"] = 4.0 },
                                     new[] { "glow3", "size" });
                Assert.Equal("glow3=4\nsize=0.5\nflicker=false\n#end\n", File.ReadAllText(f.Settings));
            }
        }

        [Fact]
        public void Written_in_place_while_the_game_holds_it_open()
        {
            using (var f = new Folder(false))
            {
                Protocol.WriteValues(f.Settings, new Dictionary<string, object> { ["size"] = 0.5 }, new string[0]);
                // as the game's Lua holds it: fopen "rb" (shares reading and writing)
                using (var held = new FileStream(f.Settings, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    Protocol.WriteValues(f.Settings, new Dictionary<string, object> { ["size"] = 0.6 }, new string[0]);
                    held.Seek(0, SeekOrigin.Begin);
                    var buf = new byte[64];
                    int n = held.Read(buf, 0, buf.Length);
                    Assert.Equal("size=0.6\n#end\n", Encoding.UTF8.GetString(buf, 0, n));
                }
            }
        }

        [Fact]
        public void Finds_the_mod_and_nothing_in_a_missing_folder()
        {
            using (var f = new Folder())
            {
                File.WriteAllText(Path.Combine(f.Dir, "other.txt"), "");
                Assert.Equal(new[] { "super_holo_hud" }, Protocol.FindMods(f.Dir).Select(m => m.Name).ToArray());
                Assert.Empty(Protocol.FindMods(Path.Combine(f.Dir, "none")));
            }
        }

        [Fact]
        public void Applied_line_read_from_the_log_end()
        {
            using (var f = new Folder(false))
            {
                var log = Path.Combine(f.Dir, "super_holo_hud.log");
                File.WriteAllText(log, "12:00:01 something\n12:00:05 settings window: applied size=0.5\n12:00:09 other\n");
                Assert.Equal("12:00:05", Protocol.LastApplied(log));
                Assert.Null(Protocol.LastApplied(Path.Combine(f.Dir, "none.log")));
            }
        }

        [Fact]
        public void Colours_only_as_hex()
        {
            var spec = new OptionSpec { Kind = Kind.Color, Default = "#5EE7FF" };
            Assert.Equal("#abcdef", Protocol.Parse(spec, "#ABCDEF"));
            Assert.Null(Protocol.Parse(spec, "blue"));
            Assert.Equal("#5ee7ff", spec.DefaultValue);
        }
    }

    public class JsonTests
    {
        [Fact]
        public void Reads_what_the_kit_writes()
        {
            var v = (Dictionary<string, object>)Json.Parse("{\"a\":[1,2.5,-3e2],\"b\":\"x\\\"y\\u00e9\",\"c\":true,\"d\":null,\"e\":{}}");
            Assert.Equal(new object[] { 1.0, 2.5, -300.0 }, ((List<object>)v["a"]).ToArray());
            Assert.Equal("x\"yé", v["b"]);
            Assert.Equal(true, v["c"]);
            Assert.Null(v["d"]);
            Assert.Empty((Dictionary<string, object>)v["e"]);
        }

        [Fact]
        public void Round_trip()
        {
            var cfg = new Dictionary<string, object> { ["game_dir"] = "C:\\x \"y\"" };
            Assert.Equal(cfg["game_dir"], ((Dictionary<string, object>)Json.Parse(Json.Write(cfg)))["game_dir"]);
        }
    }

    public class ModTests
    {
        [Fact]
        public void The_kits_list()
        {
            using (var f = new Folder())
            {
                var m = ModList.Load(f.List);
                Assert.Equal("Super Holo HUD", m.Title);
                Assert.True(m.Options.Count >= 60);
                Assert.Equal(f.Settings, m.SettingsPath);
                Assert.All(m.Pages.SelectMany(p => p.Keys), k => Assert.NotNull(m.Spec(k)));
                Assert.Equal(new[] { Kind.Toggle, Kind.Choice, Kind.Slider }, m.Options.Select(o => o.Kind).Distinct().OrderBy(k => k).ToArray());
                var size = m.Spec("size");
                Assert.Equal((0.2, 1.0, 0.05), (size.Min, size.Max, size.Step));
                Assert.Equal("宝蓝", m.Spec("palette").Choices[1].Name.Zh);
            }
        }

        [Fact]
        public void Defaults_when_no_file()
        {
            using (var f = new Folder())
            {
                var s = new ModState(ModList.Load(f.List));
                Assert.Equal(s.List.Spec("size").DefaultValue, s.Value("size"));
                Assert.IsType<bool>(s.Value("flicker"));
                Assert.Equal(0, s.ChangedCount);
            }
        }

        [Fact]
        public void Reads_what_is_there_and_keeps_unknown_keys()
        {
            using (var f = new Folder())
            {
                File.WriteAllText(f.Settings, "size=0.45\nflicker=false\nsomething_else=7\n#end\n");
                var s = new ModState(ModList.Load(f.List));
                Assert.Equal(0.45, (double)s.Value("size"), 6);
                Assert.Equal(false, s.Value("flicker"));
                s.Save();
                var back = f.LuaRead();
                Assert.Equal("0.45", back["size"]);
                Assert.Equal("false", back["flicker"]);
                Assert.Equal("7", back["something_else"]);
                Assert.EndsWith("#end\n", File.ReadAllText(f.Settings));
            }
        }

        [Fact]
        public void Saved_file_reads_back_in_lua()
        {
            using (var f = new Folder())
            {
                var s = new ModState(ModList.Load(f.List));
                s.Set("size", 0.55);
                s.Set("palette", 2.0);
                s.Save();
                var back = f.LuaRead();
                Assert.Equal(s.List.Options.Select(o => o.Key).OrderBy(k => k), back.Keys.OrderBy(k => k));
                foreach (var o in s.List.Options)
                {
                    if (o.Kind == Kind.Toggle) Assert.Contains(back[o.Key], new[] { "true", "false" });
                    else double.Parse(back[o.Key], System.Globalization.CultureInfo.InvariantCulture);
                }
                Assert.Equal("0.55", back["size"]);
                Assert.Equal("2", back["palette"]);
            }
        }

        [Fact]
        public void Undo_one_step_at_a_time()
        {
            using (var f = new Folder())
            {
                var s = new ModState(ModList.Load(f.List));
                var before = s.Value("size");
                s.Set("size", 0.35);
                s.Set("flicker", !(bool)s.Value("flicker"));
                Assert.True(s.Undo());
                Assert.Equal(0.35, s.Value("size"));
                Assert.True(s.Undo());
                Assert.Equal(before, s.Value("size"));
                Assert.False(s.Undo());
            }
        }

        [Fact]
        public void Reset_a_group_leaves_the_others()
        {
            using (var f = new Folder())
            {
                var s = new ModState(ModList.Load(f.List));
                var mine = s.List.Pages[0].Keys.First(k => s.List.Spec(k).Kind == Kind.Slider);
                var other = s.List.Pages.Skip(1).SelectMany(p => p.Keys).First(k => s.List.Spec(k).Kind == Kind.Slider);
                s.Set(mine, s.List.Spec(mine).Min);
                s.Set(other, s.List.Spec(other).Min);
                s.Reset(s.List.Pages[0].Keys);
                Assert.False(s.IsChanged(mine));
                Assert.True(s.IsChanged(other));
            }
        }

        [Fact]
        public void Menu_owned_keys_are_not_reset_or_counted()
        {
            using (var f = new Folder())
            {
                var text = File.ReadAllText(f.List).Replace("\"menu_owned\":{}", "\"menu_owned\":[\"size\"]").Replace("\"menu_owned\":[]", "\"menu_owned\":[\"size\"]");
                File.WriteAllText(f.List, text);
                File.WriteAllText(f.Settings, "size=0.3\n#end\n");
                var s = new ModState(ModList.Load(f.List));
                Assert.Contains("size", s.List.MenuOwned);
                Assert.Equal(0, s.ChangedCount);
                s.Reset(new[] { "size" });
                Assert.Equal(0.3, s.Value("size"));
            }
        }

        [Fact]
        public void A_colour_setting()
        {
            using (var f = new Folder())
            {
                var text = File.ReadAllText(f.List);
                int at = text.IndexOf("\"options\":[", StringComparison.Ordinal) + "\"options\":[".Length;
                text = text.Insert(at, "{\"key\":\"holo_rgb\",\"type\":\"color\",\"default\":\"#5EE7FF\",\"choices\":[[\"#5ee7ff\",\"Cyan\",\"青\"],[\"#ffd23f\",\"Yellow\",\"黄\"]],\"en\":[\"Colour\",\"\"],\"zh\":[\"颜色\",\"\"]},");
                File.WriteAllText(f.List, text);
                File.WriteAllText(f.Settings, "holo_rgb=blue\n#end\n");
                var s = new ModState(ModList.Load(f.List));
                Assert.Equal("#5ee7ff", s.Value("holo_rgb"));            // not a colour: its default
                Assert.Equal(("#ffd23f", "黄"), (s.List.Spec("holo_rgb").Choices[1].Value, s.List.Spec("holo_rgb").Choices[1].Name.Zh));
                s.Set("holo_rgb", "#12ab34");
                s.Save();
                Assert.Equal("#12ab34", f.LuaRead()["holo_rgb"]);
                s.Undo();
                Assert.False(s.IsChanged("holo_rgb"));
            }
        }
    }
}
