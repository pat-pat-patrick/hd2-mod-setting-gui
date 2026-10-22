using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Path = System.Windows.Shapes.Path;
using System.Windows.Threading;
using Hd2ModSettingGui.Core;

namespace Hd2ModSettingGui
{
    public partial class MainWindow : Window
    {
        string folder;
        List<(string Name, string Path)> mods = new List<(string, string)>();
        ModState state;
        readonly Dictionary<string, int> groupOf = new Dictionary<string, int>();   // mod -> the group shown
        string query = "", modQuery = "";
        bool running;
        int scans;
        readonly DispatcherTimer writeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        readonly DispatcherTimer tick = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        readonly Dictionary<string, (Action<object> Show, FrameworkElement Reset)> rows = new Dictionary<string, (Action<object>, FrameworkElement)>();
        StackPanel navList, groupList, panel;
        TextBlock scanText;
        Button copyBtn;
        // the acrylic (Windows 11), and the peek (see Peek)
        bool acrylic;
        readonly Dictionary<string, FrameworkElement> controlOf = new Dictionary<string, FrameworkElement>();
        readonly DispatcherTimer peekEnd = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        FrameworkElement peekOf;

        public MainWindow()
        {
            InitializeComponent();
            peekEnd.Tick += (s, e) => Unpeek();
            var args = Environment.GetCommandLineArgs();
            string Arg(string name) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
            // the language: --lang (tests), else the one chosen last, else Windows's
            var lastLang = Environs.LoadConfig().TryGetValue("lang", out var lg) ? lg as string : null;
            Texts.Zh = Arg("--lang") is string l ? l == "zh" : lastLang != null ? lastLang == "zh" : Environs.UiChinese;
            folder = Arg("--folder");
            if (folder == null)
            {
                var kept = Environs.LoadConfig().TryGetValue("game_dir", out var d) ? d as string : null;
                folder = kept != null && Directory.Exists(kept) ? kept : Environs.GameFolder;
            }
            writeTimer.Tick += (s, e) => { writeTimer.Stop(); Write(); };
            tick.Tick += (s, e) => Tick();
            running = Environs.GameRunning();
            Load();
            tick.Start();
            StateChanged += (s, e) => FitMaximized();
        }

        // ---------------------------------------------------------------- loading
        void Load()
        {
            mods = Protocol.FindMods(folder);
            if (state == null || !mods.Any(m => m.Name == state.List.Name))
                state = null;
            if (state == null && mods.Count > 0) Open(0, build: false);
            Build();
        }

        void Open(int index, bool build = true)
        {
            try
            {
                state = new ModState(ModList.Load(mods[index].Path));
                state.Changed += () => writeTimer.Stop();
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is ArgumentException || e is InvalidOperationException)
            {
                MessageBox.Show(this, mods[index].Path + "\n" + e.Message, Texts.Get("window"));
                state = null;
            }
            query = "";
            if (build) Build();
        }

        void Build()
        {
            Title = Texts.Get("window");
            TitleText.Text = Texts.Get("title");
            SummaryText.Text = mods.Count == 1 ? Texts.Get("summary_one") : Texts.Get("summary", mods.Count);
            LangBtn.Content = Texts.Get("lang");
            MinBtn.ToolTip = Texts.Get("minimize");
            CloseBtn.ToolTip = Texts.Get("close");
            UndoBtn.Content = Texts.Get("undo");
            ResetModBtn.Content = Texts.Get("reset_mod");
            rows.Clear();
            if (state == null)
            {
                Footer.Visibility = Visibility.Collapsed;
                Body.Content = BuildEmpty();
            }
            else
            {
                Footer.Visibility = Visibility.Visible;
                Body.Content = BuildMain();
            }
            PaintStatus();
            PaintMax();
        }

        // ---------------------------------------------------------------- helpers
        static Brush B(string key) => (Brush)Application.Current.Resources[key];
        static Style S(string key) => (Style)Application.Current.Resources[key];

        static TextBlock Text(string text, string style = null, Brush fg = null, double size = 0, FontWeight? weight = null)
        {
            var t = new TextBlock { Text = text };
            if (style != null) t.Style = S(style);
            if (fg != null) t.Foreground = fg;
            if (size > 0) t.FontSize = size;
            if (weight.HasValue) t.FontWeight = weight.Value;
            return t;
        }

        static Border Line(bool vertical) => new Border
        {
            Background = B("Grid"),
            Width = vertical ? 1 : double.NaN,
            Height = vertical ? double.NaN : 1,
        };

        static Button Ghost(string text, RoutedEventHandler click, string style = "Ghost")
        {
            var b = new Button { Content = text, Style = S(style) };
            b.Click += click;
            return b;
        }

        static TextBox Input(string hint, string text, Action<string> changed)
        {
            var t = new TextBox { Style = S("Input"), Tag = hint, Text = text };
            t.TextChanged += (s, e) => changed(t.Text);
            return t;
        }

