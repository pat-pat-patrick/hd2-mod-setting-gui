using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Hd2ModSettingGui.Core
{
    public sealed class Profile
    {
        public string Name;
        /// <summary>"&lt;list&gt;|&lt;key&gt;" -> the value (bool, double, "#rrggbb").</summary>
        public Dictionary<string, object> Values = new Dictionary<string, object>();
    }

    /// <summary>A mod's profiles, kept by the window (the user, 10-24), not by
    /// the mod: each a whole set of the mod's values, put into the mod's files
    /// when chosen. In the window's config.json:
    /// "profiles": { "&lt;title&gt;": { "active": i, "list": [{ "name", "values" }] } }.
    /// Kept: every setting with a value (the user, 10-24: "所有的设置", keys and
    /// a mod's own profile choice too), but those another menu holds. A switch
    /// of a mod's own profile in the same write makes Boot Mod Loader drop the
    /// other values: in the game the mod then loads that profile's own.</summary>
    public sealed class ProfileSet
    {
        const string Field = "profiles";
        public readonly List<Profile> Items = new List<Profile>();
        /// <summary>The profile in use, which follows the window's changes; -1: none.</summary>
        public int Active = -1;

        public static string Id(ModList l, string key) => l.Name + "|" + key;

        public static bool Kept(ModList l, OptionSpec o) =>
            o.HasValue && !l.MenuOwned.Contains(o.Key);

        public static Dictionary<string, object> Snapshot(IEnumerable<ModState> states)
        {
            var v = new Dictionary<string, object>();
            foreach (var st in states)
                foreach (var o in st.List.Options.Where(o => Kept(st.List, o)))
                    v[Id(st.List, o.Key)] = st.Value(o.Key);
            return v;
        }

        /// <summary>What puts the lists at the profile's values: per list, only
        /// the values that differ. A value the list no longer has, or of the
        /// wrong kind, is left out.</summary>
        public static List<(ModState St, Dictionary<string, object> Patch)> Changes(Profile p, IEnumerable<ModState> states)
        {
            var all = new List<(ModState, Dictionary<string, object>)>();
            foreach (var st in states)
            {
                var patch = new Dictionary<string, object>();
                foreach (var o in st.List.Options.Where(o => Kept(st.List, o)))
                {
                    if (!p.Values.TryGetValue(Id(st.List, o.Key), out var raw)) continue;
                    var v = Fit(o, raw);
                    if (v != null && !Equals(v, st.Value(o.Key))) patch[o.Key] = v;
                }
                if (patch.Count > 0) all.Add((st, patch));
            }
            return all;
        }

        static object Fit(OptionSpec o, object raw)
        {
            switch (o.Kind)
            {
                case Kind.Toggle: return raw is bool b ? (object)b : null;
                case Kind.Color: return raw is string s && Protocol.IsHex(s) ? s.ToLowerInvariant() : null;
                case Kind.Key: return raw is string k ? Protocol.KeyName(k) : null;
                case Kind.Choice:
                {
                    var d = Protocol.ToDouble(raw);
                    return d.HasValue && d.Value % 1 == 0 && d.Value >= 1 && d.Value <= o.Choices.Count ? (object)d.Value : null;
                }
                case Kind.Slider:
                {
                    var d = Protocol.ToDouble(raw);
                    return d.HasValue ? (object)o.Snap(d.Value) : null;
                }
                default: return null;
            }
        }

        /// <summary>The pattern ("Profile {0}") with the first number no profile's name has.</summary>
        public string NewName(string pattern)
        {
            for (int n = 1; ; n++)
            {
                var name = string.Format(CultureInfo.InvariantCulture, pattern, n);
                if (!Items.Any(p => p.Name == name)) return name;
            }
        }

        /// <summary>A new profile at the end, in use.</summary>
        public void Add(string name, Dictionary<string, object> values)
        {
            Items.Add(new Profile { Name = name, Values = values });
            Active = Items.Count - 1;
        }

        /// <summary>A copy right after it; the one in use stays the same.</summary>
        public void Duplicate(int i, string name)
        {
            Items.Insert(i + 1, new Profile { Name = name, Values = new Dictionary<string, object>(Items[i].Values) });
            if (Active > i) Active++;
        }

        /// <summary>Gone; the mod's values stay as they are.</summary>
        public void Delete(int i)
        {
            Items.RemoveAt(i);
            if (Active == i) Active = -1;
            else if (Active > i) Active--;
        }

        /// <summary>Moved so that it ends at index to.</summary>
        public void Move(int from, int to)
        {
            to = Math.Max(0, Math.Min(Items.Count - 1, to));
            if (from == to) return;
            var p = Items[from];
            var active = Active >= 0 ? Items[Active] : null;
            Items.RemoveAt(from);
            Items.Insert(to, p);
            Active = active == null ? -1 : Items.IndexOf(active);
        }

        public static ProfileSet Load(IDictionary<string, object> cfg, string mod)
        {
            var set = new ProfileSet();
            if (!(cfg.TryGetValue(Field, out var all) && all is IDictionary<string, object> a
                  && a.TryGetValue(mod, out var one) && one is IDictionary<string, object> o)) return set;
            if (o.TryGetValue("list", out var l) && l is System.Collections.IList list)
                foreach (var e in list.OfType<IDictionary<string, object>>())
                {
                    var p = new Profile { Name = e.TryGetValue("name", out var n) && n is string ns && ns.Trim() != "" ? ns.Trim() : "?" };
                    if (e.TryGetValue("values", out var v) && v is IDictionary<string, object> vd)
                        foreach (var kv in vd) p.Values[kv.Key] = kv.Value;
                    set.Items.Add(p);
                }
            var act = o.TryGetValue("active", out var ac) ? Protocol.ToDouble(ac) : null;
            set.Active = act.HasValue && act.Value >= 0 && act.Value < set.Items.Count ? (int)act.Value : -1;
            return set;
        }

        /// <summary>Into the config; a mod with no profiles leaves nothing behind.</summary>
        public void Save(IDictionary<string, object> cfg, string mod)
        {
            var a = cfg.TryGetValue(Field, out var all) && all is IDictionary<string, object> x ? x : new Dictionary<string, object>();
            if (Items.Count == 0) a.Remove(mod);
            else
                a[mod] = new Dictionary<string, object>
                {
                    ["active"] = (double)Active,
                    ["list"] = Items.Select(p => (object)new Dictionary<string, object> { ["name"] = p.Name, ["values"] = p.Values }).ToList(),
                };
            if (a.Count == 0) cfg.Remove(Field);
            else cfg[Field] = a;
        }
    }
}
