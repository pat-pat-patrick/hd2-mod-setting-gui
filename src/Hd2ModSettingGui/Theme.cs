using System;
using System.Windows;

namespace Hd2ModSettingGui
{
    /// <summary>The window's look (Theme.xaml), one dictionary for the whole process: the
    /// application's when the window runs on its own; merged into each window when HD2 Mod
    /// Quick Deploy hosts it as a plugin, whose application keeps its own resources (the
    /// same keys, other values).</summary>
    public static class Theme
    {
        static ResourceDictionary res;

        // with the version, as the compiled XAML has it: a host holding two versions of the
        // plugin then reads each one's Theme from its own copy
        public static ResourceDictionary Res => res ?? (res = (ResourceDictionary)Application.LoadComponent(
            new Uri("/" + typeof(Theme).Assembly.GetName().Name + ";V" + typeof(Theme).Assembly.GetName().Version + ";component/Theme.xaml", UriKind.Relative)));

        public static object Get(string key) => Res[key];

        /// <summary>The look on a window of ours when the application does not carry it.</summary>
        public static void On(Window w)
        {
            if (Application.Current == null || !Application.Current.Resources.MergedDictionaries.Contains(Res))
                w.Resources.MergedDictionaries.Add(Res);
        }
    }
}
