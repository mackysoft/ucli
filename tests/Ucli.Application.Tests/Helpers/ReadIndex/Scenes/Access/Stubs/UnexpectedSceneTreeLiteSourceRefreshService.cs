using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class UnexpectedSceneTreeLiteSourceRefreshService : ISceneTreeLiteSourceRefreshService
{
    public ValueTask<SceneTreeLiteRefreshResult> RefreshAsync (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        UnityExecutionMode mode,
        TimeSpan timeout,
        string scenePath,
        string fallbackReason,
        bool failFast = false,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Scene-tree-lite source refresh was not expected.");
    }
}
