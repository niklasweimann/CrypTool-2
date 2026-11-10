namespace System.Windows
{
    /// <summary>Short forms for registering dependency properties inside this library.</summary>
    internal static class Dp
    {
        public static DependencyProperty Register<TValue, TOwner>(string name, TValue defaultValue = default)
        {
            return DependencyProperty.Register(name, typeof(TValue), typeof(TOwner), new PropertyMetadata(defaultValue));
        }

        public static DependencyProperty Register<TValue, TOwner>(string name, TValue defaultValue, PropertyChangedCallback changed)
        {
            return DependencyProperty.Register(name, typeof(TValue), typeof(TOwner), new PropertyMetadata(defaultValue, changed));
        }

        public static DependencyProperty Attached<TValue, TOwner>(string name, TValue defaultValue = default)
        {
            return DependencyProperty.RegisterAttached(name, typeof(TValue), typeof(TOwner), new PropertyMetadata(defaultValue));
        }
    }
}
