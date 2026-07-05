using MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Preflight;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingOpsPreflightService : IOpsPreflightService
{
    private readonly List<Invocation> invocations = [];

    public IReadOnlyList<Invocation> Invocations => invocations;

    public OpsPreflightResult Result { get; set; } =
        OpsPreflightResult.Failure("not configured", UcliCoreErrorCodes.InternalError);

    public ValueTask<OpsPreflightResult> ExecuteAsync (
        OpsPreflightInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(input, cancellationToken));
        return ValueTask.FromResult(Result);
    }

    internal readonly record struct Invocation (
        OpsPreflightInput Input,
        CancellationToken CancellationToken);
}
