namespace System.Windows.Documents
{
    public abstract class Adorner : System.Windows.FrameworkElement
    {
        protected Adorner(System.Windows.UIElement adorned) { AdornedElement = adorned; }
        public System.Windows.UIElement AdornedElement { get; }
        public object Parent { get; set; }
        protected virtual System.Windows.Media.Visual GetVisualChild(int index) => null;
        protected virtual int VisualChildrenCount => 0;
        protected virtual System.Windows.Size MeasureOverride(System.Windows.Size s) => s;
        protected virtual System.Windows.Size ArrangeOverride(System.Windows.Size s) => s;
        public virtual System.Windows.Media.GeneralTransform GetDesiredTransform(System.Windows.Media.GeneralTransform t) => t;
    }
}
