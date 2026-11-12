using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Data;

namespace System.Windows
{
    /// <summary>
    /// Style triggers: setters that apply while a condition holds. They are evaluated when a property is read
    /// (<see cref="DependencyObject.GetValue"/> asks <see cref="FrameworkElement.TryGetTriggerValue"/>), so they
    /// follow the element's state without change tracking. A trigger value wins over the style's own setters but
    /// not over a value the element sets itself, as in WPF.
    /// The browser reports IsMouseOver and IsPressed for elements whose triggers depend on them (see
    /// FrameworkElement.HasTriggerOn); IsFocused is always false.
    /// </summary>
    public abstract class TriggerBase : DependencyObject
    {
        /// <summary>True when the trigger's condition holds for <paramref name="element"/>.</summary>
        internal abstract bool IsActive(FrameworkElement element);

        /// <summary>The setters that apply while the trigger is active.</summary>
        internal abstract IEnumerable<SetterBase> ActiveSetters { get; }
    }

    /// <summary>Active while a property of the styled element has a value.</summary>
    [Markup.ContentProperty(nameof(Setters))]
    public class Trigger : TriggerBase
    {
        public DependencyProperty Property { get; set; }

        /// <summary>Property name as written in XAML; resolved against the styled element.</summary>
        internal string PropertyName { get; set; }

        public object Value { get; set; }

        public string SourceName { get; set; }

        public Collection<SetterBase> Setters { get; } = new Collection<SetterBase>();

        internal override IEnumerable<SetterBase> ActiveSetters => Setters;

        internal override bool IsActive(FrameworkElement element) =>
            TriggerCondition.PropertyMatches(element, Property, PropertyName, Value);
    }

    /// <summary>Active while a binding (evaluated on the styled element) has a value.</summary>
    [Markup.ContentProperty(nameof(Setters))]
    public class DataTrigger : TriggerBase
    {
        public BindingBase Binding { get; set; }

        public object Value { get; set; }

        public Collection<SetterBase> Setters { get; } = new Collection<SetterBase>();

        internal override IEnumerable<SetterBase> ActiveSetters => Setters;

        internal override bool IsActive(FrameworkElement element) =>
            TriggerCondition.BindingMatches(element, Binding, Value);
    }

    /// <summary>One condition of a <see cref="MultiTrigger"/> or <see cref="MultiDataTrigger"/>.</summary>
    public class Condition
    {
        public DependencyProperty Property { get; set; }

        internal string PropertyName { get; set; }

        public BindingBase Binding { get; set; }

        public object Value { get; set; }

        public string SourceName { get; set; }

        internal bool Holds(FrameworkElement element) => Binding != null
            ? TriggerCondition.BindingMatches(element, Binding, Value)
            : TriggerCondition.PropertyMatches(element, Property, PropertyName, Value);
    }

    /// <summary>Active while all its property conditions hold.</summary>
    [Markup.ContentProperty(nameof(Setters))]
    public class MultiTrigger : TriggerBase
    {
        public Collection<Condition> Conditions { get; } = new Collection<Condition>();

        public Collection<SetterBase> Setters { get; } = new Collection<SetterBase>();

        internal override IEnumerable<SetterBase> ActiveSetters => Setters;

        internal override bool IsActive(FrameworkElement element)
        {
            foreach (Condition condition in Conditions)
            {
                if (!condition.Holds(element)) return false;
            }
            return Conditions.Count > 0;
        }
    }

    /// <summary>Active while all its binding conditions hold.</summary>
    [Markup.ContentProperty(nameof(Setters))]
    public class MultiDataTrigger : MultiTrigger { }

    /// <summary>Comparing the current value of a trigger condition with the value written in XAML.</summary>
    internal static class TriggerCondition
    {
        // a stand-in property to evaluate a data trigger's binding as "object"
        private static readonly DependencyProperty ProbeProperty = DependencyProperty.RegisterAttached("TriggerProbe", typeof(object), typeof(TriggerCondition));

        // triggers can depend on properties that other triggers set: do not evaluate them recursively
        [ThreadStatic] private static int _depth;

