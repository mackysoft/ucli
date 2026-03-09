namespace MackySoft.Ucli.Execution;

/// <summary> Represents process execution request values. </summary>
/// <param name="FileName"> The executable file path. </param>
/// <param name="Arguments"> The command-line arguments. </param>
/// <param name="Timeout"> The timeout budget. </param>
internal sealed record ProcessRunRequest (
    string FileName,
    IReadOnlyList<string> Arguments,
    TimeSpan Timeout);
