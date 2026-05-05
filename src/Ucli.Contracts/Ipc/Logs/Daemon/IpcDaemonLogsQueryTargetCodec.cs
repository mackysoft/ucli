using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Converts daemon-log query-target values to canonical IPC literals. </summary>
public static class IpcDaemonLogsQueryTargetCodec
{
    /// <summary> Gets the query-target value for searching message only. </summary>
    public const string Message = "message";

    /// <summary> Gets the query-target value for searching raw stack only. </summary>
    public const string Stack = "stack";

    /// <summary> Gets the query-target value for searching both message and raw. </summary>
    public const string Both = "both";

    private static readonly string[] CanonicalLiterals =
    {
        Message,
        Stack,
        Both,
    };

    /// <summary> Gets one daemon-logs validation error message for unsupported query-target values. </summary>
    /// <param name="value"> The raw query-target value. </param>
    /// <returns> The invalid-argument message text. </returns>
    public static string CreateDaemonLogsUnsupportedValueMessage (string? value)
    {
        return $"queryTarget must be one of: {Message}, {Both}. Actual: {value}.";
    }

    /// <summary> Gets one validation error message for unsupported query-target values. </summary>
    /// <param name="value"> The raw query-target value. </param>
    /// <returns> The invalid-argument message text. </returns>
    public static string CreateUnsupportedValueMessage (string? value)
    {
        return $"queryTarget must be one of: {Message}, {Stack}, {Both}. Actual: {value}.";
    }

    /// <summary> Gets one daemon-logs validation error message for stack-only query-target value. </summary>
    /// <returns> The invalid-argument message text. </returns>
    public static string CreateDaemonLogsStackNotSupportedMessage ()
    {
        return $"queryTarget '{Stack}' is not supported for daemon logs. Supported: {Message}, {Both}.";
    }

    /// <summary> Tries to parse one query-target literal to canonical IPC value. </summary>
    /// <param name="value"> The optional query-target literal. </param>
    /// <param name="queryTarget"> The normalized query-target literal when operation succeeds; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when one supported query-target value is normalized; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        out string? queryTarget)
    {
        return LiteralCodecUtilities.TryNormalizeLiteral(
            value,
            CanonicalLiterals,
            StringComparison.OrdinalIgnoreCase,
            out queryTarget);
    }

    /// <summary> Tries to resolve one daemon-logs query-target literal with daemon-specific validation rules. </summary>
    /// <param name="value"> The optional raw query-target literal. </param>
    /// <param name="queryTarget"> The normalized query-target literal when operation succeeds. </param>
    /// <param name="errorMessage"> The daemon-logs validation message when operation fails; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when value is valid for daemon logs; otherwise <see langword="false" />. </returns>
    public static bool TryParseForDaemonLogs (
        string? value,
        out string queryTarget,
        out string? errorMessage)
    {
        if (!StringValueNormalizer.TryTrimToNonEmpty(value, out var normalizedValue))
        {
            queryTarget = Message;
            errorMessage = null;
            return true;
        }

        if (!TryParse(normalizedValue, out var normalizedQueryTarget))
        {
            queryTarget = string.Empty;
            errorMessage = CreateDaemonLogsUnsupportedValueMessage(value);
            return false;
        }

        if (string.Equals(normalizedQueryTarget, Stack, StringComparison.Ordinal))
        {
            queryTarget = string.Empty;
            errorMessage = CreateDaemonLogsStackNotSupportedMessage();
            return false;
        }

        queryTarget = normalizedQueryTarget!;
        errorMessage = null;
        return true;
    }

    /// <summary> Tries to resolve one Unity-logs query-target literal with Unity-specific defaulting rules. </summary>
    /// <param name="value"> The optional raw query-target literal. </param>
    /// <param name="queryTarget"> The normalized query-target literal when operation succeeds. </param>
    /// <param name="errorMessage"> The validation message when operation fails; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when value is valid for Unity logs; otherwise <see langword="false" />. </returns>
    public static bool TryParseForUnityLogs (
        string? value,
        out string queryTarget,
        out string? errorMessage)
    {
        if (!StringValueNormalizer.TryTrimToNonEmpty(value, out var normalizedValue))
        {
            queryTarget = Message;
            errorMessage = null;
            return true;
        }

        if (!TryParse(normalizedValue, out var normalizedQueryTarget))
        {
            queryTarget = string.Empty;
            errorMessage = CreateUnsupportedValueMessage(value);
            return false;
        }

        queryTarget = normalizedQueryTarget!;
        errorMessage = null;
        return true;
    }
}
