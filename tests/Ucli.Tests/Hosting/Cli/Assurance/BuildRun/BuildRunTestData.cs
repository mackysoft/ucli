using MackySoft.Ucli.Application.Features.Assurance.Build.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests;

internal static class BuildRunTestData
{
    public const string RunId = "build-run-1";

    public const string ProjectFingerprint = "project-fingerprint";

    public static BuildExecutionOutput CreateOutput (
        string? verdict = null,
        string? reportResult = null,
        string? completionReason = null,
        int errorCount = 0)
    {
        var normalizedReportResult = reportResult ?? "succeeded";
        var normalizedCompletionReason = completionReason ?? "completed";
        var normalizedVerdict = verdict ?? "pass";
        var project = ProjectIdentityInfoTestFactory.Create(projectPath: "/workspace/UnityProject", projectFingerprint: ProjectFingerprint);
        var build = CreateBuild(normalizedReportResult, normalizedCompletionReason, errorCount);
        var claims = CreateClaims(normalizedReportResult);

        return new BuildExecutionOutput(
            Verdict: normalizedVerdict,
            Project: project,
            Build: build,
            Verifiers:
            [
                new BuildVerifierOutput(
                    Id: BuildReportRefs.Build,
                    Kind: BuildReportRefs.Build,
                    Deterministic: false,
                    Required: true,
                    PrimaryClaims: claims.Where(static claim => claim.Required).Select(static claim => claim.Id).ToArray(),
                    Effects: BuildPipelineEffectValues,
                    ReportRef: BuildReportRefs.Build),
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
            Phase: "started",
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
            Phase: "completed",
            RunnerKind: "buildPipeline",
            RunnerStatus: "succeeded",
            Verdict: "pass",
            ReportRefs:
            [
                BuildReportRefs.Build,
                BuildReportRefs.BuildReport,
                BuildReportRefs.BuildOutputManifest,
                BuildReportRefs.BuildLog,
            ],
            ErrorCode: null);
    }

    private static BuildOutput CreateBuild (
        string reportResult,
        string completionReason,
        int errorCount)
    {
        return new BuildOutput(
            RunId: RunId,
            Profile: new BuildProfileOutput("/workspace/.ucli/build/player.json", Repeat('a')),
            Inputs: new BuildInputsOutput(
                InputKind: "explicit",
                Target: new BuildTargetOutput("standaloneLinux64", "StandaloneLinux64"),
                Scenes: new BuildScenesOutput("explicit", ["Assets/Scenes/Main.unity"]),
                Options: new BuildOptionsOutput(Development: true),
                UnityBuildProfile: null),
            Runner: new BuildRunnerOutput(
                Kind: "buildPipeline",
                Method: null,
                Invocation: new BuildRunnerInvocationOutput(
                    Arguments: new Dictionary<string, string>(StringComparer.Ordinal),
                    Environment: new BuildRunnerInvocationEnvironmentOutput(
                        Variables: [],
                        Secrets: []))),
            RunnerResult: new BuildRunnerResultOutput(
                Source: "buildPipelineBuildReport",
                Status: reportResult),
            Output: new BuildArtifactOutput(
                ManifestRef: BuildReportRefs.BuildOutputManifest,
                ManifestDigest: Repeat('b'),
                EntryCount: 1,
                FileCount: 1,
                TotalBytes: 4096),
            Generations: new BuildGenerationsOutput(
                Before: new IpcUnityGenerationSnapshot(1, 1, 1, 1),
                After: new IpcUnityGenerationSnapshot(2, 1, 1, 1),
                ValidFor: new IpcUnityGenerationSnapshot(2, 1, 1, 1)),
            Summary: new BuildSummaryOutput(
                Result: reportResult,
                DurationMilliseconds: 2500,
                ErrorCount: errorCount,
                WarningCount: 1,
                ReportRef: BuildReportRefs.BuildReport),
            Logs: new BuildLogsOutput(
                ReportRef: BuildReportRefs.BuildLog,
                EntryCount: 3,
                ErrorCount: errorCount,
                WarningCount: 1,
                CompletionReason: completionReason,
                Window: new BuildLogWindowOutput(
                    DateTimeOffset.Parse("2026-06-12T00:00:00+00:00"),
                    DateTimeOffset.Parse("2026-06-12T00:00:03+00:00"))));
    }

    private static IReadOnlyList<BuildClaimOutput> CreateClaims (string reportResult)
    {
        const string passed = "passed";
        var succeededStatus = string.Equals(reportResult, "succeeded", StringComparison.Ordinal)
            ? passed
            : "failed";

        return
        [
            CreateClaim(BuildClaimCodes.UnityBuildProfileResolved, passed, "Build profile resolved.", BuildReportRefs.Build),
            CreateClaim(BuildClaimCodes.UnityReadyForBuild, passed, "Unity was ready for build.", null),
            CreateClaim(BuildClaimCodes.UnityBuildInputsResolved, passed, "Build inputs resolved.", BuildReportRefs.Build),
            CreateClaim(BuildClaimCodes.UnityBuildRunnerResolved, passed, "Build runner resolved.", BuildReportRefs.Build),
            CreateClaim(BuildClaimCodes.UnityBuildCompleted, passed, "BuildPipeline completed.", BuildReportRefs.BuildReport),
            CreateClaim(BuildClaimCodes.UnityBuildSucceeded, succeededStatus, "BuildPipeline succeeded.", BuildReportRefs.BuildReport),
            CreateClaim(BuildClaimCodes.UnityBuildResultAccounted, passed, "Build result accounted.", BuildReportRefs.Build, reportResult),
            CreateClaim(BuildClaimCodes.UnityBuildReportAccounted, passed, "BuildReport artifact accounted.", BuildReportRefs.BuildReport),
            CreateClaim(BuildClaimCodes.UnityBuildArtifactsAccounted, passed, "Build artifacts accounted.", BuildReportRefs.Build),
            CreateClaim(BuildClaimCodes.UnityBuildOutputDigested, passed, "Build output digested.", BuildReportRefs.BuildOutputManifest),
            CreateClaim(BuildClaimCodes.UnityBuildLogsAccounted, passed, "Build logs accounted.", BuildReportRefs.BuildLog),
            CreateClaim(BuildClaimCodes.UnityBuildProjectMutationAccounted, passed, "Project mutation accounted.", BuildReportRefs.Build),
            CreateClaim(BuildClaimCodes.UnityBuildValidForGeneration, passed, "Build generations captured.", BuildReportRefs.Build),
        ];
    }

    private static BuildClaimOutput CreateClaim (
        UcliCode code,
        string status,
        string statement,
        string? evidenceRef,
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
                ["kind"] = BuildReportRefs.Build,
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
                ? new BuildEvidenceOutput("lifecycleSnapshot", Data: new { state = "ready" })
                : new BuildEvidenceOutput("artifact", evidenceRef);

        return new BuildClaimOutput(
            Id: code.Value,
            Status: status,
            Coverage: "full",
            Required: true,
            VerifierRef: BuildReportRefs.Build,
            Statement: statement,
            Subject: subject,
            Evidence: [evidence],
            ResidualRisks: []);
    }

    private static IReadOnlyDictionary<string, BuildReportOutput> CreateReports ()
    {
        return new Dictionary<string, BuildReportOutput>(StringComparer.Ordinal)
        {
            [BuildReportRefs.Build] = new("build.json", Repeat('c')),
            [BuildReportRefs.BuildReport] = new("build-report.json", Repeat('d')),
            [BuildReportRefs.BuildOutputManifest] = new("output-manifest.json", Repeat('e')),
            [BuildReportRefs.BuildLog] = new("build.log", Repeat('f')),
        };
    }

    private static string Repeat (char value)
    {
        return new string(value, 64);
    }

    private static readonly string[] BuildPipelineEffectValues =
    [
        "unityLifecycleRead",
        "unityBuildPipeline",
        "unityBuildReportRead",
        "unityLogWindowRead",
        "ucliArtifactWrite",
        "outputManifestWrite",
        "generationSnapshot",
        "projectMutationAudit",
    ];
}
