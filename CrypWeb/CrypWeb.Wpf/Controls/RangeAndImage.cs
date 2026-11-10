using System.Windows.Media;

namespace System.Windows.Controls.Primitives
{
    /// <summary>A control with a numeric value between a minimum and a maximum.</summary>
    public abstract class RangeBase : Control
    {
        public static readonly DependencyProperty MinimumProperty = Dp.Register<double, RangeBase>(nameof(Minimum));
        public static readonly DependencyProperty MaximumProperty = Dp.Register<double, RangeBase>(nameof(Maximum), 1.0);
        public static readonly DependencyProperty ValueProperty = Dp.Register<double, RangeBase>(nameof(Value), 0.0, OnValueChanged);
        public static readonly DependencyProperty SmallChangeProperty = Dp.Register<double, RangeBase>(nameof(SmallChange), 0.1);
        public static readonly DependencyProperty LargeChangeProperty = Dp.Register<double, RangeBase>(nameof(LargeChange), 1.0);

        public static readonly RoutedEvent ValueChangedEvent = EventManager.RegisterRoutedEvent(
            "ValueChanged", RoutingStrategy.Bubble, typeof(RoutedPropertyChangedEventHandler<double>), typeof(RangeBase));

        public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
        public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
        public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
        public double SmallChange { get => (double)GetValue(SmallChangeProperty); set => SetValue(SmallChangeProperty, value); }
        public double LargeChange { get => (double)GetValue(LargeChangeProperty); set => SetValue(LargeChangeProperty, value); }

        public event RoutedPropertyChangedEventHandler<double> ValueChanged;

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var range = (RangeBase)d;
            var args = new RoutedPropertyChangedEventArgs<double>((double)e.OldValue, (double)e.NewValue) { Source = range, RoutedEvent = ValueChangedEvent };
            range.ValueChanged?.Invoke(range, args);
            range.RaiseEvent(args);
        }

        /// <summary>A value chosen in the browser (slider).</summary>
        internal void SetValueFromUser(double value)
        {
            if (GetStoredValue(ValueProperty) is Data.BindingExpression binding)
            {
                binding.WriteBack(this, value);
                RenderVersion.Bump();
            }
            else
            {
                Value = value;
            }
        }
    }

    public enum TickPlacement { None, TopLeft, BottomRight, Both }

    public class ScrollBar : RangeBase
    {
        public Orientation Orientation { get; set; } = Orientation.Vertical;
        public double ViewportSize { get; set; }
    }
}

namespace System.Windows.Controls
{
    using System.Windows.Controls.Primitives;

    public class ProgressBar : RangeBase
    {
        public static readonly DependencyProperty IsIndeterminateProperty = Dp.Register<bool, ProgressBar>(nameof(IsIndeterminate));
        public static readonly DependencyProperty OrientationProperty = Dp.Register<Orientation, ProgressBar>(nameof(Orientation), Orientation.Horizontal);

        public ProgressBar() => Maximum = 100;

        public bool IsIndeterminate { get => (bool)GetValue(IsIndeterminateProperty); set => SetValue(IsIndeterminateProperty, value); }
        public Orientation Orientation { get => (Orientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    }

    public class Slider : RangeBase
    {
        public static readonly DependencyProperty OrientationProperty = Dp.Register<Orientation, Slider>(nameof(Orientation), Orientation.Horizontal);
        public static readonly DependencyProperty TickFrequencyProperty = Dp.Register<double, Slider>(nameof(TickFrequency), 1.0);
        public static readonly DependencyProperty IsSnapToTickEnabledProperty = Dp.Register<bool, Slider>(nameof(IsSnapToTickEnabled));

        public Slider() => Maximum = 10;

        public Orientation Orientation { get => (Orientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
        public double TickFrequency { get => (double)GetValue(TickFrequencyProperty); set => SetValue(TickFrequencyProperty, value); }
        public bool IsSnapToTickEnabled { get => (bool)GetValue(IsSnapToTickEnabledProperty); set => SetValue(IsSnapToTickEnabledProperty, value); }
        public TickPlacement TickPlacement { get; set; }
        public DoubleCollection Ticks { get; set; }
        public bool IsMoveToPointEnabled { get; set; }
        public bool IsDirectionReversed { get; set; }
        public int AutoToolTipPrecision { get; set; }
        public object AutoToolTipPlacement { get; set; }
    }

    /// <summary>Shows a picture.</summary>
    public class Image : FrameworkElement
    {
        public static readonly DependencyProperty SourceProperty = Dp.Register<ImageSource, Image>(nameof(Source));
        public static readonly DependencyProperty StretchProperty = Dp.Register<Stretch, Image>(nameof(Stretch), Stretch.Uniform);

        public ImageSource Source { get => (ImageSource)GetValue(SourceProperty); set => SetValue(SourceProperty, value); }
        public Stretch Stretch { get => (Stretch)GetValue(StretchProperty); set => SetValue(StretchProperty, value); }
        public StretchDirection StretchDirection { get; set; } = StretchDirection.Both;
    }
}

namespace System.Windows.Media
{
    public enum BitmapScalingMode { Unspecified, LowQuality, HighQuality, Linear = 1, Fant = 2, NearestNeighbor = 3 }

    public enum EdgeMode { Unspecified, Aliased }

    /// <summary>Rendering hints; they have no effect in the browser.</summary>
    public static class RenderOptions
    {
        public static readonly DependencyProperty BitmapScalingModeProperty = DependencyProperty.RegisterAttached("BitmapScalingMode", typeof(BitmapScalingMode), typeof(RenderOptions));
        public static readonly DependencyProperty EdgeModeProperty = DependencyProperty.RegisterAttached("EdgeMode", typeof(EdgeMode), typeof(RenderOptions));

        public static void SetBitmapScalingMode(DependencyObject target, BitmapScalingMode value) => target.SetValue(BitmapScalingModeProperty, value);
        public static void SetEdgeMode(DependencyObject target, EdgeMode value) => target.SetValue(EdgeModeProperty, value);
    }
}
