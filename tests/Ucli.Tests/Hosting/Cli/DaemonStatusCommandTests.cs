using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Application.Shared.CommandContracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Daemon;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class DaemonStatusCommandTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Status_WhenGuiSessionIsRunning_WritesSessionFieldsAndOmitsRuntimeKind ()
    {
        var session = new DaemonSessionOutput(
            ProjectFingerprint: "fp-gui",
            IssuedAtUtc: new DateTimeOffset(2026, 03, 12, 1, 2, 3, TimeSpan.Zero),
            EditorMode: DaemonEditorModeValues.Gui,
            OwnerKind: DaemonSessionOwnerKindValues.User,
            CanShutdownProcess: false,
            EndpointTransportKind: "unixDomainSocket",
            EndpointAddress: "/tmp/ucli-gui.sock",
            ProcessId: 1234,
            ProcessStartedAtUtc: new DateTimeOffset(2026, 03, 12, 1, 2, 0, TimeSpan.Zero),
            OwnerProcessId: 9876);
        var service = new StubDaemonStatusService(
            DaemonStatusExecutionResult.Success(new DaemonStatusExecutionOutput(
                DaemonStatus: DaemonStatusKind.Running,
                ServerVersion: "0.0.2",
                EditorMode: DaemonEditorModeValues.Gui,
                LifecycleState: IpcEditorLifecycleStateCodec.Playmode,
                BlockingReason: IpcEditorBlockingReasonCodec.PlayMode,
                CompileState: IpcCompileStateCodec.Ready,
                CompileGeneration: "3",
                DomainReloadGeneration: "5",
                CanAcceptExecutionRequests: false,
                TimeoutMilliseconds: 3000,
                Session: session,
                Diagnosis: null,
                LastLaunchAttempt: null,
                ObservedAtUtc: new DateTimeOffset(2026, 03, 12, 1, 3, 0, TimeSpan.Zero),
                ActionRequired: null,
                PrimaryDiagnostic: null,
                PlayMode: new PlayModeSnapshotOutput(
                    State: IpcPlayModeStateNames.Playing,
                    Transition: IpcPlayModeTransitionNames.None,
                    IsPlaying: true,
                    IsPlayingOrWillChangePlaymode: true,
                    Generation: "8"))));
        var command = new DaemonStatusCommand(service, CommandResultTestWriter.Create());

        CommandExecutionState.Reset();
        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.StatusAsync(
            projectPath: "/repo/wt-a/UnityProject",
            timeout: "3000",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.DaemonStatus,
            "ok",
            (int)CliExitCode.Success);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("daemonStatus", "running")
                .HasString("editorMode", DaemonEditorModeValues.Gui)
                .HasString("lifecycleState", IpcEditorLifecycleStateCodec.Playmode)
                .HasString("blockingReason", IpcEditorBlockingReasonCodec.PlayMode)
                .HasBoolean("canAcceptExecutionRequests", false)
                .HasProperty("playMode", playMode => playMode
                    .HasString("state", IpcPlayModeStateNames.Playing)
                    .HasString("transition", IpcPlayModeTransitionNames.None)
                    .HasBoolean("isPlaying", true)
                    .HasBoolean("isPlayingOrWillChangePlaymode", true)
                    .HasString("generation", "8"))
                .HasProperty("session", sessionJson => sessionJson
                    .HasString("editorMode", DaemonEditorModeValues.Gui)
                    .HasString("ownerKind", DaemonSessionOwnerKindValues.User)
                    .HasBoolean("canShutdownProcess", false)));

        var payloadJson = outputJson.RootElement.GetProperty("payload");
        Assert.False(payloadJson.TryGetProperty("runtimeKind", out _));
        Assert.False(payloadJson.GetProperty("session").TryGetProperty("runtimeKind", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Status_WhenLastLaunchAttemptExists_WritesLastLaunchAttemptPayload ()
    {
        var diagnosis = new DaemonDiagnosisOutput(
            Reason: DaemonDiagnosisReasonValues.GuiEndpointNotRegistered,
            Message: "GUI endpoint was not registered.",
            ReportedBy: DaemonDiagnosisReportedByValues.Cli,
            IsInferred: true,
            UpdatedAtUtc: new DateTimeOffset(2026, 03, 12, 4, 5, 6, TimeSpan.Zero),
            ProcessId: 1234,
            EditorInstancePath: null,
            ProcessStartedAtUtc: new DateTimeOffset(2026, 03, 12, 4, 5, 0, TimeSpan.Zero),
            UnityLogPath: "/repo/.ucli/local/fingerprints/fp/unity.log",
            StartupPhase: DaemonDiagnosisStartupPhaseValues.EndpointRegistration,
            ActionRequired: DaemonDiagnosisActionRequiredValues.InspectUnityLog,
            PrimaryDiagnostic: null);
        var launchAttempt = new DaemonLaunchAttemptOutput(
            LaunchAttemptId: "20260312_040500Z_00abcdef",
            StartupStatus: "timeout",
            StartupBlockingReason: "endpointNotRegistered",
            RetryDisposition: "retryFreshLaunch",
            ProcessAction: "keep",
            ArtifactPath: "/repo/.ucli/local/fingerprints/fp/launch-attempts/20260312_040500Z_00abcdef/startup-diagnosis.json",
            UnityLogPath: "/repo/.ucli/local/fingerprints/fp/unity.log",
            UpdatedAtUtc: new DateTimeOffset(2026, 03, 12, 4, 5, 6, TimeSpan.Zero),
            ProcessId: 1234,
            ProcessStartedAtUtc: new DateTimeOffset(2026, 03, 12, 4, 5, 0, TimeSpan.Zero),
            Diagnosis: diagnosis);
        var service = new StubDaemonStatusService(
            DaemonStatusExecutionResult.Success(new DaemonStatusExecutionOutput(
                DaemonStatus: DaemonStatusKind.NotRunning,
                ServerVersion: null,
                EditorMode: null,
                LifecycleState: null,
                BlockingReason: null,
                CompileState: null,
                CompileGeneration: null,
                DomainReloadGeneration: null,
                CanAcceptExecutionRequests: false,
                TimeoutMilliseconds: 3000,
                Session: null,
                Diagnosis: null,
                LastLaunchAttempt: launchAttempt,
                ObservedAtUtc: new DateTimeOffset(2026, 03, 12, 4, 6, 0, TimeSpan.Zero),
                ActionRequired: null,
                PrimaryDiagnostic: null)));
        var command = new DaemonStatusCommand(service, CommandResultTestWriter.Create());

        CommandExecutionState.Reset();
        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.StatusAsync(
            projectPath: "/repo/wt-a/UnityProject",
            timeout: "3000",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("daemonStatus", "notRunning")
                .HasProperty("lastLaunchAttempt", attemptJson => attemptJson
                    .HasString("launchAttemptId", "20260312_040500Z_00abcdef")
                    .HasString("startupStatus", "timeout")
                    .HasString("startupBlockingReason", "endpointNotRegistered")
                    .HasString("retryDisposition", "retryFreshLaunch")
                    .HasString("processAction", "keep")
                    .HasString("artifactPath", "/repo/.ucli/local/fingerprints/fp/launch-attempts/20260312_040500Z_00abcdef/startup-diagnosis.json")
                    .HasString("unityLogPath", "/repo/.ucli/local/fingerprints/fp/unity.log")
                    .HasString("updatedAtUtc", "2026-03-12T04:05:06+00:00")
                    .HasInt32("processId", 1234)
                    .HasString("processStartedAtUtc", "2026-03-12T04:05:00+00:00")
                    .HasProperty("diagnosis", diagnosisJson => diagnosisJson
                        .HasString("reason", DaemonDiagnosisReasonValues.GuiEndpointNotRegistered)
                        .HasString("unityLogPath", "/repo/.ucli/local/fingerprints/fp/unity.log")
                        .HasString("startupPhase", DaemonDiagnosisStartupPhaseValues.EndpointRegistration)
                        .HasString("actionRequired", DaemonDiagnosisActionRequiredValues.InspectUnityLog))));

        var payloadJson = outputJson.RootElement.GetProperty("payload");
        Assert.False(payloadJson.TryGetProperty("runtimeKind", out _));
        Assert.False(payloadJson.GetProperty("lastLaunchAttempt").TryGetProperty("runtimeKind", out _));
        JsonGoldenFileAssert.Matches(CliOutputGoldenFiles.GetPath("daemon", "status-last-launch-attempt.json"), standardOutput);
    }

    private sealed class StubDaemonStatusService : IDaemonStatusService
    {
        private readonly DaemonStatusExecutionResult result;

        public StubDaemonStatusService (DaemonStatusExecutionResult result)
        {
            this.result = result;
        }

        public ValueTask<DaemonStatusExecutionResult> GetStatusAsync (
            string? projectPath,
            int? timeoutMilliseconds,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(result);
        }
    }
}
