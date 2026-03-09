#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents parsed arguments for <c>ucli.comp.ensure</c>. </summary>
    internal readonly struct CompEnsureArguments
    {
        public CompEnsureArguments (
            UnityObjectReference targetReference,
            string typeId)
        {
            TargetReference = targetReference;
            TypeId = typeId;
        }

        public UnityObjectReference TargetReference { get; }

        public string TypeId { get; }
    }
}