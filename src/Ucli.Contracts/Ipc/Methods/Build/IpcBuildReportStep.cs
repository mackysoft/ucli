namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one normalized BuildReport step summary. </summary>
/// <param name="Name"> The step name. </param>
/// <param name="DurationMilliseconds"> The step duration in milliseconds. </param>
/// <param name="Depth"> The BuildReport step depth. </param>
/// <param name="MessageCount"> The number of messages attached to the step. </param>
public sealed record IpcBuildReportStep (
    string Name,
    long DurationMilliseconds,
    int Depth,
    int MessageCount);
