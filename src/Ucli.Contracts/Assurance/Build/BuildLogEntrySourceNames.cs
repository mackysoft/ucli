namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Defines the closed <c>build.log.entry</c> source set. </summary>
public static class BuildLogEntrySourceNames
{
    /// <summary> Gets the source for Unity log stream entries. </summary>
    public const string UnityLog = "unityLog";

    /// <summary> Gets the source for application-side ucli entries. </summary>
    public const string Ucli = "ucli";

    /// <summary> Gets the complete closed log source set. </summary>
    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[]
    {
        UnityLog,
        Ucli,
    });
}
