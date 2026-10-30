using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace Hd2ModSettingGui
{
    /// <summary>The key that brings the window up over the game (config.json
    /// "overlay_key"): Ctrl, Alt and Shift, any of them, and one key by the
    /// name a key setting writes ("ctrl+shift+f10"); "" for none.</summary>
    public sealed class Hotkey
    {
        public const string Default = "page down";   // the user, 10-30
        public bool Ctrl, Alt, Shift;
        public string Key = "";

        static readonly Dictionary<string, Key> ByName = Enum.GetValues(typeof(Key)).Cast<Key>()
            .Select(k => (k, n: KeyNames.FromKey(k))).Where(x => x.n != null)
            .GroupBy(x => x.n).ToDictionary(g => g.Key, g => g.First().k);

        /// <summary>null for "" or a name it does not know.</summary>
        public static Hotkey Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var h = new Hotkey();
            var parts = text.Trim().ToLowerInvariant().Split('+');
            // the key's own name may have a '+' in it ("numpad +")
            int last = parts.Length - 1;
            if (last > 0 && parts[last] == "" && parts[last - 1].EndsWith("numpad ")) { parts[last - 1] += "+"; last--; }
            for (int i = 0; i < last; i++)
                switch (parts[i])
                {
                    case "ctrl": h.Ctrl = true; break;
                    case "alt": h.Alt = true; break;
                    case "shift": h.Shift = true; break;
                    default: return null;
                }
            h.Key = parts[last];
            return ByName.ContainsKey(h.Key) ? h : null;
        }

        /// <summary>From a key pressed with the modifiers held; null for a key it cannot take.</summary>
        public static Hotkey From(Key k, ModifierKeys mods)
        {
            var name = KeyNames.FromKey(k);
            if (name == null || name.EndsWith(" ctrl") || name.EndsWith(" alt") || name.EndsWith(" shift")) return null;
            return new Hotkey
            {
                Key = name, Ctrl = (mods & ModifierKeys.Control) != 0,
                Alt = (mods & ModifierKeys.Alt) != 0, Shift = (mods & ModifierKeys.Shift) != 0,
            };
        }

        public override string ToString() =>
            (Ctrl ? "ctrl+" : "") + (Alt ? "alt+" : "") + (Shift ? "shift+" : "") + Key;

        /// <summary>As shown: "Ctrl + F10".</summary>
        public string Label() =>
            string.Join(" + ", new[] { Ctrl ? "Ctrl" : null, Alt ? "Alt" : null, Shift ? "Shift" : null, KeyNames.Label(Key) }.Where(s => s != null));

        /// <summary>For RegisterHotKey: MOD_ALT 1, MOD_CONTROL 2, MOD_SHIFT 4, and the virtual key.</summary>
        public uint Modifiers => (Alt ? 1u : 0) | (Ctrl ? 2u : 0) | (Shift ? 4u : 0);
        public uint VirtualKey => (uint)KeyInterop.VirtualKeyFromKey(ByName[Key]);
    }
}
