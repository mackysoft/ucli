using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.TestSupport;

internal sealed class RecordingOpsCatalogReader : IOpsCatalogReader
{
    private readonly Queue<OpsCatalogFetchResult> results = new();
    private readonly List<Invocation> invocations = [];

    public IReadOnlyList<Invocation> Invocations => invocations;

    public OpsCatalogFetchResult? Result { get; set; }
        = OpsCatalogFetchResult.Failure("not configured", UcliCoreErrorCodes.InternalError);

    public void Enqueue (OpsCatalogFetchResult result)
    {
        results.Enqueue(result);
    }

    public ValueTask<OpsCatalogFetchResult> ReadAsync (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UnityExecutionMode mode,
        TimeSpan timeout,
        bool failFast,
        bool requireReadinessGate,
        bool includeEditLoweringOnly = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(
            project,
            config,
            mode,
            timeout,
            failFast,
            requireReadinessGate,
            includeEditLoweringOnly,
            cancellationToken));

        if (results.TryDequeue(out var result))
        {
            return ValueTask.FromResult(result);
        }

        if (Result is not null)
        {
            return ValueTask.FromResult(Result);
        }

        throw new InvalidOperationException("Ops catalog fetch result is not configured.");
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext Project,
        UcliConfig Config,
        UnityExecutionMode Mode,
        TimeSpan Timeout,
        bool FailFast,
        bool RequireReadinessGate,
        bool IncludeEditLoweringOnly,
        CancellationToken CancellationToken);
}
