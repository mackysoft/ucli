using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingSceneTreeLiteDirtySourceProbeService : ISceneTreeLiteDirtySourceProbeService
{
    private readonly List<Invocation> invocations = [];

    public SceneTreeLiteDirtySourceProbeResult Result { get; set; }
        = SceneTreeLiteDirtySourceProbeResult.NotAvailable("not configured");

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<SceneTreeLiteDirtySourceProbeResult> ProbeAsync (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        UnityExecutionMode mode,
        TimeSpan timeout,
        UnityScenePath scenePath,
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
        CancellationToken CancellationToken);
}
