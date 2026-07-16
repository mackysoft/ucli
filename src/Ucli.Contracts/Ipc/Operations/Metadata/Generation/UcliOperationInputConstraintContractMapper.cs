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
            AssetKind = attribute.HasAssetKind ? ContractLiteralCodec.ToValue(attribute.AssetKind) : null,
            TargetKind = attribute.HasTargetKind ? ContractLiteralCodec.ToValue(attribute.TargetKind) : null,
            TypeKind = attribute.HasTypeKind ? ContractLiteralCodec.ToValue(attribute.TypeKind) : null,
            Access = attribute.HasAccess ? ContractLiteralCodec.ToValue(attribute.Access) : null,
            Min = attribute.HasMin ? attribute.Min : null,
            Max = attribute.HasMax ? attribute.Max : null,
        };
    }
}
