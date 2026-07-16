using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class StubBuildRunArtifactStore : IBuildRunArtifactStore
{
    public static readonly Sha256Digest BuildMetadataDigest = Sha256Digest.Parse(new string('a', 64));
    public static readonly Sha256Digest BuildReportArtifactDigest = Sha256Digest.Parse(new string('b', 64));
    public static readonly Sha256Digest BuildOutputManifestArtifactDigest = Sha256Digest.Parse(new string('c', 64));
    public static readonly Sha256Digest BuildLogArtifactDigest = Sha256Digest.Parse(new string('d', 64));
    public static readonly Sha256Digest OutputManifestDigest = Sha256Digest.Parse(new string('e', 64));

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
        Guid runId)
    {
        var runIdText = runId.ToString("D");
        var runDirectory = Path.Combine(rootPath, runIdText);
        var runnerOutputDirectory = Path.Combine(rootPath, "work", runIdText, "output");
        var artifactOutputDirectory = Path.Combine(runDirectory, "output");
        Directory.CreateDirectory(runnerOutputDirectory);
        PreparedPaths = new BuildRunArtifactPaths(
            repositoryRoot: rootPath,
            runId: runId,
            artifactsDirectory: runDirectory,
            buildJsonPath: Path.Combine(runDirectory, "build.json"),
            buildReportJsonPath: Path.Combine(runDirectory, "build-report.json"),
            buildLogPath: Path.Combine(runDirectory, "build.log"),
            outputManifestJsonPath: Path.Combine(runDirectory, "output-manifest.json"),
            runnerOutputDirectory: runnerOutputDirectory,
            artifactOutputDirectory: artifactOutputDirectory);
        return BuildRunArtifactPreparationResult.Success(PreparedPaths);
    }

    public BuildRunArtifactPreparationResult PrepareBuildPipelineOutputLayout (
        BuildRunArtifactPaths paths,
        BuildTargetStableName buildTarget,
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
