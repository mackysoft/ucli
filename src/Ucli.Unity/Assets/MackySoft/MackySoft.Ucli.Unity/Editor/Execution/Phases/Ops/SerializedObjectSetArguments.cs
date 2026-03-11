using System.Collections.Generic;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents parsed arguments for one serialized-object set operation. </summary>
    internal readonly struct SerializedObjectSetArguments
    {
        public SerializedObjectSetArguments (
            UnityObjectReference targetReference,
            IReadOnlyList<SerializedPropertyAssignment> sets)
        {
            TargetReference = targetReference;
            Sets = sets;
        }

        public UnityObjectReference TargetReference { get; }

        public IReadOnlyList<SerializedPropertyAssignment> Sets { get; }
    }
}
