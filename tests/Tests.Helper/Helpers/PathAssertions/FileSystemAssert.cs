namespace MackySoft.Tests;

using Xunit.Sdk;

internal static class FileSystemAssert
{
    public static PathAssertion ForPath (string path)
    {
        return new PathAssertion(path);
    }

    public static DirectoryAssertion ForDirectory (string directoryPath)
    {
        return new DirectoryAssertion(directoryPath);
    }

    public static FileAssertion ForFile (string filePath)
    {
        return new FileAssertion(filePath);
    }
}

internal sealed class PathAssertion : PathAssertionBase<PathAssertion>
{
    internal PathAssertion (string path)
        : base(path)
    {
    }
}

internal sealed class DirectoryAssertion : PathAssertionBase<DirectoryAssertion>
{
    private readonly Dictionary<string, FileSystemEntryKind> expectedEntries;

    internal DirectoryAssertion (string path)
        : base(path)
    {
        expectedEntries = new Dictionary<string, FileSystemEntryKind>(PathNameComparer);
    }

    public DirectoryAssertion HasDirectory (string childName)
    {
        EnsureDirectoryExists(ActualPath);
        var childPath = ResolveChildPath(childName);
        if (!Directory.Exists(childPath))
        {
            throw new XunitException(
                $"Directory '{ActualPath}' expected child directory '{childName}', but it was not found.");
        }

        AddExpectedEntry(childName, FileSystemEntryKind.Directory);
        return this;
    }

    public DirectoryAssertion HasDirectory (string childName, Action<DirectoryAssertion> assertion)
    {
        ArgumentNullException.ThrowIfNull(assertion);

        HasDirectory(childName);
        var childPath = ResolveChildPath(childName);
        assertion(new DirectoryAssertion(childPath));
        return this;
    }

    public DirectoryAssertion HasFile (string childName)
    {
        EnsureDirectoryExists(ActualPath);
        var childPath = ResolveChildPath(childName);
        if (!File.Exists(childPath))
        {
            throw new XunitException(
                $"Directory '{ActualPath}' expected child file '{childName}', but it was not found.");
        }

        AddExpectedEntry(childName, FileSystemEntryKind.File);
        return this;
    }

    public DirectoryAssertion HasFile (string childName, Action<FileAssertion> assertion)
    {
        ArgumentNullException.ThrowIfNull(assertion);

        HasFile(childName);
        var childPath = ResolveChildPath(childName);
        assertion(new FileAssertion(childPath));
        return this;
    }

    public DirectoryAssertion MatchesExactly ()
    {
        EnsureDirectoryExists(ActualPath);

        var actualEntries = new Dictionary<string, FileSystemEntryKind>(PathNameComparer);
        foreach (var entryPath in Directory.EnumerateFileSystemEntries(ActualPath))
        {
            var entryName = Path.GetFileName(entryPath);
            var actualKind = ResolveEntryKind(entryPath);
            if (!expectedEntries.TryGetValue(entryName, out var expectedKind))
            {
                throw new XunitException(
                    $"Directory '{ActualPath}' contains unexpected {DescribeEntryKind(actualKind)} '{entryName}'.");
            }

            if (expectedKind != actualKind)
            {
                throw new XunitException(
                    $"Directory '{ActualPath}' expected '{entryName}' to be a {DescribeEntryKind(expectedKind)}, but it was {DescribeEntryKind(actualKind)}.");
            }

            actualEntries[entryName] = actualKind;
        }

        foreach (var (entryName, expectedKind) in expectedEntries)
        {
            if (!actualEntries.ContainsKey(entryName))
            {
                throw new XunitException(
                    $"Directory '{ActualPath}' expected {DescribeEntryKind(expectedKind)} '{entryName}', but it was not found.");
            }
        }

        return this;
    }

    private string ResolveChildPath (string childName)
    {
        ValidateChildName(childName);
        return Path.Combine(ActualPath, childName);
    }

