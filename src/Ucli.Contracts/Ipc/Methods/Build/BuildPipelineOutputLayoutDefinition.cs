namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary>
/// Carries a BuildPipeline output node shape and its guarded portable location relative to the runner output root.
/// </summary>
internal sealed class BuildPipelineOutputLayoutDefinition
{
    /// <summary> Initializes one internally resolved portable BuildPipeline output layout. </summary>
    internal BuildPipelineOutputLayoutDefinition (
        IpcBuildOutputLayoutShape shape,
        BuildRunnerOutputPath runnerOutputPath)
    {
        Shape = shape;
        RunnerOutputPath = runnerOutputPath ?? throw new ArgumentNullException(nameof(runnerOutputPath));
    }

    /// <summary> Gets the expected filesystem node shape. </summary>
    public IpcBuildOutputLayoutShape Shape { get; }

    /// <summary> Gets the guarded portable path relative to the runner output root. </summary>
    public BuildRunnerOutputPath RunnerOutputPath { get; }
}
