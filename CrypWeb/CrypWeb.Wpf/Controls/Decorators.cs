using System.Collections.Generic;
using System.Windows.Media;

namespace System.Windows.Controls
{
    /// <summary>An element with exactly one child.</summary>
    [Markup.ContentProperty(nameof(Child))]
    public class Decorator : FrameworkElement
    {
        private UIElement _child;

        public virtual UIElement Child
        {
            get => _child;
            set
            {
                SetParent(_child, null);
                _child = value;
                SetParent(_child, this);
                RenderVersion.Bump();
            }
        }

        internal override IEnumerable<UIElement> ChildElements
        {
            get
            {
                if (_child != null)
                {
                    yield return _child;
                }
            }
        }
    }

    /// <summary>Draws a border and/or background around its child.</summary>
    public class Border : Decorator
    {
        public static readonly DependencyProperty BackgroundProperty = Panel.BackgroundProperty;
        public static readonly DependencyProperty BorderBrushProperty = Dp.Register<Brush, Border>(nameof(BorderBrush));
        public static readonly DependencyProperty BorderThicknessProperty = Dp.Register<Thickness, Border>(nameof(BorderThickness));
        public static readonly DependencyProperty CornerRadiusProperty = Dp.Register<CornerRadius, Border>(nameof(CornerRadius));
        public static readonly DependencyProperty PaddingProperty = Dp.Register<Thickness, Border>(nameof(Padding));

        public Brush Background { get => (Brush)GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }
        public Brush BorderBrush { get => (Brush)GetValue(BorderBrushProperty); set => SetValue(BorderBrushProperty, value); }
        public Thickness BorderThickness { get => (Thickness)GetValue(BorderThicknessProperty); set => SetValue(BorderThicknessProperty, value); }
        public CornerRadius CornerRadius { get => (CornerRadius)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
        public Thickness Padding { get => (Thickness)GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }
    }

    public enum StretchDirection { UpOnly, DownOnly, Both }

    /// <summary>Scales its child to the available space.</summary>
    public class Viewbox : Decorator
    {
        public static readonly DependencyProperty StretchProperty = Dp.Register<Stretch, Viewbox>(nameof(Stretch), Stretch.Uniform);

        public Stretch Stretch { get => (Stretch)GetValue(StretchProperty); set => SetValue(StretchProperty, value); }
        public StretchDirection StretchDirection { get; set; } = StretchDirection.Both;
    }
}

namespace System.Windows.Documents
{
    /// <summary>Layer for adorners; adorners are not drawn in the browser.</summary>
    public class AdornerDecorator : Controls.Decorator { }
}
