namespace System.Windows
{
    public interface INameScope
    {
        object FindName(string name);

        void RegisterName(string name, object scopedElement);

        void UnregisterName(string name);
    }

    /// <summary>The names declared in a XAML file; the scope belongs to the root element of the file.</summary>
    public sealed class NameScope : INameScope
    {
        private readonly FrameworkElement _owner;

        private NameScope(FrameworkElement owner) => _owner = owner;

        public static readonly DependencyProperty NameScopeProperty =
            DependencyProperty.RegisterAttached("NameScope", typeof(INameScope), typeof(NameScope));

        /// <summary>The scope that <paramref name="element"/> belongs to.</summary>
        public static INameScope GetNameScope(DependencyObject element)
        {
            return element is FrameworkElement frameworkElement ? new NameScope(frameworkElement) : null;
        }

        public static void SetNameScope(DependencyObject element, INameScope value) { }

        public object FindName(string name) => _owner.FindName(name);

        public void RegisterName(string name, object scopedElement) => _owner.RegisterName(name, scopedElement);

        public void UnregisterName(string name) => _owner.UnregisterName(name);
    }
}
