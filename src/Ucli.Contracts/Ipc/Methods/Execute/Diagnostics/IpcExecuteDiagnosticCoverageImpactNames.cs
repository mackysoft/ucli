namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines stable <c>execute</c> diagnostic coverage-impact literals. </summary>
public static class IpcExecuteDiagnosticCoverageImpactNames
{
    /// <summary> Indicates that the diagnostic has no coverage impact. </summary>
    public const string None = "none";

    /// <summary> Indicates that the operation covered only part of the requested target set. </summary>
    public const string Partial = "partial";

    /// <summary> Indicates that coverage could not be determined. </summary>
    public const string Indeterminate = "indeterminate";
}
