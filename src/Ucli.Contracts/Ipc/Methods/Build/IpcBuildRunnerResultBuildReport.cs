namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents optional BuildReport evidence declared by a build runner result. </summary>
/// <param name="Path"> The BuildReport JSON source path relative to the runner output directory. </param>
public sealed record IpcBuildRunnerResultBuildReport (
    string Path);
