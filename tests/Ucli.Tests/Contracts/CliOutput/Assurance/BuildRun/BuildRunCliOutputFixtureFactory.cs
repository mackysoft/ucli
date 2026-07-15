using MackySoft.Ucli.Application.Features.Assurance.Build.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Build.Payload;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Assurance;

namespace MackySoft.Ucli.Tests;

internal static class BuildRunCliOutputFixtureFactory
{
    private static readonly AssuranceVerifierId BuildVerifierId = new("build");

    private static readonly Sha256Digest SuccessManifestDigest = Sha256Digest.Parse("5676e19e1fc5c210fba288de8bed4841cbd2efbc2bc352653fd6306505e83264");
    private static readonly Sha256Digest FailedManifestDigest = Sha256Digest.Parse("04d7d7e1eb32bc4521986964ba5e86b772fe46a3b50a73e4dd3783d4c4577d21");

    private static readonly Sha256Digest BuildDigest = Sha256Digest.Parse(new string('a', 64));
    private static readonly Sha256Digest BuildReportDigest = Sha256Digest.Parse(new string('b', 64));
    private static readonly Sha256Digest BuildOutputManifestArtifactDigest = Sha256Digest.Parse(new string('c', 64));
    private static readonly Sha256Digest BuildLogDigest = Sha256Digest.Parse(new string('d', 64));
    private static readonly Sha256Digest ProfileDigest = Sha256Digest.Parse(new string('e', 64));
    private static readonly DateTimeOffset BuildStartedAtUtc = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset BuildCompletedAtUtc = new(2026, 6, 1, 0, 0, 2, 500, TimeSpan.Zero);
    private static readonly string BuildProfilePath = Path.Combine(
        ProjectPathTestValues.WorkspaceUnityProject,
        ".ucli",
        "build",
        "player.json");

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

    public static CommandResult CreateCommandResult (string caseName)
    {
        return caseName switch
        {
            "success" => BuildRunCommandResultFactory.Create(BuildExecutionResult.Success(CreateOutput(succeeded: true))),
            "build-report-failed" => BuildRunCommandResultFactory.Create(BuildExecutionResult.Success(CreateOutput(succeeded: false))),
            "invalid-profile" => BuildRunCommandResultFactory.Create(BuildExecutionResult.Failure(
                ExecutionError.InvalidArgument($"Build profile is invalid: {BuildProfilePath}.", BuildErrorCodes.BuildProfileInvalid),
                ProjectIdentityInfoTestFactory.CreateWithProjectPath(projectPath: ProjectPathTestValues.WorkspaceUnityProject))),
            "unsupported-buildTarget" => BuildRunCommandResultFactory.Create(BuildExecutionResult.Failure(
                ExecutionError.InvalidArgument("Build profile inputs.buildTarget is unsupported: unknownTarget.", BuildErrorCodes.BuildTargetUnsupported),
                ProjectIdentityInfoTestFactory.CreateWithProjectPath(projectPath: ProjectPathTestValues.WorkspaceUnityProject))),
            "dirty-scene" => BuildRunCommandResultFactory.Create(BuildExecutionResult.Failure(
                ApplicationFailure.FromCode(BuildErrorCodes.BuildDirtyStatePresent, "Dirty scene state is present."),
                ProjectIdentityInfoTestFactory.CreateWithProjectPath(projectPath: ProjectPathTestValues.WorkspaceUnityProject),
                CreateDirtyState())),
            "buildTarget-module-missing" => BuildRunCommandResultFactory.Create(BuildExecutionResult.Failure(
                ApplicationFailure.FromCode(BuildErrorCodes.BuildTargetModuleMissing, "buildTarget module is missing: standaloneLinux64."),
                ProjectIdentityInfoTestFactory.CreateWithProjectPath(projectPath: ProjectPathTestValues.WorkspaceUnityProject))),
            "artifact-write-failed" => BuildRunCommandResultFactory.Create(BuildExecutionResult.Failure(
                ExecutionError.InternalError("Build artifacts could not be written.", BuildErrorCodes.BuildArtifactWriteFailed),
                ProjectIdentityInfoTestFactory.CreateWithProjectPath(projectPath: ProjectPathTestValues.WorkspaceUnityProject))),
            "output-manifest-failed" => BuildRunCommandResultFactory.Create(BuildExecutionResult.Failure(
                ExecutionError.InternalError("Build output manifest could not be generated.", BuildErrorCodes.BuildOutputManifestFailed),
                ProjectIdentityInfoTestFactory.CreateWithProjectPath(projectPath: ProjectPathTestValues.WorkspaceUnityProject))),
            _ => throw new ArgumentOutOfRangeException(nameof(caseName), caseName, "Unknown build.run Golden case."),
        };
    }

