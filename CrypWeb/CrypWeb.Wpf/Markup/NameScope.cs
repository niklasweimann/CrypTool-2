using System.Collections.Generic;

namespace System.Windows
{
    public interface INameScope
    {
        object FindName(string name);

        void RegisterName(string name, object scopedElement);

        void UnregisterName(string name);
    }

    /// <summary>
    /// The names declared in a XAML file; the scope belongs to the root element of the file. A scope created by
    /// code (new NameScope(), e.g. for storyboards) keeps its names itself.
    /// </summary>
    public sealed class NameScope : INameScope
    {
        private readonly FrameworkElement _owner;
        private readonly Dictionary<string, object> _names;

        public NameScope() => _names = new Dictionary<string, object>();

        private NameScope(FrameworkElement owner) => _owner = owner;

        public static readonly DependencyProperty NameScopeProperty =
            DependencyProperty.RegisterAttached("NameScope", typeof(INameScope), typeof(NameScope));

        /// <summary>The scope that <paramref name="element"/> belongs to.</summary>
        public static INameScope GetNameScope(DependencyObject element)
        {
            return element is FrameworkElement frameworkElement ? new NameScope(frameworkElement) : null;
        }

        public static void SetNameScope(DependencyObject element, INameScope value) { }

        public object FindName(string name)
        {
            if (_owner != null)
            {
                return _owner.FindName(name);
            }
            return _names.TryGetValue(name, out object found) ? found : null;
        }

        public void RegisterName(string name, object scopedElement)
        {
            if (_owner != null)
            {
                _owner.RegisterName(name, scopedElement);
            }
            else
            {
                _names[name] = scopedElement;
            }
        }

        public void UnregisterName(string name)
        {
            if (_owner != null)
            {
                _owner.UnregisterName(name);
            }
            else
            {
                _names.Remove(name);
            }
        }
    }
}
