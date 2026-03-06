using System.Collections.Generic;
using MackySoft.Ucli.Unity.Execution.Requests;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one preplanned operation prepared by validate/plan pass. </summary>
    /// <param name="Operation"> The normalized operation model. </param>
    /// <param name="PhaseOperation"> The resolved phase operation implementation. </param>
    /// <param name="PlanTouched"> The touched list produced by validate and plan phases. </param>
    /// <param name="RequiresPreCallPlanReplay"> Whether plan should be replayed immediately before call. </param>
    internal sealed record PreparedOperation (
        NormalizedOperation Operation,
        IUcliOperation PhaseOperation,
        IReadOnlyList<OperationTouch> PlanTouched,
        bool RequiresPreCallPlanReplay);
}
