using System.Text;
using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Assurance.Build;

/// <summary>
/// Owns one build output staging directory until the output tree and its manifest commit marker are published.
/// </summary>
/// <remarks>
/// The caller must hold the build-run accounting lock for the complete lifetime of this object. Under that lock,
/// the reserved staging path and an output tree without a regular manifest commit marker belong exclusively to
/// the artifact store. Cleanup rejects reparse points, special files, and committed output instead of removing them.
/// </remarks>
internal sealed class BuildOutputPublication : IDisposable
{
    private const string StagingDirectoryName = ".output-staging";

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string commitMarkerPath;
    private readonly string finalOutputDirectory;

    private bool commitMarkerOwned;
    private string? commitMarkerTemporaryPath;
    private bool finalOutputOwned;
    private bool stagingOwned;

    private BuildOutputPublication (
        string stagingDirectory,
        string finalOutputDirectory,
        string commitMarkerPath)
    {
        StagingDirectory = stagingDirectory;
        this.finalOutputDirectory = finalOutputDirectory;
        this.commitMarkerPath = commitMarkerPath;
        stagingOwned = true;
    }

    public string StagingDirectory { get; }

    /// <summary>
    /// Reserves the run-scoped staging directory after recovering an uncommitted tree owned by the artifact store.
    /// </summary>
    /// <param name="paths"> The validated paths for one prepared build run. </param>
    /// <returns> The publication owner that must be disposed before releasing the build-run accounting lock. </returns>
    /// <exception cref="InvalidOperationException"> Thrown when a regular manifest already commits this run. </exception>
    /// <exception cref="IOException"> Thrown when a reserved path contains a node the artifact store cannot safely recover. </exception>
    public static BuildOutputPublication Begin (BuildRunArtifactPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        if (File.Exists(paths.OutputManifestJsonPath))
        {
            FileUtilities.EnsureRegularFile(paths.OutputManifestJsonPath, "Build output manifest commit marker");
            EnsureOwnedOutputDirectory(
                paths.ArtifactOutputDirectory,
                "Committed build output directory");
            throw new InvalidOperationException(
                $"Build output artifacts have already been committed: {paths.OutputManifestJsonPath}");
        }

        var stagingDirectory = Path.Combine(paths.ArtifactsDirectory, StagingDirectoryName);
        DeleteOwnedOutputDirectoryIfExists(
            stagingDirectory,
            "Uncommitted build output staging directory");
        DeleteOwnedOutputDirectoryIfExists(
            paths.ArtifactOutputDirectory,
            "Uncommitted build output directory");
        FileSystemAccessBoundary.EnsureSecureDirectory(stagingDirectory);
        return new BuildOutputPublication(
            stagingDirectory,
            paths.ArtifactOutputDirectory,
            paths.OutputManifestJsonPath);
    }

