namespace MackySoft.Ucli.Infrastructure.Execution.Lifecycle;

/// <summary>
/// Exposes only the action-owned checkpoint marker used to prove that a
/// Lifecycle Execution side-effect right reached durable admission.
/// </summary>
internal interface ILifecycleExecutionSideEffectAdmissionCheckpointStore<TCheckpoint>
    where TCheckpoint : class
{
    bool IsAdmitted (TCheckpoint checkpoint);

    ValueTask<TCheckpoint?> ReadAsync (
        Guid executionId,
        CancellationToken cancellationToken);

    ValueTask<TCheckpoint> MarkAdmittedAsync (
        TCheckpoint checkpoint,
        CancellationToken cancellationToken);
}
