using System.Text.Json;
using MackySoft.Ucli.Application.Features.Recording.Registry;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Recording.Registry;

/// <summary>Persists recording execution state under its guarded project work scope.</summary>
internal sealed class FileGameViewRecordingExecutionStore : IGameViewRecordingExecutionStore
{
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(5);

    public async ValueTask<GameViewRecordingStoredExecution?> ReadAsync (
        ResolvedUnityProjectContext project,
        Guid recordingId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (recordingId == Guid.Empty)
        {
            throw new ArgumentException("Recording id must not be empty.", nameof(recordingId));
        }

        var statePath = UcliStoragePathResolver.ResolveGameViewRecordingExecutionStatePath(
            project.RepositoryRoot,
            project.ProjectFingerprint,
            recordingId);
        if (!File.Exists(statePath.Value))
        {
            return null;
        }

        var lockPath = UcliStoragePathResolver.ResolveGameViewRecordingExecutionStateLockPath(
            project.RepositoryRoot,
            project.ProjectFingerprint,
            recordingId);
        using var stateLock = await FileExclusiveLock.AcquireAsync(
                lockPath,
                LockTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!File.Exists(statePath.Value))
        {
            return null;
        }

        FileSystemAccessBoundary.EnsureSecureFile(statePath);
        var json = await File.ReadAllTextAsync(statePath.Value, cancellationToken).ConfigureAwait(false);
        return Deserialize(json, recordingId);
    }

    public async ValueTask<GameViewRecordingStoredExecution?> ReadCurrentAsync (
        ResolvedUnityProjectContext project,
        Guid runtimeId,
        CancellationToken cancellationToken = default)
    {
        if (runtimeId == Guid.Empty)
        {
            throw new ArgumentException("Runtime id must not be empty.", nameof(runtimeId));
        }

        ArgumentNullException.ThrowIfNull(project);
        var workDirectory = UcliStoragePathResolver.ResolveGameViewRecordingWorkDirectory(
            project.RepositoryRoot,
            project.ProjectFingerprint);
        if (!Directory.Exists(workDirectory.Value))
        {
            return null;
        }

        FileSystemAccessBoundary.EnsureSecureDirectory(workDirectory);
        GameViewRecordingStoredExecution? selected = null;
        foreach (var entryText in Directory.EnumerateDirectories(workDirectory.Value))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!AbsolutePath.TryParse(entryText, out var entryPath, out _))
            {
                throw new InvalidDataException("Recording work entry is not an absolute guarded path.");
            }

            var containedEntry = ContainedPath.Create(workDirectory, entryPath);
            if (File.GetAttributes(containedEntry.Target.Value).HasFlag(FileAttributes.ReparsePoint))
            {
                throw new InvalidDataException("Recording work entries must not be reparse points.");
            }

            var entryName = containedEntry.RelativePath.Value;
            if (!StoragePathSegmentCodec.TryDecodeNonEmptyGuid(entryName, out var recordingId))
            {
                throw new InvalidDataException("Recording work entry name is not a recording storage key.");
            }

            var candidate = await ReadAsync(project, recordingId, cancellationToken).ConfigureAwait(false);
            if (candidate is null
                || candidate.Payload.ExecutionReference.Lifecycle == ExecutionLifecycle.Terminal
                || candidate.StartBinding.Runtime.RuntimeId != runtimeId)
            {
                continue;
            }

            if (selected is not null && selected.RecordingId != candidate.RecordingId)
            {
                throw new InvalidDataException(
                    "More than one non-terminal GameView recording is registered for one Unity runtime.");
            }

