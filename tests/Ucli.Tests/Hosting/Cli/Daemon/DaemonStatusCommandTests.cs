using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Application.Shared.CommandContracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
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
            ProjectFingerprint: ProjectFingerprintTestFactory.Create("fp-gui"),
            IssuedAtUtc: new DateTimeOffset(2026, 03, 12, 1, 2, 3, TimeSpan.Zero),
            EditorMode: "gui",
            OwnerKind: "user",
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
                EditorMode: "gui",
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
                    State: "playing",
                    Transition: "none",
                    IsPlaying: true,
                    IsPlayingOrWillChangePlaymode: true,
                    Generation: "8"))));
        var command = new DaemonStatusCommand(service, CommandResultTestWriter.Create());

        CommandExecutionState.Reset();
        var result = await CommandResultCapture.ExecuteAsync(() => command.StatusAsync(
            projectPath: "/repo/wt-a/UnityProject",
            timeout: "3000",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.DaemonStatus);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("daemonStatus", "running")
                .HasString("editorMode", "gui")
                .HasString("lifecycleState", IpcEditorLifecycleStateCodec.Playmode)
                .HasString("blockingReason", IpcEditorBlockingReasonCodec.PlayMode)
                .HasBoolean("canAcceptExecutionRequests", false)
                .HasProperty("playMode", playMode => playMode
                    .HasString("state", "playing")
                    .HasString("transition", "none")
                    .HasBoolean("isPlaying", true)
                    .HasBoolean("isPlayingOrWillChangePlaymode", true)
                    .HasString("generation", "8"))
                .HasProperty("session", sessionJson => sessionJson
                    .HasString("editorMode", "gui")
                    .HasString("ownerKind", "user")
                    .HasBoolean("canShutdownProcess", false)));

        var payloadJson = outputJson.RootElement.GetProperty("payload");
        Assert.False(payloadJson.TryGetProperty("runtimeKind", out _));
        Assert.False(payloadJson.GetProperty("session").TryGetProperty("runtimeKind", out _));
    }

    [Fact]
    [Trait("Size", "Medium")]
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
            StartupPhase: ContractLiteralCodec.ToValue(DaemonDiagnosisStartupPhase.EndpointRegistration),
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
        var result = await CommandResultCapture.ExecuteAsync(() => command.StatusAsync(
            projectPath: "/repo/wt-a/UnityProject",
            timeout: "3000",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
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
                        .HasString("startupPhase", ContractLiteralCodec.ToValue(DaemonDiagnosisStartupPhase.EndpointRegistration))
                        .HasString("actionRequired", DaemonDiagnosisActionRequiredValues.InspectUnityLog))));

        var payloadJson = outputJson.RootElement.GetProperty("payload");
        Assert.False(payloadJson.TryGetProperty("runtimeKind", out _));
        Assert.False(payloadJson.GetProperty("lastLaunchAttempt").TryGetProperty("runtimeKind", out _));
        JsonGoldenFileAssert.Matches(CliOutputGoldenFiles.GetPath("daemon", "status-last-launch-attempt.json"), result.StdOut);
    }

}
