using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Hd2ModSettingGui
{
    /// <summary>Windows 11's acrylic behind the window: what is behind it,
    /// blurred, through the window's translucent grounds. Windows before 11
    /// 22H2 refuse the backdrop: the grounds stay solid.</summary>
    static class Backdrop
    {
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins m);
        struct Margins { public int Left, Right, Top, Bottom; }

        const int DarkMode = 20, SystemBackdrop = 38, None = 1, Acrylic = 3;

        /// <summary>The acrylic on, the grounds made translucent; false (and
        /// nothing changed) where Windows has no system backdrop.</summary>
        public static bool Enable(Window w)
        {
            var src = PresentationSource.FromVisual(w) as HwndSource;
            if (src == null) return false;
            int dark = 1, kind = Acrylic;
            DwmSetWindowAttribute(src.Handle, DarkMode, ref dark, 4);
            if (DwmSetWindowAttribute(src.Handle, SystemBackdrop, ref kind, 4) != 0) return false;
            src.CompositionTarget.BackgroundColor = Colors.Transparent;
            // the frame over the whole window (WindowChrome would put back its own)
            var chrome = System.Windows.Shell.WindowChrome.GetWindowChrome(w);
            if (chrome != null)
            {
                if (chrome.IsFrozen) { chrome = (System.Windows.Shell.WindowChrome)chrome.Clone(); System.Windows.Shell.WindowChrome.SetWindowChrome(w, chrome); }
                chrome.GlassFrameThickness = new Thickness(-1);
            }
            var all = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
            DwmExtendFrameIntoClientArea(src.Handle, ref all);
            w.Background = Brushes.Transparent;
            Translucent("Ground900", 0xE0);
            Translucent("Ground700", 0xF0);
            return true;
        }

        /// <summary>The acrylic and the frame over the window off (on again
        /// with false). The system draws them over the window's whole
        /// rectangle, its region or not: the peek's cut-down window stayed
        /// grey and blurred round its control (the user, 10-22).</summary>
        public static void Pause(Window w, bool pause)
        {
            if (!(PresentationSource.FromVisual(w) is HwndSource src)) return;
            int kind = pause ? None : Acrylic;
            DwmSetWindowAttribute(src.Handle, SystemBackdrop, ref kind, 4);
            int edge = pause ? 0 : -1;
            var m = new Margins { Left = edge, Right = edge, Top = edge, Bottom = edge };
            DwmExtendFrameIntoClientArea(src.Handle, ref m);
        }

        static void Translucent(string key, byte alpha)
        {
            var b = (SolidColorBrush)Application.Current.Resources[key];
            var c = b.Color;
            c.A = alpha;
            if (b.IsFrozen) Application.Current.Resources[key] = new SolidColorBrush(c);
            else b.Color = c;
        }
    }
}
