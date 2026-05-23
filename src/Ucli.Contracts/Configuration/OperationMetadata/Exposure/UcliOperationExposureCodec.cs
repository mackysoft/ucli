using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Converts operation-exposure values between enum and contract literals. </summary>
public static class UcliOperationExposureCodec
{
    private static readonly (UcliOperationExposure Value, string Literal)[] Mappings =
    {
        (UcliOperationExposure.Public, UcliOperationExposureValues.Public),
        (UcliOperationExposure.EditLoweringOnly, UcliOperationExposureValues.EditLoweringOnly),
    };

    /// <summary> Converts one operation-exposure enum value to contract literal. </summary>
    /// <param name="operationExposure"> The operation-exposure enum value. </param>
    /// <returns> The contract literal value. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="operationExposure" /> is unsupported. </exception>
    public static string ToValue (UcliOperationExposure operationExposure)
    {
        return LiteralCodecUtilities.ToValue(
            operationExposure,
            Mappings,
            nameof(operationExposure),
            "Unsupported operationExposure.");
    }

    /// <summary> Tries to parse contract literal to operation-exposure enum. </summary>
    /// <param name="value"> The contract literal value. </param>
    /// <param name="operationExposure"> The parsed enum value. </param>
    /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        out UcliOperationExposure operationExposure)
    {
        return LiteralCodecUtilities.TryParse(
            value,
            Mappings,
            StringComparison.OrdinalIgnoreCase,
            out operationExposure);
    }
}
