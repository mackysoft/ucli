using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Contracts.Ipc;
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
                ObservedAtUtc: new DateTimeOffset(2026, 03, 12, 1, 3, 0, TimeSpan.Zero),
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
                .HasProperty("session", sessionJson => sessionJson
                    .HasString("editorMode", DaemonEditorModeValues.Gui)
                    .HasString("ownerKind", DaemonSessionOwnerKindValues.User)
                    .HasBoolean("canShutdownProcess", false)));

        var payloadJson = outputJson.RootElement.GetProperty("payload");
        Assert.False(payloadJson.TryGetProperty("runtimeKind", out _));
        Assert.False(payloadJson.GetProperty("session").TryGetProperty("runtimeKind", out _));
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
