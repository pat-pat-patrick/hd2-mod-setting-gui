using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Hd2ModSettingGui.Core
{
    /// <summary>One mod's values: read from its file, changed, undone, saved.</summary>
    public sealed class ModState
    {
        public const int UndoMax = 100;
        public readonly ModList List;
        /// <summary>key -> its value (bool, double, "#rrggbb"); a key the list
        /// does not know keeps its text as it was.</summary>
        readonly Dictionary<string, object> values = new Dictionary<string, object>();
        readonly List<Dictionary<string, object>> history = new List<Dictionary<string, object>>();
        public DateTime? SavedAt { get; private set; }
        public event Action Changed;
        string seen;                 // the file's text as last read or written here

        public ModState(ModList list)
        {
            List = list;
            seen = FileText();
            Take();
        }

        void Take()
        {
            foreach (var kv in Protocol.ReadValues(List.SettingsPath))
            {
                var spec = List.Spec(kv.Key);
                var v = spec != null ? Protocol.Parse(spec, kv.Value) : null;
                values[kv.Key] = v ?? kv.Value;
            }
        }

        string FileText()
        {
            try
            {
                using (var f = new FileStream(List.SettingsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var r = new StreamReader(f, Encoding.UTF8))
                    return r.ReadToEnd();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { return null; }
        }

        /// <summary>The file changed by someone else (the mod in the game: a key
        /// it took over from Mod Bindings Menu, a value its own keys changed):
        /// its values taken, not as a change of ours (no undo). A text without
        /// its #end is being written: the next look.</summary>
        public bool TakeOutside()
        {
            var text = FileText();
            if (text == null || text == seen || !text.TrimEnd().EndsWith(Protocol.EndMark)) return false;
            seen = text;
            Take();
            return true;
        }

        public object Value(string key)
        {
            var spec = List.Spec(key);
            if (spec == null) return values.TryGetValue(key, out var raw) ? raw : null;
            values.TryGetValue(key, out var v);
            switch (spec.Kind)
            {
                case Kind.Toggle: return v is bool b ? b : spec.DefaultValue;
                case Kind.Color: return v is string s && Protocol.IsHex(s) ? s : spec.DefaultValue;
                case Kind.Key: return v is string k ? k : spec.DefaultValue;
                default: return v is double d ? d : spec.DefaultValue;
            }
        }

        public bool IsChanged(string key)
        {
            var spec = List.Spec(key);
            return spec != null && spec.HasValue && !Equals(Value(key), spec.DefaultValue);
        }


        public bool CanUndo => history.Count > 0;

        /// <summary>Values set, the old ones kept for one undo.</summary>
        public void Put(IDictionary<string, object> patch)
        {
            var prev = new Dictionary<string, object>();
            foreach (var k in patch.Keys) prev[k] = values.TryGetValue(k, out var o) ? o : null;
            history.Add(prev);
            if (history.Count > UndoMax) history.RemoveAt(0);
            foreach (var kv in patch) values[kv.Key] = kv.Value;
            Changed?.Invoke();
        }

        public void Set(string key, object v) => Put(new Dictionary<string, object> { [key] = v });

        /// <summary>The keys at their defaults, as one undo step; false: none to reset.</summary>
        public bool Reset(IEnumerable<string> keys)
        {
            var patch = keys.Where(k => !List.MenuOwned.Contains(k)).Select(List.Spec).Where(s => s != null && s.HasValue)
                            .ToDictionary(s => s.Key, s => s.DefaultValue);
            if (patch.Count > 0) Put(patch);
            return patch.Count > 0;
        }

        public bool Undo()
        {
            if (history.Count == 0) return false;
            var prev = history[history.Count - 1];
            history.RemoveAt(history.Count - 1);
            foreach (var kv in prev)
            {
                if (kv.Value == null) values.Remove(kv.Key);
                else values[kv.Key] = kv.Value;
            }
            Changed?.Invoke();
            return true;
        }

        /// <summary>Every setting the list knows, at its value; the keys it does not, as they were.</summary>
        public void Save()
        {
            var order = List.Options.Where(o => o.HasValue).Select(o => o.Key).ToList();
            var all = new Dictionary<string, object>();
            foreach (var k in order) all[k] = Value(k);
            foreach (var kv in values) if (!all.ContainsKey(kv.Key)) all[kv.Key] = kv.Value;
            Protocol.WriteValues(List.SettingsPath, all, order);
            seen = FileText();
            SavedAt = DateTime.Now;
        }
    }
}
