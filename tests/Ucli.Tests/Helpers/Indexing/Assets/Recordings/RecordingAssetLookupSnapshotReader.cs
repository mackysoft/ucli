using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.UnityIntegration.Indexing.Assets;

namespace MackySoft.Ucli.Tests.Helpers.Indexing.Assets;

internal sealed class RecordingAssetLookupSnapshotReader : IAssetLookupSnapshotReader
{
    private readonly Queue<AssetLookupSnapshotFetchResult> results = new();
    private readonly List<Invocation> invocations = [];

    public IReadOnlyList<Invocation> Invocations => invocations;

    public void Enqueue (AssetLookupSnapshotFetchResult result)
    {
        results.Enqueue(result);
    }

    public ValueTask<AssetLookupSnapshotFetchResult> ReadAsync (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        UnityExecutionMode mode,
        TimeSpan timeout,
        bool failFast = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(project, config, command, mode, timeout, failFast, cancellationToken));
        if (!results.TryDequeue(out var result))
        {
            throw new InvalidOperationException("Asset lookup snapshot result is not configured.");
        }

        return ValueTask.FromResult(result);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext Project,
        UcliConfig Config,
        UcliCommand Command,
        UnityExecutionMode Mode,
        TimeSpan Timeout,
        bool FailFast,
        CancellationToken CancellationToken);
}
