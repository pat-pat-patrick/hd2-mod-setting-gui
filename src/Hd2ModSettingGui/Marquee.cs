using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Hd2ModSettingGui
{
    /// <summary>A name in a list that may be longer than its room (the user,
    /// 10-24: a mod's long English name, no wider column). It keeps its width:
    /// longer, its right end fades out; while active (hovered, or the item
    /// chosen) it pans to its end and back, pausing at each.</summary>
    sealed class Marquee : Panel
    {
        const double Speed = 40, Pause = 1.2, Fade = 24;      // DIPs a second, seconds, DIPs
        readonly TextBlock text;
        bool active;
        double running = -1;                                  // the overflow the panning runs for; -1: none

        public static readonly DependencyProperty OffsetProperty = DependencyProperty.Register(
            "Offset", typeof(double), typeof(Marquee), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsArrange));

        double Offset { get => (double)GetValue(OffsetProperty); set => SetValue(OffsetProperty, value); }

        public Marquee(TextBlock t)
        {
            text = t;
            t.TextWrapping = TextWrapping.NoWrap;
            Children.Add(t);
            ClipToBounds = true;
            SizeChanged += (s, e) => Update();
        }

        public bool Active
        {
            get => active;
            set { if (active == value) return; active = value; Update(); }
        }

        protected override Size MeasureOverride(Size avail)
        {
            text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return new Size(Math.Min(text.DesiredSize.Width, avail.Width), text.DesiredSize.Height);
        }

        protected override Size ArrangeOverride(Size size)
        {
            text.Arrange(new Rect(-Offset, 0, text.DesiredSize.Width, size.Height));
            return size;
        }

        void Update()
        {
            double over = text.DesiredSize.Width - ActualWidth;
            if (over <= 0.5)
            {
                Stop();
                OpacityMask = null;
                return;
            }
            if (!active)
            {
                Stop();
                OpacityMask = new LinearGradientBrush(new GradientStopCollection
                {
                    new GradientStop(Colors.Black, 0),
                    new GradientStop(Colors.Black, Math.Max(0, 1 - Fade / ActualWidth)),
                    new GradientStop(Colors.Transparent, 1),
                }, 0);
                return;
            }
            OpacityMask = null;
            if (Math.Abs(running - over) < 0.5) return;
            running = over;
            double go = over / Speed;
            var a = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever };
            a.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            a.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(Pause))));
            a.KeyFrames.Add(new EasingDoubleKeyFrame(over, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(Pause + go)), new SineEase()));
            a.KeyFrames.Add(new LinearDoubleKeyFrame(over, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2 * Pause + go))));
            a.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2 * Pause + 2 * go)), new SineEase()));
            BeginAnimation(OffsetProperty, a);
        }

        void Stop()
        {
            if (running < 0) return;
            running = -1;
            BeginAnimation(OffsetProperty, null);
            Offset = 0;
        }
    }
}