    /// <summary>
    /// Writes and validates the manifest in an owned temporary file without making the commit marker visible.
    /// </summary>
    /// <param name="contents"> The complete manifest JSON to publish without an encoding preamble. </param>
    /// <param name="cancellationToken"> The token observed while writing the temporary file. </param>
    /// <returns> The temporary file path retained by this publication owner. </returns>
    public async ValueTask<string> PrepareCommitMarkerAsync (
        string contents,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contents);
        if (commitMarkerTemporaryPath != null || commitMarkerOwned)
        {
            throw new InvalidOperationException("Build output commit marker has already been prepared.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var directoryPath = Path.GetDirectoryName(commitMarkerPath)
            ?? throw new InvalidOperationException(
                $"Build output commit marker directory could not be resolved: {commitMarkerPath}");
        FileSystemAccessBoundary.EnsureSecureDirectory(directoryPath);
        var temporaryStream = FileUtilities.OpenAtomicWriteTemporaryFileInDirectory(
            directoryPath,
            out var temporaryPath);
        commitMarkerTemporaryPath = temporaryPath;
        using (temporaryStream)
        using (var writer = new StreamWriter(temporaryStream, Utf8NoBom))
        {
            await writer.WriteAsync(contents.AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        FileSystemAccessBoundary.EnsureSecureFile(temporaryPath);
        return temporaryPath;
    }

    /// <summary>
    /// Atomically renames the complete staging tree to the final output path while retaining rollback ownership.
    /// </summary>
    public void PublishOutput ()
    {
        if (!stagingOwned || finalOutputOwned)
        {
            throw new InvalidOperationException("Build output staging is not available for publication.");
        }

        if (File.Exists(finalOutputDirectory) || Directory.Exists(finalOutputDirectory))
        {
            throw new IOException($"Build output publication target already exists: {finalOutputDirectory}");
        }

        Directory.Move(StagingDirectory, finalOutputDirectory);
        stagingOwned = false;
        finalOutputOwned = true;
        FileSystemAccessBoundary.EnsureSecureDirectory(finalOutputDirectory);
    }

    /// <summary>
    /// Publishes the prepared manifest as the last commit marker while retaining rollback ownership.
    /// </summary>
    /// <param name="cancellationToken"> The token observed before publication and during transient replacement retries. </param>
    public async ValueTask PublishCommitMarkerAsync (CancellationToken cancellationToken)
    {
        if (!finalOutputOwned)
        {
            throw new InvalidOperationException("Build output must be published before its commit marker.");
        }

        var temporaryPath = commitMarkerTemporaryPath
            ?? throw new InvalidOperationException("Build output commit marker has not been prepared.");
        try
        {
            await FileUtilities.PublishAtomicWriteTemporaryFileAsync(
                    temporaryPath,
                    commitMarkerPath,
                    cancellationToken)
                .ConfigureAwait(false);
            commitMarkerTemporaryPath = null;
            commitMarkerOwned = true;
        }
        catch
        {
            if (!File.Exists(temporaryPath) && File.Exists(commitMarkerPath))
            {
                commitMarkerTemporaryPath = null;
                commitMarkerOwned = true;
            }

            throw;
        }
    }

    /// <summary> Releases rollback ownership after both output paths have been published. </summary>
    public void Complete ()
    {
        if (!finalOutputOwned || !commitMarkerOwned)
        {
            throw new InvalidOperationException(
                "Build output and its commit marker must both be published before completion.");
        }

        finalOutputOwned = false;
        commitMarkerOwned = false;
    }

    /// <summary>
    /// Removes only paths still owned by this publication attempt; cleanup failures are reported as an I/O failure.
    /// </summary>
    public void Dispose ()
    {
        List<Exception>? cleanupFailures = null;
        var commitMarkerRemoved = TryCleanup(
            commitMarkerOwned,
            () => DeleteOwnedCommitMarkerIfExists(commitMarkerPath),
            ref cleanupFailures);
        _ = TryCleanup(
            commitMarkerTemporaryPath != null,
            () => FileUtilities.DeleteIfExists(commitMarkerTemporaryPath!),
            ref cleanupFailures);
        _ = TryCleanup(
            finalOutputOwned && commitMarkerRemoved,
            () => DeleteOwnedOutputDirectoryIfExists(
                finalOutputDirectory,
                "Uncommitted build output directory"),
            ref cleanupFailures);
        _ = TryCleanup(
            stagingOwned,
            () => DeleteOwnedOutputDirectoryIfExists(
                StagingDirectory,
                "Uncommitted build output staging directory"),
            ref cleanupFailures);

        if (cleanupFailures != null)
        {
            throw new BuildOutputPublicationRollbackException(cleanupFailures);
        }
    }

    private static void DeleteOwnedOutputDirectoryIfExists (
        string path,
        string subject)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return;
        }

        EnsureOwnedOutputDirectory(path, subject);
        Directory.Delete(path, recursive: true);
    }

    private static void EnsureOwnedOutputDirectory (
        string path,
        string subject)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            throw new IOException($"{subject} was not found: {path}");
        }

        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"{subject} must not be a reparse point: {path}");
        }

        if ((attributes & FileAttributes.Directory) == 0)
        {
            throw new IOException($"{subject} must be a directory: {path}");
        }

        foreach (var entryPath in Directory.EnumerateFileSystemEntries(path))
        {
            var entryAttributes = File.GetAttributes(entryPath);
            if ((entryAttributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"{subject} must not contain a reparse point: {entryPath}");
            }

            if ((entryAttributes & FileAttributes.Directory) != 0)
            {
                EnsureOwnedOutputDirectory(entryPath, subject);
                continue;
            }

            if (!FileSystemNodeClassifier.IsRegularFile(entryPath, entryAttributes))
            {
                throw new IOException($"{subject} must contain only regular files: {entryPath}");
            }
        }
    }

    private static void DeleteOwnedCommitMarkerIfExists (string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return;
        }

        FileUtilities.EnsureRegularFile(path, "Build output manifest commit marker");
        File.Delete(path);
    }

    private static bool TryCleanup (
        bool isOwned,
        Action cleanup,
        ref List<Exception>? failures)
    {
        if (!isOwned)
        {
            return true;
        }

        try
        {
            cleanup();
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            failures ??= [];
            failures.Add(exception);
            return false;
        }
    }

    private sealed class BuildOutputPublicationRollbackException : IOException
    {
        public BuildOutputPublicationRollbackException (IReadOnlyCollection<Exception> cleanupFailures)
            : base(
                "Failed to roll back uncommitted build output artifacts.",
                new AggregateException(cleanupFailures))
        {
        }
    }
}
