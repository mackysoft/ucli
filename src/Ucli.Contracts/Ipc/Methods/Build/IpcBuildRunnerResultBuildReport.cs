namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents optional BuildReport evidence declared by a uCLI build runner result. </summary>
public sealed record IpcBuildRunnerResultBuildReport
{
    /// <summary> Initializes one BuildReport evidence reference. </summary>
    /// <param name="Path"> The normalized path relative to the runner output directory. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="Path" /> is <see langword="null" />. </exception>
    public IpcBuildRunnerResultBuildReport (BuildRunnerOutputPath Path)
    {
        this.Path = ContractArgumentGuard.RequireNotNull(Path, nameof(Path));
    }

    /// <summary> Gets the normalized BuildReport path relative to the runner output directory. </summary>
    public BuildRunnerOutputPath Path { get; }
}
