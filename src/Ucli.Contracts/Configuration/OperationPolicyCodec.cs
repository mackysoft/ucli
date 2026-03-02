namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Converts operation-policy values between enum and contract literals. </summary>
public static class OperationPolicyCodec
{
    /// <summary> Converts one operation-policy enum value to config literal. </summary>
    /// <param name="operationPolicy"> The operation-policy enum value. </param>
    /// <returns> The config literal value. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="operationPolicy" /> is unsupported. </exception>
    public static string ToValue (OperationPolicy operationPolicy)
    {
        return operationPolicy switch
        {
            OperationPolicy.Safe => OperationPolicyValues.Safe,
            OperationPolicy.Advanced => OperationPolicyValues.Advanced,
            OperationPolicy.Dangerous => OperationPolicyValues.Dangerous,
            _ => throw new ArgumentOutOfRangeException(nameof(operationPolicy), operationPolicy, "Unsupported operationPolicy."),
        };
    }

    /// <summary> Tries to parse config literal to operation-policy enum. </summary>
    /// <param name="value"> The config literal value. </param>
    /// <param name="operationPolicy"> The parsed enum value. </param>
    /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        out OperationPolicy operationPolicy)
    {
        if (string.Equals(value, OperationPolicyValues.Safe, StringComparison.OrdinalIgnoreCase))
        {
            operationPolicy = OperationPolicy.Safe;
            return true;
        }

        if (string.Equals(value, OperationPolicyValues.Advanced, StringComparison.OrdinalIgnoreCase))
        {
            operationPolicy = OperationPolicy.Advanced;
            return true;
        }

        if (string.Equals(value, OperationPolicyValues.Dangerous, StringComparison.OrdinalIgnoreCase))
        {
            operationPolicy = OperationPolicy.Dangerous;
            return true;
        }

        operationPolicy = default;
        return false;
    }
}