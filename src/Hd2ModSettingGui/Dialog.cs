using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Hd2ModSettingGui
{
    /// <summary>The window's own message box (the user, 10-24: Windows's own
    /// did not match the window): its colours and buttons, over the window.
    /// Esc or the cancel button says no; the action is the yellow button.</summary>
    static class Dialog
    {
        static Brush B(string key) => (Brush)Application.Current.Resources[key];

        /// <summary>A question: true when the action is chosen.</summary>
        public static bool Ask(Window owner, string text, string action) => Show(owner, text, action, Texts.Get("cancel"));

        /// <summary>A message with one button.</summary>
        public static void Tell(Window owner, string text) => Show(owner, text, null, Texts.Get("ok"));

        static bool Show(Window owner, string text, string action, string dismiss)
        {
            var w = new Window
            {
                Owner = owner, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false,
                SizeToContent = SizeToContent.WidthAndHeight, WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = B("Ground700"), BorderBrush = B("Grid"), BorderThickness = new Thickness(1),
                FontFamily = (FontFamily)Application.Current.Resources["Ui"], Title = Texts.Get("window"),
                MinWidth = 380, MaxWidth = 560,
            };
            var col = new StackPanel();
            col.Children.Add(new Border { Height = 3, Background = B("Yellow") });      // the window's tape
            var body = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };
            col.Children.Add(body);
            body.Children.Add(new TextBlock
            {
                Text = text, Foreground = B("Ink"), FontSize = 15, LineHeight = 22, TextWrapping = TextWrapping.Wrap,
            });
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 24, 0, 0) };
            body.Children.Add(buttons);
            bool chosen = false;
            var no = new Button { Content = dismiss, Style = (Style)Application.Current.Resources["Ghost"], MinWidth = 96, IsCancel = true };
            no.Click += (s, e) => w.Close();
            buttons.Children.Add(no);
            if (action != null)
            {
                var yes = new Button { Content = action, Style = (Style)Application.Current.Resources["Primary"], MinWidth = 96, Margin = new Thickness(8, 0, 0, 0) };
                yes.Click += (s, e) => { chosen = true; w.Close(); };
                buttons.Children.Add(yes);
            }
            w.Content = col;
            // moved by any place that is not a button
            w.MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed) w.DragMove(); };
            w.Loaded += (s, e) => no.Focus();
            w.ShowDialog();
            return chosen;
        }
    }
}
