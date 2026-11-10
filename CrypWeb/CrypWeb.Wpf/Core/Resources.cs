using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace System.Windows
{
    /// <summary>Keyed objects of an element (brushes, styles, converters, ...), looked up by <c>{StaticResource}</c>.</summary>
    public class ResourceDictionary : IEnumerable
    {
        private readonly Dictionary<object, object> _entries = new Dictionary<object, object>();

        public Collection<ResourceDictionary> MergedDictionaries { get; } = new Collection<ResourceDictionary>();

        public Uri Source { get; set; }

        public int Count => _entries.Count;

        public ICollection Keys => _entries.Keys;

        public ICollection Values => _entries.Values;

        public object this[object key]
        {
            get => TryGetResource(key, out object value) ? value : null;
            set => _entries[key] = value;
        }

        public void Add(object key, object value) => _entries[key] = value;

        public bool Contains(object key) => TryGetResource(key, out _);

        public void Remove(object key) => _entries.Remove(key);

        public void Clear() => _entries.Clear();

        internal bool TryGetResource(object key, out object value)
        {
            if (key != null && _entries.TryGetValue(key, out value))
            {
                return true;
            }
            foreach (ResourceDictionary merged in MergedDictionaries)
            {
                if (merged.TryGetResource(key, out value))
                {
                    return true;
                }
            }
            value = null;
            return false;
        }

        public IEnumerator GetEnumerator() => _entries.GetEnumerator();
    }

    public abstract class SetterBase { }

    /// <summary>One property value of a <see cref="Style"/>.</summary>
    public class Setter : SetterBase
    {
        public Setter() { }

        public Setter(DependencyProperty property, object value)
        {
            Property = property;
            Value = value;
        }

        public DependencyProperty Property { get; set; }

        /// <summary>Property name as written in XAML; resolved against the styled element when the style is applied.</summary>
        internal string PropertyName { get; set; }

        public object Value { get; set; }

        public string TargetName { get; set; }
    }

    public class EventSetter : SetterBase
    {
        public RoutedEvent Event { get; set; }
        public Delegate Handler { get; set; }
    }

    /// <summary>
    /// A set of property values for elements of one type. Setters are applied once, when the style is assigned,
    /// and never override values the element already has. Triggers are kept but not evaluated.
    /// </summary>
    [Markup.ContentProperty(nameof(Setters))]
    public class Style
    {
        public Style() { }

        public Style(Type targetType) => TargetType = targetType;

        public Style(Type targetType, Style basedOn)
        {
            TargetType = targetType;
            BasedOn = basedOn;
        }

        public Type TargetType { get; set; }

        public Style BasedOn { get; set; }

        public Collection<SetterBase> Setters { get; } = new Collection<SetterBase>();

        public Collection<object> Triggers { get; } = new Collection<object>();

        public ResourceDictionary Resources { get; set; } = new ResourceDictionary();

        internal void ApplyTo(FrameworkElement element)
        {
            foreach (SetterBase setterBase in Setters)
            {
                if (setterBase is not Setter setter || !string.IsNullOrEmpty(setter.TargetName))
                {
                    continue;
                }

                DependencyProperty property = setter.Property ?? Markup.DependencyPropertyLookup.Find(element.GetType(), setter.PropertyName);
                if (property == null || element.HasLocalValue(property))
                {
                    continue;
                }

                object value = setter.Value;
                if (value is string text && property.PropertyType != typeof(string) && property.PropertyType != typeof(object))
                {
                    if (!Markup.XamlValueConverter.TryConvert(text, property.PropertyType, out value))
                    {
                        continue;
                    }
                }
                element.SetValue(property, value);
            }

            BasedOn?.ApplyTo(element);
        }
    }

    /// <summary>A template declared in XAML. Its content is created on demand by <see cref="LoadContent"/>.</summary>
    public class FrameworkTemplate
    {
        /// <summary>Creates the template content; set by the XAML loader.</summary>
        internal Func<object> ContentFactory { get; set; }

        public object LoadContent() => ContentFactory?.Invoke();
    }

    public class DataTemplate : FrameworkTemplate
    {
        public object DataType { get; set; }
    }

    public class HierarchicalDataTemplate : DataTemplate
    {
        public Data.BindingBase ItemsSource { get; set; }
    }
}

namespace System.Windows.Controls
{
    public class ControlTemplate : FrameworkTemplate
    {
        public Type TargetType { get; set; }
    }

    public class ItemsPanelTemplate : FrameworkTemplate { }
}
