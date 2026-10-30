using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Hd2ModSettingGui.Core;

namespace Hd2ModSettingGui
{
    // (the user, 10-30: "说的就是叠加") the window over the game: a key, while
    // the game runs, brings it up on top of the game and takes the mouse and
    // the keys; the same key again puts it away and gives the game back its
    // focus. A key of the system's (RegisterHotKey), held only while the game
    // runs, so that other programs have it the rest of the time. Nothing goes
    // into the game's process: a window over a borderless game window.
    public partial class MainWindow
    {
        const int WM_HOTKEY = 0x312, HotkeyId = 0x4844;
        const uint MOD_NOREPEAT = 0x4000;
        Hotkey overlayKey;               // null: none
        bool hotkeyHeld, hotkeyBusy;     // held: registered; busy: another program has it
        bool overlaid;                   // brought up by the key, on top until put away or left
        WindowState restoreTo = WindowState.Normal;

        void LoadOverlayKey()
        {
            var cfg = Environs.LoadConfig();
            overlayKey = cfg.TryGetValue("overlay_key", out var k) && k is string ks ? Hotkey.Parse(ks) : Hotkey.Parse(Hotkey.Default);
            Deactivated += (s, e) => { if (overlaid) { overlaid = false; Topmost = false; } };
        }

        void SetOverlayKey(Hotkey h)
        {
            overlayKey = h;
            var cfg = Environs.LoadConfig();
            cfg["overlay_key"] = h?.ToString() ?? "";
            Environs.SaveConfig(cfg);
            ReleaseHotkey();
            hotkeyBusy = false;
            ArmHotkey();
        }

        /// <summary>Held while the game runs and a key is set; let go otherwise.</summary>
        void ArmHotkey()
        {
            bool want = running && overlayKey != null && !keyWaiting;
            if (want == hotkeyHeld || !(PresentationSource.FromVisual(this) is HwndSource src)) return;
            if (!want) { ReleaseHotkey(); return; }
            hotkeyHeld = RegisterHotKey(src.Handle, HotkeyId, overlayKey.Modifiers | MOD_NOREPEAT, overlayKey.VirtualKey);
            hotkeyBusy = !hotkeyHeld;
            PaintOverlayNote();
        }

        // ---------------------------------------------------------------- its row in the window's settings
        TextBlock overlayNote;
        bool keyWaiting;                 // the key being chosen: let go meanwhile, so pressing it is seen here

        FrameworkElement OverlayRow()
        {
            var line = new DockPanel();
            var btn = new Button { Style = S("Ghost"), MinWidth = 150, FontFamily = (FontFamily)Application.Current.Resources["Mono"], VerticalAlignment = VerticalAlignment.Top };
            var clear = new Button { Content = "×", Style = S("Ghost"), MinWidth = 40, Margin = new Thickness(6, 0, 0, 0), ToolTip = Texts.Get("unbind"), VerticalAlignment = VerticalAlignment.Top };
            void Paint()
            {
                btn.Content = keyWaiting ? Texts.Get("press_key") : overlayKey == null ? Texts.Get("no_key") : overlayKey.Label();
                btn.BorderBrush = keyWaiting ? B("Yellow") : B("Grid");
                clear.Visibility = overlayKey == null ? Visibility.Hidden : Visibility.Visible;
            }
            void Stop(Hotkey taken, bool take)
            {
                keyWaiting = false;
                if (take) SetOverlayKey(taken); else ArmHotkey();
                Paint();
            }
            btn.Click += (s, e) => { keyWaiting = true; ReleaseHotkey(); Paint(); btn.Focus(); };
            btn.PreviewKeyDown += (s, e) =>
            {
                if (!keyWaiting) return;
                e.Handled = true;
                var k = e.Key == Key.System ? e.SystemKey : e.Key;
                if (k == Key.Escape) { Stop(null, false); return; }
                var h = Hotkey.From(k, Keyboard.Modifiers);
                if (h != null) Stop(h, true);
            };
            btn.LostKeyboardFocus += (s, e) => { if (keyWaiting) Stop(null, false); };
            clear.Click += (s, e) => Stop(null, true);
            Paint();
            var keys = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(24, 0, 0, 0) };
            keys.Children.Add(btn);
            keys.Children.Add(clear);
            DockPanel.SetDock(keys, Dock.Right);
            line.Children.Add(keys);
            var words = new StackPanel();
            words.Children.Add(Text(Texts.Get("overlay_d"), "Hint"));
            overlayNote = Text("", fg: B("Yellow"), size: 12);
            overlayNote.Margin = new Thickness(0, 4, 0, 0);
            words.Children.Add(overlayNote);
            line.Children.Add(words);
            PaintOverlayNote();
            return line;
        }

