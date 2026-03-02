namespace MackySoft.Ucli.TestRun.Execution;

/// <summary> Represents process execution request values. </summary>
/// <param name="FileName"> The executable file path. </param>
/// <param name="Arguments"> The command-line arguments. </param>
/// <param name="TimeoutSeconds"> The timeout in seconds. </param>
internal sealed record ProcessRunRequest (
    string FileName,
    IReadOnlyList<string> Arguments,
    int TimeoutSeconds);