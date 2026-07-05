using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingOpsCatalogSourceRefreshService : IOpsCatalogSourceRefreshService
{
    private readonly List<RefreshInvocation> refreshInvocations = [];

    public IReadOnlyList<RefreshInvocation> RefreshInvocations => refreshInvocations;

    public OpsCatalogSourceRefreshResult? Result { get; set; }

    public ValueTask<OpsCatalogSourceRefreshResult> RefreshAsync (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UnityExecutionMode mode,
        TimeSpan timeout,
        bool failFast,
        string fallbackReason,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        refreshInvocations.Add(new RefreshInvocation(
            project,
            config,
            mode,
            timeout,
            failFast,
            fallbackReason,
            cancellationToken));

        if (Result is null)
        {
            throw new InvalidOperationException("Ops catalog source refresh result is not configured.");
        }

        return ValueTask.FromResult(Result);
    }

    internal readonly record struct RefreshInvocation (
        ResolvedUnityProjectContext Project,
        UcliConfig Config,
        UnityExecutionMode Mode,
        TimeSpan Timeout,
        bool FailFast,
        string FallbackReason,
        CancellationToken CancellationToken);
}