        void PaintOverlayNote()
        {
            if (overlayNote == null) return;
            bool busy = hotkeyBusy && overlayKey != null;
            overlayNote.Text = busy ? Texts.Get("overlay_busy") : "";
            overlayNote.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        }

        void ReleaseHotkey()
        {
            if (hotkeyHeld && PresentationSource.FromVisual(this) is HwndSource src) UnregisterHotKey(src.Handle, HotkeyId);
            hotkeyHeld = false;
        }

        void ToggleOverlay()
        {
            var me = new WindowInteropHelper(this).Handle;
            var game = Environs.GameWindow();
            if (WindowState != WindowState.Minimized && GetForegroundWindow() == me)
            {
                // away: the game in front again, with the keys
                // minimized without passing the front on (SW_MINIMIZE gives it to the next window
                // in line), then the front given to the game while this window still has it
                overlaid = false;
                Topmost = false;
                restoreTo = WindowState;
                ShowWindow(me, 7);              // SW_SHOWMINNOACTIVE
                if (game != IntPtr.Zero) SetForegroundWindow(game);
                return;
            }
            if (game != IntPtr.Zero && (WindowState == WindowState.Normal || restoreTo == WindowState.Normal)) OntoGame(me, game);
            if (WindowState == WindowState.Minimized) WindowState = restoreTo;
            overlaid = true;
            Topmost = true;
            Activate();
            SetForegroundWindow(me);
        }

        // a window off the game's screen is brought to its middle, at most as large as the game:
        // its place when not minimized (minimized, the window's own rectangle is far off screen)
        // set before it is shown, so that it opens there
        static void OntoGame(IntPtr me, IntPtr game)
        {
            var p = new Placement { Length = Marshal.SizeOf(typeof(Placement)) };
            if (!GetWindowRect(game, out var g) || !GetWindowPlacement(me, ref p)) return;
            // the placement's rectangle is in the work area's coordinates (they differ from the
            // screen's by a taskbar on the left or the top)
            var mon = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
            if (!GetMonitorInfo(MonitorFromWindow(me, 2), ref mon)) return;
            int dx = mon.Work.Left - mon.Screen.Left, dy = mon.Work.Top - mon.Screen.Top;
            var w = p.Normal;
            int cx = (w.Left + w.Right) / 2 + dx, cy = (w.Top + w.Bottom) / 2 + dy;
            if (cx >= g.Left && cx < g.Right && cy >= g.Top && cy < g.Bottom) return;
            int ww = Math.Min(w.Right - w.Left, g.Right - g.Left), wh = Math.Min(w.Bottom - w.Top, g.Bottom - g.Top);
            var gm = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
            if (!GetMonitorInfo(MonitorFromWindow(game, 2), ref gm)) return;
            int left = g.Left + (g.Right - g.Left - ww) / 2 - (gm.Work.Left - gm.Screen.Left);
            int top = g.Top + (g.Bottom - g.Top - wh) / 2 - (gm.Work.Top - gm.Screen.Top);
            p.Normal = new Rect32 { Left = left, Top = top, Right = left + ww, Bottom = top + wh };
            if (p.ShowCmd != 2) p.ShowCmd = 1;     // as it is: minimized stays so (shown next), else normal
            SetWindowPlacement(me, ref p);
        }

        struct Placement { public int Length, Flags, ShowCmd; public int MinX, MinY, MaxX, MaxY; public Rect32 Normal; }
        struct MonitorInfo { public int Size; public Rect32 Screen, Work; public int Flags; }
        [DllImport("user32.dll")] static extern bool GetWindowPlacement(IntPtr h, ref Placement p);
        [DllImport("user32.dll")] static extern bool SetWindowPlacement(IntPtr h, ref Placement p);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr h, uint flags);
        [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr m, ref MonitorInfo i);

        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int hh, uint flags);
    }
}