    private static BuildExecutionOutput CreateOutput (bool succeeded)
    {
        var reportResult = succeeded
            ? IpcBuildReportResult.Succeeded
            : IpcBuildReportResult.Failed;
        var manifestDigest = succeeded ? SuccessManifestDigest : FailedManifestDigest;
        var errorCount = succeeded ? 0 : 1;
        var warningCount = succeeded ? 1 : 0;
        var entryCount = succeeded ? 1 : 0;
        var fileCount = succeeded ? 2 : 0;
        var totalBytes = succeeded ? 33 : 0;
        var build = new BuildOutput(
            runId: RunIdTestValues.Build,
            profile: new BuildProfileOutput(BuildProfilePath, ProfileDigest),
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
                ManifestDigest: manifestDigest,
                EntryCount: entryCount,
                FileCount: fileCount,
                TotalBytes: totalBytes),
            generations: CreateGenerations(),
            summary: new BuildSummaryOutput(
                Result: reportResult,
                DurationMilliseconds: succeeded ? 2500 : 2400,
                ErrorCount: errorCount,
                WarningCount: warningCount,
                ReportRef: BuildArtifactKind.BuildReport),
            logs: new BuildLogsOutput(
                ReportRef: BuildArtifactKind.BuildLog,
                EntryCount: succeeded ? 3 : 2,
                ErrorCount: errorCount,
                WarningCount: warningCount,
                CompletionReason: succeeded
                    ? IpcBuildLogCompletionReason.Completed
                    : IpcBuildLogCompletionReason.Failed,
                Window: new BuildLogWindowOutput(BuildStartedAtUtc, BuildCompletedAtUtc)));
        var claims = BuildRunCliOutputClaimFixtureFactory.CreateClaims(build, succeeded);

        return new BuildExecutionOutput(
            Verdict: succeeded
                ? AssuranceVerdict.Pass
                : AssuranceVerdict.Fail,
            Project: ProjectIdentityInfoTestFactory.CreateWithProjectPath(projectPath: ProjectPathTestValues.WorkspaceUnityProject),
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

    private static IReadOnlyDictionary<BuildArtifactKind, AssuranceReportReference> CreateReports ()
    {
        return new Dictionary<BuildArtifactKind, AssuranceReportReference>
        {
            [BuildArtifactKind.Build] = AssuranceReportReference.FromPath("build.json", BuildDigest),
            [BuildArtifactKind.BuildReport] = AssuranceReportReference.FromPath("build-report.json", BuildReportDigest),
            [BuildArtifactKind.BuildOutputManifest] = AssuranceReportReference.FromPath("output-manifest.json", BuildOutputManifestArtifactDigest),
            [BuildArtifactKind.BuildLog] = AssuranceReportReference.FromPath("build.log", BuildLogDigest),
        };
    }

    private static BuildGenerationsOutput CreateGenerations ()
    {
        return new BuildGenerationsOutput(
            Before: new IpcUnityGenerationSnapshot(1, 1, 1, 1),
            After: new IpcUnityGenerationSnapshot(2, 1, 1, 1),
            ValidFor: new IpcUnityGenerationSnapshot(2, 1, 1, 1));
    }

    private static IpcBuildDirtyState CreateDirtyState ()
    {
        return new IpcBuildDirtyState(
            Dirty: true,
            Coverage: IpcBuildDirtyStateCoverage.Full,
            Items:
            [
                new IpcBuildDirtyStateItem(
                    IpcBuildDirtyStateItemKind.Scene,
                    new ProjectMutationAuditPath("Assets/Scenes/Main.unity")),
            ]);
    }
}
