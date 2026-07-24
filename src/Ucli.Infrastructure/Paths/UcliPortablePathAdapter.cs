using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts;

namespace MackySoft.Ucli.Infrastructure.Paths;

/// <summary>
/// Adapts guarded current-platform paths to uCLI's portable slash-separated path contracts.
/// </summary>
internal static class UcliPortablePathAdapter
{
    /// <summary>
    /// Attempts to represent one guarded current-platform relative path as a portable uCLI path.
    /// </summary>
    /// <param name="path"> The guarded current-platform relative path. </param>
    /// <param name="portablePath">
    /// The normalized slash-separated path when every filename character is representable by the
    /// portable contract; otherwise <see langword="null" />.
    /// </param>
    /// <returns>
    /// <see langword="true" /> when <paramref name="path" /> is representable by the portable
    /// contract; otherwise <see langword="false" />.
    /// </returns>
    /// <remarks>
    /// On Windows, backslashes in <see cref="RootRelativePath.Value" /> are directory separators and
    /// are converted to forward slashes. On Unix, a backslash is a filename character, so a path
    /// containing one cannot be represented without changing its identity.
    /// </remarks>
    public static bool TryFormat (
        RootRelativePath path,
        [NotNullWhen(true)] out string? portablePath)
    {
        if (path is null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        var candidate = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? path.Value.Replace('\\', '/')
            : path.Value;
        if (!RelativePathContract.IsNormalized(candidate))
        {
            portablePath = null;
            return false;
        }

        portablePath = candidate;
        return true;
    }
}
