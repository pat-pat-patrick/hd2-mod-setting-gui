// What HD2 Mod Quick Deploy tells the window when it hosts it as a plugin.
using System;
using System.Collections.Generic;
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

        [Fact]
        public void A_manager_with_nothing_to_say_leaves_every_list_unknown()
        {
            Host.Library = () => null;
            Assert.Empty(Host.Read());
        }
    }
}
