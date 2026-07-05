using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class StubBuildRunArtifactStore : IBuildRunArtifactStore
{
    public const string BuildMetadataDigest = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    public const string BuildReportArtifactDigest = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    public const string BuildOutputManifestArtifactDigest = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    public const string BuildLogArtifactDigest = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
    public const string OutputManifestDigest = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";

    private readonly string rootPath;

    private readonly Func<BuildRunArtifactAccountingRequest, CancellationToken, ValueTask<BuildRunArtifactAccountingOperationResult>>? accountArtifactsOverride;

    public StubBuildRunArtifactStore (
        string rootPath,
        Func<BuildRunArtifactAccountingRequest, CancellationToken, ValueTask<BuildRunArtifactAccountingOperationResult>>? accountArtifactsOverride = null)
    {
        this.rootPath = rootPath;
        this.accountArtifactsOverride = accountArtifactsOverride;
    }

    public BuildRunArtifactPaths? PreparedPaths { get; private set; }

    public BuildRunMetadataDocument? WrittenMetadata { get; private set; }

    public BuildRunArtifactAccountingRequest? AccountingRequest { get; private set; }

    public IpcBuildOutputLayout? PreparedOutputLayout { get; private set; }

    public BuildRunArtifactPreparationResult Prepare (
        ResolvedUnityProjectContext unityProject,
        string runId)
    {
        var runDirectory = Path.Combine(rootPath, runId);
        var runnerOutputDirectory = Path.Combine(rootPath, "work", runId, "output");
        var artifactOutputDirectory = Path.Combine(runDirectory, "output");
        Directory.CreateDirectory(runnerOutputDirectory);
        PreparedPaths = new BuildRunArtifactPaths(
            RepositoryRoot: rootPath,
            RunId: runId,
            ArtifactsDirectory: runDirectory,
            BuildJsonPath: Path.Combine(runDirectory, "build.json"),
            BuildReportJsonPath: Path.Combine(runDirectory, "build-report.json"),
            BuildLogPath: Path.Combine(runDirectory, "build.log"),
            OutputManifestJsonPath: Path.Combine(runDirectory, "output-manifest.json"),
            RunnerOutputDirectory: runnerOutputDirectory,
            ArtifactOutputDirectory: artifactOutputDirectory);
        return BuildRunArtifactPreparationResult.Success(PreparedPaths);
    }

    public BuildRunArtifactPreparationResult PrepareBuildPipelineOutputLayout (
        BuildRunArtifactPaths paths,
        string buildTarget,
        IpcBuildOutputLayout outputLayout)
    {
        PreparedOutputLayout = outputLayout;
        return BuildRunArtifactPreparationResult.Success(paths);
    }

    public ValueTask<BuildRunArtifactAccountingOperationResult> AccountArtifactsAsync (
        BuildRunArtifactAccountingRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AccountingRequest = request;
        if (accountArtifactsOverride != null)
        {
            return accountArtifactsOverride(request, cancellationToken);
        }

        var buildReport = request.BuildReport == null
            ? null
            : new BuildArtifactRef(BuildArtifactKind.BuildReport, "build-report.json", BuildReportArtifactDigest);
        return ValueTask.FromResult(BuildRunArtifactAccountingOperationResult.Success(new BuildRunArtifactAccountingResult(
            BuildReport: buildReport,
            BuildOutputManifest: new BuildArtifactRef(BuildArtifactKind.BuildOutputManifest, "output-manifest.json", BuildOutputManifestArtifactDigest),
            BuildLog: new BuildArtifactRef(BuildArtifactKind.BuildLog, "build.log", BuildLogArtifactDigest),
            OutputManifest: new BuildOutputManifestSummary(
                ManifestDigest: OutputManifestDigest,
                EntryCount: request.OutputSources.Count,
                FileCount: request.OutputSources.Count,
                TotalBytes: request.OutputSources.Count == 0 ? 0 : 12))));
    }

    public ValueTask<BuildArtifactRefWriteResult> WriteMetadataAsync (
        BuildRunMetadataWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WrittenMetadata = request.Metadata;
        return ValueTask.FromResult(BuildArtifactRefWriteResult.Success(new BuildArtifactRef(
            BuildArtifactKind.Build,
            "build.json",
            BuildMetadataDigest)));
    }
}
