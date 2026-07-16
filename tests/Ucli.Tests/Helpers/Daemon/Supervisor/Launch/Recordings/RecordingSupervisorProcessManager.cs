using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal sealed class RecordingSupervisorProcessManager : ISupervisorProcessManager
{
    private readonly List<Invocation> invocations = [];

    private readonly List<ReleaseInvocation> releaseInvocations = [];

    public Func<string, CancellationToken, ValueTask<ExecutionError?>>? LaunchHandler { get; set; }

    public Func<string, SupervisorProcessReleaseMode, CancellationToken, ValueTask<ExecutionError?>>? ReleaseHandler { get; set; }

    public ExecutionError? LaunchError { get; set; }

    public TaskCompletionSource? LaunchStarted { get; set; }

    public bool ObservedCancellation { get; private set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public IReadOnlyList<ReleaseInvocation> ReleaseInvocations => releaseInvocations;

    public async ValueTask<ExecutionError?> LaunchAsync (
        string storageRoot,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            invocations.Add(new Invocation(storageRoot, cancellationToken));
            LaunchStarted?.TrySetResult();

            if (LaunchHandler is not null)
            {
                return await LaunchHandler(storageRoot, cancellationToken).ConfigureAwait(false);
            }

            if (LaunchError is not null)
            {
                return LaunchError;
            }

            throw new InvalidOperationException("Supervisor launch should not be used by this test.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            ObservedCancellation = true;
            throw;
        }
    }

    public async ValueTask<ExecutionError?> ReleaseAsync (
        string storageRoot,
        SupervisorProcessReleaseMode releaseMode,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        releaseInvocations.Add(new ReleaseInvocation(storageRoot, releaseMode, cancellationToken));
        if (ReleaseHandler is null)
        {
            throw new InvalidOperationException("Supervisor process release should not be used by this test.");
        }

        return await ReleaseHandler(storageRoot, releaseMode, cancellationToken).ConfigureAwait(false);
    }

    internal readonly record struct Invocation (
        string StorageRoot,
        CancellationToken CancellationToken);

    internal readonly record struct ReleaseInvocation (
        string StorageRoot,
        SupervisorProcessReleaseMode ReleaseMode,
        CancellationToken CancellationToken);
}
