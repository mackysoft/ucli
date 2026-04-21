using System;
using System.Globalization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Converts optional ISO 8601 timestamp literals with explicit timezone offsets. </summary>
public static class IpcIso8601TimestampCodec
{
    /// <summary> Tries to parse one optional timestamp value that requires an explicit timezone offset. </summary>
    /// <param name="value"> The optional timestamp literal. </param>
    /// <param name="timestamp"> The parsed timestamp when parsing succeeds; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when value is empty or parse succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryParseOptionalWithTimezoneOffset (
        string? value,
        out DateTimeOffset? timestamp)
    {
        if (!StringValueNormalizer.TryTrimToNonEmpty(value, out var normalizedValue))
        {
            timestamp = null;
            return true;
        }

        if (!DateTimeOffset.TryParse(
                normalizedValue,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsedTimestamp))
        {
            timestamp = null;
            return false;
        }

        if (!HasExplicitTimezoneOffset(normalizedValue))
        {
            timestamp = null;
            return false;
        }

        timestamp = parsedTimestamp;
        return true;
    }

    /// <summary> Determines whether one normalized timestamp literal contains explicit timezone offset information. </summary>
    /// <param name="normalizedTimestampText"> The normalized timestamp text. </param>
    /// <returns> <see langword="true" /> when an explicit timezone offset is present; otherwise <see langword="false" />. </returns>
    private static bool HasExplicitTimezoneOffset (string normalizedTimestampText)
    {
        var timeSeparatorIndex = normalizedTimestampText.IndexOf('T');
        if (timeSeparatorIndex < 0)
        {
            timeSeparatorIndex = normalizedTimestampText.IndexOf('t');
        }

        if (timeSeparatorIndex < 0)
        {
            return false;
        }

        if (normalizedTimestampText.EndsWith("Z", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var plusSignIndex = normalizedTimestampText.LastIndexOf('+');
        var minusSignIndex = normalizedTimestampText.LastIndexOf('-');
        var offsetSignIndex = Math.Max(plusSignIndex, minusSignIndex);
        if (offsetSignIndex <= timeSeparatorIndex)
        {
            return false;
        }

        return IsValidOffsetSuffix(normalizedTimestampText.AsSpan(offsetSignIndex));
    }

    /// <summary> Determines whether one timezone-offset suffix has supported ISO 8601 shape. </summary>
    /// <param name="offsetSuffix"> The offset suffix span that starts with sign (<c>+/-</c>). </param>
    /// <returns> <see langword="true" /> when suffix matches one supported timezone offset shape; otherwise <see langword="false" />. </returns>
    private static bool IsValidOffsetSuffix (ReadOnlySpan<char> offsetSuffix)
    {
        if (offsetSuffix.Length != 3 && offsetSuffix.Length != 5 && offsetSuffix.Length != 6)
        {
            return false;
        }

        if (offsetSuffix[0] != '+' && offsetSuffix[0] != '-')
        {
            return false;
        }

        if (!char.IsDigit(offsetSuffix[1]) || !char.IsDigit(offsetSuffix[2]))
        {
            return false;
        }

        if (offsetSuffix.Length == 3)
        {
            return true;
        }

        if (offsetSuffix.Length == 5)
        {
            return char.IsDigit(offsetSuffix[3]) && char.IsDigit(offsetSuffix[4]);
        }

        return offsetSuffix[3] == ':'
            && char.IsDigit(offsetSuffix[4])
            && char.IsDigit(offsetSuffix[5]);
    }
}