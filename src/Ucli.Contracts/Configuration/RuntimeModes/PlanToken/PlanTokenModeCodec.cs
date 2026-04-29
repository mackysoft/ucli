using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Converts plan-token mode values between enum and contract literals. </summary>
public static class PlanTokenModeCodec
{
    private static readonly (PlanTokenMode Value, string Literal)[] Mappings =
    {
        (PlanTokenMode.Optional, PlanTokenModeValues.Optional),
        (PlanTokenMode.Required, PlanTokenModeValues.Required),
    };

    /// <summary> Converts one plan-token mode enum value to config literal. </summary>
    /// <param name="planTokenMode"> The plan-token mode enum value. </param>
    /// <returns> The config literal value. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="planTokenMode" /> is unsupported. </exception>
    public static string ToValue (PlanTokenMode planTokenMode)
    {
        return LiteralCodecUtilities.ToValue(
            planTokenMode,
            Mappings,
            nameof(planTokenMode),
            "Unsupported planTokenMode.");
    }

    /// <summary> Tries to parse config literal to plan-token mode enum. </summary>
    /// <param name="value"> The config literal value. </param>
    /// <param name="planTokenMode"> The parsed enum value. </param>
    /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        out PlanTokenMode planTokenMode)
    {
        return LiteralCodecUtilities.TryParse(
            value,
            Mappings,
            StringComparison.OrdinalIgnoreCase,
            out planTokenMode);
    }
}
