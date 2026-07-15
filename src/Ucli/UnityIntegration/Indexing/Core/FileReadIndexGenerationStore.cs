using System.Buffers.Binary;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Core;

/// <summary> Owns immutable read-index generation staging, publication, resolution, and retention. </summary>
internal sealed class FileReadIndexGenerationStore
{
    private const int RetainedGenerationCount = 8;

    private const int GenerationIdCreationAttemptLimit = 10;

    private static readonly TimeSpan WriteLockAcquireTimeout = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan GenerationDeletionGracePeriod = TimeSpan.FromMinutes(5);

    private readonly IReadIndexGenerationPointerStore pointerStore;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="FileReadIndexGenerationStore" /> class. </summary>
    /// <param name="pointerStore"> The atomic current-generation pointer store. </param>
    /// <param name="timeProvider"> The time source used to enforce the reader grace period before deletion. </param>
    public FileReadIndexGenerationStore (
        IReadIndexGenerationPointerStore pointerStore,
        TimeProvider timeProvider)
    {
        this.pointerStore = pointerStore ?? throw new ArgumentNullException(nameof(pointerStore));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary> Resolves the generation directory selected by the current pointer. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The immutable generation directory, or <see langword="null" /> before the first commit. </returns>
    /// <exception cref="InvalidDataException"> Thrown when the pointer does not identify a complete generation directory. </exception>
    public async ValueTask<string?> ResolveCurrentDirectoryAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken)
    {
        var generationId = await pointerStore.ReadAsync(
                storageRoot,
                projectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (!generationId.HasValue)
        {
            return null;
        }

        var generationDirectoryPath = UcliStoragePathResolver.ResolveReadIndexGenerationDirectory(
            storageRoot,
            projectFingerprint,
            generationId.Value);
        ValidateCommittedGenerationDirectory(generationDirectoryPath);
        return generationDirectoryPath;
    }

    /// <summary> Begins one serialized generation mutation by cloning the current immutable generation. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The owned generation transaction. </returns>
    public async ValueTask<WriteTransaction> BeginWriteAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken)
    {
        var writeLock = await FileExclusiveLock.AcquireAsync(
                UcliStoragePathResolver.ResolveReadIndexWriteLockPath(storageRoot, projectFingerprint),
                WriteLockAcquireTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        var writeLockOwned = true;
        string? stagingDirectoryPath = null;
        try
        {
            var currentDirectoryPath = await ResolveCurrentDirectoryAsync(
                    storageRoot,
                    projectFingerprint,
                    cancellationToken)
                .ConfigureAwait(false);
            PruneAbandonedStagingDirectories(storageRoot, projectFingerprint);

            var generationId = CreateAvailableGenerationId(
                storageRoot,
                projectFingerprint,
                out stagingDirectoryPath);
            FileSystemAccessBoundary.EnsureSecureDirectory(stagingDirectoryPath);
            if (currentDirectoryPath != null)
            {
                CopyGeneration(currentDirectoryPath, stagingDirectoryPath);
            }

            var transaction = new WriteTransaction(
                this,
                storageRoot,
                projectFingerprint,
                generationId,
                stagingDirectoryPath,
                writeLock);
            writeLockOwned = false;
            stagingDirectoryPath = null;
            return transaction;
        }
        finally
        {
            if (stagingDirectoryPath != null)
            {
                TryDeleteOwnedDirectory(stagingDirectoryPath);
            }

            if (writeLockOwned)
            {
                writeLock.Dispose();
            }
        }
    }

    private static Guid CreateAvailableGenerationId (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        out string stagingDirectoryPath)
    {
        for (var attempt = 0; attempt < GenerationIdCreationAttemptLimit; attempt++)
        {
            var generationId = Guid.NewGuid();
            stagingDirectoryPath = UcliStoragePathResolver.ResolveReadIndexStagingGenerationDirectory(
                storageRoot,
                projectFingerprint,
                generationId);
            var generationDirectoryPath = UcliStoragePathResolver.ResolveReadIndexGenerationDirectory(
                storageRoot,
                projectFingerprint,
                generationId);
            if (!File.Exists(stagingDirectoryPath)
                && !Directory.Exists(stagingDirectoryPath)
                && !File.Exists(generationDirectoryPath)
                && !Directory.Exists(generationDirectoryPath))
            {
                return generationId;
            }
        }

        stagingDirectoryPath = string.Empty;
        throw new IOException("A unique read-index generation identifier could not be allocated.");
    }

    private static void ValidateCommittedGenerationDirectory (string generationDirectoryPath)
    {
        if (!Directory.Exists(generationDirectoryPath))
        {
            throw new InvalidDataException("The current read-index generation directory was not found.");
        }

        var attributes = File.GetAttributes(generationDirectoryPath);
        if ((attributes & FileAttributes.Directory) == 0
            || (attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("The current read-index generation must be a regular directory.");
        }
    }

    private static void CopyGeneration (
        string sourceDirectoryPath,
        string destinationDirectoryPath)
    {
        foreach (var sourcePath in Directory.EnumerateFileSystemEntries(sourceDirectoryPath, "*", SearchOption.TopDirectoryOnly))
        {
            var attributes = File.GetAttributes(sourcePath);
            if (!FileSystemNodeClassifier.IsRegularFile(sourcePath, attributes))
            {
                throw new InvalidDataException("Read-index generations must contain regular files only.");
            }

            var destinationPath = Path.Combine(destinationDirectoryPath, Path.GetFileName(sourcePath));
            File.Copy(sourcePath, destinationPath, overwrite: false);
            FileSystemAccessBoundary.EnsureSecureFile(destinationPath);
        }
    }

    private static void PruneAbandonedStagingDirectories (
        string storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        var stagingRootDirectoryPath = UcliStoragePathResolver.ResolveReadIndexStagingDirectory(
            storageRoot,
            projectFingerprint);
        if (!IsRegularDirectory(stagingRootDirectoryPath))
        {
            return;
        }

        foreach (var directoryPath in Directory.EnumerateDirectories(stagingRootDirectoryPath, "*", SearchOption.TopDirectoryOnly))
        {
            TryDeleteOwnedDirectory(directoryPath);
        }
    }

    private async ValueTask CommitAsync (
        WriteTransaction transaction,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var generationDirectoryPath = UcliStoragePathResolver.ResolveReadIndexGenerationDirectory(
            transaction.StorageRoot,
            transaction.ProjectFingerprint,
            transaction.GenerationId);
        var generationDirectoryOwned = false;
        try
        {
            FileSystemAccessBoundary.EnsureSecureDirectory(
                UcliStoragePathResolver.ResolveReadIndexGenerationsDirectory(
                    transaction.StorageRoot,
                    transaction.ProjectFingerprint));
            Directory.Move(transaction.StagingDirectoryPath, generationDirectoryPath);
            transaction.ReleaseStagingDirectoryOwnership();
            generationDirectoryOwned = true;

            try
            {
                await pointerStore.PublishAsync(
                        transaction.StorageRoot,
                        transaction.ProjectFingerprint,
                        transaction.GenerationId,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception)
            {
                Guid? publishedGenerationId;
                try
                {
                    publishedGenerationId = await pointerStore.ReadAsync(
                            transaction.StorageRoot,
                            transaction.ProjectFingerprint,
                            CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception)
                {
                    generationDirectoryOwned = false;
                    throw;
                }

                if (publishedGenerationId != transaction.GenerationId)
                {
                    throw;
                }
            }

            generationDirectoryOwned = false;
            transaction.MarkCommitted();
        }
        finally
        {
            if (generationDirectoryOwned)
            {
                TryDeleteOwnedDirectory(generationDirectoryPath);
            }
        }

        await PruneGenerationsAsync(
                transaction.StorageRoot,
                transaction.ProjectFingerprint)
            .ConfigureAwait(false);
    }

    private async ValueTask PruneGenerationsAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        var generationsDirectoryPath = UcliStoragePathResolver.ResolveReadIndexGenerationsDirectory(
            storageRoot,
            projectFingerprint);
        try
        {
            if (!IsRegularDirectory(generationsDirectoryPath))
            {
                return;
            }

            var currentGenerationId = await TryReadCurrentGenerationAsync(
                    storageRoot,
                    projectFingerprint)
                .ConfigureAwait(false);
            if (!currentGenerationId.HasValue)
            {
                return;
            }

            var generationDirectories = Directory
                .EnumerateDirectories(generationsDirectoryPath, "*", SearchOption.TopDirectoryOnly)
                .Select(static path => new DirectoryInfo(path))
                .Select(static directory => TryGetOwnedGeneration(directory))
                .Where(static generation => generation.HasValue)
                .Select(static generation => generation!.Value)
                .ToArray();
            var staleDirectories = generationDirectories
                .Where(generation => generation.GenerationId != currentGenerationId.Value)
                .OrderByDescending(static directory => directory.LastWriteTimeUtc)
                .Skip(RetainedGenerationCount - 1)
                .ToArray();
            var staleGenerationIds = staleDirectories
                .Select(static directory => directory.GenerationId)
                .ToHashSet();

            foreach (var retainedDirectory in generationDirectories)
            {
                if (!staleGenerationIds.Contains(retainedDirectory.GenerationId))
                {
                    TryDeleteOwnedRetentionMarker(
                        storageRoot,
                        projectFingerprint,
                        retainedDirectory.GenerationId);
                }
            }

            foreach (var staleDirectory in staleDirectories)
            {
                currentGenerationId = await TryReadCurrentGenerationAsync(
                        storageRoot,
                        projectFingerprint)
                    .ConfigureAwait(false);
                if (!currentGenerationId.HasValue)
                {
                    return;
                }

                if (staleDirectory.GenerationId == currentGenerationId.Value)
                {
                    TryDeleteOwnedRetentionMarker(
                        storageRoot,
                        projectFingerprint,
                        staleDirectory.GenerationId);
                    continue;
                }

                if (!await HasDeletionGracePeriodElapsedAsync(
                            storageRoot,
                            projectFingerprint,
                            staleDirectory.GenerationId)
                        .ConfigureAwait(false))
                {
                    continue;
                }

                currentGenerationId = await TryReadCurrentGenerationAsync(
                        storageRoot,
                        projectFingerprint)
                    .ConfigureAwait(false);
                if (!currentGenerationId.HasValue)
                {
                    return;
                }

                if (staleDirectory.GenerationId == currentGenerationId.Value)
                {
                    TryDeleteOwnedRetentionMarker(
                        storageRoot,
                        projectFingerprint,
                        staleDirectory.GenerationId);
                    continue;
                }

                if (TryDeleteOwnedDirectory(staleDirectory.FullName))
                {
                    TryDeleteOwnedRetentionMarker(
                        storageRoot,
                        projectFingerprint,
                        staleDirectory.GenerationId);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            // NOTE: Retention is housekeeping after the current pointer has committed.
        }
    }

    private async ValueTask<Guid?> TryReadCurrentGenerationAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        try
        {
            var generationId = await pointerStore.ReadAsync(
                    storageRoot,
                    projectFingerprint,
                    CancellationToken.None)
                .ConfigureAwait(false);
            if (!generationId.HasValue)
            {
                return null;
            }

            ValidateCommittedGenerationDirectory(
                UcliStoragePathResolver.ResolveReadIndexGenerationDirectory(
                    storageRoot,
                    projectFingerprint,
                    generationId.Value));
            return generationId;
        }
        catch (Exception exception) when (exception is
            IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            ArgumentException or
            NotSupportedException or
            System.Security.SecurityException)
        {
            return null;
        }
    }

    private async ValueTask<bool> HasDeletionGracePeriodElapsedAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        Guid generationId)
    {
        var markerPath = UcliStoragePathResolver.ResolveReadIndexRetentionMarkerPath(
            storageRoot,
            projectFingerprint,
            generationId);
        ReadOnlyMemory<byte>? markerValue;
        try
        {
            markerValue = await FileUtilities.ReadBytesOrNullWithinLimitAsync(
                    markerPath,
                    sizeof(long),
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }

        var nowUtcTicks = timeProvider.GetUtcNow().UtcDateTime.Ticks;
        if (markerValue == null)
        {
            try
            {
                FileSystemAccessBoundary.EnsureSecureDirectory(
                    UcliStoragePathResolver.ResolveReadIndexRetentionDirectory(
                        storageRoot,
                        projectFingerprint));
                var markerBytes = new byte[sizeof(long)];
                BinaryPrimitives.WriteInt64BigEndian(markerBytes, nowUtcTicks);
                await FileUtilities.WriteAllBytesAtomicallyAsync(
                        markerPath,
                        markerBytes,
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // NOTE: Failure to persist deletion eligibility must retain the generation.
            }

            return false;
        }

        if (markerValue.Value.Length != sizeof(long))
        {
            return false;
        }

        var eligibleSinceUtcTicks = BinaryPrimitives.ReadInt64BigEndian(markerValue.Value.Span);
        if (eligibleSinceUtcTicks < DateTime.MinValue.Ticks
            || eligibleSinceUtcTicks > nowUtcTicks)
        {
            return false;
        }

        return nowUtcTicks - eligibleSinceUtcTicks >= GenerationDeletionGracePeriod.Ticks;
    }

    private static GenerationDirectory? TryGetOwnedGeneration (DirectoryInfo directory)
    {
        if (!StoragePathSegmentCodec.TryDecodeNonEmptyGuid(directory.Name, out var generationId))
        {
            return null;
        }

        FileAttributes attributes;
        try
        {
            attributes = directory.Attributes;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        if ((attributes & FileAttributes.Directory) == 0
            || (attributes & FileAttributes.ReparsePoint) != 0)
        {
            return null;
        }

        return new GenerationDirectory(
            generationId,
            directory.FullName,
            directory.LastWriteTimeUtc);
    }

    private static bool IsRegularDirectory (string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return false;
        }

        var attributes = File.GetAttributes(directoryPath);
        return (attributes & FileAttributes.Directory) != 0
            && (attributes & FileAttributes.ReparsePoint) == 0;
    }

    private static bool TryDeleteOwnedDirectory (string directoryPath)
    {
        try
        {
            var directory = new DirectoryInfo(directoryPath);
            if (TryGetOwnedGeneration(directory) == null)
            {
                return false;
            }

            var entryPaths = Directory.EnumerateFileSystemEntries(
                    directoryPath,
                    "*",
                    SearchOption.TopDirectoryOnly)
                .ToArray();
            foreach (var entryPath in entryPaths)
            {
                var attributes = File.GetAttributes(entryPath);
                if (!FileSystemNodeClassifier.IsRegularFile(entryPath, attributes))
                {
                    return false;
                }
            }

            foreach (var entryPath in entryPaths)
            {
                FileUtilities.EnsureRegularFile(entryPath, "Read-index generation artifact");
                File.Delete(entryPath);
            }

            Directory.Delete(directoryPath, recursive: false);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // NOTE: Unreachable generations and staging directories are safe to retain for a later cleanup pass.
            return false;
        }
    }

    private static void TryDeleteOwnedRetentionMarker (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        Guid generationId)
    {
        var markerPath = UcliStoragePathResolver.ResolveReadIndexRetentionMarkerPath(
            storageRoot,
            projectFingerprint,
            generationId);
        try
        {
            if (!File.Exists(markerPath) && !Directory.Exists(markerPath))
            {
                return;
            }

            if (!StoragePathSegmentCodec.TryDecodeNonEmptyGuid(Path.GetFileName(markerPath), out var markerGenerationId)
                || markerGenerationId != generationId)
            {
                return;
            }

            FileUtilities.EnsureRegularFile(markerPath, "Read-index retention marker");
            File.Delete(markerPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // NOTE: A marker that cannot be verified as owned is retained.
        }
    }

    private readonly record struct GenerationDirectory (
        Guid GenerationId,
        string FullName,
        DateTime LastWriteTimeUtc);

    /// <summary> Owns one staged generation and the project writer lock until commit or disposal. </summary>
    internal sealed class WriteTransaction : IDisposable
    {
        private readonly FileReadIndexGenerationStore owner;

        private FileExclusiveLock? writeLock;

        private bool stagingDirectoryOwned = true;

        private bool committed;

        internal WriteTransaction (
            FileReadIndexGenerationStore owner,
            string storageRoot,
            ProjectFingerprint projectFingerprint,
            Guid generationId,
            string stagingDirectoryPath,
            FileExclusiveLock writeLock)
        {
            this.owner = owner;
            StorageRoot = storageRoot;
            ProjectFingerprint = projectFingerprint;
            GenerationId = generationId;
            StagingDirectoryPath = stagingDirectoryPath;
            this.writeLock = writeLock;
        }

        internal string StorageRoot { get; }

        internal ProjectFingerprint ProjectFingerprint { get; }

        internal Guid GenerationId { get; }

        /// <summary> Gets the owned mutable staging directory cloned from the current generation. </summary>
        public string StagingDirectoryPath { get; }

        /// <summary> Publishes this staged directory as the complete current generation. </summary>
        /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
        /// <returns> A task that completes after the current pointer commits. </returns>
        public ValueTask CommitAsync (CancellationToken cancellationToken)
        {
            if (writeLock == null)
            {
                throw new ObjectDisposedException(nameof(WriteTransaction));
            }

            if (committed)
            {
                throw new InvalidOperationException("The read-index generation transaction has already committed.");
            }

            return owner.CommitAsync(this, cancellationToken);
        }

        /// <inheritdoc />
        public void Dispose ()
        {
            if (stagingDirectoryOwned)
            {
                TryDeleteOwnedDirectory(StagingDirectoryPath);
                stagingDirectoryOwned = false;
            }

            writeLock?.Dispose();
            writeLock = null;
        }

        internal void ReleaseStagingDirectoryOwnership ()
        {
            stagingDirectoryOwned = false;
        }

        internal void MarkCommitted ()
        {
            committed = true;
        }
    }
}
