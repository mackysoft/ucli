using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Converts operation-policy values between enum and contract literals. </summary>
public static class OperationPolicyCodec
{
    private static readonly (OperationPolicy Value, string Literal)[] Mappings =
    {
        (OperationPolicy.Safe, OperationPolicyValues.Safe),
        (OperationPolicy.Advanced, OperationPolicyValues.Advanced),
        (OperationPolicy.Dangerous, OperationPolicyValues.Dangerous),
    };

    /// <summary> Converts one operation-policy enum value to config literal. </summary>
    /// <param name="operationPolicy"> The operation-policy enum value. </param>
    /// <returns> The config literal value. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="operationPolicy" /> is unsupported. </exception>
    public static string ToValue (OperationPolicy operationPolicy)
    {
        return LiteralCodecUtilities.ToValue(
            operationPolicy,
            Mappings,
            nameof(operationPolicy),
            "Unsupported operationPolicy.");
    }

    /// <summary> Tries to parse config literal to operation-policy enum. </summary>
    /// <param name="value"> The config literal value. </param>
    /// <param name="operationPolicy"> The parsed enum value. </param>
    /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        out OperationPolicy operationPolicy)
    {
        return LiteralCodecUtilities.TryParse(
            value,
            Mappings,
            StringComparison.OrdinalIgnoreCase,
            out operationPolicy);
    }
}
