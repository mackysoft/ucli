using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Converts operation serialized-property access values between enum and contract literals. </summary>
public static class UcliOperationSerializedPropertyAccessCodec
{
    private static readonly (UcliOperationSerializedPropertyAccess Value, string Literal)[] Mappings =
    {
        (UcliOperationSerializedPropertyAccess.Write, UcliOperationSerializedPropertyAccessValues.Write),
    };

    /// <summary> Converts one serialized-property access enum value to its contract literal. </summary>
    /// <param name="access"> The serialized-property access enum value. </param>
    /// <returns> The contract literal value. </returns>
    public static string ToValue (UcliOperationSerializedPropertyAccess access)
    {
        return LiteralCodecUtilities.ToValue(
            access,
            Mappings,
            nameof(access),
            "Unsupported operation serialized-property access.");
    }
}
