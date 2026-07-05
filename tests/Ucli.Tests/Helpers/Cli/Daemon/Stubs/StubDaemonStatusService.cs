using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;

namespace MackySoft.Tests;

internal sealed class StubDaemonStatusService : IDaemonStatusService
{
    private readonly List<Invocation> invocations = [];

    public StubDaemonStatusService (DaemonStatusExecutionResult result)
    {
        Result = result;
    }

    public DaemonStatusExecutionResult Result { get; set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<DaemonStatusExecutionResult> GetStatusAsync (
        string? projectPath,
        int? timeoutMilliseconds,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(projectPath, timeoutMilliseconds, cancellationToken));
        return ValueTask.FromResult(Result);
    }

    internal readonly record struct Invocation (
        string? ProjectPath,
        int? TimeoutMilliseconds,
        CancellationToken CancellationToken);
}
