using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class UnexpectedModeDecisionService : IUnityExecutionModeDecisionService
{
    public ValueTask<UnityExecutionModeDecisionResult> DecideAsync (
        UnityExecutionMode mode,
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Unity execution mode decision was not expected.");
    }
}
