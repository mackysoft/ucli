namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Converts plan-token mode values between enum and contract literals. </summary>
public static class PlanTokenModeCodec
{
    /// <summary> Converts one plan-token mode enum value to config literal. </summary>
    /// <param name="planTokenMode"> The plan-token mode enum value. </param>
    /// <returns> The config literal value. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="planTokenMode" /> is unsupported. </exception>
    public static string ToValue (PlanTokenMode planTokenMode)
    {
        return planTokenMode switch
        {
            PlanTokenMode.Optional => PlanTokenModeValues.Optional,
            PlanTokenMode.Required => PlanTokenModeValues.Required,
            _ => throw new ArgumentOutOfRangeException(nameof(planTokenMode), planTokenMode, "Unsupported planTokenMode."),
        };
    }

    /// <summary> Tries to parse config literal to plan-token mode enum. </summary>
    /// <param name="value"> The config literal value. </param>
    /// <param name="planTokenMode"> The parsed enum value. </param>
    /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        out PlanTokenMode planTokenMode)
    {
        if (string.Equals(value, PlanTokenModeValues.Optional, StringComparison.OrdinalIgnoreCase))
        {
            planTokenMode = PlanTokenMode.Optional;
            return true;
        }

        if (string.Equals(value, PlanTokenModeValues.Required, StringComparison.OrdinalIgnoreCase))
        {
            planTokenMode = PlanTokenMode.Required;
            return true;
        }

        planTokenMode = default;
        return false;
    }
}