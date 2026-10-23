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
        public void A_description_keeps_its_line_breaks()
        {
            Assert.Equal("Colours.\nFull colour: each its own.", Json.Parse("\"Colours.\\nFull colour: each its own.\""));
        }

        [Fact]
        public void A_log_named_by_version_found_and_its_untimed_line_given_the_log_time()
        {
            using (var f = new Folder(false))
            {
                var list = new ModList { Name = "wheres_my_shit", Folder = f.Dir };
                Assert.Equal(Path.Combine(f.Dir, "wheres_my_shit.log"), list.LogPath);      // none yet: the plain name
                var old = Path.Combine(f.Dir, "wheres_my_shit_v0.22.3.log");
                var log = Path.Combine(f.Dir, "wheres_my_shit_v0.23.0.log");
                File.WriteAllText(old, "");
                File.WriteAllText(log, "sett settings window: applied field_mode=1\n");
                File.SetLastWriteTime(old, new DateTime(2026, 10, 1, 9, 0, 0));
                File.SetLastWriteTime(log, new DateTime(2026, 10, 22, 17, 30, 15));
                Assert.Equal(log, list.LogPath);
                Assert.Equal("17:30:15", Protocol.LastApplied(log));
            }
        }

        [Fact]
        public void A_mods_chinese_name_when_it_gives_one()
        {
            using (var f = new Folder(false))
            {
                var both = Path.Combine(f.Dir, "both_options.json");
                var one = Path.Combine(f.Dir, "one_options.json");
                File.WriteAllText(both, "{\"title\":\"Super Beacon\",\"title_zh\":\"超级信标\",\"settings\":\"both_settings.txt\",\"options\":[]}");
                File.WriteAllText(one, "{\"title\":\"Ammo Lamp\",\"settings\":\"one_settings.txt\",\"options\":[]}");
                Assert.Equal("超级信标", ModList.Load(both).TitleIn(true));
                Assert.Equal("Super Beacon", ModList.Load(both).TitleIn(false));
                Assert.Equal("Ammo Lamp", ModList.Load(one).TitleIn(true));      // none given: the English one
            }
        }

        [Fact]
        public void Headings_and_texts_have_no_value()
        {
            using (var f = new Folder(false))
            {
                var path = Path.Combine(f.Dir, "demo_options.json");
                File.WriteAllText(path, "{\"title\":\"Demo\",\"settings\":\"demo_settings.txt\",\"options\":[" +
                    "{\"key\":\"look\",\"type\":\"heading\",\"en\":[\"Look\",\"How it looks.\"],\"zh\":[\"外观\",\"\"]}," +
                    "{\"key\":\"note\",\"type\":\"text\",\"en\":[\"Restart after changing these.\",\"\"],\"zh\":[\"改完需要重启。\",\"\"]}," +
                    "{\"key\":\"size\",\"type\":\"slider\",\"min\":0,\"max\":1,\"step\":0.1,\"default\":0.5}]}");
                File.WriteAllText(Path.Combine(f.Dir, "demo_settings.txt"), "look=3\nnote=x\nsize=0.5\n#end\n");
                var s = new ModState(ModList.Load(path));
                Assert.Equal(Kind.Heading, s.List.Spec("look").Kind);
                Assert.Equal(Kind.Text, s.List.Spec("note").Kind);
                Assert.False(s.List.Spec("look").HasValue);
                Assert.False(s.IsChanged("look"));
                s.Reset(new[] { "look", "note", "size" });
                s.Set("size", 0.7);
                s.Save();
                var back = File.ReadAllText(Path.Combine(f.Dir, "demo_settings.txt"));
                Assert.Contains("size=0.7", back);
                Assert.DoesNotContain("look=0", back);                     // never written as a setting of its own
                Assert.DoesNotContain("note=0", back);
            }
        }

        [Fact]
        public void A_gap_is_read_and_keeps_the_value()
        {
            using (var f = new Folder(false))
            {
                var path = Path.Combine(f.Dir, "demo_options.json");
                File.WriteAllText(path, "{\"title\":\"Demo\",\"settings\":\"demo_settings.txt\",\"options\":[" +
                    "{\"key\":\"size\",\"type\":\"slider\",\"min\":0,\"max\":1,\"step\":0.1,\"default\":0.5}," +
                    "{\"key\":\"mode\",\"type\":\"toggle\",\"default\":false,\"gap\":true}]}");
                var s = new ModState(ModList.Load(path));
                Assert.False(s.List.Spec("size").Gap);
                Assert.True(s.List.Spec("mode").Gap);
                Assert.True(s.List.Spec("mode").HasValue);              // a gap is only space: still a setting
            }
        }

        [Fact]
        public void A_profile_is_a_choice_the_list_names()
        {
            using (var f = new Folder(false))
            {
                string Make(string name, string profile, string type)
                {
                    var p = Path.Combine(f.Dir, name + "_options.json");
                    File.WriteAllText(p, "{\"title\":\"Cam\"" + profile + ",\"options\":[" +
                        "{\"key\":\"cam_profile\",\"type\":\"" + type + "\",\"default\":1,\"choices\":[[\"One\",\"一\"],[\"Two\",\"二\"]]}," +
                        "{\"key\":\"part\",\"type\":\"heading\",\"en\":[\"Part\",\"\"]}," +
                        "{\"key\":\"fov\",\"type\":\"slider\",\"min\":0,\"max\":1,\"default\":0.5}]}");
                    return p;
                }
                Assert.Equal("cam_profile", ModList.Load(Make("a", ",\"profile\":\"cam_profile\"", "choice")).ProfileKey);
                Assert.Null(ModList.Load(Make("b", "", "choice")).ProfileKey);                            // not said: none
                Assert.Null(ModList.Load(Make("c", ",\"profile\":\"fov\"", "choice")).ProfileKey);        // not a choice
                Assert.Null(ModList.Load(Make("d", ",\"profile\":\"nothing\"", "choice")).ProfileKey);    // no such setting
                Assert.Null(ModList.Load(Make("e", ",\"profile\":\"cam_profile\"", "toggle")).ProfileKey);
                // the window's own profiles: only for a mod that asks for them
                Assert.True(ModList.Load(Make("f", ",\"profiles\":true", "choice")).Profiles);
                Assert.False(ModList.Load(Make("g", ",\"profiles\":\"yes\"", "choice")).Profiles);
                Assert.False(ModList.Load(Path.Combine(f.Dir, "a_options.json")).Profiles);
                // a reset says whether it made a step (the window's undo counts on it)
                var s = new ModState(ModList.Load(Path.Combine(f.Dir, "a_options.json")));
                Assert.False(s.Reset(new[] { "part" }));
                Assert.False(s.CanUndo);
                Assert.True(s.Reset(new[] { "fov" }));
                Assert.True(s.CanUndo);
            }
        }

        // a mod of two lists, as ActionCam's: settings with the mod's own profile choice, and keys
        static List<ModState> TwoLists(Folder f)
        {
            File.WriteAllText(Path.Combine(f.Dir, "mom_cam_options.json"), "{\"title\":\"Cam\",\"profile\":\"cam_profile\",\"options\":[" +
                "{\"key\":\"cam_profile\",\"type\":\"choice\",\"default\":1,\"choices\":[[\"One\",\"一\"],[\"Two\",\"二\"]]}," +
                "{\"key\":\"fov\",\"type\":\"slider\",\"min\":40,\"max\":120,\"step\":1,\"default\":75}," +
                "{\"key\":\"sway\",\"type\":\"toggle\",\"default\":true}," +
                "{\"key\":\"tint\",\"type\":\"color\",\"default\":\"#ffffff\"}," +
                "{\"key\":\"part\",\"type\":\"heading\",\"en\":[\"Part\",\"\"]}]," +
                "\"menu_owned\":[\"sway\"]}");
            File.WriteAllText(Path.Combine(f.Dir, "cam_options.json"), "{\"title\":\"Cam\",\"options\":[" +
                "{\"key\":\"k_go\",\"type\":\"key\",\"default\":\"f6\"}," +
                "{\"key\":\"mode\",\"type\":\"choice\",\"default\":1,\"choices\":[[\"A\",\"A\"],[\"B\",\"B\"],[\"C\",\"C\"]]}]}");
            return new[] { "mom_cam", "cam" }.Select(n => new ModState(ModList.Load(Path.Combine(f.Dir, n + "_options.json")))).ToList();
        }

        [Fact]
        public void A_profile_keeps_every_setting_but_those_another_menu_holds()
        {
            using (var f = new Folder(false))
            {
                // (the user, 10-24: the mod's own profile picked in one profile came back in every other)
                var lists = TwoLists(f);
                lists[0].Set("fov", 90.0);
                lists[0].Set("cam_profile", 2.0);
                lists[1].Set("mode", 3.0);
                lists[1].Set("k_go", "f7");
                var snap = ProfileSet.Snapshot(lists);
                Assert.Equal(new[] { "cam|k_go", "cam|mode", "mom_cam|cam_profile", "mom_cam|fov", "mom_cam|tint" },
                             snap.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
                Assert.Equal(90.0, snap["mom_cam|fov"]);
                Assert.Equal(2.0, snap["mom_cam|cam_profile"]);
                Assert.Equal("f7", snap["cam|k_go"]);
                // put back: only what differs, per list
                var p = new Profile { Name = "a", Values = snap };
                lists[0].Set("fov", 60.0);
                lists[0].Set("cam_profile", 1.0);
                lists[1].Set("k_go", "q");
                var changes = ProfileSet.Changes(p, lists);
                Assert.Equal(2, changes.Count);
                Assert.Same(lists[0], changes[0].St);
                Assert.Equal(new Dictionary<string, object> { ["cam_profile"] = 2.0, ["fov"] = 90.0 }, changes[0].Patch);
                Assert.Equal(new Dictionary<string, object> { ["k_go"] = "f7" }, changes[1].Patch);
                // a value that no longer fits its setting is left out, never written; one another menu holds too
                p.Values["mom_cam|fov"] = "wide";
                p.Values["cam|mode"] = 7.0;
                p.Values["mom_cam|tint"] = "#ABCDEF";
                p.Values["mom_cam|gone"] = 1.0;
                p.Values["mom_cam|sway"] = false;
                p.Values["mom_cam|cam_profile"] = 1.0;
                p.Values["cam|k_go"] = "Q";
                changes = ProfileSet.Changes(p, lists);
                Assert.Single(changes);
                Assert.Equal(new Dictionary<string, object> { ["tint"] = "#abcdef" }, changes[0].Patch);
            }
        }

        [Fact]
        public void Profiles_add_duplicate_move_delete_and_keep_the_one_in_use()
        {
            var set = new ProfileSet();
            Assert.Equal("Profile 1", set.NewName("Profile {0}"));
            foreach (var n in new[] { "A", "B", "C" }) set.Add(n, new Dictionary<string, object> { ["l|k"] = (double)n[0] });
            Assert.Equal(2, set.Active);                                    // a new one is in use
            set.Active = 1;                                                 // B
            set.Duplicate(0, "A copy");                                     // A, A copy, B, C
            Assert.Equal(new[] { "A", "A copy", "B", "C" }, set.Items.Select(p => p.Name));
            Assert.Equal("B", set.Items[set.Active].Name);
            set.Items[1].Values["l|k"] = 0.0;                               // a copy has values of its own
            Assert.Equal((double)'A', set.Items[0].Values["l|k"]);
            set.Move(3, 0);                                                 // C, A, A copy, B
            Assert.Equal(new[] { "C", "A", "A copy", "B" }, set.Items.Select(p => p.Name));
            Assert.Equal("B", set.Items[set.Active].Name);
            set.Move(0, 99);                                                // to the end
            Assert.Equal(new[] { "A", "A copy", "B", "C" }, set.Items.Select(p => p.Name));
            set.Delete(0);
            Assert.Equal("B", set.Items[set.Active].Name);
            set.Delete(set.Active);
            Assert.Equal(-1, set.Active);                                   // none in use
            Assert.Equal("Profile 1", set.NewName("Profile {0}"));
            set.Items[0].Name = "Profile 1";
            Assert.Equal("Profile 2", set.NewName("Profile {0}"));
        }

        [Fact]
        public void Profiles_live_in_the_window_config()
        {
            var cfg = new Dictionary<string, object> { ["lang"] = "zh" };
            Assert.Empty(ProfileSet.Load(cfg, "Cam").Items);
            var set = new ProfileSet();
            set.Add("Sniper", new Dictionary<string, object> { ["mom_cam|fov"] = 55.0, ["mom_cam|sway"] = false, ["mom_cam|tint"] = "#ff0000" });
            set.Add("Wide", new Dictionary<string, object> { ["mom_cam|fov"] = 110.0 });
            set.Active = 0;
            set.Save(cfg, "Cam");
            // through the file and back, as the window keeps it
            var back = ProfileSet.Load((Dictionary<string, object>)Json.Parse(Json.Write(cfg)), "Cam");
            Assert.Equal(new[] { "Sniper", "Wide" }, back.Items.Select(p => p.Name));
            Assert.Equal(0, back.Active);
            Assert.Equal(false, back.Items[0].Values["mom_cam|sway"]);
            Assert.Equal("#ff0000", back.Items[0].Values["mom_cam|tint"]);
            Assert.Empty(ProfileSet.Load(cfg, "Other").Items);              // per mod
            // none left: nothing left behind
            back.Delete(1);
            back.Delete(0);
            back.Save(cfg, "Cam");
            Assert.Equal(new[] { "lang" }, cfg.Keys.ToArray());
        }

        [Fact]
        public void Texts_once_and_their_translations_by_language()
        {
            // (the user, 10-24) the mod's own texts written once, each language a table of its
            // own, as CowboyBingus's mods carry theirs; what has no translation shows as written
            using (var f = new Folder(false))
            {
                var path = Path.Combine(f.Dir, "demo_options.json");
                File.WriteAllText(path, "{\"title\":\"Demo\",\"options\":[" +
                    "{\"key\":\"size\",\"type\":\"slider\",\"min\":0,\"max\":1,\"default\":0.5,\"name\":\"Size\",\"hint\":\"How big.\"}," +
                    "{\"key\":\"mode\",\"type\":\"choice\",\"default\":1,\"name\":\"Mode\",\"choices\":[\"Off\",\"Scan\",\"Always\"]}," +
                    "{\"key\":\"tint\",\"type\":\"color\",\"default\":\"#ffffff\",\"name\":\"Tint\",\"choices\":[[\"#ffffff\",\"White\"],[\"#5aaaff\",\"Blue\"]]}]," +
                    "\"pages\":[{\"name\":\"General\",\"keys\":[\"size\",\"mode\"]},{\"name\":\"Look\",\"keys\":[\"tint\"]}]," +
                    "\"translations\":{" +
                    "\"zh-Hans\":{\"title\":\"演示\",\"size\":\"大小\",\"size.hint\":\"多大。\",\"mode.1\":\"关\",\"mode.3\":\"常显\",\"tint.2\":\"蓝\",\"page.2\":\"外观\",\"gone\":\"无\",\"mode.9\":\"无\"}," +
                    "\"ru\":{\"size\":\"Размер\"}}}");
                var m = ModList.Load(path);
                Assert.Equal("Demo", m.Title);                                          // the same title is the same mod, in any language
                Assert.Equal("演示", m.TitleIn(true));
                Assert.Equal("Demo", m.TitleIn(false));
                var size = m.Spec("size");
                Assert.Equal(("大小", "Size"), (size.Name.In(true), size.Name.In(false)));
                Assert.Equal(("多大。", "How big."), (size.Hint.In(true), size.Hint.In(false)));
                Assert.Contains("Размер", size.Name.All);                                // the search finds it in any language
                var mode = m.Spec("mode");
                Assert.Equal("Mode", mode.Name.In(true));                               // no translation: as written
                Assert.Equal(new[] { "关", "Scan", "常显" }, mode.Choices.Select(c => c.Name.In(true)));
                Assert.Equal(new[] { "1", "2", "3" }, mode.Choices.Select(c => c.Value));
                Assert.Equal(new[] { "White", "蓝" }, m.Spec("tint").Choices.Select(c => c.Name.In(true)));
                Assert.Equal(new[] { "General", "外观" }, m.Pages.Select(p => p.Name.In(true)));
                // an old list's [en, zh] reads as before, and its "zh" is the Chinese translation
                var old = Path.Combine(f.Dir, "old_options.json");
                File.WriteAllText(old, "{\"title\":\"Old\",\"title_zh\":\"旧\",\"options\":[" +
                    "{\"key\":\"mode\",\"type\":\"choice\",\"default\":1,\"en\":[\"Mode\",\"\"],\"zh\":[\"模式\",\"\"],\"choices\":[[\"Off\",\"关\"],[\"On\",\"开\"]]}]," +
                    "\"pages\":[{\"en\":\"General\",\"zh\":\"通用\",\"keys\":[\"mode\"]}]}");
                var o = ModList.Load(old);
                Assert.Equal(("旧", "模式", "开", "通用"), (o.TitleIn(true), o.Spec("mode").Name.In(true), o.Spec("mode").Choices[1].Name.In(true), o.Pages[0].Name.In(true)));
                Assert.Equal(("Old", "Mode", "On", "General"), (o.TitleIn(false), o.Spec("mode").Name.In(false), o.Spec("mode").Choices[1].Name.In(false), o.Pages[0].Name.In(false)));
            }
        }

        [Fact]
        public void The_peek_only_where_the_mod_asks_for_it()
        {
            using (var f = new Folder(false))
            {
                var path = Path.Combine(f.Dir, "demo_options.json");
                File.WriteAllText(path, "{\"title\":\"Demo\",\"settings\":\"demo_settings.txt\",\"options\":[" +
                    "{\"key\":\"size\",\"type\":\"slider\",\"min\":0,\"max\":1,\"step\":0.1,\"default\":0.5,\"peek\":true}," +
                    "{\"key\":\"glow\",\"type\":\"toggle\",\"default\":true}," +
                    "{\"key\":\"odd\",\"type\":\"toggle\",\"default\":true,\"peek\":\"yes\"}]}");
                var m = ModList.Load(path);
                Assert.True(m.Spec("size").Peek);
                Assert.False(m.Spec("glow").Peek);
                Assert.False(m.Spec("odd").Peek);        // only a real true
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

        [Fact]
        public void A_typed_number_goes_to_the_nearest_step_in_range()
        {
            var spec = new OptionSpec { Kind = Kind.Slider, Min = 0.2, Max = 1.0, Step = 0.05 };
            Assert.Equal(2, spec.Digits);
            Assert.Equal(0.8, spec.Snap(0.81));
            Assert.Equal(0.85, spec.Snap(0.8 + 0.05));       // a step up from a float sum stays on the step
            Assert.Equal(1.0, spec.Snap(7));
            Assert.Equal(0.2, spec.Snap(-3));
            var whole = new OptionSpec { Kind = Kind.Slider, Min = 1, Max = 6, Step = 1 };
            Assert.Equal(0, whole.Digits);
            Assert.Equal(4.0, whole.Snap(3.6));
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
                Assert.Equal(new[] { Kind.Toggle, Kind.Choice, Kind.Slider, Kind.Key }, m.Options.Select(o => o.Kind).Distinct().OrderBy(k => k).ToArray());
                Assert.Equal("equal", m.Spec("key_glow_up").DefaultValue);   // (1.129.0) its keys, by the game's names
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
                Assert.DoesNotContain(s.List.Options, o => s.IsChanged(o.Key));
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
                    else if (o.Kind == Kind.Key) Assert.Equal(o.DefaultValue, back[o.Key]);
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
        public void Menu_owned_keys_are_not_reset()
        {
            using (var f = new Folder())
            {
                var text = File.ReadAllText(f.List).Replace("\"menu_owned\":{}", "\"menu_owned\":[\"size\"]").Replace("\"menu_owned\":[]", "\"menu_owned\":[\"size\"]");
                File.WriteAllText(f.List, text);
                File.WriteAllText(f.Settings, "size=0.3\n#end\n");
                var s = new ModState(ModList.Load(f.List));
                Assert.Contains("size", s.List.MenuOwned);
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
