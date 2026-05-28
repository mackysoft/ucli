namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Defines the closed verify step skip reason values. </summary>
public static class VerifyStepSkipReasons
{
    /// <summary> Gets the skip reason used when no post-read verification is needed. </summary>
    public const string PostReadNotNeeded = "postReadNotNeeded";

    /// <summary> Gets the skip reason used when no failing or indeterminate claim requires log evidence. </summary>
    public const string LogsNotNeeded = "logsNotNeeded";
}
