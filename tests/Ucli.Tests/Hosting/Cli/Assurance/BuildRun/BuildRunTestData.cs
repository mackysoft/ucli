using MackySoft.Ucli.Application.Features.Assurance.Build.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests;

internal static class BuildRunTestData
{
    private static readonly AssuranceVerifierId BuildVerifierId = new("build");

    public const string RunIdText = RunIdTestValues.BuildText;

    public static readonly Guid RunId = RunIdTestValues.Build;

    public static readonly ProjectFingerprint ProjectFingerprint = ProjectFingerprintTestFactory.Create("project-fingerprint");

    public static BuildExecutionOutput CreateOutput (
        AssuranceVerdict? verdict = null,
        IpcBuildReportResult? reportResult = null,
        IpcBuildLogCompletionReason? completionReason = null,
        int errorCount = 0)
    {
        var normalizedReportResult = reportResult ?? IpcBuildReportResult.Succeeded;
        var normalizedCompletionReason = completionReason ?? IpcBuildLogCompletionReason.Completed;
        var normalizedVerdict = verdict ?? AssuranceVerdict.Pass;
        var project = ProjectIdentityInfoTestFactory.CreateWithProjectPath(projectPath: ProjectPathTestValues.WorkspaceUnityProject, projectFingerprint: ProjectFingerprint);
        var build = CreateBuild(normalizedReportResult, normalizedCompletionReason, errorCount);
        var claims = CreateClaims(normalizedReportResult);

        return new BuildExecutionOutput(
            Verdict: normalizedVerdict,
            Project: project,
            Build: build,
            Verifiers:
            [
                new BuildVerifierOutput(
                    Id: BuildVerifierId,
                    Deterministic: false,
                    Required: true,
                    PrimaryClaims: claims.Where(static claim => claim.Required).Select(static claim => claim.Id).ToArray(),
                    Effects: BuildPipelineEffectValues,
                    ReportRef: BuildArtifactKind.Build),
            ],
            Claims: claims,
            Reports: CreateReports(),
            ResidualRisks: []);
    }

    public static BuildProgressEntry CreateStartedEntry ()
    {
        return new BuildProgressEntry(
            RunId: RunId,
            ProfileDigest: Repeat('a'),
            Phase: BuildRunProgressPhase.Started,
            RunnerKind: null,
            RunnerStatus: null,
            Verdict: null,
            ReportRefs: [],
            ErrorCode: null);
    }

    public static BuildProgressEntry CreateCompletedEntry ()
    {
        return new BuildProgressEntry(
            RunId: RunId,
            ProfileDigest: Repeat('a'),
            Phase: BuildRunProgressPhase.Completed,
            RunnerKind: BuildRunnerKind.BuildPipeline,
            RunnerStatus: IpcBuildReportResult.Succeeded,
            Verdict: AssuranceVerdict.Pass,
            ReportRefs:
            [
                BuildArtifactKind.Build,
                BuildArtifactKind.BuildReport,
                BuildArtifactKind.BuildOutputManifest,
                BuildArtifactKind.BuildLog,
            ],
            ErrorCode: null);
    }

    private static BuildOutput CreateBuild (
        IpcBuildReportResult reportResult,
        IpcBuildLogCompletionReason completionReason,
        int errorCount)
    {
        return new BuildOutput(
            runId: RunId,
            profile: new BuildProfileOutput(
                Path.Combine(ProjectPathTestValues.WorkspaceRoot, ".ucli", "build", "player.json"),
                Repeat('a')),
            inputs: new BuildInputsOutput(
                InputKind: BuildProfileInputsKind.Explicit,
                Target: new BuildTargetOutput(BuildTargetStableName.StandaloneLinux64, "StandaloneLinux64"),
                Scenes: new BuildScenesOutput(BuildProfileSceneSource.Explicit, [new SceneAssetPath("Assets/Scenes/Main.unity")]),
                Options: new BuildOptionsOutput(Development: true),
                UnityBuildProfile: null),
            runner: new BuildRunnerOutput(
                Kind: BuildRunnerKind.BuildPipeline,
                Method: null,
                Invocation: new BuildRunnerInvocationOutput(
                    Arguments: new Dictionary<string, string>(StringComparer.Ordinal),
                    Environment: new BuildRunnerInvocationEnvironmentOutput(
                        Variables: [],
                        Secrets: []))),
            runnerResult: new BuildRunnerResultOutput(
                Source: IpcBuildRunnerResultSource.BuildPipelineBuildReport,
                Status: reportResult),
            output: new BuildArtifactOutput(
                ManifestRef: BuildArtifactKind.BuildOutputManifest,
                ManifestDigest: Repeat('b'),
                EntryCount: 1,
                FileCount: 1,
                TotalBytes: 4096),
            generations: new BuildGenerationsOutput(
                Before: new IpcUnityGenerationSnapshot(1, 1, 1, 1),
                After: new IpcUnityGenerationSnapshot(2, 1, 1, 1),
                ValidFor: new IpcUnityGenerationSnapshot(2, 1, 1, 1)),
            summary: new BuildSummaryOutput(
                Result: reportResult,
                DurationMilliseconds: 2500,
                ErrorCount: errorCount,
                WarningCount: 1,
                ReportRef: BuildArtifactKind.BuildReport),
            logs: new BuildLogsOutput(
                ReportRef: BuildArtifactKind.BuildLog,
                EntryCount: 3,
                ErrorCount: errorCount,
                WarningCount: 1,
                CompletionReason: completionReason,
                Window: new BuildLogWindowOutput(
                    DateTimeOffset.Parse("2026-06-12T00:00:00+00:00"),
                    DateTimeOffset.Parse("2026-06-12T00:00:03+00:00"))));
    }

