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
        var normalizedReportResult = reportResult ?? ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded);
        var normalizedCompletionReason = completionReason ?? ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed);
        var normalizedVerdict = verdict ?? ContractLiteralCodec.ToValue(BuildVerdict.Pass);
        var project = CreateProject();
        var build = CreateBuild(normalizedReportResult, normalizedCompletionReason, errorCount);

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
                    PrimaryClaims: BuildClaimCodes.All.Select(static code => code.Value).ToArray(),
                    Effects: BuildPipelineEffectValues,
                    ReportRef: BuildReportRefs.Build),
            ],
            Claims: CreateClaims(normalizedReportResult),
            Reports: CreateReports(),
            ResidualRisks: []);
    }

    public static BuildRunStartedEntry CreateStartedEntry ()
    {
        return new BuildRunStartedEntry(
            RunId: RunId,
            ProjectFingerprint: ProjectFingerprint,
            RequestedMode: "daemon",
            ResolvedMode: "daemon",
            SessionKind: "daemon",
            TimeoutMilliseconds: 120000,
            BuildTarget: "standaloneLinux64",
            OutputPath: "/workspace/.ucli/local/fingerprints/project-fingerprint/artifacts/build/build-run-1/output");
    }

    public static BuildRunCompletedEntry CreateCompletedEntry ()
    {
        return new BuildRunCompletedEntry(
            RunId: RunId,
            Verdict: ContractLiteralCodec.ToValue(BuildVerdict.Pass),
            Result: ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
            CompletionReason: ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
            ErrorCount: 0,
            WarningCount: 1,
            BuildJsonPath: "/workspace/.ucli/local/fingerprints/project-fingerprint/artifacts/build/build-run-1/build.json",
            BuildReportPath: "/workspace/.ucli/local/fingerprints/project-fingerprint/artifacts/build/build-run-1/build-report.json",
            BuildLogPath: "/workspace/.ucli/local/fingerprints/project-fingerprint/artifacts/build/build-run-1/build.log",
            OutputManifestPath: "/workspace/.ucli/local/fingerprints/project-fingerprint/artifacts/build/build-run-1/output-manifest.json");
    }

    private static ProjectIdentityInfo CreateProject ()
    {
        return new ProjectIdentityInfo(
            ProjectPath: "/workspace/UnityProject",
            ProjectFingerprint: ProjectFingerprint,
            UnityVersion: "6000.1.4f1");
    }

    private static BuildOutput CreateBuild (
        string reportResult,
        string completionReason,
        int errorCount)
    {
        return new BuildOutput(
            RunId: RunId,
            Profile: new BuildProfileOutput("/workspace/.ucli/build/player.json", Repeat('a')),
            BuildTarget: "standaloneLinux64",
            Scenes: new BuildScenesOutput("explicit", ["Assets/Scenes/Main.unity"]),
            Options: new BuildOptionsOutput(Development: true),
            Runner: new BuildRunnerOutput(
                Kind: "buildPipeline",
                Method: null,
                Invocation: new BuildRunnerInvocationOutput(
                    Arguments: new Dictionary<string, string>(StringComparer.Ordinal),
                    Environment: new BuildRunnerInvocationEnvironmentOutput(
                        Variables: [],
                        Secrets: []))),
            RunnerResult: new BuildRunnerResultOutput(
                Source: ContractLiteralCodec.ToValue(IpcBuildRunnerResultSource.BuildPipelineBuildReport),
                Status: reportResult),
            Output: new BuildArtifactOutput(
                ManifestRef: BuildReportRefs.BuildOutputManifest,
                ManifestDigest: Repeat('b'),
                EntryCount: 1,
                FileCount: 1,
                TotalBytes: 4096),
            Generations: new BuildGenerationsOutput(
                Before: new BuildGenerationSnapshotOutput("compile-before", "domain-before", "asset-before"),
                After: new BuildGenerationSnapshotOutput("compile-after", "domain-after", "asset-after"),
                ValidFor: new BuildGenerationSnapshotOutput("compile-after", "domain-after", "asset-after")),
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
        var passed = ContractLiteralCodec.ToValue(BuildClaimStatus.Passed);
        var succeededStatus = string.Equals(reportResult, ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded), StringComparison.Ordinal)
            ? passed
            : ContractLiteralCodec.ToValue(BuildClaimStatus.Failed);

        return
        [
            CreateClaim(BuildClaimCodes.UnityBuildProfileResolved, passed, "Build profile resolved.", BuildReportRefs.Build),
            CreateClaim(BuildClaimCodes.UnityReadyForBuild, passed, "Unity was ready for build.", null),
            CreateClaim(BuildClaimCodes.UnityBuildInputsResolved, passed, "Build inputs resolved.", BuildReportRefs.Build),
            CreateClaim(BuildClaimCodes.UnityBuildRunnerResolved, passed, "Build runner resolved.", BuildReportRefs.Build),
            CreateClaim(BuildClaimCodes.UnityBuildCompleted, passed, "BuildPipeline completed.", BuildReportRefs.BuildReport),
            CreateClaim(BuildClaimCodes.UnityBuildSucceeded, succeededStatus, "BuildPipeline succeeded.", BuildReportRefs.BuildReport),
            CreateClaim(BuildClaimCodes.UnityBuildResultAccounted, passed, "Build result accounted.", BuildReportRefs.Build),
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
        string? evidenceRef)
    {
        return new BuildClaimOutput(
            Id: code.Value,
            Status: status,
            Coverage: ContractLiteralCodec.ToValue(BuildCoverage.Full),
            Required: true,
            VerifierRef: BuildReportRefs.Build,
            Statement: statement,
            Subject: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["kind"] = BuildReportRefs.Build,
                ["runId"] = RunId,
            },
            Evidence:
            [
                evidenceRef == null
                    ? new BuildEvidenceOutput("lifecycleSnapshot", Data: new { state = "ready" })
                    : new BuildEvidenceOutput("artifact", evidenceRef),
            ],
            ResidualRisks: []);
    }

    private static IReadOnlyDictionary<string, BuildReportOutput> CreateReports ()
    {
        return new Dictionary<string, BuildReportOutput>(StringComparer.Ordinal)
        {
            [BuildReportRefs.Build] = new("/workspace/.ucli/local/fingerprints/project-fingerprint/artifacts/build/build-run-1/build.json", Repeat('c')),
            [BuildReportRefs.BuildReport] = new("/workspace/.ucli/local/fingerprints/project-fingerprint/artifacts/build/build-run-1/build-report.json", Repeat('d')),
            [BuildReportRefs.BuildOutputManifest] = new("/workspace/.ucli/local/fingerprints/project-fingerprint/artifacts/build/build-run-1/output-manifest.json", Repeat('e')),
            [BuildReportRefs.BuildLog] = new("/workspace/.ucli/local/fingerprints/project-fingerprint/artifacts/build/build-run-1/build.log", Repeat('f')),
        };
    }

    private static string Repeat (char value)
    {
        return new string(value, 64);
    }

    private static readonly string[] BuildPipelineEffectValues =
    [
        ContractLiteralCodec.ToValue(BuildEffect.UnityLifecycleRead),
        ContractLiteralCodec.ToValue(BuildEffect.UnityBuildPipeline),
        ContractLiteralCodec.ToValue(BuildEffect.UnityBuildReportRead),
        ContractLiteralCodec.ToValue(BuildEffect.UnityLogWindowRead),
        ContractLiteralCodec.ToValue(BuildEffect.UcliArtifactWrite),
        ContractLiteralCodec.ToValue(BuildEffect.OutputManifestWrite),
        ContractLiteralCodec.ToValue(BuildEffect.GenerationSnapshot),
        ContractLiteralCodec.ToValue(BuildEffect.ProjectMutationAudit),
    ];
}
