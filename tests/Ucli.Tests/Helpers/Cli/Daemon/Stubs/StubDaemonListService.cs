using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;

namespace MackySoft.Tests;

internal sealed class StubDaemonListService : IDaemonListService
{
    private readonly List<Invocation> invocations = [];

    public StubDaemonListService (DaemonListExecutionResult result)
    {
        Result = result;
    }

    public DaemonListExecutionResult Result { get; set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<DaemonListExecutionResult> GetListAsync (
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
