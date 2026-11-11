using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

namespace System.Windows
{
    /// <summary>In WPF a non-visual content element; here it is an ordinary (undrawn) element so that it can carry bindings.</summary>
    public class FrameworkContentElement : FrameworkElement { }

    public class UIPropertyMetadata : PropertyMetadata
    {
        public UIPropertyMetadata() { }

        public UIPropertyMetadata(object defaultValue) : base(defaultValue) { }

        public UIPropertyMetadata(PropertyChangedCallback propertyChangedCallback) : base(propertyChangedCallback) { }

        public UIPropertyMetadata(object defaultValue, PropertyChangedCallback propertyChangedCallback) : base(defaultValue, propertyChangedCallback) { }
    }

    /// <summary>The browser clipboard is not reachable from the engine worker: text is kept in memory only.</summary>
    public static class Clipboard
    {
        private static string _text = "";

        public static void SetText(string text) => _text = text ?? "";

        public static string GetText() => _text;

        public static bool ContainsText() => _text.Length > 0;

        public static void Clear() => _text = "";

        public static void SetDataObject(object data) => _text = data?.ToString() ?? "";

        public static void SetDataObject(object data, bool copy) => SetDataObject(data);
    }
}

namespace System.Windows.Controls
{
    public class ScrollChangedEventArgs : RoutedEventArgs
    {
        public double VerticalOffset { get; set; }
        public double HorizontalOffset { get; set; }
        public double VerticalChange { get; set; }
        public double HorizontalChange { get; set; }
        public double ExtentHeightChange { get; set; }
        public double ExtentWidthChange { get; set; }
        public double ViewportHeight { get; set; }
        public double ViewportWidth { get; set; }
        public double ExtentHeight { get; set; }
        public double ExtentWidth { get; set; }
    }

    public delegate void ScrollChangedEventHandler(object sender, ScrollChangedEventArgs e);

    /// <summary>A splitter between grid cells; dragging is not supported, it is drawn as a thin bar.</summary>
    public class GridSplitter : Control
    {
        public object ResizeDirection { get; set; }
        public object ResizeBehavior { get; set; }
        public bool ShowsPreview { get; set; }
    }
}

namespace System.Windows.Controls.Primitives
{
    /// <summary>A bullet (e.g. a check mark) next to content.</summary>
    [Markup.ContentProperty(nameof(Child))]
    public class BulletDecorator : Decorator
    {
        private UIElement _bullet;

        public UIElement Bullet
        {
            get => _bullet;
            set
            {
                SetParent(_bullet, null);
                _bullet = value;
                SetParent(_bullet, this);
                RenderVersion.Bump();
            }
        }

        public Media.Brush Background { get; set; }

        internal override IEnumerable<UIElement> ChildElements
        {
            get
            {
                if (_bullet != null)
                {
                    yield return _bullet;
                }
                if (Child != null)
                {
                    yield return Child;
                }
            }
        }
    }
}

namespace System.ComponentModel
{
    public struct SortDescription
    {
        public SortDescription(string propertyName, ListSortDirection direction)
        {
            PropertyName = propertyName;
            Direction = direction;
        }

        public string PropertyName { get; set; }
        public ListSortDirection Direction { get; set; }
    }

    public class SortDescriptionCollection : System.Collections.ObjectModel.Collection<SortDescription> { }
}

namespace System.Windows.Data
{
    /// <summary>A view of a collection with a current item, sorting and filtering.</summary>
    public interface ICollectionView : IEnumerable
    {
        object CurrentItem { get; }

        int CurrentPosition { get; }

        Predicate<object> Filter { get; set; }

        SortDescriptionCollection SortDescriptions { get; }

        void Refresh();

        bool MoveCurrentTo(object item);

        bool MoveCurrentToFirst();

        bool MoveCurrentToPosition(int position);
    }

    /// <summary>View of a list: enumerates the list filtered and sorted.</summary>
    public class ListCollectionView : ICollectionView
    {
        private readonly IList _list;

        public ListCollectionView(IList list) => _list = list;

        public IList SourceCollection => _list;

        public IComparer CustomSort { get; set; }

        public Predicate<object> Filter { get; set; }

        public SortDescriptionCollection SortDescriptions { get; } = new SortDescriptionCollection();

        public int CurrentPosition { get; private set; }

        public object CurrentItem => CurrentPosition >= 0 && CurrentPosition < _list.Count ? _list[CurrentPosition] : null;

        public int Count => _list.Count;

        public bool IsEmpty => _list.Count == 0;

        public void Refresh() => RenderVersion.Bump();

        public bool MoveCurrentTo(object item) => MoveCurrentToPosition(_list.IndexOf(item));

        public bool MoveCurrentToFirst() => MoveCurrentToPosition(0);

        public bool MoveCurrentToPosition(int position)
        {
            CurrentPosition = position;
            return CurrentItem != null;
        }

        private int CompareBySortDescriptions(object a, object b)
        {
            foreach (SortDescription sort in SortDescriptions)
            {
                var left = PropertyPathWalker.GetValue(a, sort.PropertyName, out _) as IComparable;
                object right = PropertyPathWalker.GetValue(b, sort.PropertyName, out _);
                int result = left == null ? (right == null ? 0 : -1) : left.CompareTo(right);
                if (result != 0)
                {
                    return sort.Direction == ListSortDirection.Ascending ? result : -result;
                }
            }
            return 0;
        }

        public IEnumerator GetEnumerator()
        {
            var items = new List<object>();
            foreach (object item in _list)
            {
                if (Filter == null || Filter(item))
                {
                    items.Add(item);
                }
            }
            if (CustomSort != null)
            {
                items.Sort((a, b) => CustomSort.Compare(a, b));
            }
            else if (SortDescriptions.Count > 0)
            {
                items.Sort(CompareBySortDescriptions);
            }
            return items.GetEnumerator();
        }
    }

    public static class CollectionViewSource
    {
        private static readonly Runtime.CompilerServices.ConditionalWeakTable<object, ListCollectionView> Views =
            new Runtime.CompilerServices.ConditionalWeakTable<object, ListCollectionView>();

        /// <summary>The one default view of a collection.</summary>
        public static ICollectionView GetDefaultView(object source)
        {
            if (source is not IList list)
            {
                return null;
            }
            return Views.GetValue(source, _ => new ListCollectionView(list));
        }
    }
}

namespace System.Media
{
    /// <summary>System sounds are not played in the browser.</summary>
    public sealed class SystemSound
    {
        public void Play() { }
    }

    public static class SystemSounds
    {
        public static SystemSound Beep { get; } = new SystemSound();
        public static SystemSound Asterisk { get; } = new SystemSound();
        public static SystemSound Exclamation { get; } = new SystemSound();
        public static SystemSound Hand { get; } = new SystemSound();
        public static SystemSound Question { get; } = new SystemSound();
    }
}