        // ---------------------------------------------------------------- the page with a mod
        FrameworkElement BuildMain()
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            // the mods
            var nav = new StackPanel { Margin = new Thickness(16, 24, 16, 16) };
            nav.Children.Add(Text(Texts.Get("filter"), "Label", margin: null));
            nav.Children.Add(Spaced(Input(Texts.Get("mod_ph"), modQuery, v => { modQuery = v; RenderMods(); }), 6, 16));
            nav.Children.Add(Text(Texts.Get("installed"), "Label"));
            navList = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
            nav.Children.Add(navList);
            Grid.SetColumn(nav, 0);
            g.Children.Add(nav);
            var l1 = Line(true);
            Grid.SetColumn(l1, 1);
            g.Children.Add(l1);
            // the mod: its head, its groups, its settings
            var main = new DockPanel();
            Grid.SetColumn(main, 2);
            g.Children.Add(main);
            var head = new DockPanel { Margin = new Thickness(24, 20, 24, 14), LastChildFill = false };
            var titles = new StackPanel();
            var meta = new StackPanel { Orientation = Orientation.Horizontal };
            var ver = state.List.Version;
            var num = System.Text.RegularExpressions.Regex.Match(ver ?? "", @"(\d+(?:\.\d+)+)\s*$");
            meta.Children.Add(Text(num.Success ? "v" + num.Groups[1].Value : ver, "MonoText"));
            meta.Children.Add(Text("  ·  " + Texts.Get("count", state.List.Options.Count), fg: B("Muted"), size: 12));
            titles.Children.Add(meta);
            titles.Children.Add(Text(state.List.Title, size: 28, weight: FontWeights.Bold));
            DockPanel.SetDock(titles, Dock.Left);
            head.Children.Add(titles);
            var search = Input(Texts.Get("search"), query, v => { query = v; RenderPanel(); RenderGroups(); });
            search.Width = 280;
            search.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(search, Dock.Right);
            head.Children.Add(search);
            var headBox = new Border { BorderBrush = B("Grid"), BorderThickness = new Thickness(0, 0, 0, 1), Child = head };
            DockPanel.SetDock(headBox, Dock.Top);
            main.Children.Add(headBox);
            var content = new Grid { Background = B("Ground700") };
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.ColumnDefinitions.Add(new ColumnDefinition());
            main.Children.Add(content);
            var gnav = new StackPanel { Margin = new Thickness(12, 16, 0, 16) };
            gnav.Children.Add(Spaced(Text(Texts.Get("groups"), "Label"), 0, 6, 8));
            groupList = new StackPanel();
            gnav.Children.Add(groupList);
            content.Children.Add(gnav);
            var l2 = Line(true);
            Grid.SetColumn(l2, 1);
            content.Children.Add(l2);
            panel = new StackPanel { Margin = new Thickness(24, 16, 24, 24) };
            var scroll = new ScrollViewer
            {
                Content = panel,
                Background = B("Ground900"),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Focusable = false,
            };
            Grid.SetColumn(scroll, 2);
            content.Children.Add(scroll);
            RenderMods();
            RenderGroups();
            RenderPanel();
            return g;
        }

        static T Spaced<T>(T e, double top, double bottom, double left = 0) where T : FrameworkElement
        {
            e.Margin = new Thickness(left, top, 0, bottom);
            return e;
        }

        static TextBlock Text(string text, string style, object margin) => Text(text, style);

        void RenderMods()
        {
            if (navList == null || state == null) return;
            navList.Children.Clear();
            for (int i = 0; i < mods.Count; i++)
            {
                var (name, path) = mods[i];
                string title = name;
                try { title = name == state.List.Name ? state.List.Title : ModList.Load(path).Title; } catch (Exception) { }
                if (modQuery != "" && title.IndexOf(modQuery, StringComparison.OrdinalIgnoreCase) < 0
                    && name.IndexOf(modQuery, StringComparison.OrdinalIgnoreCase) < 0) continue;
                bool cur = name == state.List.Name;
                var box = new Border
                {
                    Background = cur ? B("Ground700") : Brushes.Transparent,
                    BorderBrush = cur ? B("Yellow") : Brushes.Transparent,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(12, 8, 12, 8),
                    Margin = new Thickness(0, 2, 0, 2),
                    Cursor = cur ? null : Cursors.Hand,
                };
                var sp = new StackPanel();
                sp.Children.Add(Text(title, size: 14, weight: FontWeights.SemiBold));
                if (cur)
                {
                    int n = state.ChangedCount;
                    sp.Children.Add(Text(n > 0 ? Texts.Get("n_changed", n) : Texts.Get("all_default"), "MonoText",
                                         n > 0 ? B("Blue") : B("Muted"), 11));
                }
                else sp.Children.Add(Text(name, "MonoText", size: 11));
                box.Child = sp;
                int idx = i;
                if (!cur) box.MouseLeftButtonUp += (s, e) => Open(idx);
                navList.Children.Add(box);
            }
        }

        List<(int Index, string Name, List<string> Keys)> Groups() =>
            state.List.Pages.Select((p, i) => (i, p.Name.In(Texts.Zh), p.Keys)).ToList();

        (int Index, string Name, List<string> Keys) CurrentGroup()
        {
            var gs = Groups();
            int gi = groupOf.TryGetValue(state.List.Name, out var x) ? x : 0;
            return gs.FirstOrDefault(g => g.Index == gi) is var found && found.Keys != null ? found : gs[0];
        }

