#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents parsed arguments for <c>ucli.asset.schema</c>. </summary>
    internal readonly struct AssetSchemaArguments
    {
        public AssetSchemaArguments (
            string? typeId,
            UnityObjectReference targetReference,
            bool hasTargetReference)
        {
            TypeId = typeId;
            TargetReference = targetReference;
            HasTargetReference = hasTargetReference;
        }

        public string? TypeId { get; }

        public UnityObjectReference TargetReference { get; }

        public bool HasTargetReference { get; }
    }
}