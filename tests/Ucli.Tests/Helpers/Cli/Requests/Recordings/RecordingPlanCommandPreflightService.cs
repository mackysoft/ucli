using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Hosting.Cli.Requests.Plan.Preflight;

namespace MackySoft.Tests;

internal sealed class RecordingPlanCommandPreflightService : IPlanCommandPreflightService
{
    private readonly Func<string?, string, ReadIndexMode?, CancellationToken, ValueTask<PlanCommandPreflightResult>> handler;
    private readonly List<Invocation> invocations = [];

    public RecordingPlanCommandPreflightService (
        Func<string?, string, ReadIndexMode?, CancellationToken, ValueTask<PlanCommandPreflightResult>> handler)
    {
        this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<PlanCommandPreflightResult> PrepareAsync (
        string? projectPath,
        string requestJson,
        ReadIndexMode? readIndexMode,
        CancellationToken cancellationToken = default)
    {
        invocations.Add(new Invocation(projectPath, requestJson, readIndexMode, cancellationToken));
        return handler(projectPath, requestJson, readIndexMode, cancellationToken);
    }

    public readonly record struct Invocation (
        string? ProjectPath,
        string RequestJson,
        ReadIndexMode? ReadIndexMode,
        CancellationToken CancellationToken);
}
