namespace MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

/// <summary> Converts Unity execution mode values between raw literals and typed values. </summary>
internal static class UnityExecutionModeCodec
{
    private const string AutoValue = "auto";

    private const string DaemonValue = "daemon";

    private const string OneshotValue = "oneshot";

    /// <summary> Parses one <c>--mode</c> option value into <see cref="UnityExecutionMode" />. </summary>
    /// <param name="value"> The raw CLI option value. <see langword="null" /> is normalized to <c>auto</c>; empty or whitespace are treated as invalid. </param>
    /// <param name="mode"> The parsed mode when parsing succeeds; otherwise the default enum value. </param>
    /// <returns> <see langword="true" /> when parsing succeeded; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        out UnityExecutionMode mode)
    {
        if (value is null)
        {
            mode = UnityExecutionMode.Auto;
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
            mode = UnityExecutionMode.Auto;
            return true;
        }

        if (string.Equals(normalizedValue, DaemonValue, StringComparison.OrdinalIgnoreCase))
        {
            mode = UnityExecutionMode.Daemon;
            return true;
        }

        if (string.Equals(normalizedValue, OneshotValue, StringComparison.OrdinalIgnoreCase))
        {
            mode = UnityExecutionMode.Oneshot;
            return true;
        }

        mode = default;
        return false;
    }

    /// <summary> Converts one Unity execution mode value to the canonical CLI literal. </summary>
    /// <param name="mode"> The execution mode value. </param>
    /// <returns> The canonical CLI literal. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="mode" /> is unsupported. </exception>
    public static string ToValue (UnityExecutionMode mode)
    {
        return mode switch
        {
            UnityExecutionMode.Auto => AutoValue,
            UnityExecutionMode.Daemon => DaemonValue,
            UnityExecutionMode.Oneshot => OneshotValue,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported execution mode."),
        };
    }
}
