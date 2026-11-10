using System.Collections.Generic;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace System.Windows.Media
{
    /// <summary>Base of everything that is part of the drawn tree.</summary>
    public abstract class Visual : DependencyObject { }
}

namespace System.Windows
{
    /// <summary>An element of the tree: visibility, opacity, render transform and input events.</summary>
    public class UIElement : Visual, IInputElement
    {
        public static readonly DependencyProperty VisibilityProperty = Dp.Register<Visibility, UIElement>(nameof(Visibility), Visibility.Visible, OnVisibilityChanged);
        public static readonly DependencyProperty OpacityProperty = Dp.Register<double, UIElement>(nameof(Opacity), 1.0);
        public static readonly DependencyProperty IsEnabledProperty = Dp.Register<bool, UIElement>(nameof(IsEnabled), true);
        public static readonly DependencyProperty IsHitTestVisibleProperty = Dp.Register<bool, UIElement>(nameof(IsHitTestVisible), true);
        public static readonly DependencyProperty RenderTransformProperty = Dp.Register<Transform, UIElement>(nameof(RenderTransform));
        public static readonly DependencyProperty RenderTransformOriginProperty = Dp.Register<Point, UIElement>(nameof(RenderTransformOrigin));
        public static readonly DependencyProperty ClipToBoundsProperty = Dp.Register<bool, UIElement>(nameof(ClipToBounds));
        public static readonly DependencyProperty ClipProperty = Dp.Register<Geometry, UIElement>(nameof(Clip));
        public static readonly DependencyProperty OpacityMaskProperty = Dp.Register<Brush, UIElement>(nameof(OpacityMask));
        public static readonly DependencyProperty FocusableProperty = Dp.Register<bool, UIElement>(nameof(Focusable));
        public static readonly DependencyProperty SnapsToDevicePixelsProperty = Dp.Register<bool, UIElement>(nameof(SnapsToDevicePixels));
        public static readonly DependencyProperty AllowDropProperty = Dp.Register<bool, UIElement>(nameof(AllowDrop));
        public static readonly DependencyProperty EffectProperty = Dp.Register<object, UIElement>(nameof(Effect));

        private Dictionary<RoutedEvent, List<Delegate>> _routedHandlers;

        public Visibility Visibility { get => (Visibility)GetValue(VisibilityProperty); set => SetValue(VisibilityProperty, value); }
        public double Opacity { get => (double)GetValue(OpacityProperty); set => SetValue(OpacityProperty, value); }
        public bool IsEnabled { get => (bool)GetValue(IsEnabledProperty); set => SetValue(IsEnabledProperty, value); }
        public bool IsHitTestVisible { get => (bool)GetValue(IsHitTestVisibleProperty); set => SetValue(IsHitTestVisibleProperty, value); }
        public Transform RenderTransform { get => (Transform)GetValue(RenderTransformProperty); set => SetValue(RenderTransformProperty, value); }
        public Point RenderTransformOrigin { get => (Point)GetValue(RenderTransformOriginProperty); set => SetValue(RenderTransformOriginProperty, value); }
        public bool ClipToBounds { get => (bool)GetValue(ClipToBoundsProperty); set => SetValue(ClipToBoundsProperty, value); }
        public Geometry Clip { get => (Geometry)GetValue(ClipProperty); set => SetValue(ClipProperty, value); }
        public Brush OpacityMask { get => (Brush)GetValue(OpacityMaskProperty); set => SetValue(OpacityMaskProperty, value); }
        public bool Focusable { get => (bool)GetValue(FocusableProperty); set => SetValue(FocusableProperty, value); }
        public bool SnapsToDevicePixels { get => (bool)GetValue(SnapsToDevicePixelsProperty); set => SetValue(SnapsToDevicePixelsProperty, value); }
        public bool AllowDrop { get => (bool)GetValue(AllowDropProperty); set => SetValue(AllowDropProperty, value); }
        public object Effect { get => GetValue(EffectProperty); set => SetValue(EffectProperty, value); }

