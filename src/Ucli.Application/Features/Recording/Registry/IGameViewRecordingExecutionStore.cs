using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Recording.Registry;

/// <summary>Persists the host-owned recording registry independently of one CLI process.</summary>
internal interface IGameViewRecordingExecutionStore
{
    ValueTask<GameViewRecordingStoredExecution?> ReadAsync (
        ResolvedUnityProjectContext project,
        Guid recordingId,
        CancellationToken cancellationToken = default);

    ValueTask<GameViewRecordingStoredExecution?> ReadCurrentAsync (
        ResolvedUnityProjectContext project,
        Guid runtimeId,
        CancellationToken cancellationToken = default);

    ValueTask WriteAsync (
        ResolvedUnityProjectContext project,
        AbsolutePath executionStatePath,
        GameViewRecordingStoredExecution execution,
        CancellationToken cancellationToken = default);

    ValueTask<GameViewRecordingCheckpointExchangeResult> CompareExchangeAsync (
        ResolvedUnityProjectContext project,
        AbsolutePath executionStatePath,
        GameViewRecordingStoredExecution expected,
        GameViewRecordingStoredExecution replacement,
        CancellationToken cancellationToken = default);

    ValueTask<IGameViewRecordingAdmissionLease?> TryAcquireAdmissionLeaseAsync (
        ResolvedUnityProjectContext project,
        Guid recordingId,
        IpcGameViewRecordingStartBinding startBinding,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    ValueTask<IGameViewRecordingTerminalPublicationLease?> TryAcquireTerminalPublicationLeaseAsync (
        ResolvedUnityProjectContext project,
        Guid recordingId,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

}

/// <summary>Owns runtime-scoped recording admission and registration for one bounded critical section.</summary>
internal interface IGameViewRecordingAdmissionLease : IDisposable
{
    ResolvedUnityProjectContext Project { get; }

    Guid RecordingId { get; }

    IpcGameViewRecordingStartBinding StartBinding { get; }

    ValueTask<GameViewRecordingRegistrationResult> TryRegisterAsync (
        AbsolutePath executionStatePath,
        GameViewRecordingStoredExecution execution,
        CancellationToken cancellationToken = default);
}

/// <summary>Owns terminal artifact publication for one recording across cooperating processes.</summary>
internal interface IGameViewRecordingTerminalPublicationLease : IDisposable
{
}

/// <summary>Reports the atomic outcome of replacing one durable recording checkpoint.</summary>
internal sealed record GameViewRecordingCheckpointExchangeResult
{
    private GameViewRecordingCheckpointExchangeResult (
        bool exchanged,
        GameViewRecordingStoredExecution current)
    {
        Exchanged = exchanged;
        Current = current ?? throw new ArgumentNullException(nameof(current));
    }

    public bool Exchanged { get; }

    public GameViewRecordingStoredExecution Current { get; }

    public static GameViewRecordingCheckpointExchangeResult Replaced (
        GameViewRecordingStoredExecution replacement) =>
        new(exchanged: true, replacement);

    public static GameViewRecordingCheckpointExchangeResult Unchanged (
        GameViewRecordingStoredExecution current) =>
        new(exchanged: false, current);
}

/// <summary>Reports the atomic runtime-scoped outcome of admitting one recording start.</summary>
internal sealed record GameViewRecordingRegistrationResult
{
    private GameViewRecordingRegistrationResult (
        bool registered,
        GameViewRecordingStoredExecution? existing)
    {
        Registered = registered;
        Existing = existing;
    }

    public bool Registered { get; }

    public GameViewRecordingStoredExecution? Existing { get; }

    public static GameViewRecordingRegistrationResult Created () => new(
        registered: true,
        existing: null);

    public static GameViewRecordingRegistrationResult Rejected (
        GameViewRecordingStoredExecution existing)
    {
        ArgumentNullException.ThrowIfNull(existing);
        return new GameViewRecordingRegistrationResult(registered: false, existing);
    }
}
