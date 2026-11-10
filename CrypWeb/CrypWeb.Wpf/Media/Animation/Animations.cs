using System.Collections.ObjectModel;

namespace System.Windows.Media.Animation
{
    /// <summary>Base of animations of <c>double</c> properties; custom animations override <see cref="GetCurrentValueCore"/>.</summary>
    public abstract class DoubleAnimationBase : AnimationTimeline
    {
        protected abstract double GetCurrentValueCore(double defaultOriginValue, double defaultDestinationValue, AnimationClock animationClock);

        internal override object ValueAt(double progress, object origin)
        {
            double start = origin is double d && !double.IsNaN(d) ? d : 0;
            var clock = new AnimationClock { CurrentProgress = progress, CurrentTime = TimeSpan.FromMilliseconds(progress * DurationMilliseconds) };
            return GetCurrentValueCore(start, start, clock);
        }
    }

    public class DoubleAnimation : DoubleAnimationBase
    {
        public DoubleAnimation() { }

        public DoubleAnimation(double toValue, Duration duration)
        {
            To = toValue;
            Duration = duration;
        }

        public DoubleAnimation(double fromValue, double toValue, Duration duration)
        {
            From = fromValue;
            To = toValue;
            Duration = duration;
        }

        public DoubleAnimation(double fromValue, double toValue, Duration duration, FillBehavior fillBehavior) : this(fromValue, toValue, duration)
        {
            FillBehavior = fillBehavior;
        }

        public double? From { get; set; }
        public double? To { get; set; }
        public double? By { get; set; }
        public IEasingFunction EasingFunction { get; set; }

        protected override double GetCurrentValueCore(double defaultOriginValue, double defaultDestinationValue, AnimationClock animationClock)
        {
            double from = From ?? defaultOriginValue;
            double to = To ?? (By.HasValue ? from + By.Value : defaultDestinationValue);
            double progress = animationClock.CurrentProgress ?? 1;
            if (EasingFunction != null)
            {
                progress = EasingFunction.Ease(progress);
            }
            return from + (to - from) * progress;
        }
    }

    public class ColorAnimation : AnimationTimeline
    {
        public ColorAnimation() { }

        public ColorAnimation(Color toValue, Duration duration)
        {
            To = toValue;
            Duration = duration;
        }

        public ColorAnimation(Color fromValue, Color toValue, Duration duration)
        {
            From = fromValue;
            To = toValue;
            Duration = duration;
        }

        public Color? From { get; set; }
        public Color? To { get; set; }
        public IEasingFunction EasingFunction { get; set; }

        internal override object ValueAt(double progress, object origin)
        {
            Color from = From ?? (origin is Color c ? c : Colors.Transparent);
            Color to = To ?? from;
            static byte Mix(byte a, byte b, double t) => (byte)Math.Round(a + (b - a) * t);
            return Color.FromArgb(Mix(from.A, to.A, progress), Mix(from.R, to.R, progress), Mix(from.G, to.G, progress), Mix(from.B, to.B, progress));
        }
    }

    public class PointAnimation : AnimationTimeline
    {
        public Point? From { get; set; }
        public Point? To { get; set; }

        internal override object ValueAt(double progress, object origin)
        {
            Point from = From ?? (origin is Point p ? p : default);
            Point to = To ?? from;
            return new Point(from.X + (to.X - from.X) * progress, from.Y + (to.Y - from.Y) * progress);
        }
    }

    public class ThicknessAnimation : AnimationTimeline
    {
        public Thickness? From { get; set; }
        public Thickness? To { get; set; }

        internal override object ValueAt(double progress, object origin)
        {
            Thickness from = From ?? (origin is Thickness t ? t : default);
            Thickness to = To ?? from;
            static double Mix(double a, double b, double t) => a + (b - a) * t;
            return new Thickness(Mix(from.Left, to.Left, progress), Mix(from.Top, to.Top, progress), Mix(from.Right, to.Right, progress), Mix(from.Bottom, to.Bottom, progress));
        }
    }

