using MackySoft.Ucli.Application.Features.Assurance.Build.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Tests;

internal static class BuildRunTestData
{
    private static readonly AssuranceVerifierId BuildVerifierId = new("build");

    public const string RunIdText = RunIdTestValues.BuildText;

    public static readonly Guid RunId = RunIdTestValues.Build;

    public static readonly ProjectFingerprint ProjectFingerprint = ProjectFingerprintTestFactory.Create("project-fingerprint");

    public static BuildExecutionOutput CreatePassedOutput ()
    {
        return CreateOutput(
            IpcBuildReportResult.Succeeded,
            IpcBuildLogCompletionReason.Completed,
            errorCount: 0,
            buildSucceededClaimStatus: AssuranceClaimStatus.Passed);
    }

    public static BuildExecutionOutput CreateFailedOutput ()
    {
        return CreateOutput(
            IpcBuildReportResult.Failed,
            IpcBuildLogCompletionReason.Failed,
            errorCount: 1,
            buildSucceededClaimStatus: AssuranceClaimStatus.Failed);
    }

    public static BuildExecutionOutput CreateIncompleteOutput ()
    {
        return CreateOutput(
            IpcBuildReportResult.Succeeded,
            IpcBuildLogCompletionReason.Completed,
            errorCount: 0,
            buildSucceededClaimStatus: AssuranceClaimStatus.Indeterminate);
    }

    private static BuildExecutionOutput CreateOutput (
        IpcBuildReportResult reportResult,
        IpcBuildLogCompletionReason completionReason,
        int errorCount,
        AssuranceClaimStatus buildSucceededClaimStatus)
    {
        var project = ProjectIdentityInfoTestFactory.CreateWithProjectPath(projectPath: ProjectPathTestValues.WorkspaceUnityProject, projectFingerprint: ProjectFingerprint);
        var build = CreateBuild(reportResult, completionReason, errorCount);
        var claims = CreateClaims(build, buildSucceededClaimStatus);

        return new BuildExecutionOutput(
            Project: project,
            Build: build,
            Verifiers:
            [
                new BuildVerifierOutput(
                    Id: BuildVerifierId,
                    Deterministic: false,
                    Required: true,
                    PrimaryClaims: claims.Where(static claim => claim.Required).Select(static claim => claim.Id).ToArray(),
                    Effects: BuildPipelineEffectValues),
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
            Verdict: Verdict.Pass,
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
                ManifestDigest: Repeat('b'),
                EntryCount: 1,
                FileCount: 1,
                TotalBytes: 4096),
            generations: new BuildGenerationsOutput(
                Before: new UnityEditorGenerationSnapshot(1, 1, 1, 1),
                After: new UnityEditorGenerationSnapshot(2, 1, 1, 1),
                ValidFor: new UnityEditorGenerationSnapshot(2, 1, 1, 1)),
            summary: new BuildSummaryOutput(
                Result: reportResult,
                DurationMilliseconds: 2500,
                ErrorCount: errorCount,
                WarningCount: 1,
                ReportRef: BuildArtifactKind.BuildReport),
            logs: new BuildLogsOutput(
                EntryCount: 3,
                ErrorCount: errorCount,
                WarningCount: 1,
                CompletionReason: completionReason,
                Window: new BuildLogWindowOutput(
                    DateTimeOffset.Parse("2026-06-12T00:00:00+00:00"),
                    DateTimeOffset.Parse("2026-06-12T00:00:03+00:00"))));
    }

    public static UnityEditorObservation CreateLifecycleObservation ()
    {
        return PlayCommandOutputTestData.CreateLifecycleSnapshot(
            UnityEditorLifecycleState.Ready,
            PlayCommandOutputTestData.CreatePlayMode(
                UnityEditorPlayModeState.Stopped,
                UnityEditorPlayModeTransition.None,
                isPlaying: false,
                isPlayingOrWillChangePlaymode: false),
            playModeGeneration: 1);
    }

    public static IpcBuildInputProbe CreateInputProbe (BuildOutput build)
    {
        ArgumentNullException.ThrowIfNull(build);
        return new IpcBuildInputProbe(
            build.Inputs.InputKind,
            build.Inputs.Target.StableName,
            build.Inputs.Target.UnityBuildTarget,
            "Standalone",
            build.Inputs.Scenes.Source,
            build.Inputs.Scenes.Paths,
            "Development");
    }

