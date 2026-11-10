using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace System.Windows.Data
{
    /// <summary>Reads and writes "A.B[2].C" style paths on plain objects, as bindings do.</summary>
    internal static class PropertyPathWalker
    {
        private const BindingFlags Members = BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

        private static readonly ConditionalWeakTable<object, object> Watched = new ConditionalWeakTable<object, object>();

        public static object GetValue(object source, string path, out bool found)
        {
            found = true;
            Watch(source);
            if (source == null)
            {
                found = false;
                return null;
            }
            if (string.IsNullOrEmpty(path) || path == ".")
            {
                return source;
            }

            object current = source;
            foreach (string step in path.Split('.'))
            {
                if (current == null)
                {
                    return null;
                }
                if (!TryStep(current, step.Trim(), out current))
                {
                    found = false;
                    return null;
                }
                Watch(current);
            }
            return current;
        }

        public static void SetValue(object source, string path, object value)
        {
            if (source == null || string.IsNullOrEmpty(path))
            {
                return;
            }
            int lastDot = path.LastIndexOf('.');
            object owner = lastDot < 0 ? source : GetValue(source, path.Substring(0, lastDot), out _);
            PropertyInfo property = owner?.GetType().GetProperty(path.Substring(lastDot + 1).Trim(), Members);
            if (property != null && property.CanWrite)
            {
                property.SetValue(owner, ValueCoercion.ToPropertyType(Coerce(value, property.PropertyType), property.PropertyType));
            }
        }

        /// <summary>Reads one step ("Name", "Name[2]", "(Owner.Name)") from <paramref name="current"/>.</summary>
        internal static object GetStep(object current, string step, out bool found)
        {
            found = TryStep(current, step.Trim(), out object next);
            return found ? next : null;
        }

        private static object Coerce(object value, Type type)
        {
            if (value is string text && type != typeof(string) && Markup.XamlValueConverter.TryConvert(text, type, out object converted))
            {
                return converted;
            }
            return value;
        }

        private static bool TryStep(object current, string step, out object next)
        {
            string name = step;
            string index = null;
            int bracket = step.IndexOf('[');
            if (bracket >= 0 && step.EndsWith("]", StringComparison.Ordinal))
            {
                name = step.Substring(0, bracket);
                index = step.Substring(bracket + 1, step.Length - bracket - 2);
            }

            next = current;
            if (name.Length > 0)
            {
                // "(Owner.Property)" addresses an attached or dependency property
                if (name.StartsWith("(", StringComparison.Ordinal))
                {
                    name = name.Trim('(', ')');
                    name = name.Substring(name.LastIndexOf('.') + 1);
                }

                if (current is System.Data.DataRowView row)
                {
                    next = row.Row.Table.Columns.Contains(name) ? row[name] : null;
                }
                else
                {
                    PropertyInfo property = current.GetType().GetProperty(name, Members);
                    if (property != null)
                    {
                        next = property.GetValue(current);
                    }
                    else
                    {
                        FieldInfo field = current.GetType().GetField(name, Members);
                        if (field == null)
                        {
                            return false;
                        }
                        next = field.GetValue(current);
                    }
                }
            }

            if (index != null && next != null)
            {
                if (next is IList list && int.TryParse(index, out int i))
                {
                    next = i >= 0 && i < list.Count ? list[i] : null;
                }
                else if (next is IDictionary dictionary)
                {
                    next = dictionary.Contains(index) ? dictionary[index] : null;
                }
                else if (next is System.Data.DataRowView indexedRow)
                {
                    next = int.TryParse(index, out int column) ? indexedRow[column] : indexedRow[index];
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Redraws when an object that a binding reads from reports a change.</summary>
        internal static void Watch(object source)
        {
            if (source == null || source is string || source.GetType().IsValueType || Watched.TryGetValue(source, out _))
            {
                return;
            }
            bool observable = false;
            if (source is INotifyPropertyChanged notifying)
            {
                notifying.PropertyChanged += (s, e) => RenderVersion.Bump();
                observable = true;
            }
            if (source is INotifyCollectionChanged collection)
            {
                collection.CollectionChanged += (s, e) => RenderVersion.Bump();
                observable = true;
            }
            if (observable)
            {
                Watched.Add(source, null);
            }
        }
    }
}
