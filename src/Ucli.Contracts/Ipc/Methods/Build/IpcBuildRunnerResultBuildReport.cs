namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents optional BuildReport evidence declared by a build runner result. </summary>
public sealed record IpcBuildRunnerResultBuildReport
{
    /// <summary> Initializes one BuildReport evidence reference. </summary>
    public IpcBuildRunnerResultBuildReport (string Path)
    {
        this.Path = ContractArgumentGuard.RequireValue(Path, nameof(Path));
    }

    public string Path { get; }
}
