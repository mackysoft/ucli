using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.PlanToken
{
    /// <summary> Holds the explicit eval-only claims carried by an authenticated plan token. </summary>
    internal sealed record EvalPlanTokenClaims (
        CsEvalSourceKind SourceKind,
        bool EvalEnabled,
        bool AllowDangerous,
        bool AllowPlayMode);
}
