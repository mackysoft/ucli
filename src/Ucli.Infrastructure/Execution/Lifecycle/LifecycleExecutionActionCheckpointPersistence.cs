using System.Text;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Execution.Lifecycle;

/// <summary>
/// Persists one action-owned Lifecycle Execution checkpoint without interpreting its state machine.
/// </summary>
internal sealed class LifecycleExecutionActionCheckpointPersistence<TCheckpoint>
    where TCheckpoint : class
{
    private const int MaximumCheckpointBytes = 4 * 1024 * 1024;

    private static readonly TimeSpan LockAcquireTimeout = TimeSpan.FromSeconds(5);

    private readonly FileLifecycleExecutionStore executionStore;
    private readonly LifecycleExecutionKind kind;
    private readonly string checkpointFileName;
    private readonly Func<TCheckpoint, Guid> executionIdSelector;

    public LifecycleExecutionActionCheckpointPersistence (
        FileLifecycleExecutionStore executionStore,
        LifecycleExecutionKind kind,
        string checkpointFileName,
        Func<TCheckpoint, Guid> executionIdSelector)
    {
        this.executionStore = executionStore
            ?? throw new ArgumentNullException(nameof(executionStore));
        if (string.IsNullOrWhiteSpace(checkpointFileName))
        {
            throw new ArgumentException(
                "Checkpoint file name must not be empty.",
                nameof(checkpointFileName));
        }

        this.kind = kind;
        this.checkpointFileName = checkpointFileName;
        this.executionIdSelector = executionIdSelector
            ?? throw new ArgumentNullException(nameof(executionIdSelector));
    }

    public TCheckpoint? Read (Guid executionId)
    {
        ValidateExecutionId(executionId);
        return ReadWithoutLockAsync(executionId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();
    }

    public ValueTask<TCheckpoint?> ReadAsync (
        Guid executionId,
        CancellationToken cancellationToken)
    {
        ValidateExecutionId(executionId);
        return ReadWithoutLockAsync(executionId, cancellationToken);
    }

    public TCheckpoint Mutate (
        Guid executionId,
        Func<TCheckpoint?, TCheckpoint> mutation)
    {
        ValidateMutation(executionId, mutation);
        using var executionLock = FileExclusiveLock.Acquire(
            ResolveLockPath(executionId),
            LockAcquireTimeout,
            CancellationToken.None);
        var current = Read(executionId);
        var next = ValidateMutationResult(
            executionId,
            mutation(current));
        if (!ReferenceEquals(current, next))
        {
            WriteWithoutLock(next);
        }

        return next;
    }

    public async ValueTask<TCheckpoint> MutateAsync (
        Guid executionId,
        Func<TCheckpoint?, TCheckpoint> mutation,
        CancellationToken cancellationToken)
    {
        ValidateMutation(executionId, mutation);
        using var executionLock = await FileExclusiveLock.AcquireAsync(
                ResolveLockPath(executionId),
                LockAcquireTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        var current = await ReadWithoutLockAsync(
                executionId,
                cancellationToken)
            .ConfigureAwait(false);
        var next = ValidateMutationResult(
            executionId,
            mutation(current));
        if (!ReferenceEquals(current, next))
        {
            await WriteWithoutLockAsync(next, cancellationToken)
                .ConfigureAwait(false);
        }

        return next;
    }

    private async ValueTask<TCheckpoint?> ReadWithoutLockAsync (
        Guid executionId,
        CancellationToken cancellationToken)
    {
        var bytes = await FileUtilities.ReadBytesOrNullWithinLimitAsync(
                ResolveCheckpointPath(executionId),
                MaximumCheckpointBytes,
                cancellationToken)
            .ConfigureAwait(false);
        if (!bytes.HasValue)
        {
            return null;
        }

        TCheckpoint? checkpoint;
        try
        {
            checkpoint = JsonSerializer.Deserialize<TCheckpoint>(
                bytes.Value.Span,
                IpcJsonSerializerOptions.Default);
        }
        catch (Exception exception) when (
            exception is JsonException or ArgumentException)
        {
            throw new IOException(
                $"Lifecycle Execution action checkpoint is invalid for execution '{executionId:D}'.",
                exception);
        }

        return ValidatePersistedCheckpoint(executionId, checkpoint);
    }

    private void WriteWithoutLock (TCheckpoint checkpoint)
    {
        var executionId = executionIdSelector(checkpoint);
        var path = ResolveCheckpointPath(executionId);
        EnsureCheckpointDirectory(path);
        var json = SerializeWithinLimit(checkpoint);
        FileUtilities.WriteAllTextAtomically(path, json);
    }

    private async ValueTask WriteWithoutLockAsync (
        TCheckpoint checkpoint,
        CancellationToken cancellationToken)
    {
        var executionId = executionIdSelector(checkpoint);
        var path = ResolveCheckpointPath(executionId);
        EnsureCheckpointDirectory(path);
        var json = SerializeWithinLimit(checkpoint);
        await FileUtilities.WriteAllTextAtomicallyAsync(
                path,
                json,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private TCheckpoint ValidatePersistedCheckpoint (
        Guid executionId,
        TCheckpoint? checkpoint)
    {
        if (checkpoint is null
            || executionIdSelector(checkpoint) != executionId)
        {
            throw new IOException(
                $"Lifecycle Execution action checkpoint identity does not match execution '{executionId:D}'.");
        }

        return checkpoint;
    }

    private TCheckpoint ValidateMutationResult (
        Guid executionId,
        TCheckpoint? checkpoint)
    {
        if (checkpoint is null)
        {
            throw new InvalidOperationException(
                "Lifecycle Execution action checkpoint mutation returned no checkpoint.");
        }
        if (executionIdSelector(checkpoint) != executionId)
        {
            throw new InvalidOperationException(
                $"Lifecycle Execution action checkpoint mutation changed execution identity '{executionId:D}'.");
        }

        return checkpoint;
    }

    private string SerializeWithinLimit (TCheckpoint checkpoint)
    {
        var json = JsonSerializer.Serialize(
                checkpoint,
                IpcJsonSerializerOptions.Default)
            + Environment.NewLine;
        if (Encoding.UTF8.GetByteCount(json) > MaximumCheckpointBytes)
        {
            throw new IOException(
                $"Lifecycle Execution action checkpoint exceeds the {MaximumCheckpointBytes}-byte persistence limit.");
        }

        return json;
    }

    private static void EnsureCheckpointDirectory (
        MackySoft.FileSystem.AbsolutePath path)
    {
        if (!path.TryGetParent(out var directoryPath))
        {
            throw new InvalidOperationException(
                $"Lifecycle Execution action checkpoint directory could not be resolved: {path.Value}");
        }

        FileSystemAccessBoundary.EnsureSecureDirectory(directoryPath);
    }

    private static void ValidateMutation (
        Guid executionId,
        Func<TCheckpoint?, TCheckpoint> mutation)
    {
        ValidateExecutionId(executionId);
        if (mutation is null)
        {
            throw new ArgumentNullException(nameof(mutation));
        }
    }

    private static void ValidateExecutionId (Guid executionId)
    {
        if (executionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Lifecycle Execution identifier must not be empty.",
                nameof(executionId));
        }
    }

    private MackySoft.FileSystem.AbsolutePath ResolveCheckpointPath (
        Guid executionId)
    {
        return executionStore.Paths.ResolveCheckpointPath(
            kind,
            executionId,
            checkpointFileName);
    }

    private MackySoft.FileSystem.AbsolutePath ResolveLockPath (
        Guid executionId)
    {
        return executionStore.Paths.ResolveLockPath(kind, executionId);
    }
}