        /// <summary>
        /// True when the element's property has the expected value. The CLR property is read when there is one,
        /// because some states are computed rather than stored (TabItem.IsSelected, IsMouseOver, IsPressed).
        /// </summary>
        internal static bool PropertyMatches(FrameworkElement element, DependencyProperty property, string propertyName, object expected)
        {
            if (_depth > 0) return false;
            string name = ShortName(property?.Name ?? propertyName);
            if (string.IsNullOrEmpty(name)) return false;
            _depth++;
            try
            {
                System.Reflection.PropertyInfo clrProperty = element.GetType().GetProperty(name);
                if (clrProperty != null && clrProperty.GetIndexParameters().Length == 0)
                {
                    return ValuesMatch(clrProperty.GetValue(element), expected);
                }
                property ??= Markup.DependencyPropertyLookup.Find(element.GetType(), propertyName);
                return property != null && ValuesMatch(element.GetValue(property), expected);
            }
            finally { _depth--; }
        }

        /// <summary>True when a trigger condition (a property or its XAML name) is about <paramref name="name"/>.</summary>
        internal static bool NamesProperty(DependencyProperty property, string propertyName, string name) =>
            ShortName(property?.Name ?? propertyName) == name;

        // "Button.IsPressed" -> "IsPressed"
        private static string ShortName(string name) =>
            name == null ? null : name.Substring(name.LastIndexOf('.') + 1);

        internal static bool BindingMatches(FrameworkElement element, BindingBase binding, object expected)
        {
            if (binding == null || _depth > 0) return false;
            _depth++;
            try { return ValuesMatch(binding.CreateExpression().Evaluate(element, ProbeProperty), expected); }
            catch (Exception) { return false; }
            finally { _depth--; }
        }

        /// <summary>Equal values; XAML gives the expected value as text, so it is converted to the actual value's type.</summary>
        internal static bool ValuesMatch(object actual, object expected)
        {
            if (Equals(actual, expected)) return true;
            if (expected is string text)
            {
                if (actual == null) return text.Length == 0 || text == "{x:Null}";
                if (actual is string) return false;
                if (Markup.XamlValueConverter.TryConvert(text, actual.GetType(), out object converted) && Equals(actual, converted)) return true;
                return string.Equals(Convert.ToString(actual, CultureInfo.InvariantCulture), text, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }
    }
}

namespace System.Windows
{
    using System.Reflection;
    using System.Windows.Media.Animation;

    /// <summary>Something a trigger does when it fires (begin a storyboard, ...).</summary>
    public abstract class TriggerAction : DependencyObject
    {
        /// <summary>Carries out the action for the element the trigger belongs to.</summary>
        internal abstract void Invoke(FrameworkElement element);
    }

    public class TriggerActionCollection : Collection<TriggerAction> { }

    /// <summary>
    /// Runs its actions when the element raises an event (Loaded, Click, LostFocus, ...), in a style or in an
    /// element's <c>Triggers</c>. The event is found by name as a .NET event of the element ("Button.Click" and
    /// "Click" are the same), so the trigger fires whenever the element's code raises that event.
    /// </summary>
    [Markup.ContentProperty(nameof(Actions))]
    public class EventTrigger : TriggerBase
    {
        public RoutedEvent RoutedEvent { get; set; }

        public string SourceName { get; set; }

        public TriggerActionCollection Actions { get; } = new TriggerActionCollection();

        internal override IEnumerable<SetterBase> ActiveSetters => Array.Empty<SetterBase>();

        /// <summary>Never "active": an event trigger has no setters, it acts when the event happens.</summary>
        internal override bool IsActive(FrameworkElement element) => false;

        /// <summary>Subscribes to the event on <paramref name="element"/>; returns false when it has no such event.</summary>
        internal bool Attach(FrameworkElement element)
        {
            string name = RoutedEvent?.Name;
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }
            name = name.Substring(name.LastIndexOf('.') + 1);
            EventInfo clrEvent = element.GetType().GetEvent(name, BindingFlags.Public | BindingFlags.Instance);
            if (clrEvent?.EventHandlerType == null)
            {
                return false;
            }
            var listener = new Listener(this, element);
            MethodInfo fire = typeof(Listener).GetMethod(nameof(Listener.Fire));
            // (object, EventArgs) accepts the arguments of every event handler type
            clrEvent.AddEventHandler(element, Delegate.CreateDelegate(clrEvent.EventHandlerType, listener, fire));
            return true;
        }

        private sealed class Listener
        {
            private readonly EventTrigger _trigger;
            private readonly FrameworkElement _element;

            public Listener(EventTrigger trigger, FrameworkElement element)
            {
                _trigger = trigger;
                _element = element;
            }

            public void Fire(object sender, EventArgs e)
            {
                foreach (TriggerAction action in _trigger.Actions)
                {
                    action.Invoke(_element);
                }
            }
        }
    }
}
