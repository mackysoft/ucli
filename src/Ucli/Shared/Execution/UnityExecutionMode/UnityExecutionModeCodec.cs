using ApplicationUnityExecutionMode = MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision.UnityExecutionMode;

namespace MackySoft.Ucli.Shared.Execution.UnityExecutionMode;

/// <summary> Converts Unity execution mode values between raw host literals and typed values. </summary>
internal static class UnityExecutionModeCodec
{
    private const string AutoValue = "auto";

    private const string DaemonValue = "daemon";

    private const string OneshotValue = "oneshot";

    /// <summary> Parses one execution-mode literal into <see cref="ApplicationUnityExecutionMode" />. </summary>
    /// <param name="value"> The raw option value. <see langword="null" /> is normalized to <c>auto</c>; empty or whitespace are invalid. </param>
    /// <param name="mode"> The parsed mode when parsing succeeds; otherwise the default enum value. </param>
    /// <returns> <see langword="true" /> when parsing succeeded; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        out ApplicationUnityExecutionMode mode)
    {
        if (value is null)
        {
            mode = ApplicationUnityExecutionMode.Auto;
            return true;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            mode = default;
            return false;
        }

        var normalizedValue = value.Trim();
        if (string.Equals(normalizedValue, AutoValue, StringComparison.OrdinalIgnoreCase))
        {
            mode = ApplicationUnityExecutionMode.Auto;
            return true;
        }

        if (string.Equals(normalizedValue, DaemonValue, StringComparison.OrdinalIgnoreCase))
        {
            mode = ApplicationUnityExecutionMode.Daemon;
            return true;
        }

        if (string.Equals(normalizedValue, OneshotValue, StringComparison.OrdinalIgnoreCase))
        {
            mode = ApplicationUnityExecutionMode.Oneshot;
            return true;
        }

        mode = default;
        return false;
    }

    /// <summary> Converts one Unity execution mode value to the canonical host literal. </summary>
    /// <param name="mode"> The execution mode value. </param>
    /// <returns> The canonical host literal. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="mode" /> is unsupported. </exception>
    public static string ToValue (ApplicationUnityExecutionMode mode)
    {
        return mode switch
        {
            ApplicationUnityExecutionMode.Auto => AutoValue,
            ApplicationUnityExecutionMode.Daemon => DaemonValue,
            ApplicationUnityExecutionMode.Oneshot => OneshotValue,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported execution mode."),
        };
    }
}
