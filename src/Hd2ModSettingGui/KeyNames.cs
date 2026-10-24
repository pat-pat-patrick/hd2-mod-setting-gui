using System.Collections.Generic;
using System.Globalization;
using System.Windows.Input;

namespace Hd2ModSettingGui
{
    /// <summary>Keys as a key setting writes them: the game's own names for
    /// its keyboard ("q", "f6", "numpad 1", "page up", "left ctrl" -- the list
    /// the game's Keyboard.button_name gives), and "mouse 3" / "mouse 4" /
    /// "mouse 5" for the middle and side buttons (the mod's side finds the
    /// game's name for them).</summary>
    public static class KeyNames
    {
        static readonly Dictionary<Key, string> Names = new Dictionary<Key, string>
        {
            [Key.Space] = "space", [Key.Tab] = "tab", [Key.Back] = "backspace", [Key.Enter] = "enter",
            [Key.Delete] = "delete", [Key.Insert] = "insert", [Key.Home] = "home", [Key.End] = "end",
            [Key.PageUp] = "page up", [Key.PageDown] = "page down",
            [Key.Up] = "up", [Key.Down] = "down", [Key.Left] = "left", [Key.Right] = "right",
            [Key.CapsLock] = "caps lock", [Key.NumLock] = "num lock", [Key.Scroll] = "scroll lock",
            [Key.Pause] = "pause", [Key.PrintScreen] = "print screen",
            [Key.LeftShift] = "left shift", [Key.RightShift] = "right shift",
            [Key.LeftCtrl] = "left ctrl", [Key.RightCtrl] = "right ctrl",
            [Key.LeftAlt] = "left alt", [Key.RightAlt] = "right alt",
            [Key.Oem3] = "backtick", [Key.OemMinus] = "minus", [Key.OemPlus] = "equal",
            [Key.Oem4] = "open bracket", [Key.Oem6] = "close bracket", [Key.Oem5] = "backslash",
            [Key.Oem1] = "semicolon", [Key.Oem7] = "quote", [Key.OemComma] = "comma",
            [Key.OemPeriod] = "period", [Key.Oem2] = "slash",
            [Key.Multiply] = "numpad *", [Key.Add] = "numpad +", [Key.Subtract] = "numpad -",
            [Key.Decimal] = "numpad .", [Key.Divide] = "numpad /",
        };

        /// <summary>The name of a key pressed; null for one a setting cannot take.</summary>
        public static string FromKey(Key k)
        {
            if (k >= Key.A && k <= Key.Z) return ((char)('a' + (k - Key.A))).ToString();
            if (k >= Key.D0 && k <= Key.D9) return ((char)('0' + (k - Key.D0))).ToString();
            if (k >= Key.NumPad0 && k <= Key.NumPad9) return "numpad " + (k - Key.NumPad0);
            if (k >= Key.F1 && k <= Key.F24) return "f" + (k - Key.F1 + 1).ToString(CultureInfo.InvariantCulture);
            return Names.TryGetValue(k, out var n) ? n : null;
        }

        /// <summary>The middle and side buttons; the left and right ones are the game's own.</summary>
        public static string FromMouse(MouseButton b)
        {
            switch (b)
            {
                case MouseButton.Middle: return "mouse 3";
                case MouseButton.XButton1: return "mouse 4";
                case MouseButton.XButton2: return "mouse 5";
                default: return null;
            }
        }

        // (the user, 10-24: "equal" and "minus" in a Chinese window) as on the keycap:
        // a sign as the sign, an arrow as an arrow
        static readonly Dictionary<string, string> Signs = new Dictionary<string, string>
        {
            ["backtick"] = "`", ["minus"] = "-", ["equal"] = "=", ["open bracket"] = "[", ["close bracket"] = "]",
            ["backslash"] = "\\", ["semicolon"] = ";", ["quote"] = "'", ["comma"] = ",", ["period"] = ".", ["slash"] = "/",
            ["up"] = "↑", ["down"] = "↓", ["left"] = "←", ["right"] = "→",
        };

        // the keys whose keycap is a word: in Chinese, the word Chinese players say
        static readonly Dictionary<string, string> Chinese = new Dictionary<string, string>
        {
            ["space"] = "空格", ["enter"] = "回车", ["backspace"] = "退格",
            ["left shift"] = "左 Shift", ["right shift"] = "右 Shift", ["left ctrl"] = "左 Ctrl", ["right ctrl"] = "右 Ctrl",
            ["left alt"] = "左 Alt", ["right alt"] = "右 Alt",
        };

        /// <summary>A key's name as shown: as on the keycap, the mouse's in words.</summary>
        public static string Label(string name)
        {
            switch (name)
            {
                case "mouse 3": return Texts.Get("mouse_middle");
                case "mouse 4": return Texts.Get("mouse_side", 1);
                case "mouse 5": return Texts.Get("mouse_side", 2);
            }
            if (Signs.TryGetValue(name, out var sign)) return sign;
            if (Texts.Zh && Chinese.TryGetValue(name, out var zh)) return zh;
            if (name.StartsWith("numpad ")) return "Num " + name.Substring(7).ToUpperInvariant();
            if (name.Length <= 3) return name.ToUpperInvariant();
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name);
        }
    }
}