    private void AddExpectedEntry (string childName, FileSystemEntryKind expectedKind)
    {
        if (expectedEntries.TryGetValue(childName, out var existingKind) && existingKind != expectedKind)
        {
            throw new XunitException(
                $"Directory '{ActualPath}' has conflicting expectations for '{childName}': expected {DescribeEntryKind(existingKind)} and {DescribeEntryKind(expectedKind)}.");
        }

        expectedEntries[childName] = expectedKind;
    }

    private static void ValidateChildName (string childName)
    {
        if (string.IsNullOrWhiteSpace(childName))
        {
            throw new XunitException("Child entry name must not be null or whitespace.");
        }

        if (Path.IsPathRooted(childName))
        {
            throw new XunitException(
                $"Child entry name '{childName}' must be a relative single entry name.");
        }

        if (childName == "." || childName == "..")
        {
            throw new XunitException(
                $"Child entry name '{childName}' is not allowed.");
        }

        if (Path.GetFileName(childName) != childName)
        {
            throw new XunitException(
                $"Child entry name '{childName}' must not contain directory separators.");
        }
    }

    private static FileSystemEntryKind ResolveEntryKind (string path)
    {
        if (Directory.Exists(path))
        {
            return FileSystemEntryKind.Directory;
        }

        if (File.Exists(path))
        {
            return FileSystemEntryKind.File;
        }

        throw new XunitException($"Path '{path}' is neither a file nor a directory.");
    }

    private static string DescribeEntryKind (FileSystemEntryKind kind)
    {
        return kind switch
        {
            FileSystemEntryKind.File => "file",
            FileSystemEntryKind.Directory => "directory",
            _ => "unknown",
        };
    }
}

internal sealed class FileAssertion : PathAssertionBase<FileAssertion>
{
    internal FileAssertion (string path)
        : base(path)
    {
    }

    public FileAssertion HasExtension (string expectedExtension)
    {
        if (string.IsNullOrWhiteSpace(expectedExtension))
        {
            throw new XunitException("File extension must not be null or whitespace.");
        }

        var normalizedExpected = expectedExtension.StartsWith(".", StringComparison.Ordinal)
            ? expectedExtension
            : $".{expectedExtension}";
        var actualExtension = Path.GetExtension(ActualPath);
        if (!string.Equals(actualExtension, normalizedExpected, PathNameComparison))
        {
            throw new XunitException(
                $"Path '{ActualPath}' expected extension '{normalizedExpected}', but was '{actualExtension}'.");
        }

        return this;
    }
}

