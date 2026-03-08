using System.Collections.Generic;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents parsed arguments for <c>ucli.comp.set</c>. </summary>
    internal readonly struct CompSetArguments
    {
        public CompSetArguments (
            UnityObjectReference targetReference,
            IReadOnlyList<CompSetAssignment> sets)
        {
            TargetReference = targetReference;
            Sets = sets;
        }

        public UnityObjectReference TargetReference { get; }

        public IReadOnlyList<CompSetAssignment> Sets { get; }
    }
}