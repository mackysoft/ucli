namespace MackySoft.Ucli.Execution;

/// <summary> Represents process execution request values. </summary>
/// <param name="FileName"> The executable file path. </param>
/// <param name="Arguments"> The command-line arguments. </param>
/// <param name="TimeoutSeconds"> The timeout in seconds. </param>
/// <param name="CaptureStandardOutput"> Whether full standard-output text must be preserved for machine parsing. </param>
internal sealed record ProcessRunRequest (
    string FileName,
    IReadOnlyList<string> Arguments,
    int TimeoutSeconds,
    bool CaptureStandardOutput = false);