using System;
using System.Collections.Generic;
using System.Windows;
using Hd2ModSettingGui.Core;

namespace Hd2ModSettingGui
{
    /// <summary>The window as a plugin of HD2 Mod Quick Deploy (its contract: Api, Kind, Title,
    /// Summary, Open; called by name, nothing shared but strings, numbers, delegates and Json
    /// shapes). The manager hands over the language and, as "library", every mod in its library
    /// whose Lua writes lists for this window, with whether it is enabled.</summary>
    public static class Plugin
    {
        public const int Api = 1;
        public const string Kind = "window";

        public static string Title(bool zh) => zh ? "HD2 MOD 控制台" : "HD2 Mod Console";

        public static string Summary(bool zh) => zh
            ? "HD2 Mod Setting GUI 的设置窗口：编辑各 MOD 的设置，已启用的 MOD 列在前面，未启用及不在库中的 MOD 分开列出。"
            : "HD2 Mod Setting GUI's window: each mod's settings, with the enabled mods first and the mods that are off or not in the library listed apart.";

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
            if (host.TryGetValue("zh", out var z) && z is bool zh) Texts.Zh = zh;
            open = new MainWindow();
            open.Closed += (s, e) => open = null;
            open.Show();
        }
    }
}
