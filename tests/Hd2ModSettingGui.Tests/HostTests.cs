// What HD2 Mod Quick Deploy tells the window when it hosts it as a plugin.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hd2ModSettingGui.Core;
using Xunit;

namespace Hd2ModSettingGui.Tests
{
    public class HostTests : IDisposable
    {
        public void Dispose() => Host.Library = null;

        static Dictionary<string, object> Mod(string name, bool on, params string[] lists) =>
            new Dictionary<string, object> { ["name"] = name, ["enabled"] = on, ["settings"] = lists.Cast<object>().ToList() };

        [Fact]
        public void On_its_own_the_window_knows_no_library()
        {
            Assert.False(Host.Hosted);
            Assert.Empty(Host.Read());
        }

        /// <summary>A list one enabled mod writes is on, whatever else writes it; one only mods
        /// that are off write is off; the mods are named either way.</summary>
        [Fact]
        public void A_list_is_on_when_an_enabled_mod_writes_it()
        {
            Host.Library = () => new List<Dictionary<string, object>>
            {
                Mod("Super Holo HUD v1.191.0", true, "super_holo_hud", "super_holo_kit"),
                Mod("Ammo Lamp v0.16.1", false, "ammo_lamp", "mom_ammo_lamp"),
                Mod("ActionCam", false, "actioncam"),
                Mod("ActionCam 3.1", true, "ACTIONCAM"),                          // the names as the folder writes them, any case
            };
            Assert.True(Host.Hosted);
            var p = Host.Read();
            Assert.Equal("on", p["super_holo_kit"].State);
            Assert.Equal("off", p["ammo_lamp"].State);
            Assert.Equal(new[] { "Ammo Lamp v0.16.1" }, p["ammo_lamp"].Mods);
            Assert.Equal("on", p["actioncam"].State);
            Assert.Equal(new[] { "ActionCam", "ActionCam 3.1" }, p["actioncam"].Mods);
            Assert.False(p.ContainsKey("wheres_my_shit"));                        // no mod there writes it: unknown
        }

        static string List(string dir, string name, string version, params string[] keys)
        {
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, name + Protocol.OptionsSuffix);
            var opts = string.Join(",", keys.Select(k => "{\"key\":\"" + k + "\",\"type\":\"toggle\",\"default\":true,\"en\":[\"" + k + "\",\"\"]}"));
            File.WriteAllText(path, "{\"title\":\"" + name + "\",\"version\":\"" + version + "\",\"options\":[" + opts + "]"
                              + (version.EndsWith("game") ? ",\"menu_owned\":[\"a\"]" : "") + "}\n");
            return path;
        }

        /// <summary>(the user, 2026-11-04) A mod's settings shown before the game has run it: the
        /// list a library mod ships at the top of its folder. An enabled mod's copy is read in
        /// place of the game's (it is what the game runs next); a disabled mod's only when the
        /// game has none. The values file stays in the game's folder, and the keys the game's
        /// menus hold come from the game's copy.</summary>
        [Fact]
        public void A_list_a_library_mod_ships_is_shown_without_the_game()
        {
            var root = Path.Combine(Path.GetTempPath(), "hd2msg_host_" + Guid.NewGuid().ToString("N"));
            try
            {
                var game = Path.Combine(root, "game");
                var dial = Path.Combine(root, "lib", "Speed Dial v0.26.0");
                var lamp = Path.Combine(root, "lib", "Ammo Lamp");
                var cam = Path.Combine(root, "lib", "ActionCam");
                List(game, "speed_dial", "0.25.1 game", "a");
                List(game, "action_cam", "1.0 game", "a");
                var dialShips = List(dial, "speed_dial", "0.26.0", "a", "arrow_ms");
                var lampShips = List(lamp, "ammo_lamp", "0.17.0", "glow");          // the game never ran it
                List(cam, "action_cam", "1.1", "a", "b");                             // off, and the game has its own
                Host.Library = () => new List<Dictionary<string, object>>
                {
                    new Dictionary<string, object> { ["name"] = "Speed Dial", ["path"] = dial, ["enabled"] = true, ["settings"] = new List<object> { "speed_dial" } },
                    new Dictionary<string, object> { ["name"] = "Ammo Lamp", ["path"] = lamp, ["enabled"] = false, ["settings"] = new List<object>() },   // a name the Lua scan missed
                    new Dictionary<string, object> { ["name"] = "ActionCam", ["path"] = cam, ["enabled"] = false, ["settings"] = new List<object> { "action_cam" } },
                };
                var places = Host.Read();
                Assert.Equal(dialShips, places["speed_dial"].Package);
                Assert.True(places["speed_dial"].PackageOn);
                Assert.Equal("off", places["ammo_lamp"].State);

                var lists = Protocol.Lists(game, places);
                Assert.Equal(new[] { "action_cam", "speed_dial", "ammo_lamp" }, lists.Select(l => l.Name));
                Assert.Equal(dialShips, lists[1].Path);
                Assert.Equal(Path.Combine(game, "action_cam" + Protocol.OptionsSuffix), lists[0].Path);
                Assert.Equal(lampShips, lists[2].Path);

                var d = Protocol.LoadFor(game, "speed_dial", dialShips);
                Assert.Equal("0.26.0", d.Version);
                Assert.NotNull(d.Spec("arrow_ms"));
                Assert.Equal(Path.Combine(game, "speed_dial_settings.txt"), d.SettingsPath);
                Assert.Contains("a", d.MenuOwned);
                Assert.Equal(game, Protocol.LoadFor(game, "ammo_lamp", lampShips).Folder);

                Assert.Equal(Protocol.FindMods(game), Protocol.Lists(game, null));   // on the window's own: the game's only
            }
            finally { try { Directory.Delete(root, true); } catch (Exception) { } }
        }

        [Fact]
        public void A_manager_with_nothing_to_say_leaves_every_list_unknown()
        {
            Host.Library = () => null;
            Assert.Empty(Host.Read());
        }
    }
}
