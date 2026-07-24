using System.Runtime.InteropServices;
using MackySoft.FileSystem;

namespace MackySoft.Ucli.Infrastructure.Cryptography;

/// <summary> Adapts guarded filesystem paths to deterministic text for persisted identity inputs. </summary>
internal static class DeterministicPathText
{
    /// <summary> Gets deterministic identity text for one guarded absolute path. </summary>
    public static string ForIdentity (AbsolutePath path)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        return ApplyCurrentPlatformCasePolicy(path.Value);
    }

    /// <summary> Gets deterministic identity text for one guarded root-relative path. </summary>
    public static string ForIdentity (RootRelativePath path)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        return ApplyCurrentPlatformCasePolicy(path.Value);
    }

    private static string ApplyCurrentPlatformCasePolicy (string pathValue)
    {
        // MackySoft.FileSystem preserves input casing while comparing Windows paths
        // case-insensitively. Persisted identities must collapse the same case variants.
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? pathValue.ToUpperInvariant()
            : pathValue;
    }
}
