using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
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
            EditorMode: DaemonEditorMode.Gui,
            OwnerKind: DaemonSessionOwnerKind.User,
            CanShutdownProcess: false,
            EndpointTransportKind: IpcTransportKind.UnixDomainSocket,
            EndpointAddress: "/tmp/ucli-gui.sock",
            ProcessId: 1234,
            ProcessStartedAtUtc: new DateTimeOffset(2026, 03, 12, 1, 2, 0, TimeSpan.Zero),
            OwnerProcessId: 9876);
        var service = new StubDaemonStatusService(
            DaemonStatusExecutionResult.Success(new DaemonStatusExecutionOutput(
                DaemonStatus: DaemonStatusKind.Running,
                ServerVersion: "0.0.2",
                EditorMode: DaemonEditorMode.Gui,
                LifecycleState: IpcEditorLifecycleState.PlayMode,
                BlockingReason: IpcEditorBlockingReason.PlayMode,
                CompileState: IpcCompileState.Ready,
                Generations: new IpcUnityGenerationSnapshot(3, 5, 0, 8),
                CanAcceptExecutionRequests: false,
                TimeoutMilliseconds: 3000,
                Session: session,
                Diagnosis: null,
                LastLaunchAttempt: null,
                ObservedAtUtc: new DateTimeOffset(2026, 03, 12, 1, 3, 0, TimeSpan.Zero),
                ActionRequired: null,
                PrimaryDiagnostic: null,
                PlayMode: new IpcPlayModeSnapshot(
                    State: IpcPlayModeState.Playing,
                    Transition: IpcPlayModeTransition.None,
                    IsPlaying: true,
                    IsPlayingOrWillChangePlaymode: true))));
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
                .HasString("lifecycleState", TextVocabulary.GetText(IpcEditorLifecycleState.PlayMode))
                .HasString("blockingReason", TextVocabulary.GetText(IpcEditorBlockingReason.PlayMode))
                .HasProperty("generations", generations => generations
                    .HasInt32("compileGeneration", 3)
                    .HasInt32("domainReloadGeneration", 5)
                    .HasInt32("assetRefreshGeneration", 0)
                    .HasInt32("playModeGeneration", 8))
                .HasBoolean("canAcceptExecutionRequests", false)
                .HasProperty("playMode", playMode => playMode
                    .HasString("state", "playing")
                    .HasString("transition", "none")
                    .HasBoolean("isPlaying", true)
                    .HasBoolean("isPlayingOrWillChangePlaymode", true))
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
            Reason: DaemonDiagnosisReason.GuiEndpointNotRegistered,
            Message: "GUI endpoint was not registered.",
            ReportedBy: DaemonDiagnosisReportedBy.Cli,
            IsInferred: true,
            UpdatedAtUtc: new DateTimeOffset(2026, 03, 12, 4, 5, 6, TimeSpan.Zero),
            ProcessId: 1234,
            EditorInstancePath: null,
            ProcessStartedAtUtc: new DateTimeOffset(2026, 03, 12, 4, 5, 0, TimeSpan.Zero),
            UnityLogPath: "/repo/.ucli/local/projects/04hkaps9lf6uu0938ljojaudts0i6hb7h6lsrro14d2mf2dbpnng/unity.log",
            StartupPhase: DaemonDiagnosisStartupPhase.EndpointRegistration,
            ActionRequired: DaemonDiagnosisActionRequired.InspectUnityLog,
            PrimaryDiagnostic: null);
        var launchAttempt = new DaemonLaunchAttemptOutput(
            LaunchAttemptId: Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"),
            StartupStatus: DaemonStartupStatus.Timeout,
            StartupBlockingReason: DaemonStartupBlockingReason.EndpointNotRegistered,
            RetryDisposition: DaemonStartupRetryDisposition.RetryImmediately,
            ProcessAction: DaemonStartupProcessAction.Kept,
            ArtifactPath: "/repo/.ucli/local/projects/04hkaps9lf6uu0938ljojaudts0i6hb7h6lsrro14d2mf2dbpnng/launch-attempts/04hkaps9lf6uu0938ljojaudts/startup-diagnosis.json",
            UnityLogPath: "/repo/.ucli/local/projects/04hkaps9lf6uu0938ljojaudts0i6hb7h6lsrro14d2mf2dbpnng/unity.log",
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
                Generations: null,
                CanAcceptExecutionRequests: false,
                TimeoutMilliseconds: 3000,
                Session: null,
                Diagnosis: null,
                LastLaunchAttempt: launchAttempt,
                ObservedAtUtc: new DateTimeOffset(2026, 03, 12, 4, 6, 0, TimeSpan.Zero),
                ActionRequired: null,
                PrimaryDiagnostic: null,
                PlayMode: null)));
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
                    .HasString("launchAttemptId", "01234567-89ab-cdef-0123-456789abcdef")
                    .HasString("startupStatus", "timeout")
                    .HasString("startupBlockingReason", "endpointNotRegistered")
                    .HasString("retryDisposition", "retryImmediately")
                    .HasString("processAction", "kept")
                    .HasString("artifactPath", "/repo/.ucli/local/projects/04hkaps9lf6uu0938ljojaudts0i6hb7h6lsrro14d2mf2dbpnng/launch-attempts/04hkaps9lf6uu0938ljojaudts/startup-diagnosis.json")
                    .HasString("unityLogPath", "/repo/.ucli/local/projects/04hkaps9lf6uu0938ljojaudts0i6hb7h6lsrro14d2mf2dbpnng/unity.log")
                    .HasString("updatedAtUtc", "2026-03-12T04:05:06+00:00")
                    .HasInt32("processId", 1234)
                    .HasString("processStartedAtUtc", "2026-03-12T04:05:00+00:00")
                    .HasProperty("diagnosis", diagnosisJson => diagnosisJson
                        .HasString("reason", TextVocabulary.GetText(DaemonDiagnosisReason.GuiEndpointNotRegistered))
                        .HasString("unityLogPath", "/repo/.ucli/local/projects/04hkaps9lf6uu0938ljojaudts0i6hb7h6lsrro14d2mf2dbpnng/unity.log")
                        .HasString("startupPhase", TextVocabulary.GetText(DaemonDiagnosisStartupPhase.EndpointRegistration))
                        .HasString("actionRequired", TextVocabulary.GetText(DaemonDiagnosisActionRequired.InspectUnityLog)))));

        var payloadJson = outputJson.RootElement.GetProperty("payload");
        Assert.False(payloadJson.TryGetProperty("runtimeKind", out _));
        Assert.False(payloadJson.GetProperty("lastLaunchAttempt").TryGetProperty("runtimeKind", out _));
        JsonGoldenFileAssert.Matches(CliOutputGoldenFiles.GetPath("daemon", "status-last-launch-attempt.json"), result.StdOut);
    }

}
