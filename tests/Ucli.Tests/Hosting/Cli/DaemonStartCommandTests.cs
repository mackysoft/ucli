using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Daemon;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class DaemonStartCommandTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData("batchmode", DaemonEditorMode.Batchmode)]
    [InlineData("gui", DaemonEditorMode.Gui)]
    public async Task Start_WhenEditorModeIsSpecified_PassesTypedEditorModeToService (
        string editorModeOption,
        DaemonEditorMode expectedEditorMode)
    {
        var service = new StubDaemonStartService(DaemonStartExecutionResult.Success(CreateSuccessOutput()));
        var command = new DaemonStartCommand(service, CommandResultTestWriter.Create());

        CommandExecutionState.Reset();
        var (exitCode, _) = await StandardOutputCapture.ExecuteAsync(() => command.StartAsync(
            projectPath: "/repo/UnityProject",
            timeout: "1234",
            editorMode: editorModeOption,
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        Assert.Equal(1, service.CallCount);
        Assert.Equal("/repo/UnityProject", service.LastProjectPath);
        Assert.Equal(1234, service.LastTimeoutMilliseconds);
        Assert.Equal(expectedEditorMode, service.LastEditorMode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenEditorModeIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new StubDaemonStartService(DaemonStartExecutionResult.Success(CreateSuccessOutput()));
        var command = new DaemonStartCommand(service, CommandResultTestWriter.Create());

        CommandExecutionState.Reset();
        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.StartAsync(
            editorMode: "unsupported",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);
        Assert.Equal(0, service.CallCount);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.DaemonStart,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenServiceFailureHasDiagnosis_EmitsDiagnosisPayload ()
    {
        var diagnosis = new DaemonDiagnosisOutput(
            Reason: DaemonDiagnosisReasonValues.GuiEndpointNotRegistered,
            Message: "GUI endpoint was not registered.",
            ReportedBy: DaemonDiagnosisReportedByValues.Cli,
            IsInferred: true,
            UpdatedAtUtc: new DateTimeOffset(2026, 03, 12, 4, 5, 6, TimeSpan.Zero),
            ProcessId: 1234,
            EditorInstancePath: "/repo/UnityProject/Library/EditorInstance.json",
            ProcessStartedAtUtc: new DateTimeOffset(2026, 03, 12, 4, 5, 0, TimeSpan.Zero),
            UnityLogPath: "/repo/.ucli/local/fingerprints/fp/unity.log",
            StartupPhase: DaemonDiagnosisStartupPhaseValues.EndpointRegistration,
            ActionRequired: DaemonDiagnosisActionRequiredValues.InspectUnityLog,
            PrimaryDiagnostic: null);
        var service = new StubDaemonStartService(DaemonStartExecutionResult.Failure(
            ExecutionError.Timeout("registration timeout", ExecutionErrorCodes.IpcTimeout),
            diagnosis));
        var command = new DaemonStartCommand(service, CommandResultTestWriter.Create());

        CommandExecutionState.Reset();
        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.StartAsync(
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.ToolError, exitCode);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.DaemonStart,
            IpcProtocol.StatusError,
            (int)CliExitCode.ToolError);
        CommandResultAssert.HasSingleError(outputJson.RootElement, ExecutionErrorCodes.IpcTimeout);
        var payload = outputJson.RootElement.GetProperty("payload");
        var diagnosisJson = payload.GetProperty("diagnosis");
        Assert.Equal(DaemonDiagnosisReasonValues.GuiEndpointNotRegistered, diagnosisJson.GetProperty("reason").GetString());
        Assert.Equal("/repo/UnityProject/Library/EditorInstance.json", diagnosisJson.GetProperty("editorInstancePath").GetString());
        Assert.Equal("2026-03-12T04:05:00+00:00", diagnosisJson.GetProperty("processStartedAtUtc").GetString());
        Assert.Equal("/repo/.ucli/local/fingerprints/fp/unity.log", diagnosisJson.GetProperty("unityLogPath").GetString());
        Assert.Equal(DaemonDiagnosisStartupPhaseValues.EndpointRegistration, diagnosisJson.GetProperty("startupPhase").GetString());
        Assert.Equal(DaemonDiagnosisActionRequiredValues.InspectUnityLog, diagnosisJson.GetProperty("actionRequired").GetString());
        Assert.True(diagnosisJson.GetProperty("isInferred").GetBoolean());
    }

    private static DaemonStartExecutionOutput CreateSuccessOutput ()
    {
        return new DaemonStartExecutionOutput(
            StartStatus: DaemonStartStatus.Started,
            DaemonStatus: DaemonStatusKind.Running,
            TimeoutMilliseconds: 1234,
            Session: new DaemonSessionOutput(
                ProjectFingerprint: "fingerprint",
                IssuedAtUtc: new DateTimeOffset(2026, 03, 12, 1, 2, 3, TimeSpan.Zero),
                EditorMode: DaemonEditorModeValues.Batchmode,
                OwnerKind: DaemonSessionOwnerKindValues.Cli,
                CanShutdownProcess: true,
                EndpointTransportKind: "namedPipe",
                EndpointAddress: "ucli-daemon-endpoint",
                ProcessId: 1234,
                ProcessStartedAtUtc: DateTimeOffset.UtcNow,
                OwnerProcessId: 5678));
    }

    private sealed class StubDaemonStartService : IDaemonStartService
    {
        private readonly DaemonStartExecutionResult result;

        public StubDaemonStartService (DaemonStartExecutionResult result)
        {
            this.result = result;
        }

        public int CallCount { get; private set; }

        public string? LastProjectPath { get; private set; }

        public int? LastTimeoutMilliseconds { get; private set; }

        public DaemonEditorMode? LastEditorMode { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public ValueTask<DaemonStartExecutionResult> StartAsync (
            string? projectPath,
            int? timeoutMilliseconds,
            DaemonEditorMode? editorMode,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastProjectPath = projectPath;
            LastTimeoutMilliseconds = timeoutMilliseconds;
            LastEditorMode = editorMode;
            LastCancellationToken = cancellationToken;
            return ValueTask.FromResult(result);
        }
    }
}