        void RenderGroups()
        {
            if (groupList == null || state == null) return;
            groupList.Children.Clear();
            int cur = query.Trim() == "" ? CurrentGroup().Index : -1;
            foreach (var (index, name, keys) in Groups())
            {
                bool on = index == cur;
                var row = new Border
                {
                    Background = on ? B("Ground900") : Brushes.Transparent,
                    BorderBrush = on ? B("Yellow") : Brushes.Transparent,
                    BorderThickness = new Thickness(2, 0, 0, 0),
                    Padding = new Thickness(12, 0, 12, 0),
                    MinHeight = 44,
                    Cursor = Cursors.Hand,
                };
                var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                sp.Children.Add(Text(name, fg: on ? B("Yellow") : B("Ink"), size: 14, weight: FontWeights.SemiBold));
                if (keys.Any(k => !state.List.MenuOwned.Contains(k) && state.IsChanged(k)))
                    sp.Children.Add(new Ellipse { Width = 6, Height = 6, Fill = B("Blue"), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
                row.Child = sp;
                int gi = index;
                row.MouseLeftButtonUp += (s, e) => { groupOf[state.List.Name] = gi; query = ""; Build(); };
                groupList.Children.Add(row);
            }
            ResetGroupBtn.Content = Texts.Get("reset_group", CurrentGroup().Name);
        }

        void RenderPanel()
        {
            if (panel == null || state == null) return;
            panel.Children.Clear();
            rows.Clear();
            var q = query.Trim();
            var list = new List<(string Group, string Key)>();
            if (q != "")
            {
                foreach (var (_, gname, keys) in Groups())
                    foreach (var k in keys)
                    {
                        var spec = state.List.Spec(k);
                        var hay = gname + " " + spec.Name.En + " " + spec.Name.Zh + " " + k;
                        if (hay.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) list.Add((gname, k));
                    }
            }
            else
            {
                var cg = CurrentGroup();
                list.AddRange(cg.Keys.Select(k => ((string)null, k)));
            }
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            var meta = Text(q != "" ? Texts.Get("matches", list.Count) : "", "MonoText");
            DockPanel.SetDock(meta, Dock.Right);
            meta.VerticalAlignment = VerticalAlignment.Bottom;
            head.Children.Add(meta);
            head.Children.Add(Text(q != "" ? Texts.Get("results") : CurrentGroup().Name, size: 18, weight: FontWeights.Bold));
            panel.Children.Add(head);
            if (list.Count == 0)
            {
                panel.Children.Add(Spaced(Text(Texts.Get("no_match"), fg: B("Muted")), 24, 24));
                return;
            }
            var box = new StackPanel();
            panel.Children.Add(new Border { BorderBrush = B("Grid"), BorderThickness = new Thickness(1), Background = B("Ground700"), Child = box });
            for (int i = 0; i < list.Count; i++)
                box.Children.Add(Row(list[i].Key, list[i].Group, i == list.Count - 1));
        }

        // ---------------------------------------------------------------- a setting
        FrameworkElement Row(string key, string group, bool last)
        {
            var spec = state.List.Spec(key);
            var body = new StackPanel { Margin = new Thickness(16, 12, 16, 12) };
            if (group != null) body.Children.Add(Text(group, "MonoText", B("Blue"), 11));
            var line = new DockPanel();
            body.Children.Add(line);
            var reset = ResetIcon(() => Change(key, spec.DefaultValue));
            reset.ToolTip = Texts.Get("reset_one", spec.Name.In(Texts.Zh));
            DockPanel.SetDock(reset, Dock.Right);
            line.Children.Add(reset);
            Action<object> show = null;
            FrameworkElement control;
            if (state.List.MenuOwned.Contains(key))
                control = Text(Texts.Get("owned"), fg: B("Muted"), size: 12);
            else switch (spec.Kind)
            {
                case Kind.Toggle:
                {
                    var cb = new CheckBox { Style = S("Switch"), IsChecked = (bool)state.Value(key), VerticalAlignment = VerticalAlignment.Center };
                    bool quiet = false;
                    cb.Checked += (s, e) => { if (!quiet) Change(key, true, fromWidget: true); };
                    cb.Unchecked += (s, e) => { if (!quiet) Change(key, false, fromWidget: true); };
                    show = v => { quiet = true; cb.IsChecked = (bool)v; quiet = false; };
                    control = cb;
                    break;
                }
                case Kind.Choice:
                {
                    var seg = new StackPanel { Orientation = Orientation.Horizontal };
                    var group_ = "c_" + key;
                    var buttons = new List<RadioButton>();
                    bool quiet = false;
                    foreach (var (value, name) in spec.Choices)
                    {
                        var rb = new RadioButton { Style = S("Segment"), Content = name.In(Texts.Zh), GroupName = group_, Tag = value };
                        rb.Checked += (s, e) => { if (!quiet) Change(key, double.Parse((string)rb.Tag, CultureInfo.InvariantCulture), fromWidget: true); };
                        buttons.Add(rb);
                        seg.Children.Add(rb);
                    }
                    show = v =>
                    {
                        quiet = true;
                        var want = ((int)Math.Round((double)v)).ToString(CultureInfo.InvariantCulture);
                        foreach (var rb in buttons) rb.IsChecked = (string)rb.Tag == want;
                        quiet = false;
                    };
                    control = new Border { BorderBrush = B("Grid"), BorderThickness = new Thickness(1), Child = seg, VerticalAlignment = VerticalAlignment.Center };
                    break;
                }
                case Kind.Slider:
                {
                    var sl = new Slider
                    {
                        Style = S("Range"), Minimum = spec.Min, Maximum = spec.Max, TickFrequency = spec.Step,
                        SmallChange = spec.Step, LargeChange = spec.Step, Width = 220, VerticalAlignment = VerticalAlignment.Center,
                    };
                    bool quiet = false;
                    var num = NumberBox(spec, v =>
                    {
                        quiet = true;
                        sl.Value = v;
                        quiet = false;
                        if (!Equals(state.Value(key), v)) Change(key, v, fromWidget: true);
                    }, out var shown);
                    num.Margin = new Thickness(12, 0, 0, 0);
                    sl.ValueChanged += (s, e) =>
                    {
                        if (quiet) return;
                        var v = spec.Snap(sl.Value);
                        shown(v);
                        if (!Equals(state.Value(key), v)) Change(key, v, fromWidget: true);
                    };
                    show = v => { quiet = true; sl.Value = (double)v; shown((double)v); quiet = false; };
                    var sp = new StackPanel { Orientation = Orientation.Horizontal };
                    sp.Children.Add(sl);
                    sp.Children.Add(num);
                    control = sp;
                    break;
                }
                case Kind.Color:
                    control = ColorControl(key, spec, body, out show);
                    break;
                default:
                    control = Text(Convert.ToString(state.Value(key), CultureInfo.InvariantCulture), fg: B("Muted"));
                    break;
            }
            control.Margin = new Thickness(0, 0, 8, 0);
            var both = new NameAndControl();
            both.Children.Add(new TextBlock { Text = spec.Name.In(Texts.Zh), FontSize = 15, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            both.Children.Add(control);
            line.Children.Add(both);
            var hint = spec.Hint.In(Texts.Zh);
            if (hint != "") body.Children.Add(Spaced(Text(hint, "Hint"), 4, 0));
            show?.Invoke(state.Value(key));
            rows[key] = (show, reset);
            controlOf[key] = control;
            PaintReset(key);
            return new Border { BorderBrush = B("Grid"), BorderThickness = new Thickness(0, 0, 0, last ? 0 : 1), Child = body };
        }

        // a setting's name and its control side by side; the control under the
        // name when beside it the name would get less than MinName (a choice's
        // long English labels squeezed the name to a letter a line)
        sealed class NameAndControl : Panel
        {
            const double MinName = 220, Gap = 24, Below = 10;
            bool stacked;

            protected override Size MeasureOverride(Size avail)
            {
                UIElement name = InternalChildren[0], ctl = InternalChildren[1];
                ctl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double cw = ctl.DesiredSize.Width;
                bool wide = double.IsInfinity(avail.Width);
                stacked = !wide && avail.Width - cw - Gap < MinName;
                if (stacked)
                {
                    name.Measure(new Size(avail.Width, double.PositiveInfinity));
                    ctl.Measure(new Size(avail.Width, double.PositiveInfinity));
                    return new Size(Math.Max(name.DesiredSize.Width, Math.Min(cw, avail.Width)), name.DesiredSize.Height + Below + ctl.DesiredSize.Height);
                }
                name.Measure(new Size(wide ? double.PositiveInfinity : avail.Width - cw - Gap, double.PositiveInfinity));
                return new Size(wide ? name.DesiredSize.Width + Gap + cw : avail.Width, Math.Max(name.DesiredSize.Height, ctl.DesiredSize.Height));
            }

            protected override Size ArrangeOverride(Size size)
            {
                UIElement name = InternalChildren[0], ctl = InternalChildren[1];
                if (stacked)
                {
                    name.Arrange(new Rect(0, 0, size.Width, name.DesiredSize.Height));
                    ctl.Arrange(new Rect(0, name.DesiredSize.Height + Below, Math.Min(ctl.DesiredSize.Width, size.Width), ctl.DesiredSize.Height));
                }
                else
                {
                    double cw = ctl.DesiredSize.Width;
                    name.Arrange(new Rect(0, (size.Height - name.DesiredSize.Height) / 2, Math.Max(0, size.Width - cw - Gap), name.DesiredSize.Height));
                    ctl.Arrange(new Rect(size.Width - cw, (size.Height - ctl.DesiredSize.Height) / 2, cw, ctl.DesiredSize.Height));
                }
                return size;
            }
        }

        FrameworkElement NumberBox(OptionSpec spec, Action<double> changed, out Action<double> show)
        {
            // a slider's value to type (Enter or leaving the box takes it, Esc
            // drops it), or to step: Up/Down, the wheel while the box has the
            // focus, or the two arrows (held, they repeat); out of range or off
            // a step, the nearest step that is in it
            double cur = spec.Min;
            var box = new TextBox
            {
                Style = S("Input"), Width = 64, MinHeight = 0, BorderThickness = new Thickness(0), Padding = new Thickness(8, 0, 2, 0),
                FontFamily = (FontFamily)Application.Current.Resources["Mono"], TextAlignment = TextAlignment.Right,
            };
            void Put(double v) { cur = v; box.Text = v.ToString("F" + spec.Digits, CultureInfo.InvariantCulture); }
            double? Typed() => double.TryParse(box.Text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
                               && !double.IsNaN(d) && !double.IsInfinity(d) ? d : (double?)null;
            void Take(double v) { v = spec.Snap(v); Put(v); changed(v); }
            void Step(int n) => Take((Typed() ?? cur) + n * spec.Step);
            box.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Up) Step(1);
                else if (e.Key == Key.Down) Step(-1);
                else if (e.Key == Key.Enter) { Take(Typed() ?? cur); box.SelectAll(); }
                else if (e.Key == Key.Escape) { Put(cur); box.SelectAll(); }
                else return;
                e.Handled = true;
            };
            box.PreviewMouseWheel += (s, e) =>
            {
                if (!box.IsKeyboardFocusWithin) return;     // scrolling the page past it changes nothing
                Step(e.Delta > 0 ? 1 : -1);
                e.Handled = true;
            };
            box.GotKeyboardFocus += (s, e) => box.SelectAll();
            var frame = new Border { BorderBrush = B("Grid"), BorderThickness = new Thickness(1), Background = B("Ground700"), Height = 40, VerticalAlignment = VerticalAlignment.Center };
            box.IsKeyboardFocusWithinChanged += (s, e) =>
            {
                frame.BorderBrush = box.IsKeyboardFocusWithin ? B("Blue") : B("Grid");
                if (!box.IsKeyboardFocusWithin) Take(Typed() ?? cur);
            };
            var arrows = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            arrows.Children.Add(Arrow(true, () => Step(1)));
            arrows.Children.Add(Arrow(false, () => Step(-1)));
            var dock = new DockPanel();
            DockPanel.SetDock(arrows, Dock.Right);
            dock.Children.Add(arrows);
            dock.Children.Add(box);
            frame.Child = dock;
            show = Put;
            return frame;
        }

        static RepeatButton Arrow(bool up, Action click)
        {
            var p = new Path
            {
                Data = Geometry.Parse(up ? "M 0,5 L 5,0 L 10,5 Z" : "M 0,0 L 10,0 L 5,5 Z"),
                Fill = B("Muted"), Width = 9, Height = 5, Stretch = Stretch.Fill,
            };
            var b = new RepeatButton { Width = 24, Height = 19, Delay = 400, Interval = 60, Cursor = Cursors.Hand, Content = p, Focusable = false };
            b.Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(
                "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='RepeatButton'>" +
                "<Border Background='Transparent'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border></ControlTemplate>");
            b.MouseEnter += (s, e) => p.Fill = B("Ink");
            b.MouseLeave += (s, e) => p.Fill = B("Muted");
            b.Click += (s, e) => click();
            return b;
        }

        FrameworkElement ResetIcon(Action click)
        {
            // the reset: an eraser, the change rubbed out (the user, 10-22: "回到默认")
            var p = new Path
            {
                Data = Geometry.Parse("M 19,20 H 8.5 L 4.3,15.7 A 1,1 0 0 1 4.3,14.3 L 14.3,4.3 A 1,1 0 0 1 15.7,4.3 L 20.7,9.3 A 1,1 0 0 1 20.7,10.7 L 11.5,20 M 18,13.3 L 11.7,7"),
                Stroke = B("Muted"), StrokeThickness = 2, Width = 18, Height = 18, Stretch = Stretch.Uniform,
                StrokeLineJoin = PenLineJoin.Miter,
            };
            var b = new Button { Width = 36, Height = 36, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Content = p, FocusVisualStyle = null };
            b.Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(
                "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'>" +
                "<Border Background='Transparent'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border></ControlTemplate>");
            b.MouseEnter += (s, e) => p.Stroke = B("Ink");
            b.MouseLeave += (s, e) => p.Stroke = B("Muted");
            b.Click += (s, e) => click();
            return b;
        }

        FrameworkElement ColorControl(string key, OptionSpec spec, StackPanel below, out Action<object> show)
        {
            // presets (the one in use ringed yellow), the picker (ringed when
            // the colour is none of them), its #RRGGBB to type
            var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var rings = new List<(string Hex, Border Ring)>();
            foreach (var (hex, name) in spec.Choices)
            {
                var ring = Swatch(hex, name.In(Texts.Zh));
                ring.MouseLeftButtonUp += (s, e) => Change(key, hex);
                rings.Add((hex, ring));
                sp.Children.Add(ring);
            }
            sp.Children.Add(new Border { Width = 1, Height = 28, Background = B("Grid"), Margin = new Thickness(8, 0, 8, 0) });
            var custom = Swatch((string)state.Value(key), Texts.Get("pick"));
            custom.MouseLeftButtonUp += (s, e) => PickColor(key);
            sp.Children.Add(custom);
            var box = new TextBox { Style = S("Input"), Width = 104, FontFamily = (FontFamily)Application.Current.Resources["Mono"], Margin = new Thickness(8, 0, 0, 0), MaxLength = 7 };
            var bad = Text(Texts.Get("hex_bad"), fg: B("Yellow"), size: 12);
            bad.Visibility = Visibility.Collapsed;
            bad.Margin = new Thickness(0, 4, 0, 0);
            below.Children.Add(bad);
            bool quiet = false;
            box.TextChanged += (s, e) =>
            {
                if (quiet) return;
                var t = box.Text.Trim();
                if (t != "" && t[0] != '#') t = "#" + t;
                bool ok = Protocol.IsHex(t);
                box.BorderBrush = ok ? B("Grid") : B("Yellow");
                bad.Visibility = ok ? Visibility.Collapsed : Visibility.Visible;
                if (ok && !string.Equals((string)state.Value(key), t, StringComparison.OrdinalIgnoreCase))
                    Change(key, t.ToLowerInvariant(), fromWidget: true);
            };
            sp.Children.Add(box);
            show = v =>
            {
                var hex = ((string)v).ToLowerInvariant();
                bool known = false;
                foreach (var (h, ring) in rings)
                {
                    bool on = h == hex;
                    known |= on;
                    ring.BorderBrush = on ? B("Yellow") : B("Grid");
                }
                custom.BorderBrush = known ? B("Grid") : B("Yellow");
                ((Border)custom.Child).Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                if (!string.Equals(box.Text.TrimStart('#'), hex.TrimStart('#'), StringComparison.OrdinalIgnoreCase))
                {
                    quiet = true;
                    box.Text = hex.ToUpperInvariant();
                    box.BorderBrush = B("Grid");
                    bad.Visibility = Visibility.Collapsed;
                    quiet = false;
                }
            };
            return sp;
        }

        static Border Swatch(string hex, string tip) => new Border
        {
            Width = 36, Height = 36, Margin = new Thickness(3, 0, 3, 0), BorderThickness = new Thickness(2),
            BorderBrush = B("Grid"), Padding = new Thickness(3), Cursor = Cursors.Hand, ToolTip = tip,
            Child = new Border { Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)) },
        };

        void PickColor(string key)
        {
            var c = (Color)ColorConverter.ConvertFromString((string)state.Value(key));
            using (var dlg = new System.Windows.Forms.ColorDialog { FullOpen = true, Color = System.Drawing.Color.FromArgb(c.R, c.G, c.B) })
            {
                if (dlg.ShowDialog(new Win32Owner(this)) == System.Windows.Forms.DialogResult.OK)
                    Change(key, string.Format("#{0:x2}{1:x2}{2:x2}", dlg.Color.R, dlg.Color.G, dlg.Color.B));
            }
        }

        sealed class Win32Owner : System.Windows.Forms.IWin32Window
        {
            public Win32Owner(Window w) => Handle = new WindowInteropHelper(w).Handle;
            public IntPtr Handle { get; }
        }

        void PaintReset(string key)
        {
            if (!rows.TryGetValue(key, out var r)) return;
            r.Reset.Visibility = !state.List.MenuOwned.Contains(key) && state.IsChanged(key) ? Visibility.Visible : Visibility.Hidden;
        }

        // ---------------------------------------------------------------- changes
        void Change(string key, object v, bool fromWidget = false)
        {
            state.Set(key, v);
            if (!fromWidget && rows.TryGetValue(key, out var r)) r.Show?.Invoke(v);
            if (controlOf.TryGetValue(key, out var c)) Peek(c);
            PaintReset(key);
            RenderGroups();
            RenderMods();
            writeTimer.Stop();
            writeTimer.Start();
        }

        void Refresh()
        {
            foreach (var kv in rows)
            {
                kv.Value.Show?.Invoke(state.Value(kv.Key));
                PaintReset(kv.Key);
            }
            RenderGroups();
            RenderMods();
            writeTimer.Stop();
            writeTimer.Start();
        }

        void Write()
        {
            if (state == null) return;
            try { state.Save(); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                MessageBox.Show(this, Texts.Get("write_failed", state.List.SettingsPath + "\n" + e.Message), Texts.Get("window"));
            }
            PaintStatus();
        }

        void Undo_Click(object sender, RoutedEventArgs e) { if (state != null && state.Undo()) Refresh(); }
        void ResetGroup_Click(object sender, RoutedEventArgs e) { if (state != null) { state.Reset(CurrentGroup().Keys); Refresh(); } }

        void ResetMod_Click(object sender, RoutedEventArgs e)
        {
            if (state == null) return;
            if (MessageBox.Show(this, Texts.Get("confirm_all", state.List.Title), Texts.Get("window"), MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            state.Reset(state.List.Options.Select(o => o.Key));
            Refresh();
        }

        void Lang_Click(object sender, RoutedEventArgs e)
        {
            Texts.Zh = !Texts.Zh;
            var cfg = Environs.LoadConfig();
            cfg["lang"] = Texts.Zh ? "zh" : "en";
            Environs.SaveConfig(cfg);
            Build();
        }

        // ---------------------------------------------------------------- the watch and the status
        void Tick()
        {
            running = Environs.GameRunning();
            // a list that appears (or goes) is loaded: the same ones leave all as it is
            var found = Protocol.FindMods(folder).Select(m => m.Name).ToList();
            if (!found.SequenceEqual(mods.Select(m => m.Name))) Load();
            PaintStatus();
        }

        void PaintStatus()
        {
            if (state == null)
            {
                ConnDot.Fill = B("Muted");
                ConnText.Text = Texts.Get("waiting_list");
                return;
            }
            ConnDot.Fill = running ? B("Blue") : B("Muted");
            ConnText.Text = Texts.Get(running ? "running" : "not_running");
            var colour = B("Muted");
            if (state.SavedAt == null) FootText.Text = Texts.Get("idle");
            else
            {
                var when = state.SavedAt.Value.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
                var applied = Protocol.LastApplied(state.List.LogPath);
                if (applied != null && string.CompareOrdinal(applied, when) >= 0)
                {
                    FootText.Text = Texts.Get("applied", applied);
                    colour = B("Blue");
                }
                else FootText.Text = Texts.Get(running ? "waiting" : "saved_off", when);
            }
            FootDot.Fill = colour;
            FootText.Foreground = colour;
        }

        // ---------------------------------------------------------------- the empty page
        FrameworkElement BuildEmpty()
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            var nav = new StackPanel { Margin = new Thickness(16, 24, 16, 16) };
            nav.Children.Add(Spaced(Text(Texts.Get("installed"), "Label"), 0, 8, 8));
            foreach (var k in new[] { 1.0, 0.6, 0.3 })
                nav.Children.Add(new Border
                {
                    Height = 56, Margin = new Thickness(0, 4, 0, 4), Opacity = k,
                    BorderBrush = new VisualBrush(), BorderThickness = new Thickness(0),
                    Child = new Rectangle { Stroke = B("Grid"), StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 4, 3 } },
                });
            var navHint = Text(Texts.Get("empty_nav"), "Hint");
            navHint.Margin = new Thickness(8, 4, 0, 0);
            nav.Children.Add(navHint);
            g.Children.Add(nav);
            var l1 = Line(true);
            Grid.SetColumn(l1, 1);
            g.Children.Add(l1);
            var col = new StackPanel { MaxWidth = 720, Margin = new Thickness(24, 48, 24, 24), HorizontalAlignment = HorizontalAlignment.Stretch };
            var scroll = new ScrollViewer { Content = col, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false };
            Grid.SetColumn(scroll, 2);
            g.Children.Add(scroll);
            col.Children.Add(Tape());
            col.Children.Add(Spaced(Text(Texts.Get("empty_title"), size: 28, weight: FontWeights.Bold), 24, 8));
            var body = Text(Texts.Get("empty_body"), "Hint");
            body.FontSize = 14;
            body.LineHeight = 21;
            col.Children.Add(body);
            col.Children.Add(Spaced(Text(Texts.Get("folder"), "Label"), 32, 8));
            var row = new DockPanel();
            copyBtn = Ghost(Texts.Get("copy"), (s, e) => CopyPath());
            copyBtn.Margin = new Thickness(8, 0, 0, 0);
            var open = Ghost(Texts.Get("open_folder"), (s, e) => OpenFolder());
            open.Margin = new Thickness(8, 0, 0, 0);
            DockPanel.SetDock(open, Dock.Right);
            DockPanel.SetDock(copyBtn, Dock.Right);
            row.Children.Add(open);
            row.Children.Add(copyBtn);
            row.Children.Add(new Border
            {
                Background = B("Ground700"), BorderBrush = B("Grid"), BorderThickness = new Thickness(1), Padding = new Thickness(12, 9, 12, 9),
                Child = new TextBlock { Text = folder, FontFamily = (FontFamily)Application.Current.Resources["Mono"], FontSize = 13, TextWrapping = TextWrapping.Wrap },
            });
            col.Children.Add(row);
            col.Children.Add(Spaced(Text(Texts.Get("how"), "Label"), 32, 12));
            var steps = new UniformGrid { Columns = 3 };
            var keys = new[] { ("s1", "s1d"), ("s2", "s2d"), ("s3", "s3d") };
            for (int i = 0; i < 3; i++)
            {
                var card = new StackPanel { Margin = new Thickness(16) };
                card.Children.Add(Text((i + 1).ToString("00", CultureInfo.InvariantCulture), "MonoText", B("Yellow"), weight: FontWeights.Bold));
                card.Children.Add(Spaced(Text(Texts.Get(keys[i].Item1), size: 14, weight: FontWeights.SemiBold), 8, 8));
                card.Children.Add(Text(Texts.Get(keys[i].Item2), "Hint"));
                steps.Children.Add(new Border
                {
                    Background = B("Ground700"), BorderBrush = B("Grid"), BorderThickness = new Thickness(1),
                    Margin = new Thickness(i == 0 ? 0 : 4, 0, i == 2 ? 0 : 4, 0), Child = card,
                });
            }
            col.Children.Add(steps);
            var bar = new DockPanel { Margin = new Thickness(16, 10, 12, 10) };
            var scan = Ghost(Texts.Get("rescan"), (s, e) => Rescan(), "Primary");
            scan.Margin = new Thickness(8, 0, 0, 0);
            DockPanel.SetDock(scan, Dock.Right);
            bar.Children.Add(scan);
            var change = Ghost(Texts.Get("change_folder"), (s, e) => ChangeFolder());
            DockPanel.SetDock(change, Dock.Right);
            bar.Children.Add(change);
            // the status takes what the buttons leave and wraps there (a
            // horizontal panel gave it all it asked for: the English line ran
            // under the buttons)
            var st = new DockPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
            var pulse = new Ellipse { Width = 8, Height = 8, Fill = B("Blue"), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(pulse, Dock.Left);
            pulse.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = (Color)Application.Current.Resources["CBlue"], BlurRadius = 8, ShadowDepth = 0 };
            pulse.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0.35, TimeSpan.FromSeconds(0.8))
            { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
            st.Children.Add(pulse);
            scanText = Text(Texts.Get(scans > 0 ? "rescanned" : "watching"), "MonoText", B("Blue"));
            scanText.TextWrapping = TextWrapping.Wrap;
            scanText.VerticalAlignment = VerticalAlignment.Center;
            st.Children.Add(scanText);
            bar.Children.Add(st);
            col.Children.Add(Spaced(new Border { BorderBrush = B("Grid"), BorderThickness = new Thickness(1), Child = bar }, 32, 0));
            return g;
        }

        FrameworkElement Tape()
        {
            // a "NO SIGNAL" tape, its text scrolling one unit each 5 s and
            // clipped by the tape's ends (the user: "no uprights at its sides,
            // just the band" -- the design's posts and ground line gone)
            var grid = new Grid { Height = 16, Margin = new Thickness(0, 8, 0, 8) };
            var tape = new Border { Height = 16, VerticalAlignment = VerticalAlignment.Top, Background = B("Yellow"), Opacity = 0.9, ClipToBounds = true };
            tape.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = (Color)Application.Current.Resources["CYellow"], BlurRadius = 14, ShadowDepth = 0, Opacity = 0.35 };
            var unit = Texts.Get("tape");
            var text = new TextBlock
            {
                Text = string.Concat(Enumerable.Repeat(unit, 16)), FontFamily = (FontFamily)Application.Current.Resources["Mono"],
                FontSize = 13, FontWeight = FontWeights.ExtraBold, Foreground = B("TapeInk"), VerticalAlignment = VerticalAlignment.Center,
            };
            var unitText = new FormattedText(unit, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(text.FontFamily, FontStyles.Normal, FontWeights.ExtraBold, FontStretches.Normal), 13, Brushes.Black, 1.0);
            double uw = unitText.WidthIncludingTrailingWhitespace;
            var move = new TranslateTransform();
            text.RenderTransform = move;
            move.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, -uw, TimeSpan.FromSeconds(5)) { RepeatBehavior = RepeatBehavior.Forever });
            tape.Child = new Canvas { Children = { text } };
            Canvas.SetTop(text, -1);
            grid.Children.Add(tape);
            return grid;
        }

        void CopyPath()
        {
            try { Clipboard.SetText(folder); } catch (Exception) { return; }
            copyBtn.Content = Texts.Get("copied");
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            t.Tick += (s, e) => { t.Stop(); copyBtn.Content = Texts.Get("copy"); };
            t.Start();
        }

        void OpenFolder()
        {
            try { Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true }); } catch (Exception) { }
        }

        void Rescan()
        {
            if (Protocol.FindMods(folder).Count > 0) { Load(); return; }
            scans++;
            if (scanText != null) scanText.Text = Texts.Get("rescanned");
        }

        void ChangeFolder()
        {
            var d = FolderPicker.Pick(this, Directory.Exists(folder) ? folder : null, Texts.Get("change_folder"));
            if (d == null) return;
            folder = d;
            scans = 0;
            var cfg = Environs.LoadConfig();
            cfg["game_dir"] = d;
            Environs.SaveConfig(cfg);
            state = null;
            Load();
        }

        // ---------------------------------------------------------------- the window's own frame
        void Min_Click(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);
        void Max_Click(object sender, RoutedEventArgs e) => ToggleMax();
        void ToggleMax()
        {
            if (WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(this);
            else SystemCommands.MaximizeWindow(this);
        }

        void Close_Click(object sender, RoutedEventArgs e)
        {
            if (writeTimer.IsEnabled) { writeTimer.Stop(); Write(); }
            SystemCommands.CloseWindow(this);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (writeTimer.IsEnabled) { writeTimer.Stop(); Write(); }
            base.OnClosing(e);
        }

        void PaintMax()
        {
            bool max = WindowState == WindowState.Maximized;
            MaxGlyph.Data = Geometry.Parse(max ? "M2.5,0.5 H9.5 V7.5 M0.5,2.5 H7.5 V9.5 H0.5 Z" : "M0.5,0.5 H9.5 V9.5 H0.5 Z");
            MaxBtn.ToolTip = Texts.Get(max ? "restore" : "maximize");
        }

        /// <summary>Maximized, a window with the system's frame reaches past the
        /// screen by its frame's width: its content held in by as much.</summary>
        void FitMaximized()
        {
            PaintMax();
            if (WindowState == WindowState.Maximized)
            {
                var src = PresentationSource.FromVisual(this);
                double sx = src?.CompositionTarget?.TransformFromDevice.M11 ?? 1;
                int px = GetSystemMetrics(32) + GetSystemMetrics(92);   // SM_CXSIZEFRAME + SM_CXPADDEDBORDER
                Frame.Margin = new Thickness(px * sx);
                Frame.BorderThickness = new Thickness(0);
            }
            else
            {
                Frame.Margin = new Thickness(0);
                Frame.BorderThickness = new Thickness(1);
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            (PresentationSource.FromVisual(this) as HwndSource)?.AddHook(Hook);
            acrylic = Backdrop.Enable(this);
            if (acrylic) Frame.Background = B("Ground900");   // the ground the window drew (its own is clear for the acrylic)
        }

        // ---------------------------------------------------------------- the peek
        // (the user, 10-22: while a setting is changed, everything but its
        // control see-through, to watch the game take it) -- from the change
        // until 2.5 s after the last one, and while the control is held. The
        // window is cut down to the control's rectangle (its region): the rest
        // is no longer the window, and what is behind shows as it is; the
        // control keeps the mouse, the keys and a dragged slider. (Clearing
        // the backdrop left the window black, blur-behind too, and WPF refuses
        // a layered style from outside: the user, 10-22.)
        void Peek(FrameworkElement control)
        {
            if (!control.IsVisible || !(PresentationSource.FromVisual(this) is HwndSource src)) return;
            peekEnd.Stop();
            peekEnd.Start();
            if (peekOf == control) return;
            peekOf = control;
            const double pad = 4;                     // the focus ring round a box
            var scale = src.CompositionTarget.TransformToDevice;
            var at = control.PointToScreen(new Point(0, 0));
            GetWindowRect(src.Handle, out var win);
            int x = (int)Math.Floor(at.X - win.Left - pad * scale.M11), y = (int)Math.Floor(at.Y - win.Top - pad * scale.M22);
            int r = (int)Math.Ceiling(at.X - win.Left + (control.ActualWidth + pad) * scale.M11);
            int b = (int)Math.Ceiling(at.Y - win.Top + (control.ActualHeight + pad) * scale.M22);
            if (acrylic) Backdrop.Pause(this, true);
            SetWindowRgn(src.Handle, CreateRectRgn(x, y, r, b), true);   // the system owns the region from here
        }

        void Unpeek()
        {
            if (peekOf != null && Mouse.LeftButton == MouseButtonState.Pressed && peekOf.IsMouseCaptureWithin) return;   // still held: the next tick
            peekEnd.Stop();
            peekOf = null;
            if (PresentationSource.FromVisual(this) is HwndSource src) SetWindowRgn(src.Handle, IntPtr.Zero, true);
            if (acrylic) Backdrop.Pause(this, false);
        }

        struct Rect32 { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out Rect32 r);
        [DllImport("user32.dll")] static extern int SetWindowRgn(IntPtr h, IntPtr region, bool redraw);
        [DllImport("gdi32.dll")] static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);

        // Windows 11's snap layouts open over a window's maximize button: the
        // system asks the window where its maximize button is (WM_NCHITTEST);
        // over ours it is told HTMAXBUTTON, and its clicks come as the frame's
        const int WM_NCHITTEST = 0x84, WM_NCMOUSELEAVE = 0x2A2, WM_NCLBUTTONDOWN = 0xA1, WM_NCLBUTTONUP = 0xA2, WM_NCMOUSEMOVE = 0xA0, HTMAXBUTTON = 9;
        bool overMax;

        IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WM_NCHITTEST:
                    if (OverMax(lParam)) { SetMaxHover(true); handled = true; return new IntPtr(HTMAXBUTTON); }
                    SetMaxHover(false);
                    break;
                case WM_NCMOUSELEAVE:
                    SetMaxHover(false);
                    break;
                case WM_NCMOUSEMOVE:
                    if (wParam.ToInt32() != HTMAXBUTTON) SetMaxHover(false);
                    break;
                case WM_NCLBUTTONDOWN:
                    if (wParam.ToInt32() == HTMAXBUTTON) { handled = true; return IntPtr.Zero; }
                    break;
                case WM_NCLBUTTONUP:
                    if (wParam.ToInt32() == HTMAXBUTTON) { handled = true; ToggleMax(); return IntPtr.Zero; }
                    break;
            }
            return IntPtr.Zero;
        }

        bool OverMax(IntPtr lParam)
        {
            if (!MaxBtn.IsVisible) return false;
            int v = unchecked((int)lParam.ToInt64());
            var p = new Point((short)(v & 0xFFFF), (short)((v >> 16) & 0xFFFF));
            try
            {
                var local = MaxBtn.PointFromScreen(p);
                return local.X >= 0 && local.Y >= 0 && local.X < MaxBtn.ActualWidth && local.Y < MaxBtn.ActualHeight;
            }
            catch (InvalidOperationException) { return false; }
        }

        void SetMaxHover(bool on)
        {
            if (on == overMax) return;
            overMax = on;
            MaxBtn.Background = on ? B("Grid") : Brushes.Transparent;
        }

        [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
    }
}
