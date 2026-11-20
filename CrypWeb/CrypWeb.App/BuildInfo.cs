using System.Reflection;

namespace CrypWeb.App;

/// <summary>Facts about this build, stamped in by the project file (target StampGitHash).</summary>
public static class BuildInfo
{
    /// <summary>Short git commit hash of the build, or "dev" when it was built outside a git checkout.</summary>
    public static readonly string GitHash =
        typeof(BuildInfo).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "GitHash")?.Value ?? "dev";

    /// <summary>The commit on GitHub, or the repository when the hash is unknown.</summary>
    public static string CommitUrl => GitHash == "dev"
        ? "https://github.com/niklasweimann/CrypTool-2"
        : "https://github.com/niklasweimann/CrypTool-2/commit/" + GitHash;
}
