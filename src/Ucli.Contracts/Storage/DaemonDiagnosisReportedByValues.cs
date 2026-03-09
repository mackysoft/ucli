namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Defines canonical string literals for daemon diagnosis reporters. </summary>
public static class DaemonDiagnosisReportedByValues
{
    /// <summary> Gets the reporter value used when Unity runtime emitted the diagnosis. </summary>
    public const string Unity = "unity";

    /// <summary> Gets the reporter value used when CLI emitted the diagnosis. </summary>
    public const string Cli = "cli";

    /// <summary> Determines whether one daemon diagnosis reporter value is supported. </summary>
    /// <param name="value"> The daemon diagnosis reporter value. </param>
    /// <returns> <see langword="true" /> when value is supported; otherwise <see langword="false" />. </returns>
    public static bool IsSupported (string value)
    {
        return string.Equals(value, Unity, System.StringComparison.Ordinal)
            || string.Equals(value, Cli, System.StringComparison.Ordinal);
    }
}