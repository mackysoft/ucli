using MackySoft.Ucli.Application.Features.Assurance.Build.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Build.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Assurance;

namespace MackySoft.Ucli.Tests;

internal static class BuildRunCliOutputFixtureFactory
{
    private const string SuccessManifestDigest = "1047a19f8c4eb95c4258d04da06d8b7335d006b88bcdc7c34dc6dbb78f98cdba";
    private const string FailedManifestDigest = "04d7d7e1eb32bc4521986964ba5e86b772fe46a3b50a73e4dd3783d4c4577d21";

    private static readonly string BuildDigest = new('a', 64);
    private static readonly string BuildReportDigest = new('b', 64);
    private static readonly string BuildOutputManifestArtifactDigest = new('c', 64);
    private static readonly string BuildLogDigest = new('d', 64);
    private static readonly string ProfileDigest = new('e', 64);
    private static readonly DateTimeOffset BuildStartedAtUtc = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset BuildCompletedAtUtc = new(2026, 6, 1, 0, 0, 2, 500, TimeSpan.Zero);

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

    public static CommandResult CreateCommandResult (string caseName)
    {
        return caseName switch
        {
            "success" => BuildRunCommandResultFactory.Create(BuildExecutionResult.Success(CreateOutput(succeeded: true))),
            "build-report-failed" => BuildRunCommandResultFactory.Create(BuildExecutionResult.Success(CreateOutput(succeeded: false))),
            "invalid-profile" => BuildRunCommandResultFactory.Create(BuildExecutionResult.Failure(
                ExecutionError.InvalidArgument("Build profile is invalid: /workspace/UnityProject/.ucli/build/player.json.", BuildErrorCodes.BuildProfileInvalid),
                ProjectIdentityInfoTestFactory.Create(projectPath: "/workspace/UnityProject"))),
            "unsupported-buildTarget" => BuildRunCommandResultFactory.Create(BuildExecutionResult.Failure(
                ExecutionError.InvalidArgument("Build profile inputs.buildTarget is unsupported: unknownTarget.", BuildErrorCodes.BuildTargetUnsupported),
                ProjectIdentityInfoTestFactory.Create(projectPath: "/workspace/UnityProject"))),
            "dirty-scene" => BuildRunCommandResultFactory.Create(BuildExecutionResult.Failure(
                ApplicationFailure.FromCode(BuildErrorCodes.BuildDirtyStatePresent, "Dirty scene state is present."),
                ProjectIdentityInfoTestFactory.Create(projectPath: "/workspace/UnityProject"),
                CreateDirtyState())),
            "buildTarget-module-missing" => BuildRunCommandResultFactory.Create(BuildExecutionResult.Failure(
                ApplicationFailure.FromCode(BuildErrorCodes.BuildTargetModuleMissing, "buildTarget module is missing: standaloneLinux64."),
                ProjectIdentityInfoTestFactory.Create(projectPath: "/workspace/UnityProject"))),
            "artifact-write-failed" => BuildRunCommandResultFactory.Create(BuildExecutionResult.Failure(
                ExecutionError.InternalError("Build artifacts could not be written.", BuildErrorCodes.BuildArtifactWriteFailed),
                ProjectIdentityInfoTestFactory.Create(projectPath: "/workspace/UnityProject"))),
            "output-manifest-failed" => BuildRunCommandResultFactory.Create(BuildExecutionResult.Failure(
                ExecutionError.InternalError("Build output manifest could not be generated.", BuildErrorCodes.BuildOutputManifestFailed),
                ProjectIdentityInfoTestFactory.Create(projectPath: "/workspace/UnityProject"))),
            _ => throw new ArgumentOutOfRangeException(nameof(caseName), caseName, "Unknown build.run Golden case."),
        };
    }

    private static BuildExecutionOutput CreateOutput (bool succeeded)
    {
        var reportResult = succeeded
            ? ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded)
            : ContractLiteralCodec.ToValue(IpcBuildReportResult.Failed);
        var manifestDigest = succeeded ? SuccessManifestDigest : FailedManifestDigest;
        var errorCount = succeeded ? 0 : 1;
        var warningCount = succeeded ? 1 : 0;
        var entryCount = succeeded ? 1 : 0;
        var fileCount = succeeded ? 2 : 0;
        var totalBytes = succeeded ? 33 : 0;
        var build = new BuildOutput(
            RunId: RunIdTestValues.Build,
            Profile: new BuildProfileOutput("/workspace/UnityProject/.ucli/build/player.json", ProfileDigest),
            Inputs: new BuildInputsOutput(
                InputKind: ContractLiteralCodec.ToValue(BuildProfileInputsKind.Explicit),
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
                Source: ContractLiteralCodec.ToValue(IpcBuildRunnerResultSource.BuildPipelineBuildReport),
                Status: reportResult),
            Output: new BuildArtifactOutput(
                ManifestRef: BuildReportRefs.BuildOutputManifest,
                ManifestDigest: manifestDigest,
                EntryCount: entryCount,
                FileCount: fileCount,
                TotalBytes: totalBytes),
            Generations: CreateGenerations(),
            Summary: new BuildSummaryOutput(
                Result: reportResult,
                DurationMilliseconds: succeeded ? 2500 : 2400,
                ErrorCount: errorCount,
                WarningCount: warningCount,
                ReportRef: BuildReportRefs.BuildReport),
            Logs: new BuildLogsOutput(
                ReportRef: BuildReportRefs.BuildLog,
                EntryCount: succeeded ? 3 : 2,
                ErrorCount: errorCount,
                WarningCount: warningCount,
                CompletionReason: succeeded
                    ? ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed)
                    : ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Failed),
                Window: new BuildLogWindowOutput(BuildStartedAtUtc, BuildCompletedAtUtc)));
        var claims = BuildRunCliOutputClaimFixtureFactory.CreateClaims(build, succeeded);

        return new BuildExecutionOutput(
            Verdict: succeeded
                ? ContractLiteralCodec.ToValue(BuildVerdict.Pass)
                : ContractLiteralCodec.ToValue(BuildVerdict.Fail),
            Project: ProjectIdentityInfoTestFactory.Create(projectPath: "/workspace/UnityProject"),
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

    private static IReadOnlyDictionary<string, BuildReportOutput> CreateReports ()
    {
        return new Dictionary<string, BuildReportOutput>(StringComparer.Ordinal)
        {
            [BuildReportRefs.Build] = new("build.json", BuildDigest),
            [BuildReportRefs.BuildReport] = new("build-report.json", BuildReportDigest),
            [BuildReportRefs.BuildOutputManifest] = new("output-manifest.json", BuildOutputManifestArtifactDigest),
            [BuildReportRefs.BuildLog] = new("build.log", BuildLogDigest),
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
            Checked: true,
            Dirty: true,
            Coverage: ContractLiteralCodec.ToValue(IpcBuildDirtyStateCoverage.Full),
            Items:
            [
                new IpcBuildDirtyStateItem(
                    ContractLiteralCodec.ToValue(IpcBuildDirtyStateItemKind.Scene),
                    "Assets/Scenes/Main.unity"),
            ]);
    }
}