    private static IReadOnlyList<BuildClaimOutput> CreateClaims (IpcBuildReportResult reportResult)
    {
        const AssuranceClaimStatus passed = AssuranceClaimStatus.Passed;
        var succeededStatus = reportResult == IpcBuildReportResult.Succeeded
            ? passed
            : AssuranceClaimStatus.Failed;
        var reportResultLiteral = ContractLiteralCodec.ToValue(reportResult);

        return
        [
            CreateClaim(BuildClaimCodes.UnityBuildProfileResolved, passed, "Build profile resolved.", BuildArtifactKind.Build),
            CreateClaim(BuildClaimCodes.UnityReadyForBuild, passed, "Unity was ready for build.", null),
            CreateClaim(BuildClaimCodes.UnityBuildInputsResolved, passed, "Build inputs resolved.", BuildArtifactKind.Build),
            CreateClaim(BuildClaimCodes.UnityBuildRunnerResolved, passed, "Build runner resolved.", BuildArtifactKind.Build),
            CreateClaim(BuildClaimCodes.UnityBuildCompleted, passed, "BuildPipeline completed.", BuildArtifactKind.BuildReport),
            CreateClaim(BuildClaimCodes.UnityBuildSucceeded, succeededStatus, "BuildPipeline succeeded.", BuildArtifactKind.BuildReport),
            CreateClaim(BuildClaimCodes.UnityBuildResultAccounted, passed, "Build result accounted.", BuildArtifactKind.Build, reportResultLiteral),
            CreateClaim(BuildClaimCodes.UnityBuildReportAccounted, passed, "BuildReport artifact accounted.", BuildArtifactKind.BuildReport),
            CreateClaim(BuildClaimCodes.UnityBuildArtifactsAccounted, passed, "Build artifacts accounted.", BuildArtifactKind.Build),
            CreateClaim(BuildClaimCodes.UnityBuildOutputDigested, passed, "Build output digested.", BuildArtifactKind.BuildOutputManifest),
            CreateClaim(BuildClaimCodes.UnityBuildLogsAccounted, passed, "Build logs accounted.", BuildArtifactKind.BuildLog),
            CreateClaim(BuildClaimCodes.UnityBuildProjectMutationAccounted, passed, "Project mutation accounted.", BuildArtifactKind.Build),
            CreateClaim(BuildClaimCodes.UnityBuildValidForGeneration, passed, "Build generations captured.", BuildArtifactKind.Build),
        ];
    }

    private static BuildClaimOutput CreateClaim (
        UcliCode code,
        AssuranceClaimStatus status,
        string statement,
        BuildArtifactKind? evidenceRef,
        string? reportResult = null)
    {
        var subject = BuildClaimCodes.UnityBuildResultAccounted.Equals(code) && reportResult != null
            ? new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["source"] = "buildPipelineBuildReport",
                ["status"] = reportResult,
            }
            : new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["kind"] = "build",
                ["runId"] = RunId,
            };
        var evidence = BuildClaimCodes.UnityBuildResultAccounted.Equals(code) && evidenceRef != null && reportResult != null
            ? new BuildEvidenceOutput(
                "artifact",
                evidenceRef,
                new
                {
                    source = "buildPipelineBuildReport",
                    status = reportResult,
                })
            : evidenceRef == null
                ? new BuildEvidenceOutput("lifecycleSnapshot", EvidenceRef: null, Data: new { state = "ready" })
                : new BuildEvidenceOutput("artifact", evidenceRef, Data: null);

        return new BuildClaimOutput(
            Id: code,
            Status: status,
            Coverage: AssuranceCoverage.Full,
            Required: true,
            VerifierRef: BuildVerifierId,
            Statement: statement,
            Subject: subject,
            Evidence: [evidence],
            ResidualRisks: []);
    }

    private static IReadOnlyDictionary<BuildArtifactKind, AssuranceReportReference> CreateReports ()
    {
        return new Dictionary<BuildArtifactKind, AssuranceReportReference>
        {
            [BuildArtifactKind.Build] = AssuranceReportReference.FromPath("build.json", Repeat('c')),
            [BuildArtifactKind.BuildReport] = AssuranceReportReference.FromPath("build-report.json", Repeat('d')),
            [BuildArtifactKind.BuildOutputManifest] = AssuranceReportReference.FromPath("output-manifest.json", Repeat('e')),
            [BuildArtifactKind.BuildLog] = AssuranceReportReference.FromPath("build.log", Repeat('f')),
        };
    }

    private static Sha256Digest Repeat (char value)
    {
        return Sha256Digest.Parse(new string(value, 64));
    }

    private static readonly AssuranceEffect[] BuildPipelineEffectValues =
    [
        AssuranceEffect.UnityLifecycleRead,
        AssuranceEffect.UnityBuildPipeline,
        AssuranceEffect.UnityBuildReportRead,
        AssuranceEffect.UnityLogWindowRead,
        AssuranceEffect.UcliArtifactWrite,
        AssuranceEffect.OutputManifestWrite,
        AssuranceEffect.GenerationSnapshot,
        AssuranceEffect.ProjectMutationAudit,
    ];
}
