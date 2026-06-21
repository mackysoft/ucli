using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Represents the source used to account optional BuildReport evidence. </summary>
internal sealed record BuildReportSourceEntry
{
    private BuildReportSourceEntry (
        IpcBuildReportArtifact? artifact,
        string? runnerOutputRelativePath)
    {
        Artifact = artifact;
        RunnerOutputRelativePath = runnerOutputRelativePath;
    }

    /// <summary> Gets the already normalized BuildReport artifact. </summary>
    public IpcBuildReportArtifact? Artifact { get; }

    /// <summary> Gets the BuildReport source path relative to <c>RunnerOutputDirectory</c>. </summary>
    public string? RunnerOutputRelativePath { get; }

    /// <summary> Creates a BuildReport source from an already normalized artifact. </summary>
    public static BuildReportSourceEntry FromArtifact (IpcBuildReportArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        return new BuildReportSourceEntry(artifact, null);
    }

    /// <summary> Creates a BuildReport source from a runner-output-relative JSON file path. </summary>
    public static BuildReportSourceEntry FromRunnerOutputRelativePath (string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new BuildReportSourceEntry(null, path);
    }
}
