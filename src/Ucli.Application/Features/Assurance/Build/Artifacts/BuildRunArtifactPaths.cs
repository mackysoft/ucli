using MackySoft.FileSystem;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Represents the resolved filesystem layout for one build run. </summary>
internal sealed record BuildRunArtifactPaths
{
    /// <summary> Initializes the complete artifact layout for one identified build run. </summary>
    /// <param name="repositoryRoot"> The repository root used to resolve storage layout identity for this build run. </param>
    /// <param name="runId"> The non-empty build run identifier. </param>
    /// <param name="artifactsDirectory"> The absolute build-run artifact directory path. </param>
    /// <param name="buildJsonPath"> The absolute <c>build.json</c> path. </param>
    /// <param name="buildReportJsonPath"> The absolute <c>build-report.json</c> path. </param>
    /// <param name="buildLogPath"> The absolute <c>build.log</c> path. </param>
    /// <param name="outputManifestJsonPath"> The absolute <c>output-manifest.json</c> path. </param>
    /// <param name="runnerOutputDirectory"> The absolute runner working output root path. </param>
    /// <param name="artifactOutputDirectory"> The absolute artifact-store output root path. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="runId" /> is empty. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when a required argument is <see langword="null" />. </exception>
    public BuildRunArtifactPaths (
        AbsolutePath repositoryRoot,
        Guid runId,
        AbsolutePath artifactsDirectory,
        AbsolutePath buildJsonPath,
        AbsolutePath buildReportJsonPath,
        AbsolutePath buildLogPath,
        AbsolutePath outputManifestJsonPath,
        AbsolutePath runnerOutputDirectory,
        AbsolutePath artifactOutputDirectory)
    {
        if (runId == Guid.Empty)
        {
            throw new ArgumentException("Run id must not be empty.", nameof(runId));
        }

        ArgumentNullException.ThrowIfNull(repositoryRoot);
        ArgumentNullException.ThrowIfNull(artifactsDirectory);
        ArgumentNullException.ThrowIfNull(buildJsonPath);
        ArgumentNullException.ThrowIfNull(buildReportJsonPath);
        ArgumentNullException.ThrowIfNull(buildLogPath);
        ArgumentNullException.ThrowIfNull(outputManifestJsonPath);
        ArgumentNullException.ThrowIfNull(runnerOutputDirectory);
        ArgumentNullException.ThrowIfNull(artifactOutputDirectory);

        RepositoryRoot = repositoryRoot;
        RunId = runId;
        ArtifactsDirectory = artifactsDirectory;
        BuildJsonPath = buildJsonPath;
        BuildReportJsonPath = buildReportJsonPath;
        BuildLogPath = buildLogPath;
        OutputManifestJsonPath = outputManifestJsonPath;
        RunnerOutputDirectory = runnerOutputDirectory;
        ArtifactOutputDirectory = artifactOutputDirectory;
    }

    public AbsolutePath RepositoryRoot { get; }

    /// <summary> Gets the non-empty build run identifier. </summary>
    public Guid RunId { get; }

    public AbsolutePath ArtifactsDirectory { get; }

    public AbsolutePath BuildJsonPath { get; }

    public AbsolutePath BuildReportJsonPath { get; }

    public AbsolutePath BuildLogPath { get; }

    public AbsolutePath OutputManifestJsonPath { get; }

    public AbsolutePath RunnerOutputDirectory { get; }

    public AbsolutePath ArtifactOutputDirectory { get; }
}
