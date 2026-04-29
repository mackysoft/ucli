namespace MackySoft.Tests;

internal enum DirectoryCleanupMode
{
    Strict,
    BestEffort,
}

internal static class TestDirectories
{
    internal static TestDirectoryScope CreateTempScope (
        string areaName,
        string testCaseName,
        DirectoryCleanupMode cleanupMode = DirectoryCleanupMode.Strict)
    {
        ValidateSegment(areaName, nameof(areaName));
        ValidateSegment(testCaseName, nameof(testCaseName));

        var fullPath = Path.Combine(
            Path.GetTempPath(),
            areaName,
            testCaseName,
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(fullPath);
        return new TestDirectoryScope(fullPath, cleanupMode);
    }

    private static void ValidateSegment (string segment, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            throw new ArgumentException("Segment must not be null or whitespace.", parameterName);
        }

        if (segment.Contains(Path.DirectorySeparatorChar) || segment.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("Segment must not contain directory separators.", parameterName);
        }
    }
}

internal sealed class TestDirectoryScope : IDisposable
{
    private readonly DirectoryCleanupMode cleanupMode;
    private readonly string normalizedRootPath;
    private readonly TestDirectoryScope lifetimeOwner;
    private readonly bool ownsLifetime;
    private bool preserve;
    private bool disposed;

    internal TestDirectoryScope (string fullPath, DirectoryCleanupMode cleanupMode)
        : this(fullPath, cleanupMode, lifetimeOwner: null, ownsLifetime: true)
    {
    }

    private TestDirectoryScope (
        string fullPath,
        DirectoryCleanupMode cleanupMode,
        TestDirectoryScope? lifetimeOwner,
        bool ownsLifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        FullPath = Path.GetFullPath(fullPath);
        normalizedRootPath = EnsureTrailingDirectorySeparator(FullPath);
        this.cleanupMode = cleanupMode;
        this.ownsLifetime = ownsLifetime;
        this.lifetimeOwner = lifetimeOwner ?? this;
    }

    internal string FullPath { get; }

    internal TestDirectoryScope GetDirectory (string relativePath)
    {
        var fullPath = GetPath(relativePath);
        return new TestDirectoryScope(
            fullPath,
            cleanupMode,
            lifetimeOwner: lifetimeOwner,
            ownsLifetime: false);
    }

    internal string GetPath (string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        if (Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException("Path must be relative.", nameof(relativePath));
        }

        ValidateRelativePathSegments(relativePath);

        var fullPath = Path.GetFullPath(Path.Combine(FullPath, relativePath));
        if (!fullPath.StartsWith(normalizedRootPath, StringComparison.Ordinal))
        {
            throw new ArgumentException("Path must remain under the scope root.", nameof(relativePath));
        }

        return fullPath;
    }

    internal string CreateDirectory (string relativePath)
    {
        var fullPath = GetPath(relativePath);
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    internal string WriteFile (string relativePath, string contents)
    {
        var fullPath = GetPath(relativePath);
        var directoryPath = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new InvalidOperationException($"Directory path could not be determined: {fullPath}");
        }

        Directory.CreateDirectory(directoryPath);
        File.WriteAllText(fullPath, contents);
        return fullPath;
    }

    internal async Task<string> WriteFileAsync (
        string relativePath,
        string contents,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = GetPath(relativePath);
        var directoryPath = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new InvalidOperationException($"Directory path could not be determined: {fullPath}");
        }

        Directory.CreateDirectory(directoryPath);
        await File.WriteAllTextAsync(fullPath, contents, cancellationToken).ConfigureAwait(false);
        return fullPath;
    }

    internal void Preserve ()
    {
        lifetimeOwner.preserve = true;
    }

    public void Dispose ()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (!ownsLifetime)
        {
            return;
        }

        if (preserve)
        {
            return;
        }

        if (!Directory.Exists(FullPath))
        {
            return;
        }

        try
        {
            Directory.Delete(FullPath, recursive: true);
        }
        catch (Exception) when (cleanupMode == DirectoryCleanupMode.BestEffort)
        {
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to delete directory '{FullPath}'.", ex);
        }
    }

    private static void ValidateRelativePathSegments (string relativePath)
    {
        var segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.None);
        foreach (var segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                throw new ArgumentException("Path segment must not be null, empty, or whitespace.", nameof(relativePath));
            }

            if (segment is "." or "..")
            {
                throw new ArgumentException("Path traversal is not allowed.", nameof(relativePath));
            }
        }
    }

    private static string EnsureTrailingDirectorySeparator (string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}
