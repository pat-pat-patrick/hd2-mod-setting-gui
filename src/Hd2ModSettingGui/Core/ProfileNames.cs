using System.Collections.Generic;

namespace Hd2ModSettingGui.Core
{
    /// <summary>The names the player gave a mod's profiles (the user, 10-24),
    /// kept by the window in its own config.json, never in the mod's files:
    /// "profile_names": { "&lt;list&gt;|&lt;key&gt;": { "&lt;choice&gt;": "&lt;name&gt;" } }.</summary>
    public static class ProfileNames
    {
        const string Field = "profile_names";

        /// <summary>The player's name for that choice; null: none, the mod's own.</summary>
        public static string Get(IDictionary<string, object> cfg, string list, string key, string choice) =>
            cfg.TryGetValue(Field, out var all) && all is IDictionary<string, object> a
            && a.TryGetValue(list + "|" + key, out var one) && one is IDictionary<string, object> o
            && o.TryGetValue(choice, out var n) && n is string s && s.Trim() != "" ? s.Trim() : null;

        /// <summary>A name empty (or blank): the mod's own name again.</summary>
        public static void Set(IDictionary<string, object> cfg, string list, string key, string choice, string name)
        {
            var a = cfg.TryGetValue(Field, out var all) && all is IDictionary<string, object> x ? x : new Dictionary<string, object>();
            var id = list + "|" + key;
            var o = a.TryGetValue(id, out var one) && one is IDictionary<string, object> y ? y : new Dictionary<string, object>();
            name = (name ?? "").Trim();
            if (name == "") o.Remove(choice);
            else o[choice] = name;
            if (o.Count == 0) a.Remove(id);
            else a[id] = o;
            if (a.Count == 0) cfg.Remove(Field);
            else cfg[Field] = a;
        }
    }
}
