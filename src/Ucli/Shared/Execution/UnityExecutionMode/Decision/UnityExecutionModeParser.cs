namespace MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;

/// <summary> Parses and normalizes Unity execution mode option values. </summary>
internal static class UnityExecutionModeParser
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
}