namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Maps input constraint authoring attributes to describe payload constraint contracts. </summary>
internal static class UcliOperationInputConstraintContractMapper
{
    /// <summary> Creates one describe payload constraint from one input constraint authoring attribute. </summary>
    /// <param name="attribute"> The input constraint attribute. </param>
    /// <returns> The describe payload constraint contract. </returns>
    public static UcliOperationInputConstraintContract Map (UcliInputConstraintAttribute attribute)
    {
        if (attribute == null)
        {
            throw new ArgumentNullException(nameof(attribute));
        }

        return new UcliOperationInputConstraintContract(UcliOperationInputConstraintKindCodec.ToValue(attribute.Kind))
        {
            AssetKind = attribute.AssetKind == UcliOperationAssetKind.Unspecified ? null : UcliOperationAssetKindCodec.ToValue(attribute.AssetKind),
            TargetKind = attribute.TargetKind == UcliOperationReferenceTargetKind.Unspecified ? null : UcliOperationReferenceTargetKindCodec.ToValue(attribute.TargetKind),
            TypeKind = attribute.TypeKind == UcliOperationTypeKind.Unspecified ? null : UcliOperationTypeKindCodec.ToValue(attribute.TypeKind),
            Access = attribute.Access == UcliOperationSerializedPropertyAccess.Unspecified ? null : UcliOperationSerializedPropertyAccessCodec.ToValue(attribute.Access),
            Min = double.IsNaN(attribute.Min) ? null : attribute.Min,
            Max = double.IsNaN(attribute.Max) ? null : attribute.Max,
        };
    }
}
