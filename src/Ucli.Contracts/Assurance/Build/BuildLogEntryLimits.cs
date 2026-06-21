namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Defines size limits for <c>build.log.entry</c> progress payloads. </summary>
public static class BuildLogEntryLimits
{
    /// <summary> Gets the maximum UTF-8 byte length of one progress log message. </summary>
    public const int MaxMessageUtf8Bytes = 64 * 1024;
}
