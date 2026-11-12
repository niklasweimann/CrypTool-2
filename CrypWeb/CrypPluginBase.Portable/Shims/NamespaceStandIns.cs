// Namespaces of desktop assemblies that plugin sources import with a using directive although they use nothing
// from them (e.g. LorenzSZ42: "using CrypTool.Core.Properties;"). An empty type makes the namespace exist.
namespace CrypTool.Core.Properties
{
    internal static class NamespaceStandIn { }
}
