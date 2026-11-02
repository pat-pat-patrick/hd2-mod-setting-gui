using System;
using System.Collections.Generic;
using System.Linq;

namespace Hd2ModSettingGui.Core
{
    /// <summary>What HD2 Mod Quick Deploy says when it hosts the window as a plugin (the user,
    /// 2026-11-02: the window reads every mod's settings, but not which mods run). On its own
    /// the window knows none of it, and every list is one of "the mods".</summary>
    public static class Host
    {
        /// <summary>Every mod in the manager's library whose Lua writes lists for this window:
        /// {name, enabled, settings: [list names]}; null when no manager hosts the window.</summary>
        public static Func<List<Dictionary<string, object>>> Library;

        public static bool Hosted => Library != null;

        /// <summary>Where a list stands in the manager's library: "on" (a mod that is enabled
        /// writes it), "off" (only mods that are not), "unknown" (no mod there writes it), with
        /// the mods' names. Without a manager, every list is "on".</summary>
        public sealed class Place
        {
            public string State = "on";
            public List<string> Mods = new List<string>();
        }

        public static Dictionary<string, Place> Read()
        {
            var by = new Dictionary<string, Place>(StringComparer.OrdinalIgnoreCase);
            if (Library == null) return by;
            foreach (var m in Library() ?? new List<Dictionary<string, object>>())
            {
                var name = m.TryGetValue("name", out var n) ? n as string : null;
                bool on = m.TryGetValue("enabled", out var e) && e is bool b && b;
                var lists = m.TryGetValue("settings", out var s) && s is System.Collections.IEnumerable l ? l.OfType<string>() : Enumerable.Empty<string>();
                foreach (var list in lists)
                {
                    if (!by.TryGetValue(list, out var p)) by[list] = p = new Place { State = "off" };
                    if (on) p.State = "on";
                    if (name != null && !p.Mods.Contains(name)) p.Mods.Add(name);
                }
            }
            return by;
        }
    }
}
