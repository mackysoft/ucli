using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingSceneTreeLiteSourceRefreshService : ISceneTreeLiteSourceRefreshService
{
    private readonly List<Invocation> invocations = [];

    public SceneTreeLiteRefreshResult Result { get; set; }
        = SceneTreeLiteRefreshResult.Failure("not configured", UcliCoreErrorCodes.InternalError);

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<SceneTreeLiteRefreshResult> RefreshAsync (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        UnityExecutionMode mode,
        TimeSpan timeout,
        UnityScenePath scenePath,
        SceneTreeLiteSourcePaths? indexSourcePaths,
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
            scenePath,
            indexSourcePaths,
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
        UnityScenePath ScenePath,
        SceneTreeLiteSourcePaths? IndexSourcePaths,
        string FallbackReason,
        bool FailFast,
        CancellationToken CancellationToken);
}
