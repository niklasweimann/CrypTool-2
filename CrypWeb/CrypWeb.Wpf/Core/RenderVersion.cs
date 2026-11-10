namespace System.Windows
{
    /// <summary>
    /// Global change counter of all element trees. Every property or collection change increases it; a
    /// <see cref="Hosting.PresentationHost"/> draws again when the counter moved since its last drawing.
    /// One counter for everything keeps invalidation trivially correct (shared brushes, detached sub trees, ...).
    /// </summary>
    public static class RenderVersion
    {
        private static long _current;

        public static long Current => _current;

        public static void Bump() => _current++;
    }
}
