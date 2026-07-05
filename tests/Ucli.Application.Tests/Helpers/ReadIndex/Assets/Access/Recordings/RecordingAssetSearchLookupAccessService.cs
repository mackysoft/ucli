using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingAssetSearchLookupAccessService : IAssetSearchLookupAccessService
{
    private readonly List<Invocation> invocations = [];

    public IReadOnlyList<Invocation> Invocations => invocations;

    public AssetSearchLookupReadResult? Result { get; set; }

    public ValueTask<AssetSearchLookupReadResult> SearchAsync (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UnityExecutionMode mode,
        TimeSpan timeout,
        ReadIndexMode readIndexMode,
        AssetSearchLookupQuery query,
        bool failFast = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(
            project,
            config,
            mode,
            timeout,
            readIndexMode,
            query,
            failFast,
            cancellationToken));

        if (Result is null)
        {
            throw new InvalidOperationException("Asset-search lookup read result is not configured.");
        }

        return ValueTask.FromResult(Result);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext Project,
        UcliConfig Config,
        UnityExecutionMode Mode,
        TimeSpan Timeout,
        ReadIndexMode ReadIndexMode,
        AssetSearchLookupQuery Query,
        bool FailFast,
        CancellationToken CancellationToken);
}
