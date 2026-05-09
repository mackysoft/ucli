namespace MackySoft.Ucli.Contracts;

/// <summary> Defines stable string values for error-code retry classification. </summary>
public static class UcliErrorRetryClassValues
{
    /// <summary> Indicates that replay is safe without an additional state check. </summary>
    public const string Yes = "yes";

    /// <summary> Indicates that replay is not safe. </summary>
    public const string No = "no";

    /// <summary> Indicates that callers should wait for the blocking condition to clear, then retry. </summary>
    public const string WaitThenRetry = "waitThenRetry";

    /// <summary> Indicates that callers must run plan again before retrying call. </summary>
    public const string ReplanRequired = "replanRequired";

    /// <summary> Indicates that retry safety depends on response payload evidence or diagnostics. </summary>
    public const string ContextDependent = "contextDependent";

    /// <summary> Indicates that retry safety is unknown to this client. </summary>
    public const string Unknown = "unknown";

    /// <summary> Determines whether a retry classification is one of the stable contract values. </summary>
    /// <param name="value"> The retry classification string to validate. </param>
    /// <returns> <see langword="true" /> when <paramref name="value" /> is a supported retry classification; otherwise <see langword="false" />. </returns>
    public static bool IsKnown (string? value)
    {
        return value is Yes
            or No
            or WaitThenRetry
            or ReplanRequired
            or ContextDependent
            or Unknown;
    }
}