    public struct KeyTime
    {
        internal TimeSpan? Time { get; private set; }

        public static KeyTime FromTimeSpan(TimeSpan timeSpan) => new KeyTime { Time = timeSpan };

        public static KeyTime Uniform => default;

        public static implicit operator KeyTime(TimeSpan timeSpan) => FromTimeSpan(timeSpan);
    }

    /// <summary>A value that an animation reaches at a point in time.</summary>
    public abstract class KeyFrame<T> : Freezable
    {
        public KeyTime KeyTime { get; set; }
        public T Value { get; set; }

        /// <summary>True when the value jumps at the key time instead of being interpolated towards it.</summary>
        internal virtual bool IsDiscrete => false;
    }

    public abstract class DoubleKeyFrame : KeyFrame<double> { }

    public class LinearDoubleKeyFrame : DoubleKeyFrame
    {
        public LinearDoubleKeyFrame() { }

        public LinearDoubleKeyFrame(double value, KeyTime keyTime)
        {
            Value = value;
            KeyTime = keyTime;
        }
    }

    public class EasingDoubleKeyFrame : LinearDoubleKeyFrame
    {
        public IEasingFunction EasingFunction { get; set; }
    }

    public class SplineDoubleKeyFrame : LinearDoubleKeyFrame
    {
        public object KeySpline { get; set; }
    }

    public class DiscreteDoubleKeyFrame : DoubleKeyFrame
    {
        internal override bool IsDiscrete => true;
    }

    [Markup.ContentProperty(nameof(KeyFrames))]
    public class DoubleAnimationUsingKeyFrames : AnimationTimeline
    {
        public Collection<DoubleKeyFrame> KeyFrames { get; } = new Collection<DoubleKeyFrame>();

        internal override double DurationMilliseconds
        {
            get
            {
                if (Duration.HasTimeSpan)
                {
                    return base.DurationMilliseconds;
                }
                double last = 0;
                foreach (DoubleKeyFrame frame in KeyFrames)
                {
                    last = Math.Max(last, frame.KeyTime.Time?.TotalMilliseconds ?? 0);
                }
                return last > 0 ? last : 1000;
            }
        }

        internal override object ValueAt(double progress, object origin)
        {
            double now = progress * DurationMilliseconds;
            double previousValue = origin is double d && !double.IsNaN(d) ? d : 0;
            double previousTime = 0;
            foreach (DoubleKeyFrame frame in KeyFrames)
            {
                double time = frame.KeyTime.Time?.TotalMilliseconds ?? DurationMilliseconds;
                if (now < time)
                {
                    if (frame.IsDiscrete || time <= previousTime)
                    {
                        return previousValue;
                    }
                    return previousValue + (frame.Value - previousValue) * (now - previousTime) / (time - previousTime);
                }
                previousValue = frame.Value;
                previousTime = time;
            }
            return previousValue;
        }
    }

    public abstract class ObjectKeyFrame : KeyFrame<object> { }

    public class DiscreteObjectKeyFrame : ObjectKeyFrame
    {
        public DiscreteObjectKeyFrame() { }

        public DiscreteObjectKeyFrame(object value, KeyTime keyTime)
        {
            Value = value;
            KeyTime = keyTime;
        }

        internal override bool IsDiscrete => true;
    }

    [Markup.ContentProperty(nameof(KeyFrames))]
    public class ObjectAnimationUsingKeyFrames : AnimationTimeline
    {
        public Collection<ObjectKeyFrame> KeyFrames { get; } = new Collection<ObjectKeyFrame>();

        internal override object ValueAt(double progress, object origin)
        {
            double now = progress * DurationMilliseconds;
            object value = origin;
            foreach (ObjectKeyFrame frame in KeyFrames)
            {
                if ((frame.KeyTime.Time?.TotalMilliseconds ?? 0) <= now)
                {
                    value = frame.Value;
                }
            }
            return value;
        }
    }
}
