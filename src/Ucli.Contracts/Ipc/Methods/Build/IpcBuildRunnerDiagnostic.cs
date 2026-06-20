namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one diagnostic returned by a build runner terminal result. </summary>
/// <param name="Code"> The runner diagnostic code. </param>
/// <param name="Severity"> The diagnostic severity literal. </param>
/// <param name="Message"> The diagnostic message. </param>
public sealed record IpcBuildRunnerDiagnostic (
    string Code,
    string Severity,
    string Message);
