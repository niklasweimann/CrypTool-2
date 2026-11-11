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
    /// States the browser does not report (IsMouseOver, IsPressed, IsFocused) are false, so such triggers show the
    /// idle look.
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

        internal static bool PropertyMatches(FrameworkElement element, DependencyProperty property, string propertyName, object expected)
        {
            property ??= Markup.DependencyPropertyLookup.Find(element.GetType(), propertyName);
            if (property == null || _depth > 0) return false;
            _depth++;
            try { return ValuesMatch(element.GetValue(property), expected); }
            finally { _depth--; }
        }

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
