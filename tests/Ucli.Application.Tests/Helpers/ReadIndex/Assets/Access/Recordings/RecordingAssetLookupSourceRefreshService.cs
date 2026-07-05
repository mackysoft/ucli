using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingAssetLookupSourceRefreshService : IAssetLookupSourceRefreshService
{
    private readonly List<Invocation> invocations = [];

    public AssetLookupRefreshResult Result { get; set; }
        = AssetLookupRefreshResult.Failure("not configured", UcliCoreErrorCodes.InternalError);

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<AssetLookupRefreshResult> RefreshAsync (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        UnityExecutionMode mode,
        TimeSpan timeout,
        string fallbackReason,
        bool failFast = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(
            project,
            config,
            command,
            mode,
            timeout,
            fallbackReason,
            failFast,
            cancellationToken));
        return ValueTask.FromResult(Result);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext Project,
        UcliConfig Config,
        UcliCommand Command,
        UnityExecutionMode Mode,
        TimeSpan Timeout,
        string FallbackReason,
        bool FailFast,
        CancellationToken CancellationToken);
}
