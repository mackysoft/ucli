using System.Runtime.InteropServices;
using MackySoft.FileSystem;

namespace MackySoft.Ucli.Infrastructure.Storage;

/// <summary> Projects one guarded absolute path to the native filesystem namespace of the current platform. </summary>
internal static class FileSystemNativePathText
{
    private const string ExtendedPathPrefix = @"\\?\";
    private const string UncPathPrefix = @"\\";
    private const string ExtendedUncPathPrefix = @"\\?\UNC\";

    public static string FromGuardedPath (AbsolutePath path)
    {
        if (path is null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return path.Value;
        }

        if (path.Value.StartsWith(ExtendedPathPrefix, StringComparison.Ordinal))
        {
            return path.Value;
        }

        if (path.Value.StartsWith(UncPathPrefix, StringComparison.Ordinal))
        {
            return ExtendedUncPathPrefix
                + path.Value.Substring(UncPathPrefix.Length);
        }

        return ExtendedPathPrefix + path.Value;
    }
}
