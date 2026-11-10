using System.Collections.Generic;

namespace System.Windows.Media.Animation
{
    /// <summary>A group of animations, each aimed at a property of a (named) element.</summary>
    public class Storyboard : ParallelTimeline
    {
        public static readonly DependencyProperty TargetProperty = DependencyProperty.RegisterAttached("Target", typeof(DependencyObject), typeof(Storyboard));
        public static readonly DependencyProperty TargetNameProperty = DependencyProperty.RegisterAttached("TargetName", typeof(string), typeof(Storyboard));
        public static readonly DependencyProperty TargetPropertyProperty = DependencyProperty.RegisterAttached("TargetProperty", typeof(PropertyPath), typeof(Storyboard));

        private object _completion;

        public static void SetTarget(DependencyObject element, DependencyObject value) => element.SetValue(TargetProperty, value);

        public static DependencyObject GetTarget(DependencyObject element) => (DependencyObject)element.GetValue(TargetProperty);

        public static void SetTargetName(DependencyObject element, string name) => element.SetValue(TargetNameProperty, name);

        public static string GetTargetName(DependencyObject element) => (string)element.GetValue(TargetNameProperty);

        public static void SetTargetProperty(DependencyObject element, PropertyPath path) => element.SetValue(TargetPropertyProperty, path);

        public static PropertyPath GetTargetProperty(DependencyObject element) => (PropertyPath)element.GetValue(TargetPropertyProperty);

        public void Begin() => Begin(null);

        public void Begin(FrameworkElement containingObject, bool isControllable) => Begin(containingObject);

        public void Begin(FrameworkElement containingObject, HandoffBehavior handoffBehavior, bool isControllable) => Begin(containingObject);

        public void Begin(FrameworkElement containingObject)
        {
            StartChildren(this, containingObject, GetTarget(this), GetTargetName(this));
            AnimationScheduler.Cancel(_completion);
            _completion = AnimationScheduler.After(TotalMilliseconds, RaiseCompleted);
        }

        private static void StartChildren(TimelineGroup group, FrameworkElement scope, DependencyObject inheritedTarget, string inheritedName)
        {
            foreach (Timeline child in group.Children)
            {
                DependencyObject target = GetTarget(child) ?? inheritedTarget;
                string targetName = GetTargetName(child) ?? inheritedName;
                if (child is TimelineGroup nested)
                {
                    StartChildren(nested, scope, target, targetName);
                    continue;
                }

                if (child is not AnimationTimeline animation)
                {
                    continue;
                }
                target ??= string.IsNullOrEmpty(targetName) ? scope : scope?.FindName(targetName) as DependencyObject;
                string path = GetTargetProperty(child)?.Path;
                if (target != null && Markup.DependencyPropertyLookup.ResolvePath(target, path, out DependencyObject owner, out DependencyProperty property))
                {
                    animation.ApplyTo(owner, property);
                }
            }
        }

        public void Stop() => Stop(null);

        public void Stop(FrameworkElement containingObject)
        {
            AnimationScheduler.Cancel(_completion);
        }

        public void Pause() { }

        public void Pause(FrameworkElement containingObject) { }

        public void Resume() { }

        public void Resume(FrameworkElement containingObject) { }

        public void Remove() => Stop();

        public void Remove(FrameworkElement containingObject) => Stop();

        public void SkipToFill() { }

        public void SkipToFill(FrameworkElement containingObject) { }

        public void Seek(TimeSpan offset) { }

        public double? GetCurrentProgress() => 1;

        public double? GetCurrentProgress(FrameworkElement containingObject) => 1;

        public ClockState GetCurrentState() => ClockState.Filling;

        public ClockState GetCurrentState(FrameworkElement containingObject) => ClockState.Filling;

        public void SetSpeedRatio(double speedRatio) => SpeedRatio = speedRatio;

        public void SetSpeedRatio(FrameworkElement containingObject, double speedRatio) => SpeedRatio = speedRatio;

        public new Storyboard Clone() => (Storyboard)MemberwiseClone();
    }

    /// <summary>XAML trigger action that starts a storyboard; kept so that the XAML loads, never fired.</summary>
    [Markup.ContentProperty(nameof(Storyboard))]
    public class BeginStoryboard
    {
        public Storyboard Storyboard { get; set; }
        public string Name { get; set; }
    }
}

namespace System.Windows.Threading
{
    using System.Windows.Media.Animation;

    /// <summary>A timer whose ticks are delivered by the host's regular <see cref="AnimationScheduler.Tick"/> calls.</summary>
    public class DispatcherTimer
    {
        private object _pending;

        public DispatcherTimer() { }

        public DispatcherTimer(DispatcherPriority priority) { }

        public DispatcherTimer(DispatcherPriority priority, Dispatcher dispatcher) { }

        public DispatcherTimer(TimeSpan interval, DispatcherPriority priority, EventHandler callback, Dispatcher dispatcher)
        {
            Interval = interval;
            Tick += callback;
            Start();
        }

        public TimeSpan Interval { get; set; }

        public bool IsEnabled
        {
            get => _pending != null;
            set
            {
                if (value)
                {
                    Start();
                }
                else
                {
                    Stop();
                }
            }
        }

        public object Tag { get; set; }

        public Dispatcher Dispatcher => Dispatcher.CurrentDispatcher;

        public event EventHandler Tick;

        public void Start()
        {
            Stop();
            _pending = AnimationScheduler.After(Interval.TotalMilliseconds, OnDue);
        }

        public void Stop()
        {
            AnimationScheduler.Cancel(_pending);
            _pending = null;
        }

        private void OnDue()
        {
            _pending = AnimationScheduler.After(Interval.TotalMilliseconds, OnDue);
            Tick?.Invoke(this, EventArgs.Empty);
        }
    }
}
