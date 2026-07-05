using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Hosting.Cli.Daemon;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.DaemonStartCommandTestSupport;

namespace MackySoft.Ucli.Tests;

public sealed class DaemonStartCommandFailurePayloadTests
{
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
            StartupPhase: ContractLiteralCodec.ToValue(DaemonDiagnosisStartupPhase.EndpointRegistration),
            ActionRequired: DaemonDiagnosisActionRequiredValues.InspectUnityLog,
            PrimaryDiagnostic: new DaemonPrimaryDiagnosticOutput(
                Kind: DaemonDiagnosisPrimaryDiagnosticKindValues.Compiler,
                Code: "CS0103",
                File: "Assets/Foo.cs",
                Line: 12,
                Column: 34,
                Message: "The name 'MissingType' does not exist in the current context"));
        var service = new RecordingDaemonStartService(DaemonStartExecutionResult.Failure(
            ExecutionError.Timeout("registration timeout", ExecutionErrorCodes.IpcTimeout),
            DaemonStartFailureExecutionOutput.Create(
                DaemonStatusKind.NotRunning,
                1234,
                startup: null,
                diagnosis)));
        var command = new DaemonStartCommand(service, CommandResultTestWriter.Create());

        CommandExecutionState.Reset();
        var result = await CommandResultCapture.ExecuteAsync(() => command.StartAsync(
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
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
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonDiagnosisStartupPhase.EndpointRegistration), diagnosisJson.GetProperty("startupPhase").GetString());
        Assert.Equal(DaemonDiagnosisActionRequiredValues.InspectUnityLog, diagnosisJson.GetProperty("actionRequired").GetString());
        Assert.True(diagnosisJson.GetProperty("isInferred").GetBoolean());
        var primaryDiagnosticJson = diagnosisJson.GetProperty("primaryDiagnostic");
        Assert.Equal(DaemonDiagnosisPrimaryDiagnosticKindValues.Compiler, primaryDiagnosticJson.GetProperty("kind").GetString());
        Assert.Equal("CS0103", primaryDiagnosticJson.GetProperty("code").GetString());
        Assert.Equal("Assets/Foo.cs", primaryDiagnosticJson.GetProperty("file").GetString());
        Assert.Equal(12, primaryDiagnosticJson.GetProperty("line").GetInt32());
        Assert.Equal(34, primaryDiagnosticJson.GetProperty("column").GetInt32());
        Assert.Equal("The name 'MissingType' does not exist in the current context", primaryDiagnosticJson.GetProperty("message").GetString());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Start_WhenServiceFailureHasStartupBlocker_EmitsStartupFailurePayload ()
    {
        var diagnosis = CreateDiagnosis(DaemonDiagnosisReasonValues.UnityScriptCompilationFailed);
        var startup = new DaemonStartupObservation(
            StartupStatus: ContractLiteralCodec.ToValue(DaemonStartupStatus.Blocked),
            StartupBlockingReason: ContractLiteralCodec.ToValue(DaemonStartupBlockingReason.Compile),
            LaunchAttemptId: "20260312_040500Z_00abcdef",
            ProcessAction: ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Kept),
            RetryDisposition: ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.RetryAfterFix),
            EditorMode: "batchmode",
            OwnerKind: "cli",
            CanShutdownProcess: true,
            ProcessId: 4321,
            StartedAtUtc: new DateTimeOffset(2026, 03, 12, 4, 5, 1, TimeSpan.Zero),
            ElapsedMilliseconds: 2500,
            ArtifactPath: "/repo/.ucli/local/fingerprints/fp/launchAttempts/20260312_040500Z_00abcdef/startup-diagnosis.json");
        var service = new RecordingDaemonStartService(DaemonStartExecutionResult.Failure(
            ExecutionError.InternalError("Unity startup is blocked.", DaemonErrorCodes.DaemonStartupBlocked),
            DaemonStartFailureExecutionOutput.Create(
                DaemonStatusKind.NotRunning,
                1234,
                startup,
                diagnosis)));
        var command = new DaemonStartCommand(service, CommandResultTestWriter.Create());

        CommandExecutionState.Reset();
        var result = await CommandResultCapture.ExecuteAsync(() => command.StartAsync(
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.DaemonStart,
            IpcProtocol.StatusError,
            (int)CliExitCode.ToolError);
        CommandResultAssert.HasSingleError(outputJson.RootElement, DaemonErrorCodes.DaemonStartupBlocked);

        var payload = outputJson.RootElement.GetProperty("payload");
        JsonAssert.For(payload)
            .HasString("startStatus", "failed")
            .HasString("daemonStatus", "notRunning")
            .HasInt32("timeoutMilliseconds", 1234)
            .IsNull("session")
            .HasString("retryDisposition", ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.RetryAfterFix))
            .HasBoolean("safeToRetryImmediately", false)
            .HasProperty("startup", startupJson => startupJson
                .HasString("startupStatus", ContractLiteralCodec.ToValue(DaemonStartupStatus.Blocked))
                .HasString("startupBlockingReason", ContractLiteralCodec.ToValue(DaemonStartupBlockingReason.Compile))
                .HasString("launchAttemptId", "20260312_040500Z_00abcdef")
                .HasString("editorMode", "batchmode")
                .HasString("ownerKind", "cli")
                .HasBoolean("canShutdownProcess", true)
                .HasInt32("processId", 4321)
                .HasString("startedAtUtc", "2026-03-12T04:05:01+00:00")
                .HasInt32("elapsedMilliseconds", 2500)
                .HasString("processAction", ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Kept))
                .IsNull("processTermination")
                .HasString("artifactPath", "/repo/.ucli/local/fingerprints/fp/launchAttempts/20260312_040500Z_00abcdef/startup-diagnosis.json")
                .HasString("retryDisposition", ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.RetryAfterFix)))
            .HasProperty("diagnosis", diagnosisJson => diagnosisJson
                .HasString("reason", DaemonDiagnosisReasonValues.UnityScriptCompilationFailed));
        Assert.False(payload.TryGetProperty("lifecycleState", out _));
        Assert.False(payload.TryGetProperty("blockingReason", out _));
        Assert.False(payload.TryGetProperty("canAcceptExecutionRequests", out _));
        Assert.False(payload.TryGetProperty("runtimeKind", out _));
        Assert.False(payload.GetProperty("startup").TryGetProperty("runtimeKind", out _));
        JsonGoldenFileAssert.Matches(CliOutputGoldenFiles.GetPath("daemon", "start-startup-blocked.json"), result.StdOut);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Start_WhenEndpointRegistrationTimesOut_NormalizesFinalWaitThenRetryToUnknown ()
    {
        var startup = new DaemonStartupObservation(
            StartupStatus: ContractLiteralCodec.ToValue(DaemonStartupStatus.Timeout),
            StartupBlockingReason: ContractLiteralCodec.ToValue(DaemonStartupBlockingReason.EndpointNotRegistered),
            LaunchAttemptId: "20260312_040500Z_00abcdef",
            ProcessAction: ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Terminated),
            RetryDisposition: ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.WaitThenRetry));
        var service = new RecordingDaemonStartService(DaemonStartExecutionResult.Failure(
            ExecutionError.Timeout("endpoint registration timeout", ExecutionErrorCodes.IpcTimeout),
            DaemonStartFailureExecutionOutput.Create(
                DaemonStatusKind.NotRunning,
                1234,
                startup,
                diagnosis: null)));
        var command = new DaemonStartCommand(service, CommandResultTestWriter.Create());

        CommandExecutionState.Reset();
        var result = await CommandResultCapture.ExecuteAsync(() => command.StartAsync(
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasSingleError(outputJson.RootElement, ExecutionErrorCodes.IpcTimeout);
        var payload = outputJson.RootElement.GetProperty("payload");
        JsonAssert.For(payload)
            .HasString("startStatus", "failed")
            .HasString("daemonStatus", "notRunning")
            .IsNull("session")
            .HasString("retryDisposition", ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.Unknown))
            .HasBoolean("safeToRetryImmediately", false)
            .HasProperty("startup", startupJson => startupJson
                .HasString("startupStatus", ContractLiteralCodec.ToValue(DaemonStartupStatus.Timeout))
                .HasString("startupBlockingReason", ContractLiteralCodec.ToValue(DaemonStartupBlockingReason.EndpointNotRegistered))
                .HasString("retryDisposition", ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.Unknown)));
        Assert.False(payload.TryGetProperty("lifecycleState", out _));
        Assert.False(payload.TryGetProperty("blockingReason", out _));
        Assert.False(payload.TryGetProperty("canAcceptExecutionRequests", out _));
        Assert.False(payload.TryGetProperty("runtimeKind", out _));
        Assert.False(payload.GetProperty("startup").TryGetProperty("runtimeKind", out _));
        JsonGoldenFileAssert.Matches(CliOutputGoldenFiles.GetPath("daemon", "start-endpoint-timeout.json"), result.StdOut);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenFailureCanRetryImmediately_EmitsSafeToRetryImmediately ()
    {
        var startup = new DaemonStartupObservation(
            StartupStatus: ContractLiteralCodec.ToValue(DaemonStartupStatus.Failed),
            StartupBlockingReason: ContractLiteralCodec.ToValue(DaemonStartupBlockingReason.Unknown),
            LaunchAttemptId: "20260312_040500Z_00abcdef",
            ProcessAction: ContractLiteralCodec.ToValue(DaemonStartupProcessAction.None),
            RetryDisposition: ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.RetryImmediately));
        var service = new RecordingDaemonStartService(DaemonStartExecutionResult.Failure(
            ExecutionError.InternalError("transient startup failure", DaemonErrorCodes.DaemonStartupBlocked),
            DaemonStartFailureExecutionOutput.Create(
                DaemonStatusKind.NotRunning,
                1234,
                startup,
                diagnosis: null)));
        var command = new DaemonStartCommand(service, CommandResultTestWriter.Create());

        CommandExecutionState.Reset();
        var result = await CommandResultCapture.ExecuteAsync(() => command.StartAsync(
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        JsonAssert.For(outputJson.RootElement.GetProperty("payload"))
            .HasString("retryDisposition", ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.RetryImmediately))
            .HasBoolean("safeToRetryImmediately", true)
            .HasProperty("startup", startupJson => startupJson
                .HasString("retryDisposition", ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.RetryImmediately)));
    }
}
