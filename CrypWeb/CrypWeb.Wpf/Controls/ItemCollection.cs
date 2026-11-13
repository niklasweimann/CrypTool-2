using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace System.Windows.Controls
{
    /// <summary>The items added directly to an items control (as opposed to an <c>ItemsSource</c>).</summary>
    public class ItemCollection : Collection<object>
    {
        private readonly UIElement _owner;

        internal ItemCollection(UIElement owner) => _owner = owner;

        protected override void InsertItem(int index, object item)
        {
            base.InsertItem(index, item);
            UIElement.SetParent(item, _owner);
            RenderVersion.Bump();
        }

        protected override void RemoveItem(int index)
        {
            UIElement.SetParent(this[index], null);
            base.RemoveItem(index);
            RenderVersion.Bump();
        }

        protected override void ClearItems()
        {
            foreach (object item in this)
            {
                UIElement.SetParent(item, null);
            }
            base.ClearItems();
            RenderVersion.Bump();
        }

        public void Refresh() => RenderVersion.Bump();

        public object CurrentItem => Count > 0 ? this[0] : null;

        /// <summary>The order in which the items are shown (by property values).</summary>
        public System.ComponentModel.SortDescriptionCollection SortDescriptions { get; } = new System.ComponentModel.SortDescriptionCollection();

        // As in WPF, Items is the view of what the control shows: with an ItemsSource it lists the source's items,
        // in the order of SortDescriptions.

        /// <summary>The shown item at a position.</summary>
        public new object this[int index]
        {
            get => View[index];
            set => base[index] = value;
        }

        /// <summary>The number of shown items.</summary>
        public new int Count => View.Count;

        public new int IndexOf(object item) => View.IndexOf(item);

        public new bool Contains(object item) => View.Contains(item);

        private IList<object> View => (_owner as ItemsControl)?.EffectiveItems ?? this;

        /// <summary>The items in the order of <see cref="SortDescriptions"/> (unchanged when there are none).</summary>
        internal IList<object> Sort(IList<object> items)
        {
            if (SortDescriptions.Count == 0)
            {
                return items;
            }
            IEnumerable<object> sorted = items;
            IOrderedEnumerable<object> ordered = null;
            foreach (System.ComponentModel.SortDescription description in SortDescriptions)
            {
                Func<object, object> key = item => SortKey(item, description.PropertyName);
                bool descending = description.Direction == System.ComponentModel.ListSortDirection.Descending;
                ordered = ordered == null
                    ? (descending ? sorted.OrderByDescending(key, Comparer<object>.Default) : sorted.OrderBy(key, Comparer<object>.Default))
                    : (descending
                    ? ordered.ThenByDescending(key, Comparer<object>.Default)
                    : ordered.ThenBy(key, Comparer<object>.Default));
            }
            return ordered.ToList();
        }

        private static object SortKey(object item, string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName) || item == null)
            {
                return item;
            }
            return item.GetType().GetProperty(propertyName)?.GetValue(item);
        }
    }
}
