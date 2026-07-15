using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingSceneTreeLiteAccessService : ISceneTreeLiteAccessService
{
    private readonly List<Invocation> invocations = [];

    public IReadOnlyList<Invocation> Invocations => invocations;

    public SceneTreeLiteReadResult? Result { get; set; }

    public ValueTask<SceneTreeLiteReadResult> ReadAsync (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        UnityExecutionMode mode,
        TimeSpan timeout,
        ReadIndexMode readIndexMode,
        UnityScenePath scenePath,
        int? depth,
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
            readIndexMode,
            scenePath,
            depth,
            failFast,
            cancellationToken));

        if (Result is null)
        {
            throw new InvalidOperationException("Scene-tree-lite read result is not configured.");
        }

        return ValueTask.FromResult(Result);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext Project,
        UcliConfig Config,
        UcliCommand Command,
        UnityExecutionMode Mode,
        TimeSpan Timeout,
        ReadIndexMode ReadIndexMode,
        UnityScenePath ScenePath,
        int? Depth,
        bool FailFast,
        CancellationToken CancellationToken);
}
