using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingDaemonListQueryService : IDaemonListQueryService
{
    private readonly DaemonListExecutionResult result;
    private readonly List<Invocation> invocations = [];

    public RecordingDaemonListQueryService (DaemonListExecutionResult result)
    {
        this.result = result ?? throw new ArgumentNullException(nameof(result));
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<DaemonListExecutionResult> GetListAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(
            unityProject,
            timeout,
            cancellationToken));

        return ValueTask.FromResult(result);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        TimeSpan Timeout,
        CancellationToken CancellationToken);
}
