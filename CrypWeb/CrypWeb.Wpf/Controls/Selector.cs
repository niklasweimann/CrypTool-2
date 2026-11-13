using System.Collections;
using System.Collections.Generic;
using System.Windows.Data;

namespace System.Windows.Controls.Primitives
{
    /// <summary>An items control in which one item can be selected.</summary>
    public class Selector : ItemsControl
    {
        public static readonly DependencyProperty SelectedIndexProperty = Dp.Register<int, Selector>(nameof(SelectedIndex), -1, OnSelectedIndexChanged);
        public static readonly DependencyProperty SelectedValuePathProperty = Dp.Register<string, Selector>(nameof(SelectedValuePath));
        public static readonly DependencyProperty IsSynchronizedWithCurrentItemProperty = Dp.Register<bool?, Selector>(nameof(IsSynchronizedWithCurrentItem));

        public int SelectedIndex { get => (int)GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }

        public string SelectedValuePath
        {
            get => (string)GetValue(SelectedValuePathProperty);
            set => SetValue(SelectedValuePathProperty, value);
        }

        public bool? IsSynchronizedWithCurrentItem
        {
            get => (bool?)GetValue(IsSynchronizedWithCurrentItemProperty);
            set => SetValue(IsSynchronizedWithCurrentItemProperty, value);
        }

        public object SelectedItem
        {
            get
            {
                IList<object> items = EffectiveItems;
                int index = SelectedIndex;
                return index >= 0 && index < items.Count ? items[index] : null;
            }
            set => SelectedIndex = EffectiveItems.IndexOf(value);
        }

        public object SelectedValue
        {
            get
            {
                object item = SelectedItem;
                return string.IsNullOrEmpty(SelectedValuePath) ? item : PropertyPathWalker.GetValue(item, SelectedValuePath, out _);
            }
            set => SelectedItem = value;
        }

        public IList SelectedItems
        {
            get
            {
                var selected = new List<object>();
                if (SelectedItem != null)
                {
                    selected.Add(SelectedItem);
                }
                return selected;
            }
        }

        public event SelectionChangedEventHandler SelectionChanged;

        private static void OnSelectedIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var selector = (Selector)d;
            var args = new SelectionChangedEventArgs { Source = selector };
            if (selector.SelectedItem != null)
            {
                args.AddedItems.Add(selector.SelectedItem);
            }
            selector.SelectionChanged?.Invoke(selector, args);
        }

        /// <summary>A selection made in the browser.</summary>
        internal void SelectFromUser(int index) => SelectedIndex = index;

        /// <summary>A double click on an item in the browser.</summary>
        internal void ActivateFromUser(int index)
        {
            SelectedIndex = index;
            object item = SelectedItem;
            if (item != null)
            {
                OnItemActivated(item);
            }
        }

        /// <summary>Called when the user double-clicks an item (WPF: MouseDoubleClick on the item container).</summary>
        protected virtual void OnItemActivated(object item) { }
    }
}
