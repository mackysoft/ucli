using MackySoft.Ucli.Application.Features.Assurance.Build.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Payload;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Payload;

internal static class AssuranceExecutionOutputTestFactory
{
    private static readonly Guid CompileExecutionId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static BuildOutput CreateBuildOutput ()
    {
        var digest = Sha256DigestTestFactory.Create('1');
        return new BuildOutput(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            new BuildProfileOutput("build-profile.json", digest),
            new BuildInputsOutput(
                BuildProfileInputsKind.Explicit,
                new BuildTargetOutput(BuildTargetStableName.StandaloneOsx, "StandaloneOSX"),
                new BuildScenesOutput(BuildProfileSceneSource.EditorBuildSettings, []),
                new BuildOptionsOutput(Development: false),
                UnityBuildProfile: null),
            new BuildRunnerOutput(
                BuildRunnerKind.BuildPipeline,
                Method: null,
                new BuildRunnerInvocationOutput(
                    new Dictionary<string, string>(StringComparer.Ordinal),
                    new BuildRunnerInvocationEnvironmentOutput([], []))),
            new BuildRunnerResultOutput(
                IpcBuildRunnerResultSource.BuildPipelineBuildReport,
                IpcBuildReportResult.Succeeded),
            new BuildArtifactOutput(
                digest,
                EntryCount: 0,
                FileCount: 0,
                TotalBytes: 0),
            new BuildGenerationsOutput(Before: null, After: null, ValidFor: null),
            new BuildSummaryOutput(
                IpcBuildReportResult.Succeeded,
                DurationMilliseconds: 0,
                ErrorCount: 0,
                WarningCount: 0,
                ReportRef: null),
            new BuildLogsOutput(
                EntryCount: 0,
                ErrorCount: 0,
                WarningCount: 0,
                IpcBuildLogCompletionReason.Completed,
                new BuildLogWindowOutput(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch)));
    }

    public static CompileOutput CreateCompileOutput ()
    {
        return new CompileOutput(
            new CompileRefreshOutput(
                CompileLifecycleRefreshOrigin.AssetDatabaseRefresh,
                Requested: true,
                DateTimeOffset.UnixEpoch,
                DateTimeOffset.UnixEpoch,
                Completed: true),
            new CompileScriptCompilationOutput(
                Started: true,
                Completed: true,
                CompileGenerationBefore: 1,
                CompileGenerationAfter: 2,
                new CompileDiagnosticsOutput(0, 0, PrimaryDiagnostic: null)),
            new CompileDomainReloadOutput(
                ReloadRequired: true,
                ReloadObserved: true,
                GenerationBefore: 1,
                GenerationAfter: 2,
                Settled: true),
            new CompileLifecycleOutput(
                ServerVersion: null,
                UnityVersion: null,
                EditorMode: null,
                LifecycleState: null,
                BlockingReason: null,
                CompileState: null,
                Generations: null,
                CanAcceptExecutionRequests: true,
                ObservedAtUtc: DateTimeOffset.UnixEpoch,
                ActionRequired: null,
                PrimaryDiagnostic: null));
    }

    public static TerminalExecutionRef CreateCompileExecutionRef (
        Guid? executionId = null)
    {
        var actualExecutionId = executionId ?? CompileExecutionId;
        var definition = new LifecycleExecutionDefinition(
            LifecycleExecutionKind.Compile);
        return new TerminalExecutionRef(
            definition.ExecutionKind,
            actualExecutionId,
            LifecycleExecutionDefinitionDigest.Calculate(definition),
            new ExecutionState(TextVocabulary.GetText(LifecycleExecutionState.Completed)),
            statusLocator: null,
            new PathArtifactRef(
                LifecycleExecutionArtifactContract.TerminalRecordKind,
                LifecycleExecutionArtifactContract.TerminalRecordMediaType,
                new ArtifactPath(
                    $"lifecycle-executions/{actualExecutionId:N}/terminal-record.json"),
                Sha256Digest.Parse(new string('a', 64)),
                sizeBytes: 512,
                DateTimeOffset.UnixEpoch));
    }

    public static ReadyLifecycleOutput CreateReadyLifecycleOutput ()
    {
        return new ReadyLifecycleOutput(
            ServerVersion: null,
            UnityVersion: null,
            EditorMode: null,
            LifecycleState: null,
            BlockingReason: null,
            CompileState: null,
            Generations: null,
            CanAcceptExecutionRequests: true,
            ObservedAtUtc: DateTimeOffset.UnixEpoch,
            ActionRequired: null,
            PrimaryDiagnostic: null,
            PlayMode: null);
    }

    public static VerifyProfileOutput CreateVerifyProfileOutput ()
    {
        return VerifyProfileOutput.BuiltIn(
            "default",
            Sha256DigestTestFactory.Create('2'));
    }
}