            selected = candidate;
        }

        return selected;
    }

    public async ValueTask WriteAsync (
        ResolvedUnityProjectContext project,
        AbsolutePath executionStatePath,
        GameViewRecordingStoredExecution execution,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(executionStatePath);
        ArgumentNullException.ThrowIfNull(execution);

        var expectedPath = UcliStoragePathResolver.ResolveGameViewRecordingExecutionStatePath(
            project.RepositoryRoot,
            project.ProjectFingerprint,
            execution.RecordingId);
        if (executionStatePath != expectedPath)
        {
            throw new InvalidOperationException("Recording state path does not match its guarded storage identity.");
        }

        var lockPath = UcliStoragePathResolver.ResolveGameViewRecordingExecutionStateLockPath(
            project.RepositoryRoot,
            project.ProjectFingerprint,
            execution.RecordingId);
        using var stateLock = await FileExclusiveLock.AcquireAsync(
                lockPath,
                LockTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        var workDirectory = UcliStoragePathResolver.ResolveGameViewRecordingExecutionWorkDirectory(
            project.RepositoryRoot,
            project.ProjectFingerprint,
            execution.RecordingId);
        FileSystemAccessBoundary.EnsureSecureDirectory(workDirectory);
        await FileUtilities.WriteAllTextAtomicallyAsync(
                executionStatePath,
                Serialize(execution),
                cancellationToken)
            .ConfigureAwait(false);
        FileSystemAccessBoundary.EnsureSecureFile(executionStatePath);
    }

    public async ValueTask<GameViewRecordingCheckpointExchangeResult> CompareExchangeAsync (
        ResolvedUnityProjectContext project,
        AbsolutePath executionStatePath,
        GameViewRecordingStoredExecution expected,
        GameViewRecordingStoredExecution replacement,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(executionStatePath);
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(replacement);
        if (expected.RecordingId != replacement.RecordingId
            || expected.RequestDigest != replacement.RequestDigest
            || expected.RequestRef != replacement.RequestRef
            || expected.StartBinding != replacement.StartBinding
            || expected.StartDispatchDeadlineUtc != replacement.StartDispatchDeadlineUtc)
        {
            throw new ArgumentException(
                "A recording checkpoint replacement must preserve its durable execution identity.",
                nameof(replacement));
        }

        var expectedPath = UcliStoragePathResolver.ResolveGameViewRecordingExecutionStatePath(
            project.RepositoryRoot,
            project.ProjectFingerprint,
            expected.RecordingId);
        if (executionStatePath != expectedPath)
        {
            throw new InvalidOperationException("Recording state path does not match its guarded storage identity.");
        }

        var lockPath = UcliStoragePathResolver.ResolveGameViewRecordingExecutionStateLockPath(
            project.RepositoryRoot,
            project.ProjectFingerprint,
            expected.RecordingId);
        using var stateLock = await FileExclusiveLock.AcquireAsync(
                lockPath,
                LockTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!File.Exists(executionStatePath.Value))
        {
            throw new InvalidOperationException("Recording execution state disappeared before checkpoint replacement.");
        }

        FileSystemAccessBoundary.EnsureSecureFile(executionStatePath);
        var currentJson = await File.ReadAllTextAsync(executionStatePath.Value, cancellationToken)
            .ConfigureAwait(false);
        var current = Deserialize(currentJson, expected.RecordingId);
        var expectedJson = Serialize(expected);
        if (!string.Equals(currentJson, expectedJson, StringComparison.Ordinal))
        {
            return GameViewRecordingCheckpointExchangeResult.Unchanged(current);
        }

        var replacementJson = Serialize(replacement);
        await FileUtilities.WriteAllTextAtomicallyAsync(
                executionStatePath,
                replacementJson,
                cancellationToken)
            .ConfigureAwait(false);
        FileSystemAccessBoundary.EnsureSecureFile(executionStatePath);
        return GameViewRecordingCheckpointExchangeResult.Replaced(replacement);
    }

    public async ValueTask<IGameViewRecordingAdmissionLease?> TryAcquireAdmissionLeaseAsync (
        ResolvedUnityProjectContext project,
        Guid recordingId,
        IpcGameViewRecordingStartBinding startBinding,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(startBinding);
        if (recordingId == Guid.Empty)
        {
            throw new ArgumentException("Recording id must not be empty.", nameof(recordingId));
        }

        var admissionLockPath = UcliStoragePathResolver.ResolveGameViewRecordingAdmissionLockPath(
            project.RepositoryRoot,
            project.ProjectFingerprint,
            startBinding.Runtime.RuntimeId);
        FileExclusiveLock? admissionLock = null;
        try
        {
            admissionLock = await FileExclusiveLock.AcquireAsync(
                    admissionLockPath,
                    timeout,
                    cancellationToken)
                .ConfigureAwait(false);
            await RecoverOrphanedStateWriteTemporaryFilesAsync(
                    project,
                    recordingId,
                    cancellationToken)
                .ConfigureAwait(false);
            var lease = new AdmissionLease(
                this,
                project,
                recordingId,
                startBinding,
                admissionLock);
            admissionLock = null;
            return lease;
        }
        catch (TimeoutException)
        {
            return null;
        }
        finally
        {
            admissionLock?.Dispose();
        }
    }

    public async ValueTask<IGameViewRecordingTerminalPublicationLease?> TryAcquireTerminalPublicationLeaseAsync (
        ResolvedUnityProjectContext project,
        Guid recordingId,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (recordingId == Guid.Empty)
        {
            throw new ArgumentException("Recording id must not be empty.", nameof(recordingId));
        }

        var publicationLockPath = UcliStoragePathResolver.ResolveGameViewRecordingTerminalPublicationLockPath(
            project.RepositoryRoot,
            project.ProjectFingerprint,
            recordingId);
        try
        {
            var publicationLock = await FileExclusiveLock.AcquireAsync(
                    publicationLockPath,
                    timeout,
                    cancellationToken)
                .ConfigureAwait(false);
            return new TerminalPublicationLease(publicationLock);
        }
        catch (TimeoutException)
        {
            return null;
        }
    }

    private static GameViewRecordingStoredExecution Deserialize (
        string json,
        Guid expectedRecordingId)
    {
        var execution = JsonSerializer.Deserialize<GameViewRecordingStoredExecution>(
            json,
            IpcJsonSerializerOptions.Default);
        if (execution is null || execution.RecordingId != expectedRecordingId)
        {
            throw new InvalidDataException("Recording execution state does not match its storage identity.");
        }

        return execution;
    }

    private static string Serialize (GameViewRecordingStoredExecution execution) =>
        JsonSerializer.Serialize(execution, IpcJsonSerializerOptions.Default)
        + Environment.NewLine;

    private async ValueTask<GameViewRecordingRegistrationResult> TryRegisterWithinAdmissionAsync (
        ResolvedUnityProjectContext project,
        Guid runtimeId,
        AbsolutePath executionStatePath,
        GameViewRecordingStoredExecution execution,
        CancellationToken cancellationToken)
    {
        var expectedPath = UcliStoragePathResolver.ResolveGameViewRecordingExecutionStatePath(
            project.RepositoryRoot,
            project.ProjectFingerprint,
            execution.RecordingId);
        if (executionStatePath != expectedPath)
        {
            throw new InvalidOperationException("Recording state path does not match its guarded storage identity.");
        }

        var current = await ReadCurrentAsync(project, runtimeId, cancellationToken).ConfigureAwait(false);
        if (current is not null)
        {
            return GameViewRecordingRegistrationResult.Rejected(current);
        }

        var lockPath = UcliStoragePathResolver.ResolveGameViewRecordingExecutionStateLockPath(
            project.RepositoryRoot,
            project.ProjectFingerprint,
            execution.RecordingId);
        using var stateLock = await FileExclusiveLock.AcquireAsync(
                lockPath,
                LockTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (File.Exists(executionStatePath.Value))
        {
            FileSystemAccessBoundary.EnsureSecureFile(executionStatePath);
            var existingJson = await File.ReadAllTextAsync(
                    executionStatePath.Value,
                    cancellationToken)
                .ConfigureAwait(false);
            return GameViewRecordingRegistrationResult.Rejected(
                Deserialize(existingJson, execution.RecordingId));
        }

        RecoverOrphanedStateWriteTemporaryFiles(
            project,
            execution.RecordingId,
            cancellationToken);
        var workDirectory = UcliStoragePathResolver.ResolveGameViewRecordingExecutionWorkDirectory(
            project.RepositoryRoot,
            project.ProjectFingerprint,
            execution.RecordingId);
        FileSystemAccessBoundary.EnsureSecureDirectory(workDirectory);
        await FileUtilities.WriteAllTextAtomicallyAsync(
                executionStatePath,
                Serialize(execution),
                cancellationToken)
            .ConfigureAwait(false);
        FileSystemAccessBoundary.EnsureSecureFile(executionStatePath);
        return GameViewRecordingRegistrationResult.Created();
    }

    private static void RecoverOrphanedStateWriteTemporaryFiles (
        ResolvedUnityProjectContext project,
        Guid recordingId,
        CancellationToken cancellationToken)
    {
        var executionStatePath = UcliStoragePathResolver.ResolveGameViewRecordingExecutionStatePath(
            project.RepositoryRoot,
            project.ProjectFingerprint,
            recordingId);
        if (File.Exists(executionStatePath.Value))
        {
            return;
        }

        var executionWorkDirectory = UcliStoragePathResolver.ResolveGameViewRecordingExecutionWorkDirectory(
            project.RepositoryRoot,
            project.ProjectFingerprint,
            recordingId);
        if (!Directory.Exists(executionWorkDirectory.Value))
        {
            return;
        }

        FileSystemAccessBoundary.EnsureSecureDirectory(executionWorkDirectory);
        foreach (var entryText in Directory.EnumerateFiles(executionWorkDirectory.Value))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entryName = Path.GetFileName(entryText);
            if (!entryName.StartsWith(
                    FileUtilities.AtomicWriteTemporaryFileNamePrefix,
                    StringComparison.Ordinal))
            {
                continue;
            }
            if (!AbsolutePath.TryParse(entryText, out var entryPath, out _))
            {
                throw new InvalidDataException(
                    "Recording execution-state temporary entry is not an absolute guarded path.");
            }

            var containedEntry = ContainedPath.Create(executionWorkDirectory, entryPath);
            FileSystemAccessBoundary.EnsureSecureFile(containedEntry.Target);
            File.Delete(containedEntry.Target.Value);
        }
    }

    private static async ValueTask RecoverOrphanedStateWriteTemporaryFilesAsync (
        ResolvedUnityProjectContext project,
        Guid recordingId,
        CancellationToken cancellationToken)
    {
        var lockPath = UcliStoragePathResolver.ResolveGameViewRecordingExecutionStateLockPath(
            project.RepositoryRoot,
            project.ProjectFingerprint,
            recordingId);
        using var stateLock = await FileExclusiveLock.AcquireAsync(
                lockPath,
                LockTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        RecoverOrphanedStateWriteTemporaryFiles(project, recordingId, cancellationToken);
    }

    private sealed class AdmissionLease : IGameViewRecordingAdmissionLease
    {
        private readonly FileGameViewRecordingExecutionStore store;
        private FileExclusiveLock? admissionLock;

        public AdmissionLease (
            FileGameViewRecordingExecutionStore store,
            ResolvedUnityProjectContext project,
            Guid recordingId,
            IpcGameViewRecordingStartBinding startBinding,
            FileExclusiveLock admissionLock)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            Project = project ?? throw new ArgumentNullException(nameof(project));
            RecordingId = recordingId;
            StartBinding = startBinding ?? throw new ArgumentNullException(nameof(startBinding));
            this.admissionLock = admissionLock
                ?? throw new ArgumentNullException(nameof(admissionLock));
        }

        public ResolvedUnityProjectContext Project { get; }

        public Guid RecordingId { get; }

        public IpcGameViewRecordingStartBinding StartBinding { get; }

        public ValueTask<GameViewRecordingRegistrationResult> TryRegisterAsync (
            AbsolutePath executionStatePath,
            GameViewRecordingStoredExecution execution,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(admissionLock is null, this);
            ArgumentNullException.ThrowIfNull(executionStatePath);
            ArgumentNullException.ThrowIfNull(execution);
            if (execution.RecordingId != RecordingId)
            {
                throw new ArgumentException(
                    "The registered execution must match the admission lease recording id.",
                    nameof(execution));
            }
            if (execution.StartBinding != StartBinding)
            {
                throw new ArgumentException(
                    "The registered execution must match the admission lease start binding.",
                    nameof(execution));
            }

            return store.TryRegisterWithinAdmissionAsync(
                Project,
                StartBinding.Runtime.RuntimeId,
                executionStatePath,
                execution,
                cancellationToken);
        }

        public void Dispose () =>
            Interlocked.Exchange(ref admissionLock, null)?.Dispose();
    }

    private sealed class TerminalPublicationLease : IGameViewRecordingTerminalPublicationLease
    {
        private FileExclusiveLock? publicationLock;

        public TerminalPublicationLease (FileExclusiveLock publicationLock)
        {
            this.publicationLock = publicationLock
                ?? throw new ArgumentNullException(nameof(publicationLock));
        }

        public void Dispose () =>
            Interlocked.Exchange(ref publicationLock, null)?.Dispose();
    }
}
