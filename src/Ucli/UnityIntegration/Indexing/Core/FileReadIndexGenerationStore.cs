using System.Buffers.Binary;
using MackySoft.FileSystem;
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
    public async ValueTask<AbsolutePath?> ResolveCurrentDirectoryAsync (
        AbsolutePath storageRoot,
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
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken)
    {
        var writeLock = await FileExclusiveLock.AcquireAsync(
                UcliStoragePathResolver.ResolveReadIndexWriteLockPath(storageRoot, projectFingerprint),
                WriteLockAcquireTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        var writeLockOwned = true;
        AbsolutePath? stagingDirectoryPath = null;
        try
        {
            var currentDirectoryPath = await ResolveCurrentDirectoryAsync(
                    storageRoot,
                    projectFingerprint,
                    cancellationToken)
                .ConfigureAwait(false);
            PruneAbandonedStagingDirectories(storageRoot, projectFingerprint);

            var allocation = CreateAvailableGeneration(
                storageRoot,
                projectFingerprint);
            var generationId = allocation.GenerationId;
            stagingDirectoryPath = allocation.StagingDirectoryPath;
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

    private static (Guid GenerationId, AbsolutePath StagingDirectoryPath) CreateAvailableGeneration (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        for (var attempt = 0; attempt < GenerationIdCreationAttemptLimit; attempt++)
        {
            var generationId = Guid.NewGuid();
            var stagingDirectoryPath = UcliStoragePathResolver.ResolveReadIndexStagingGenerationDirectory(
                storageRoot,
                projectFingerprint,
                generationId);
            var generationDirectoryPath = UcliStoragePathResolver.ResolveReadIndexGenerationDirectory(
                storageRoot,
                projectFingerprint,
                generationId);
            if (!File.Exists(stagingDirectoryPath.Value)
                && !Directory.Exists(stagingDirectoryPath.Value)
                && !File.Exists(generationDirectoryPath.Value)
                && !Directory.Exists(generationDirectoryPath.Value))
            {
                return (generationId, stagingDirectoryPath);
            }
        }

        throw new IOException("A unique read-index generation identifier could not be allocated.");
    }

    private static void ValidateCommittedGenerationDirectory (AbsolutePath generationDirectoryPath)
    {
        if (!Directory.Exists(generationDirectoryPath.Value))
        {
            throw new InvalidDataException("The current read-index generation directory was not found.");
        }

        var attributes = File.GetAttributes(generationDirectoryPath.Value);
        if ((attributes & FileAttributes.Directory) == 0
            || (attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("The current read-index generation must be a regular directory.");
        }
    }

    private static void CopyGeneration (
        AbsolutePath sourceDirectoryPath,
        AbsolutePath destinationDirectoryPath)
    {
        ValidateCommittedGenerationDirectory(sourceDirectoryPath);
        foreach (var rawSourcePath in Directory.EnumerateFileSystemEntries(
                     sourceDirectoryPath.Value,
                     "*",
                     SearchOption.TopDirectoryOnly))
        {
            if (!AbsolutePath.TryParse(rawSourcePath, out var sourcePath, out _)
                || !ContainedPath.TryCreate(sourceDirectoryPath, sourcePath, out var containedSourcePath, out _))
            {
                throw new InvalidDataException("Read-index generation entry escaped its generation directory.");
            }

            var attributes = File.GetAttributes(containedSourcePath.Target.Value);
            if (!FileSystemNodeClassifier.IsRegularFile(containedSourcePath.Target, attributes))
            {
                throw new InvalidDataException("Read-index generations must contain regular files only.");
            }

            var destinationPath = ContainedPath.Create(
                destinationDirectoryPath,
                containedSourcePath.RelativePath).Target;
            File.Copy(containedSourcePath.Target.Value, destinationPath.Value, overwrite: false);
            FileSystemAccessBoundary.EnsureSecureFile(destinationPath);
        }
    }

    private static void PruneAbandonedStagingDirectories (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        var stagingRootDirectoryPath = UcliStoragePathResolver.ResolveReadIndexStagingDirectory(
            storageRoot,
            projectFingerprint);
        if (!IsRegularDirectory(stagingRootDirectoryPath))
        {
            return;
        }

        foreach (var rawDirectoryPath in Directory.EnumerateDirectories(
                     stagingRootDirectoryPath.Value,
                     "*",
                     SearchOption.TopDirectoryOnly))
        {
            if (AbsolutePath.TryParse(rawDirectoryPath, out var directoryPath, out _)
                && ContainedPath.TryCreate(stagingRootDirectoryPath, directoryPath, out var containedDirectoryPath, out _))
            {
                TryDeleteOwnedDirectory(containedDirectoryPath.Target);
            }
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
            Directory.Move(transaction.StagingDirectoryPath.Value, generationDirectoryPath.Value);
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
        AbsolutePath storageRoot,
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
                .EnumerateDirectories(generationsDirectoryPath.Value, "*", SearchOption.TopDirectoryOnly)
                .Select(path => TryGetOwnedGeneration(generationsDirectoryPath, path))
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
        AbsolutePath storageRoot,
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
        AbsolutePath storageRoot,
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

    private static GenerationDirectory? TryGetOwnedGeneration (
        AbsolutePath expectedParent,
        string rawDirectoryPath)
    {
        if (!AbsolutePath.TryParse(rawDirectoryPath, out var directoryPath, out _)
            || !ContainedPath.TryCreate(expectedParent, directoryPath, out var containedDirectory, out _))
        {
            return null;
        }

        var directory = new DirectoryInfo(containedDirectory.Target.Value);
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
            containedDirectory.Target,
            directory.LastWriteTimeUtc);
    }

    private static bool IsRegularDirectory (AbsolutePath directoryPath)
    {
        if (!Directory.Exists(directoryPath.Value))
        {
            return false;
        }

        var attributes = File.GetAttributes(directoryPath.Value);
        return (attributes & FileAttributes.Directory) != 0
            && (attributes & FileAttributes.ReparsePoint) == 0;
    }

    private static bool TryDeleteOwnedDirectory (AbsolutePath directoryPath)
    {
        try
        {
            var directory = new DirectoryInfo(directoryPath.Value);
            if (!StoragePathSegmentCodec.TryDecodeNonEmptyGuid(directory.Name, out _))
            {
                return false;
            }

            var directoryAttributes = directory.Attributes;
            if ((directoryAttributes & FileAttributes.Directory) == 0
                || (directoryAttributes & FileAttributes.ReparsePoint) != 0)
            {
                return false;
            }

            var rawEntryPaths = Directory
                .EnumerateFileSystemEntries(
                    directoryPath.Value,
                    "*",
                    SearchOption.TopDirectoryOnly)
                .ToArray();
            var entryPaths = new AbsolutePath[rawEntryPaths.Length];
            for (var index = 0; index < rawEntryPaths.Length; index++)
            {
                if (!AbsolutePath.TryParse(rawEntryPaths[index], out var entryPath, out _)
                    || !ContainedPath.TryCreate(directoryPath, entryPath, out var containedEntryPath, out _))
                {
                    return false;
                }

                entryPaths[index] = containedEntryPath.Target;
                var attributes = File.GetAttributes(entryPaths[index].Value);
                if (!FileSystemNodeClassifier.IsRegularFile(entryPaths[index], attributes))
                {
                    return false;
                }
            }

            foreach (var entryPath in entryPaths)
            {
                FileUtilities.EnsureRegularFile(entryPath, "Read-index generation artifact");
                File.Delete(entryPath.Value);
            }

            Directory.Delete(directoryPath.Value, recursive: false);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // NOTE: Unreachable generations and staging directories are safe to retain for a later cleanup pass.
            return false;
        }
    }

    private static void TryDeleteOwnedRetentionMarker (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        Guid generationId)
    {
        var markerPath = UcliStoragePathResolver.ResolveReadIndexRetentionMarkerPath(
            storageRoot,
            projectFingerprint,
            generationId);
        try
        {
            if (!File.Exists(markerPath.Value) && !Directory.Exists(markerPath.Value))
            {
                return;
            }

            if (!StoragePathSegmentCodec.TryDecodeNonEmptyGuid(Path.GetFileName(markerPath.Value), out var markerGenerationId)
                || markerGenerationId != generationId)
            {
                return;
            }

            FileUtilities.EnsureRegularFile(markerPath, "Read-index retention marker");
            File.Delete(markerPath.Value);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // NOTE: A marker that cannot be verified as owned is retained.
        }
    }

    private readonly record struct GenerationDirectory (
        Guid GenerationId,
        AbsolutePath FullName,
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
            AbsolutePath storageRoot,
            ProjectFingerprint projectFingerprint,
            Guid generationId,
            AbsolutePath stagingDirectoryPath,
            FileExclusiveLock writeLock)
        {
            this.owner = owner;
            StorageRoot = storageRoot;
            ProjectFingerprint = projectFingerprint;
            GenerationId = generationId;
            StagingDirectoryPath = stagingDirectoryPath;
            this.writeLock = writeLock;
        }

        internal AbsolutePath StorageRoot { get; }

        internal ProjectFingerprint ProjectFingerprint { get; }

        internal Guid GenerationId { get; }

        /// <summary> Gets the owned mutable staging directory cloned from the current generation. </summary>
        public AbsolutePath StagingDirectoryPath { get; }

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
