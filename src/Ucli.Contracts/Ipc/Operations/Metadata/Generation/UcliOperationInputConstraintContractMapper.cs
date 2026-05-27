using MackySoft.Ucli.Contracts.Operations;

using MackySoft.Ucli.Contracts.Text;

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

        return new UcliOperationInputConstraintContract(ContractLiteralCodec.ToValue(attribute.Kind))
        {
            AssetKind = MapAssetKind(attribute.AssetKind),
            TargetKind = MapTargetKind(attribute.TargetKind),
            TypeKind = MapTypeKind(attribute.TypeKind),
            Access = MapAccess(attribute.Access),
            Min = MapOptionalNumber(attribute.Min),
            Max = MapOptionalNumber(attribute.Max),
        };
    }

    private static string? MapAssetKind (UcliOperationAssetKind value)
    {
        return value == UcliOperationAssetKind.Unspecified ? null : ContractLiteralCodec.ToValue(value);
    }

    private static string? MapTargetKind (UcliOperationReferenceTargetKind value)
    {
        return value == UcliOperationReferenceTargetKind.Unspecified ? null : ContractLiteralCodec.ToValue(value);
    }

    private static string? MapTypeKind (UcliOperationTypeKind value)
    {
        return value == UcliOperationTypeKind.Unspecified ? null : ContractLiteralCodec.ToValue(value);
    }

    private static string? MapAccess (UcliOperationSerializedPropertyAccess value)
    {
        return value == UcliOperationSerializedPropertyAccess.Unspecified ? null : ContractLiteralCodec.ToValue(value);
    }

    private static double? MapOptionalNumber (double value)
    {
        return double.IsNaN(value) ? null : value;
    }
}
