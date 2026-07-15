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
        string repositoryRoot,
        Guid runId,
        string artifactsDirectory,
        string buildJsonPath,
        string buildReportJsonPath,
        string buildLogPath,
        string outputManifestJsonPath,
        string runnerOutputDirectory,
        string artifactOutputDirectory)
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

    public string RepositoryRoot { get; }

    /// <summary> Gets the non-empty build run identifier. </summary>
    public Guid RunId { get; }

    public string ArtifactsDirectory { get; }

    public string BuildJsonPath { get; }

    public string BuildReportJsonPath { get; }

    public string BuildLogPath { get; }

    public string OutputManifestJsonPath { get; }

    public string RunnerOutputDirectory { get; }

    public string ArtifactOutputDirectory { get; }
}
