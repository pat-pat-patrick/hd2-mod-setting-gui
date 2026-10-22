using System;
using System.Collections.Generic;
using System.Linq;

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

        public ModState(ModList list)
        {
            List = list;
            foreach (var kv in Protocol.ReadValues(list.SettingsPath))
            {
                var spec = list.Spec(kv.Key);
                var v = spec != null ? Protocol.Parse(spec, kv.Value) : null;
                values[kv.Key] = v ?? kv.Value;
            }
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
                default: return v is double d ? d : spec.DefaultValue;
            }
        }

        public bool IsChanged(string key)
        {
            var spec = List.Spec(key);
            return spec != null && !Equals(Value(key), spec.DefaultValue);
        }

        public int ChangedCount => List.Options.Count(o => !List.MenuOwned.Contains(o.Key) && IsChanged(o.Key));

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

        public void Reset(IEnumerable<string> keys)
        {
            var patch = keys.Where(k => !List.MenuOwned.Contains(k)).Select(List.Spec).Where(s => s != null)
                            .ToDictionary(s => s.Key, s => s.DefaultValue);
            if (patch.Count > 0) Put(patch);
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
            var order = List.Options.Select(o => o.Key).ToList();
            var all = new Dictionary<string, object>();
            foreach (var k in order) all[k] = Value(k);
            foreach (var kv in values) if (!all.ContainsKey(kv.Key)) all[kv.Key] = kv.Value;
            Protocol.WriteValues(List.SettingsPath, all, order);
            SavedAt = DateTime.Now;
        }
    }
}