        /// <summary>The element this one is a child of (logical and visual tree are the same here).</summary>
        internal UIElement ParentElement { get; private set; }

        public bool IsVisible
        {
            get
            {
                for (UIElement e = this; e != null; e = e.ParentElement)
                {
                    if (e.Visibility != Visibility.Visible)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        public bool IsMouseOver => false;
        public bool IsFocused => false;
        public bool IsKeyboardFocused => false;
        public bool IsMouseCaptured => false;

        /// <summary>Size measured by the browser, if it was reported back; otherwise empty.</summary>
        public Size RenderSize { get; set; }

        public Size DesiredSize => RenderSize;

        // ---- events -------------------------------------------------------------------------------------------

        public event MouseButtonEventHandler MouseDown;
        public event MouseButtonEventHandler MouseUp;
        public event MouseButtonEventHandler MouseLeftButtonDown;
        public event MouseButtonEventHandler MouseLeftButtonUp;
        public event MouseButtonEventHandler MouseRightButtonDown;
        public event MouseButtonEventHandler MouseRightButtonUp;
        public event MouseButtonEventHandler PreviewMouseDown;
        public event MouseButtonEventHandler PreviewMouseUp;
        public event MouseButtonEventHandler PreviewMouseLeftButtonDown;
        public event MouseButtonEventHandler PreviewMouseLeftButtonUp;
        public event MouseButtonEventHandler PreviewMouseRightButtonDown;
        public event MouseEventHandler MouseEnter;
        public event MouseEventHandler MouseLeave;
        public event MouseEventHandler MouseMove;
        public event MouseEventHandler PreviewMouseMove;
        public event MouseWheelEventHandler MouseWheel;
        public event MouseWheelEventHandler PreviewMouseWheel;
        public event KeyEventHandler KeyDown;
        public event KeyEventHandler KeyUp;
        public event KeyEventHandler PreviewKeyDown;
        public event KeyEventHandler PreviewKeyUp;
        public event TextCompositionEventHandler PreviewTextInput;
        public event TextCompositionEventHandler TextInput;
        public event RoutedEventHandler GotFocus;
        public event RoutedEventHandler LostFocus;
        public event DependencyPropertyChangedEventHandler IsVisibleChanged;
        public event DependencyPropertyChangedEventHandler IsEnabledChanged;

        /// <summary>True when the browser has to report clicks on this element.</summary>
        internal virtual bool WantsClicks =>
            MouseDown != null || MouseUp != null || MouseLeftButtonDown != null || MouseLeftButtonUp != null
            || PreviewMouseDown != null || PreviewMouseUp != null || PreviewMouseLeftButtonDown != null || PreviewMouseLeftButtonUp != null;

        internal bool WantsHover => MouseEnter != null || MouseLeave != null;

        /// <summary>A click reported by the browser: press and release, bubbling to the ancestors until handled.</summary>
        internal void RaiseClick(Point position, int clickCount)
        {
            var args = new MouseButtonEventArgs
            {
                Source = this, OriginalSource = this, Position = position, ClickCount = clickCount,
                ChangedButton = MouseButton.Left, ButtonState = MouseButtonState.Pressed, LeftButton = MouseButtonState.Pressed,
            };
            for (UIElement e = this; e != null && !args.Handled; e = e.ParentElement)
            {
                e.PreviewMouseDown?.Invoke(e, args);
                e.PreviewMouseLeftButtonDown?.Invoke(e, args);
                e.MouseDown?.Invoke(e, args);
                e.MouseLeftButtonDown?.Invoke(e, args);
            }

            args = new MouseButtonEventArgs
            {
                Source = this, OriginalSource = this, Position = position, ClickCount = clickCount,
                ChangedButton = MouseButton.Left, ButtonState = MouseButtonState.Released,
            };
            for (UIElement e = this; e != null && !args.Handled; e = e.ParentElement)
            {
                e.PreviewMouseUp?.Invoke(e, args);
                e.PreviewMouseLeftButtonUp?.Invoke(e, args);
                e.MouseUp?.Invoke(e, args);
                e.MouseLeftButtonUp?.Invoke(e, args);
                e.OnClicked(args);
            }
        }

        /// <summary>Called on the clicked element and its ancestors; buttons raise <c>Click</c> here.</summary>
        internal virtual void OnClicked(MouseButtonEventArgs e) { }

        internal void RaiseHover(bool entered)
        {
            var args = new MouseEventArgs { Source = this, OriginalSource = this };
            (entered ? MouseEnter : MouseLeave)?.Invoke(this, args);
        }

        private static void OnVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var element = (UIElement)d;
            bool wasVisible = (Visibility)e.OldValue == Visibility.Visible;
            bool isVisible = (Visibility)e.NewValue == Visibility.Visible;
            if (wasVisible != isVisible)
            {
                element.IsVisibleChanged?.Invoke(element, new DependencyPropertyChangedEventArgs(VisibilityProperty, wasVisible, isVisible));
            }
        }

        public void AddHandler(RoutedEvent routedEvent, Delegate handler) => AddHandler(routedEvent, handler, false);

        public void AddHandler(RoutedEvent routedEvent, Delegate handler, bool handledEventsToo)
        {
            if (routedEvent == null || handler == null)
            {
                return;
            }
            _routedHandlers ??= new Dictionary<RoutedEvent, List<Delegate>>();
            if (!_routedHandlers.TryGetValue(routedEvent, out List<Delegate> list))
            {
                _routedHandlers[routedEvent] = list = new List<Delegate>();
            }
            list.Add(handler);
        }

        public void RemoveHandler(RoutedEvent routedEvent, Delegate handler)
        {
            if (routedEvent != null && _routedHandlers != null && _routedHandlers.TryGetValue(routedEvent, out List<Delegate> list))
            {
                list.Remove(handler);
            }
        }

        public void RaiseEvent(RoutedEventArgs e)
        {
            if (e?.RoutedEvent == null)
            {
                return;
            }
            e.Source ??= this;
            for (UIElement element = this; element != null && !e.Handled; element = element.ParentElement)
            {
                if (element._routedHandlers != null && element._routedHandlers.TryGetValue(e.RoutedEvent, out List<Delegate> list))
                {
                    foreach (Delegate handler in list.ToArray())
                    {
                        handler.DynamicInvoke(element, e);
                    }
                }
            }
        }

        // ---- tree ---------------------------------------------------------------------------------------------

        /// <summary>Makes <paramref name="child"/> a child of this element (or detaches it when called on null).</summary>
        internal static void SetParent(object child, UIElement parent)
        {
            if (child is UIElement element && !ReferenceEquals(element.ParentElement, parent))
            {
                element.ParentElement = parent;
                RenderVersion.Bump();
            }
        }

        /// <summary>The children that are drawn, in drawing order.</summary>
        internal virtual IEnumerable<UIElement> ChildElements => Array.Empty<UIElement>();

        // ---- layout is done by the browser; these exist so that code-behind compiles and runs --------------------

        public void Measure(Size availableSize) { }

        public void Arrange(Rect finalRect) { }

        public void UpdateLayout() { }

        public void InvalidateVisual() => RenderVersion.Bump();

        public void InvalidateMeasure() => RenderVersion.Bump();

        public void InvalidateArrange() => RenderVersion.Bump();

        public bool Focus() => false;

        public bool CaptureMouse() => true;

        public void ReleaseMouseCapture() { }

        public Point TranslatePoint(Point point, UIElement relativeTo) => point;

        /// <summary>Animations are not played yet: the property jumps to the end value of the animation.</summary>
        public void BeginAnimation(DependencyProperty dp, AnimationTimeline animation)
        {
            animation?.ApplyTo(this, dp);
        }

        public void ApplyAnimationClock(DependencyProperty dp, AnimationClock clock) { }
    }
}
