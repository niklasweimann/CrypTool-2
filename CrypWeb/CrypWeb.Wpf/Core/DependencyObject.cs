using System.Collections.Generic;
using System.Windows.Threading;

namespace System.Windows
{
    /// <summary>A value that is computed when it is read (a binding or a dynamic resource reference).</summary>
    public interface IDeferredValue
    {
        object Evaluate(DependencyObject target, DependencyProperty property);
    }

    /// <summary>
    /// Base of everything that carries dependency properties. Values are stored per instance; a change bumps
    /// <see cref="RenderVersion"/> so that the hosts know they have to draw again.
    /// </summary>
    public partial class DependencyObject
    {
        private readonly Dictionary<DependencyProperty, object> _values = new Dictionary<DependencyProperty, object>();

        public Dispatcher Dispatcher => Dispatcher.CurrentDispatcher;

        public bool CheckAccess() => true;

        public void VerifyAccess() { }

        public object GetValue(DependencyProperty dp)
        {
            if (HasStyleTriggers && TryGetTriggerValue(dp, out object triggered))
            {
                return triggered;
            }
            return GetValueIgnoringTriggers(dp);
        }

        /// <summary>
        /// The set (or default) value without asking triggers; for code that decides whether triggers apply (the
        /// control template, which may have triggers itself).
        /// </summary>
        internal object GetValueIgnoringTriggers(DependencyProperty dp)
        {
            if (_values.TryGetValue(dp, out object value))
            {
                return value is IDeferredValue deferred ? deferred.Evaluate(this, dp) : value;
            }
            return GetDefaultValue(dp);
        }

        /// <summary>True when a style with triggers applies (see <see cref="TryGetTriggerValue"/>).</summary>
        internal virtual bool HasStyleTriggers => false;

        /// <summary>The value an active style trigger gives the property, if any.</summary>
        internal virtual bool TryGetTriggerValue(DependencyProperty dp, out object value)
        {
            value = null;
            return false;
        }

        /// <summary>Value of a property that was never set. Elements override this for inherited properties.</summary>
        protected virtual object GetDefaultValue(DependencyProperty dp) => dp.Metadata.DefaultValue;

        /// <summary>
        /// The object this one belongs to when it is not an element itself (a brush of a rectangle, a gradient stop of a
        /// brush): bindings on it use that owner's data context, as WPF's "inheritance context" does.
        /// </summary>
        internal DependencyObject InheritanceContext { get; set; }

        /// <summary>The data context of this object: its own for elements, else the one of the element it belongs to.</summary>
        internal object InheritedDataContext()
        {
            for (DependencyObject current = this; current != null; current = current.InheritanceContext)
            {
                if (current is FrameworkElement element)
                {
                    return element.DataContext;
                }
            }
            return null;
        }

        public void SetValue(DependencyProperty dp, object value)
        {
            if (value is DependencyObject owned && !(owned is UIElement) && !ReferenceEquals(owned, this))
            {
                owned.InheritanceContext = this;
            }
            if (value is IDeferredValue)
            {
                _values[dp] = value;
                RenderVersion.Bump();
                return;
            }

            object oldValue = GetValue(dp);
            value = ValueCoercion.ToPropertyType(value, dp.PropertyType);
            if (dp.Metadata.CoerceValueCallback != null)
            {
                value = dp.Metadata.CoerceValueCallback(this, value);
            }

            _values[dp] = value;
            if (Equals(oldValue, value))
            {
                return;
            }

            RenderVersion.Bump();
            var args = new DependencyPropertyChangedEventArgs(dp, oldValue, value);
            dp.Metadata.PropertyChangedCallback?.Invoke(this, args);
            OnPropertyChanged(args);
        }

        public void SetValue(DependencyPropertyKey key, object value) => SetValue(key.DependencyProperty, value);

        public void SetCurrentValue(DependencyProperty dp, object value) => SetValue(dp, value);

        public void ClearValue(DependencyProperty dp)
        {
            if (_values.Remove(dp))
            {
                RenderVersion.Bump();
            }
        }

        public object ReadLocalValue(DependencyProperty dp)
        {
            return _values.TryGetValue(dp, out object value) ? value : DependencyProperty.UnsetValue;
        }

        /// <summary>True when the property has a local value (as opposed to its default).</summary>
        internal bool HasLocalValue(DependencyProperty dp) => _values.ContainsKey(dp);

        /// <summary>True when the property was set on this object or an active style trigger sets it (drawing uses this).</summary>
        internal bool HasValue(DependencyProperty dp) => _values.ContainsKey(dp) || (HasStyleTriggers && TryGetTriggerValue(dp, out _));

        /// <summary>The stored value without evaluating bindings (used to write a two-way binding back).</summary>
        internal object GetStoredValue(DependencyProperty dp) => _values.TryGetValue(dp, out object value) ? value : null;

        public void InvalidateProperty(DependencyProperty dp) => RenderVersion.Bump();

        public void CoerceValue(DependencyProperty dp) { }

        protected virtual void OnPropertyChanged(DependencyPropertyChangedEventArgs e) { }
    }
}