    public static IpcBuildProjectMutationAudit CreateProjectMutationAudit ()
    {
        return new IpcBuildProjectMutationAudit(
            BuildProfileProjectMutationMode.Forbid,
            IpcBuildProjectMutationAuditCoverage.Full,
            Mutated: false,
            BeforeDigest: Repeat('1'),
            AfterDigest: Repeat('1'),
            Items: []);
    }

    private static IReadOnlyList<BuildClaimOutput> CreateClaims (
        BuildOutput build,
        AssuranceClaimStatus buildSucceededClaimStatus)
    {
        const AssuranceClaimStatus passed = AssuranceClaimStatus.Passed;
        var reportResultLiteral = TextVocabulary.GetText(build.Summary.Result);

        return
        [
            CreateClaim(
                BuildClaimCodes.UnityBuildProfileResolved,
                passed,
                "Build profile resolved.",
                BuildProfileEvidenceOutput.Create(build.Profile)),
            CreateClaim(
                BuildClaimCodes.UnityReadyForBuild,
                passed,
                "Unity was ready for build.",
                BuildLifecycleEvidenceOutput.Create(CreateLifecycleObservation())),
            CreateClaim(
                BuildClaimCodes.UnityBuildInputsResolved,
                passed,
                "Build inputs resolved.",
                BuildInputEvidenceOutput.Create(CreateInputProbe(build))),
            CreateClaim(
                BuildClaimCodes.UnityBuildRunnerResolved,
                passed,
                "Build runner resolved.",
                BuildRunnerEvidenceOutput.Create(build.Runner)),
            CreateClaim(
                BuildClaimCodes.UnityBuildCompleted,
                passed,
                "BuildPipeline completed.",
                BuildReportSummaryEvidenceOutput.Create(build.Summary)),
            CreateClaim(
                BuildClaimCodes.UnityBuildSucceeded,
                buildSucceededClaimStatus,
                "BuildPipeline succeeded.",
                BuildReportSummaryEvidenceOutput.Create(build.Summary)),
            CreateClaim(
                BuildClaimCodes.UnityBuildResultAccounted,
                passed,
                "Build result accounted.",
                BuildRunnerResultEvidenceOutput.Create(build.RunnerResult),
                reportResultLiteral),
            CreateClaim(
                BuildClaimCodes.UnityBuildReportAccounted,
                passed,
                "BuildReport artifact accounted.",
                BuildReportSummaryEvidenceOutput.Create(build.Summary)),
            CreateClaim(
                BuildClaimCodes.UnityBuildArtifactsAccounted,
                passed,
                "Build artifacts accounted.",
                BuildOutputAccountingEvidenceOutput.Create(build.Output)),
            CreateClaim(
                BuildClaimCodes.UnityBuildOutputDigested,
                passed,
                "Build output digested.",
                BuildOutputManifestEvidenceOutput.Create(build.Output)),
            CreateClaim(
                BuildClaimCodes.UnityBuildLogsAccounted,
                passed,
                "Build logs accounted.",
                BuildLogEvidenceOutput.Create(build.Logs)),
            CreateClaim(
                BuildClaimCodes.UnityBuildProjectMutationAccounted,
                passed,
                "Project mutation accounted.",
                BuildProjectMutationEvidenceOutput.Create(CreateProjectMutationAudit())),
            CreateClaim(
                BuildClaimCodes.UnityBuildValidForGeneration,
                passed,
                "Build generations captured.",
                BuildGenerationEvidenceOutput.Create(build.Generations)),
        ];
    }

    private static BuildClaimOutput CreateClaim (
        UcliCode code,
        AssuranceClaimStatus status,
        string statement,
        BuildEvidenceOutput evidence,
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

    private static BuildReportsOutput CreateReports ()
    {
        return new BuildReportsOutput(
            Build: AssuranceReportReference.FromPath("build.json", Repeat('c')),
            BuildReport: AssuranceReportReference.FromPath("build-report.json", Repeat('d')),
            BuildOutputManifest: AssuranceReportReference.FromPath(
                "output-manifest.json",
                Repeat('e')),
            BuildLog: AssuranceReportReference.FromPath("build.log", Repeat('f')));
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
