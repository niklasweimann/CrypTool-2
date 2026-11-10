using System.Collections.Generic;

namespace System.Windows.Media.Animation
{
    /// <summary>
    /// Plays animations and timers. There is no render loop in the worker, so the host calls <see cref="Tick"/>
    /// regularly; each call moves every running animation to its value for the current time.
    /// </summary>
    public static class AnimationScheduler
    {
        private sealed class RunningAnimation
        {
            public AnimationTimeline Animation;
            public DependencyObject Target;
            public DependencyProperty Property;
            public object Origin;
            public double StartedAt;
        }

        private sealed class ScheduledAction
        {
            public double DueAt;
            public Action Action;
        }

        private static readonly List<RunningAnimation> Running = new List<RunningAnimation>();
        private static readonly List<ScheduledAction> Scheduled = new List<ScheduledAction>();

        /// <summary>Clock in milliseconds; replaceable in tests.</summary>
        public static Func<double> Now { get; set; } = () => Environment.TickCount64;

        /// <summary>True while something is animating or a timer is waiting (the host keeps ticking then).</summary>
        public static bool HasWork => Running.Count > 0 || Scheduled.Count > 0;

        internal static void Start(AnimationTimeline animation, DependencyObject target, DependencyProperty property)
        {
            Stop(target, property);
            Running.Add(new RunningAnimation
            {
                Animation = animation, Target = target, Property = property,
                Origin = target.GetValue(property), StartedAt = Now(),
            });
        }

        internal static void Stop(DependencyObject target, DependencyProperty property)
        {
            Running.RemoveAll(r => ReferenceEquals(r.Target, target) && r.Property == property);
        }

        /// <summary>Runs <paramref name="action"/> on a later tick, once <paramref name="delayMilliseconds"/> have passed.</summary>
        internal static object After(double delayMilliseconds, Action action)
        {
            var scheduled = new ScheduledAction { DueAt = Now() + Math.Max(0, delayMilliseconds), Action = action };
            Scheduled.Add(scheduled);
            return scheduled;
        }

        internal static void Cancel(object handle) => Scheduled.Remove(handle as ScheduledAction);

        /// <summary>Stops everything (when a workspace is stopped).</summary>
        public static void Reset()
        {
            Running.Clear();
            Scheduled.Clear();
        }

        public static void Tick()
        {
            double now = Now();
            var finished = new List<AnimationTimeline>();

            foreach (RunningAnimation running in Running.ToArray())
            {
                AnimationTimeline animation = running.Animation;
                double duration = animation.DurationMilliseconds;
                double elapsed = now - running.StartedAt - animation.BeginMilliseconds;
                if (elapsed < 0)
                {
                    continue;
                }

                double cycle = duration * (animation.AutoReverse ? 2 : 1);
                bool done = cycle <= 0 || (elapsed >= cycle && !animation.RepeatBehavior.IsForever);
                double position = done ? cycle : (cycle <= 0 ? 0 : elapsed % cycle);
                double progress = duration <= 0 ? 1 : position / duration;
                if (progress > 1)
                {
                    progress = 2 - progress;    // the way back of an auto-reversing animation
                }

                try
                {
                    running.Target.SetValue(running.Property, animation.ValueAt(Math.Clamp(progress, 0, 1), running.Origin));
                }
                catch (Exception)
                {
                    done = true;                // a value the property does not accept ends the animation
                }

                if (done)
                {
                    Running.Remove(running);
                    finished.Add(animation);
                }
            }

            foreach (AnimationTimeline animation in finished)
            {
                animation.RaiseCompleted();
            }

            foreach (ScheduledAction scheduled in Scheduled.ToArray())
            {
                if (scheduled.DueAt <= now && Scheduled.Remove(scheduled))
                {
                    scheduled.Action();
                }
            }
        }
    }
}
