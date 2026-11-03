using System;
using System.Collections.Generic;
using System.Windows;
using Hd2ModSettingGui.Core;

namespace Hd2ModSettingGui
{
    /// <summary>The window as a plugin of HD2 Mod Quick Deploy (its contract, Api 2: Kind,
    /// Title(lang), Summary(lang), Open; called by name, nothing shared but strings, numbers,
    /// delegates and Json shapes). The manager hands over the language as a tag ("lang") and,
    /// as "library", every mod in its library whose Lua writes lists for this window, with
    /// whether it is enabled.</summary>
    public static class Plugin
    {
        public const int Api = 2;
        public const string Kind = "window";

        /// <summary>Its button in the manager's title bar: Equalizer (E9E9), sliders, in Segoe
        /// Fluent Icons / Segoe MDL2 Assets.</summary>
        public const string Icon = "\uE9E9";

        /// <summary>In lang, a language tag ("en", "zh-CN"): from the window's own language
        /// files (Texts), else English.</summary>
        public static string Title(string lang) => Texts.In(lang, "title");

        public static string Summary(string lang) => Texts.In(lang, "plugin_summary");

        static MainWindow open;

        /// <summary>The window, or the one already open brought forward.</summary>
        public static void Open(Window owner, Dictionary<string, object> host)
        {
            if (open != null)
            {
                if (open.WindowState == WindowState.Minimized) open.WindowState = WindowState.Normal;
                open.Activate();
                return;
            }
            Host.Library = host.TryGetValue("library", out var l) ? l as Func<List<Dictionary<string, object>>> : null;
            Texts.Lang = Texts.Resolve(host.TryGetValue("lang", out var l2) ? l2 as string : null);
            open = new MainWindow();
            open.Closed += (s, e) => open = null;
            open.Show();
        }
    }
}