internal abstract class PathAssertionBase<TSelf>
    where TSelf : PathAssertionBase<TSelf>
{
    private readonly string path;

    protected PathAssertionBase (string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new XunitException("Path must not be null or whitespace.");
        }

        this.path = path;
    }

    protected string ActualPath => path;

    protected static StringComparer PathNameComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    protected static StringComparison PathNameComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private TSelf Self => (TSelf)this;

    public TSelf Exists ()
    {
        if (!PathExists(ActualPath))
        {
            throw new XunitException($"Path '{ActualPath}' expected to exist, but it does not exist.");
        }

        return Self;
    }

    public TSelf DoesNotExist ()
    {
        if (PathExists(ActualPath))
        {
            throw new XunitException($"Path '{ActualPath}' expected to not exist, but it exists.");
        }

        return Self;
    }

    public TSelf IsRooted ()
    {
        if (!Path.IsPathRooted(ActualPath))
        {
            throw new XunitException($"Path '{ActualPath}' expected to be rooted, but it was relative.");
        }

        return Self;
    }

    public TSelf EqualsNormalized (string expectedPath)
    {
        var normalizedExpected = NormalizePath(expectedPath);
        var normalizedActual = NormalizePath(ActualPath);
        if (!string.Equals(normalizedActual, normalizedExpected, PathNameComparison))
        {
            throw new XunitException(
                $"Path '{ActualPath}' expected normalized path '{normalizedExpected}', but was '{normalizedActual}'.");
        }

        return Self;
    }

    public TSelf IsUnderDirectory (string expectedParentDirectory)
    {
        var normalizedParent = NormalizePath(expectedParentDirectory);
        var normalizedActual = NormalizePath(ActualPath);
        if (!IsSameOrUnderPath(normalizedActual, normalizedParent))
        {
            throw new XunitException(
                $"Path '{normalizedActual}' expected to be under '{normalizedParent}', but it was outside.");
        }

        return Self;
    }

    public TSelf HasFileName (string expectedFileName)
    {
        if (string.IsNullOrWhiteSpace(expectedFileName))
        {
            throw new XunitException("Expected file name must not be null or whitespace.");
        }

        var actualFileName = Path.GetFileName(ActualPath);
        if (!string.Equals(actualFileName, expectedFileName, PathNameComparison))
        {
            throw new XunitException(
                $"Path '{ActualPath}' expected file name '{expectedFileName}', but was '{actualFileName}'.");
        }

        return Self;
    }

    protected static string NormalizePath (string inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new XunitException("Path must not be null or whitespace.");
        }

        var fullPath = Path.GetFullPath(inputPath);
        var resolvedPath = ResolveSymbolicLinks(fullPath);
        var aliasNormalizedPath = NormalizeUnixPrivateAlias(resolvedPath);
        return TrimTrailingDirectorySeparators(aliasNormalizedPath);
    }

    protected static void EnsureDirectoryExists (string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            throw new XunitException(
                $"Directory '{directoryPath}' expected to exist, but it was not found.");
        }
    }

    private static bool PathExists (string path)
    {
        return File.Exists(path) || Directory.Exists(path);
    }

    private static bool IsSameOrUnderPath (string path, string parentPath)
    {
        if (string.Equals(path, parentPath, PathNameComparison))
        {
            return true;
        }

        var parentPrefix = Path.EndsInDirectorySeparator(parentPath)
            ? parentPath
            : parentPath + Path.DirectorySeparatorChar;
        return path.StartsWith(parentPrefix, PathNameComparison);
    }

    private static string TrimTrailingDirectorySeparators (string fullPath)
    {
        var root = Path.GetPathRoot(fullPath);
        if (!string.IsNullOrEmpty(root) && string.Equals(fullPath, root, PathNameComparison))
        {
            return fullPath;
        }

        return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static string ResolveSymbolicLinks (string fullPath)
    {
        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(root))
        {
            return fullPath;
        }

        var relativePath = fullPath[root.Length..];
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return root;
        }

        var pathSegments = relativePath.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);
        var currentPath = root;

        for (var i = 0; i < pathSegments.Length; i++)
        {
            var candidatePath = Path.Combine(currentPath, pathSegments[i]);
            if (PathExists(candidatePath))
            {
                currentPath = ResolveSymbolicLinkTarget(candidatePath);
                continue;
            }

            currentPath = candidatePath;
            for (var j = i + 1; j < pathSegments.Length; j++)
            {
                currentPath = Path.Combine(currentPath, pathSegments[j]);
            }

            break;
        }

        return currentPath;
    }

    private static string ResolveSymbolicLinkTarget (string path)
    {
        FileSystemInfo fileSystemInfo;
        if (Directory.Exists(path))
        {
            fileSystemInfo = new DirectoryInfo(path);
        }
        else if (File.Exists(path))
        {
            fileSystemInfo = new FileInfo(path);
        }
        else
        {
            return path;
        }

        if (string.IsNullOrEmpty(fileSystemInfo.LinkTarget))
        {
            return path;
        }

        try
        {
            var finalTarget = fileSystemInfo.ResolveLinkTarget(returnFinalTarget: true);
            return finalTarget == null
                ? path
                : Path.GetFullPath(finalTarget.FullName);
        }
        catch (IOException)
        {
            return path;
        }
        catch (UnauthorizedAccessException)
        {
            return path;
        }
    }

    private static string NormalizeUnixPrivateAlias (string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return path;
        }

        const string UnixPrivatePrefix = "/private/";
        if (!path.StartsWith(UnixPrivatePrefix, StringComparison.Ordinal))
        {
            return path;
        }

        var pathWithoutPrivatePrefix = path[(UnixPrivatePrefix.Length - 1)..];
        return PathExists(pathWithoutPrivatePrefix)
            ? pathWithoutPrivatePrefix
            : path;
    }
}

internal enum FileSystemEntryKind
{
    File,
    Directory,
}
