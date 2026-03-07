#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents parsed arguments for <c>ucli.go.describe</c>. </summary>
    internal readonly struct GoDescribeArguments
    {
        public GoDescribeArguments (
            UnityObjectReference targetReference,
            int? depth)
        {
            TargetReference = targetReference;
            Depth = depth;
        }

        public UnityObjectReference TargetReference { get; }

        public int? Depth { get; }
    }
}