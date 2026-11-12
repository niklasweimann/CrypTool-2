using System.Collections.ObjectModel;

namespace System.Windows.Media.Animation
{
    /// <summary>Base of objects whose properties can be animated (brushes, transforms, ...).</summary>
    public abstract class Animatable : Freezable
    {
        public void BeginAnimation(DependencyProperty dp, AnimationTimeline animation)
        {
            if (animation == null)
            {
                AnimationScheduler.Stop(this, dp);
            }
            else
            {
                animation.ApplyTo(this, dp);
            }
        }

        public void ApplyAnimationClock(DependencyProperty dp, AnimationClock clock) { }

        protected virtual Freezable CreateInstanceCore() => (Freezable)MemberwiseClone();
    }

    public enum FillBehavior { HoldEnd, Stop }

    public enum HandoffBehavior { SnapshotAndReplace, Compose }

    public struct RepeatBehavior
    {
        public RepeatBehavior(double count)
        {
            Count = count;
            IsForever = false;
        }

        public RepeatBehavior(TimeSpan duration) : this(1) { }

        public double Count { get; }

        internal bool IsForever { get; private set; }

        public static RepeatBehavior Forever => new RepeatBehavior(1) { IsForever = true };
    }

    /// <summary>State of a running animation as seen by custom animation classes.</summary>
    public class Clock
    {
        public double? CurrentProgress { get; internal set; }
        public TimeSpan? CurrentTime { get; internal set; }
        public Timeline Timeline { get; internal set; }
        public ClockController Controller { get; } = new ClockController();
        public ClockState CurrentState => ClockState.Filling;

        /// <summary>Clocks are not run separately; subscribe to the timeline's Completed instead. Never raised.</summary>
        public event EventHandler Completed;
        public event EventHandler CurrentTimeInvalidated;
    }

    public class AnimationClock : Clock { }

    /// <summary>Interactive control of a clock; clocks cannot be steered here, the calls are accepted and ignored.</summary>
    public sealed class ClockController
    {
        public double SpeedRatio { get; set; } = 1;
        public void Begin() { }
        public void Stop() { }
        public void Pause() { }
        public void Resume() { }
        public void Remove() { }
        public void SkipToFill() { }
        public void Seek(TimeSpan offset, TimeSeekOrigin origin) { }
    }

    public enum TimeSeekOrigin { BeginTime, Duration }

    public enum ClockState { Active, Filling, Stopped }

    public abstract class Timeline : Animatable
    {
        public Duration Duration { get; set; }
        public TimeSpan? BeginTime { get; set; }
        public bool AutoReverse { get; set; }
        public RepeatBehavior RepeatBehavior { get; set; } = new RepeatBehavior(1);
        public FillBehavior FillBehavior { get; set; }
        public double SpeedRatio { get; set; } = 1;
        public double AccelerationRatio { get; set; }
        public double DecelerationRatio { get; set; }
        public string Name { get; set; }

        public event EventHandler Completed;

        /// <summary>
        /// Raises <see cref="Completed"/>. As in WPF the sender is the timeline's clock (an AnimationClock for
        /// animations), finished: handlers tell animations apart by <c>((AnimationClock)sender).Timeline</c>.
        /// </summary>
        internal void RaiseCompleted()
        {
            Clock clock = this is AnimationTimeline ? new AnimationClock() : new Clock();
            clock.Timeline = this;
            clock.CurrentProgress = 1;
            Completed?.Invoke(clock, EventArgs.Empty);
        }

        /// <summary>Length of one run in milliseconds (WPF animates for one second when no duration is given).</summary>
        internal virtual double DurationMilliseconds
        {
            get
            {
                double ms = Duration.HasTimeSpan ? Duration.TimeSpan.TotalMilliseconds : 1000;
                return ms / (SpeedRatio > 0 ? SpeedRatio : 1);
            }
        }

        public Clock CreateClock() => new Clock { Timeline = this };

        internal double BeginMilliseconds => BeginTime?.TotalMilliseconds ?? 0;

        /// <summary>Time from the start until the timeline has finished (endless ones count as one run).</summary>
        internal virtual double TotalMilliseconds => BeginMilliseconds + DurationMilliseconds * (AutoReverse ? 2 : 1);

        public static void SetDesiredFrameRate(Timeline timeline, int? desiredFrameRate) { }

        public static readonly DependencyProperty DesiredFrameRateProperty =
            DependencyProperty.RegisterAttached("DesiredFrameRate", typeof(int?), typeof(Timeline));
    }

    /// <summary>A timeline that produces values for one property.</summary>
    public abstract class AnimationTimeline : Timeline
    {
        /// <summary>Starts animating <paramref name="dp"/> of <paramref name="target"/>.</summary>
        internal void ApplyTo(DependencyObject target, DependencyProperty dp) => AnimationScheduler.Start(this, target, dp);

        /// <summary>Value at <paramref name="progress"/> (0..1); <paramref name="origin"/> is the value when the animation started.</summary>
        internal abstract object ValueAt(double progress, object origin);

        public new AnimationClock CreateClock() => new AnimationClock { Timeline = this };
    }

    public interface IEasingFunction
    {
        double Ease(double normalizedTime);
    }

    public class TimelineCollection : Collection<Timeline> { }

    [Markup.ContentProperty(nameof(Children))]
    public abstract class TimelineGroup : Timeline
    {
        public TimelineCollection Children { get; set; } = new TimelineCollection();

        internal override double TotalMilliseconds
        {
            get
            {
                double longest = 0;
                foreach (Timeline child in Children)
                {
                    longest = Math.Max(longest, child.TotalMilliseconds);
                }
                return BeginMilliseconds + longest;
            }
        }
    }

    public class ParallelTimeline : TimelineGroup { }
}
