using System.Collections.Generic;
using System.Linq;

namespace System.Windows.Markup
{
    /// <summary>
    /// Collects what the XAML loader could not handle (unknown types, properties, events, values). Loading never
    /// fails because of these; the list tells which part of WPF is still missing.
    /// </summary>
    public static class XamlDiagnostics
    {
        private static readonly Dictionary<string, int> Counts = new Dictionary<string, int>();

        internal static void Report(string kind, string detail)
        {
            string key = kind + ": " + detail;
            lock (Counts)
            {
                Counts[key] = Counts.TryGetValue(key, out int count) ? count + 1 : 1;
            }
        }

        /// <summary>The problems seen so far with how often each occurred, most frequent first.</summary>
        public static IReadOnlyList<KeyValuePair<string, int>> Snapshot()
        {
            lock (Counts)
            {
                return Counts.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key).ToList();
            }
        }

        public static void Clear()
        {
            lock (Counts)
            {
                Counts.Clear();
            }
        }
    }
}
