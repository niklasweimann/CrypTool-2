using System.Runtime.CompilerServices;

namespace System.Windows.Hosting
{
    /// <summary>
    /// Makes "pack://application:,,,/Assembly;component/path" addresses valid URIs, as WPF does when it starts:
    /// without a registered "pack" scheme .NET reads ",,," as an invalid port and throws.
    /// </summary>
    internal static class PackUriScheme
    {
        // A module initializer is deliberate: plugin code parses pack:// URIs before any CrypWeb code runs.
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void Register()
        {
            if (!UriParser.IsKnownScheme("pack"))
            {
                UriParser.Register(new GenericUriParser(GenericUriParserOptions.GenericAuthority), "pack", -1);
            }
        }
    }
}
