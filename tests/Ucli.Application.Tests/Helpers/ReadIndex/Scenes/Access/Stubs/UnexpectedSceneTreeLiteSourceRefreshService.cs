using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class UnexpectedSceneTreeLiteSourceRefreshService : ISceneTreeLiteSourceRefreshService
{
    public ValueTask<SceneTreeLiteRefreshResult> RefreshAsync (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        UnityExecutionMode mode,
        TimeSpan timeout,
        UnityScenePath scenePath,
        string fallbackReason,
        bool failFast = false,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Scene-tree-lite source refresh was not expected.");
    }
}
