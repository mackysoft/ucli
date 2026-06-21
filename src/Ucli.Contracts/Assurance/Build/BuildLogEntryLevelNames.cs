namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Defines the closed <c>build.log.entry</c> level set. </summary>
public static class BuildLogEntryLevelNames
{
    /// <summary> Gets the trace log level. </summary>
    public const string Trace = "trace";

    /// <summary> Gets the debug log level. </summary>
    public const string Debug = "debug";

    /// <summary> Gets the informational log level. </summary>
    public const string Info = "info";

    /// <summary> Gets the warning log level. </summary>
    public const string Warning = "warning";

    /// <summary> Gets the error log level. </summary>
    public const string Error = "error";

    /// <summary> Gets the complete closed log level set. </summary>
    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[]
    {
        Trace,
        Debug,
        Info,
        Warning,
        Error,
    });
}
