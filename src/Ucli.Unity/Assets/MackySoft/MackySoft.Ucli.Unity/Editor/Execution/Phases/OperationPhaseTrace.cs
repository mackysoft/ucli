using System.Collections.Generic;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one final per-operation trace entry produced by phase execution. </summary>
    /// <param name="OpId"> The operation identifier. </param>
    /// <param name="Op"> The operation name. </param>
    /// <param name="Phase"> The final phase reached by this operation. </param>
    /// <param name="Applied"> Whether operation was applied. </param>
    /// <param name="Changed"> Whether operation produced changes. </param>
    /// <param name="Touched"> The touched persistence-unit list. </param>
    /// <param name="Failure"> The operation failure details; otherwise <see langword="null" />. </param>
    internal sealed record OperationPhaseTrace (
        string OpId,
        string Op,
        OperationPhase Phase,
        bool Applied,
        bool Changed,
        IReadOnlyList<OperationTouch> Touched,
        OperationFailure? Failure);
}
