namespace System.Windows
{
    public class RoutedEventArgs : EventArgs
    {
        public RoutedEventArgs() { }

        public RoutedEventArgs(RoutedEvent routedEvent) => RoutedEvent = routedEvent;

        public RoutedEventArgs(RoutedEvent routedEvent, object source)
        {
            RoutedEvent = routedEvent;
            Source = source;
        }

        public RoutedEvent RoutedEvent { get; set; }
        public object Source { get; set; }
        public object OriginalSource { get; set; }
        public bool Handled { get; set; }
    }

    public delegate void RoutedEventHandler(object sender, RoutedEventArgs e);

    public delegate void DependencyPropertyChangedEventHandler(object sender, DependencyPropertyChangedEventArgs e);

    public enum RoutingStrategy { Tunnel, Bubble, Direct }

    public sealed class RoutedEvent
    {
        public string Name { get; set; }
        public Type HandlerType { get; set; }
        public Type OwnerType { get; set; }

        public RoutedEvent AddOwner(Type ownerType) => this;
    }

    public static class EventManager
    {
        public static RoutedEvent RegisterRoutedEvent(string name, RoutingStrategy strategy, Type handlerType, Type ownerType)
        {
            return new RoutedEvent { Name = name, HandlerType = handlerType, OwnerType = ownerType };
        }
    }

    public class RoutedPropertyChangedEventArgs<T> : RoutedEventArgs
    {
        public RoutedPropertyChangedEventArgs(T oldValue, T newValue)
        {
            OldValue = oldValue;
            NewValue = newValue;
        }

        public T OldValue { get; }
        public T NewValue { get; }
    }

    public delegate void RoutedPropertyChangedEventHandler<T>(object sender, RoutedPropertyChangedEventArgs<T> e);

    public class SizeChangedEventArgs : RoutedEventArgs
    {
        public Size PreviousSize { get; set; }
        public Size NewSize { get; set; }
        public bool WidthChanged => PreviousSize.Width != NewSize.Width;
        public bool HeightChanged => PreviousSize.Height != NewSize.Height;
    }

    public delegate void SizeChangedEventHandler(object sender, SizeChangedEventArgs e);
}
