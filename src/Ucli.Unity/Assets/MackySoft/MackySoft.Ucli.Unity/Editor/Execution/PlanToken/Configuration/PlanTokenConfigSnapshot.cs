using System.Collections.Generic;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.PlanToken
{
    /// <summary> Represents normalized plan-token configuration values resolved from <c>.ucli/config.json</c>. </summary>
    /// <param name="Mode"> The resolved plan-token mode used for runtime guard decisions. </param>
    /// <param name="PlanTokenModeLiteral"> The normalized raw <c>planTokenMode</c> literal used by config digest inputs. </param>
    /// <param name="OperationPolicy"> The normalized <c>operationPolicy</c> literal used by config digest inputs. </param>
    /// <param name="OperationAllowlist"> The normalized <c>operationAllowlist</c> values used by config digest inputs. </param>
    internal sealed record PlanTokenConfigSnapshot (
        PlanTokenMode Mode,
        string PlanTokenModeLiteral,
        string OperationPolicy,
        IReadOnlyList<string> OperationAllowlist);
}