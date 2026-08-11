using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Hosting.Cli.Requests.Plan.Preflight;

namespace MackySoft.Tests;

internal sealed class RecordingPlanCommandPreflightService : IPlanCommandPreflightService
{
    private readonly Func<AbsolutePath?, string, ReadIndexMode?, CancellationToken, ValueTask<PlanCommandPreflightResult>> handler;
    private readonly List<Invocation> invocations = [];

    public RecordingPlanCommandPreflightService (
        Func<AbsolutePath?, string, ReadIndexMode?, CancellationToken, ValueTask<PlanCommandPreflightResult>> handler)
    {
        this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<PlanCommandPreflightResult> PrepareAsync (
        Guid requestId,
        AbsolutePath? projectPath,
        string requestJson,
        ReadIndexMode? readIndexMode,
        CancellationToken cancellationToken = default)
    {
        invocations.Add(new Invocation(requestId, projectPath, requestJson, readIndexMode, cancellationToken));
        return handler(projectPath, requestJson, readIndexMode, cancellationToken);
    }

    public readonly record struct Invocation (
        Guid RequestId,
        AbsolutePath? ProjectPath,
        string RequestJson,
        ReadIndexMode? ReadIndexMode,
        CancellationToken CancellationToken);
}
