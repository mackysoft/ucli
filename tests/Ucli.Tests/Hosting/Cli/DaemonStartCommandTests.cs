using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Application.Shared.Execution.Progress;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
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
        Assert.Equal(DaemonStartupBlockedProcessPolicy.Auto, service.LastOnStartupBlocked);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(null, DaemonStartupBlockedProcessPolicy.Auto)]
    [InlineData("auto", DaemonStartupBlockedProcessPolicy.Auto)]
    [InlineData("keep", DaemonStartupBlockedProcessPolicy.Keep)]
    [InlineData("terminate", DaemonStartupBlockedProcessPolicy.Terminate)]
    public async Task Start_WhenOnStartupBlockedIsSpecified_PassesTypedPolicyToService (
        string? optionValue,
        DaemonStartupBlockedProcessPolicy expectedPolicy)
    {
        var service = new StubDaemonStartService(DaemonStartExecutionResult.Success(CreateSuccessOutput()));
        var command = new DaemonStartCommand(service, CommandResultTestWriter.Create());

        CommandExecutionState.Reset();
        var (exitCode, _) = await StandardOutputCapture.ExecuteAsync(() => command.StartAsync(
            onStartupBlocked: optionValue,
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        Assert.Equal(1, service.CallCount);
        Assert.Equal(expectedPolicy, service.LastOnStartupBlocked);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenServiceSucceedsWithCompilingLifecycle_EmitsLifecycleSnapshotPayload ()
    {
        var service = new StubDaemonStartService(DaemonStartExecutionResult.Success(CreateSuccessOutput(
            lifecycleState: IpcEditorLifecycleStateCodec.Compiling,
            blockingReason: IpcEditorBlockingReasonCodec.Compile,
            canAcceptExecutionRequests: false)));
        var command = new DaemonStartCommand(service, CommandResultTestWriter.Create());

        CommandExecutionState.Reset();
        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.StartAsync(
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        Assert.Equal(string.Empty, standardError);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        JsonAssert.For(outputJson.RootElement.GetProperty("payload"))
            .HasString("startStatus", "started")
            .HasString("daemonStatus", "running")
            .HasString("lifecycleState", IpcEditorLifecycleStateCodec.Compiling)
            .HasString("blockingReason", IpcEditorBlockingReasonCodec.Compile)
            .HasBoolean("canAcceptExecutionRequests", false);

        var payload = outputJson.RootElement.GetProperty("payload");
        Assert.False(payload.TryGetProperty("runtimeKind", out _));
        Assert.False(payload.GetProperty("session").TryGetProperty("runtimeKind", out _));
        JsonGoldenFileAssert.Matches(CliOutputGoldenFiles.GetPath("daemon", "start-compiling-success.json"), standardOutput);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WithJsonFormat_WritesProgressEntriesToStandardErrorAndFinalCommandResultToStandardOutput ()
    {
        var service = new StubDaemonStartService(
            DaemonStartExecutionResult.Success(CreateSuccessOutput(
                lifecycleState: IpcEditorLifecycleStateCodec.Compiling,
                blockingReason: IpcEditorBlockingReasonCodec.Compile,
                canAcceptExecutionRequests: false)),
            EmitSampleDaemonStartProgressAsync);
        var command = new DaemonStartCommand(service, CommandResultTestWriter.Create());

        CommandExecutionState.Reset();
        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.StartAsync(
            format: "json",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        Assert.Equal(1, service.CallCount);
        var lines = standardError.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        using var startedEntry = JsonDocument.Parse(lines[0]);
        using var completedEntry = JsonDocument.Parse(lines[1]);
        JsonAssert.For(startedEntry.RootElement)
            .HasString("command", UcliCommandNames.DaemonStart)
            .HasString("event", ContractLiteralCodec.ToValue(DaemonStartProgressEvent.Started))
            .HasInt32("sequence", 1);
        JsonAssert.For(startedEntry.RootElement.GetProperty("payload"))
            .HasString("projectFingerprint", "fingerprint")
            .HasInt32("timeoutMilliseconds", 1234)
            .HasString("editorMode", "batchmode")
            .HasString("onStartupBlocked", "auto")
            .IsNull("result")
            .IsNull("startStatus")
            .IsNull("daemonStatus")
            .IsNull("errorCode");
        JsonAssert.For(completedEntry.RootElement)
            .HasString("command", UcliCommandNames.DaemonStart)
            .HasString("event", ContractLiteralCodec.ToValue(DaemonStartProgressEvent.Completed))
            .HasInt32("sequence", 2);
        JsonAssert.For(completedEntry.RootElement.GetProperty("payload"))
            .HasString("result", ContractLiteralCodec.ToValue(CommandProgressResult.Succeeded))
            .HasString("startStatus", "started")
            .HasString("daemonStatus", "running")
            .IsNull("errorCode");
        JsonGoldenFileAssert.Matches(CliOutputGoldenFiles.GetPath("daemon", "start-compiling-success.json"), standardOutput);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WithJsonFormat_WritesSupervisorProgressPayloadsToStandardErrorOnly ()
    {
        var service = new StubDaemonStartService(
            DaemonStartExecutionResult.Success(CreateSuccessOutput(
                lifecycleState: IpcEditorLifecycleStateCodec.Compiling,
                blockingReason: IpcEditorBlockingReasonCodec.Compile,
                canAcceptExecutionRequests: false)),
            EmitSampleSupervisorProgressAsync);
        var command = new DaemonStartCommand(service, CommandResultTestWriter.Create());

        CommandExecutionState.Reset();
        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.StartAsync(
            format: "json",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        var lines = standardError.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(4, lines.Length);
        using var waitingEntry = JsonDocument.Parse(lines[0]);
        using var blockerEntry = JsonDocument.Parse(lines[1]);
        using var endpointEntry = JsonDocument.Parse(lines[2]);
        using var lifecycleEntry = JsonDocument.Parse(lines[3]);
        JsonAssert.For(waitingEntry.RootElement)
            .HasString("event", ContractLiteralCodec.ToValue(DaemonStartProgressEvent.WaitingForEndpoint))
            .HasInt32("sequence", 1);
        JsonAssert.For(waitingEntry.RootElement.GetProperty("payload"))
            .HasString("payloadKind", "startupObservation")
            .HasString("projectFingerprint", "fingerprint")
            .HasString("startupStatus", "waitingForEndpoint")
            .HasString("startupPhase", "endpointRegistration");
        Assert.False(waitingEntry.RootElement.GetProperty("payload").TryGetProperty("lifecycleState", out _));
        Assert.False(waitingEntry.RootElement.GetProperty("payload").TryGetProperty("blockingReason", out _));
        Assert.False(waitingEntry.RootElement.GetProperty("payload").TryGetProperty("canAcceptExecutionRequests", out _));
        JsonAssert.For(blockerEntry.RootElement)
            .HasString("event", ContractLiteralCodec.ToValue(DaemonStartProgressEvent.BlockerDetected))
            .HasInt32("sequence", 2);
        JsonAssert.For(blockerEntry.RootElement.GetProperty("payload"))
            .HasString("payloadKind", "startupObservation")
            .HasString("startupStatus", ContractLiteralCodec.ToValue(DaemonStartupStatus.Blocked))
            .HasString("startupBlockingReason", ContractLiteralCodec.ToValue(DaemonStartupBlockingReason.Compile))
            .HasString("retryDisposition", ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.RetryAfterFix));
        JsonAssert.For(endpointEntry.RootElement)
            .HasString("event", ContractLiteralCodec.ToValue(DaemonStartProgressEvent.EndpointRegistered))
            .HasInt32("sequence", 3);
        JsonAssert.For(endpointEntry.RootElement.GetProperty("payload"))
            .HasString("payloadKind", "startupObservation")
            .HasString("projectFingerprint", "fingerprint")
            .HasInt32("processId", 1234);
        JsonAssert.For(lifecycleEntry.RootElement)
            .HasString("event", ContractLiteralCodec.ToValue(DaemonStartProgressEvent.LifecycleObserved))
            .HasInt32("sequence", 4);
        JsonAssert.For(lifecycleEntry.RootElement.GetProperty("payload"))
            .HasString("payloadKind", "lifecycleSnapshot")
            .HasString("lifecycleState", IpcEditorLifecycleStateCodec.Compiling)
            .HasString("blockingReason", IpcEditorBlockingReasonCodec.Compile)
            .HasBoolean("canAcceptExecutionRequests", false);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        JsonAssert.For(outputJson.RootElement)
            .HasString("command", UcliCommandNames.DaemonStart)
            .HasValueKind("payload", JsonValueKind.Object);
        Assert.False(outputJson.RootElement.TryGetProperty("event", out _));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(null)]
    [InlineData("text")]
    public async Task Start_WithTextFormat_WritesSingleLineProgressEntriesToStandardError (
        string? format)
    {
        var service = new StubDaemonStartService(
            DaemonStartExecutionResult.Success(CreateSuccessOutput(
                lifecycleState: IpcEditorLifecycleStateCodec.Compiling,
                blockingReason: IpcEditorBlockingReasonCodec.Compile,
                canAcceptExecutionRequests: false)),
            EmitSampleDaemonStartProgressAsync);
        var command = new DaemonStartCommand(service, CommandResultTestWriter.Create());

        CommandExecutionState.Reset();
        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.StartAsync(
            format: format,
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        Assert.Equal(1, service.CallCount);
        var lines = standardError.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Equal("daemon start workflow project=fingerprint timeoutMs=1234 started", lines[0]);
        Assert.Equal("daemon start workflow project=fingerprint timeoutMs=1234 result=succeeded startStatus=started daemonStatus=running completed", lines[1]);
        JsonGoldenFileAssert.Matches(CliOutputGoldenFiles.GetPath("daemon", "start-compiling-success.json"), standardOutput);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WithTextFormat_WritesSupervisorProgressPayloadsToStandardError ()
    {
        var service = new StubDaemonStartService(
            DaemonStartExecutionResult.Success(CreateSuccessOutput(
                lifecycleState: IpcEditorLifecycleStateCodec.Compiling,
                blockingReason: IpcEditorBlockingReasonCodec.Compile,
                canAcceptExecutionRequests: false)),
            EmitSampleSupervisorProgressAsync);
        var command = new DaemonStartCommand(service, CommandResultTestWriter.Create());

        CommandExecutionState.Reset();
        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.StartAsync(
            format: "text",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        var lines = standardError.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(4, lines.Length);
        Assert.Equal(
            "daemon start endpoint project=fingerprint timeoutMs=1234 editorMode=batchmode owner=cli canShutdownProcess=true pid=1234 launchAttempt=attempt-1 startupStatus=waitingForEndpoint startupPhase=endpointRegistration waiting",
            lines[0]);
        Assert.Equal(
            "daemon start blocker project=fingerprint timeoutMs=1234 editorMode=batchmode owner=cli canShutdownProcess=true pid=1234 launchAttempt=attempt-1 startupStatus=blocked startupBlockingReason=compile startupPhase=endpointRegistration retryDisposition=retryAfterFix errorCode=DAEMON_STARTUP_BLOCKED detected",
            lines[1]);
        Assert.Equal(
            "daemon start endpoint project=fingerprint timeoutMs=1234 editorMode=batchmode owner=cli canShutdownProcess=true pid=1234 launchAttempt=attempt-1 registered",
            lines[2]);
        Assert.Equal(
            "daemon start lifecycle project=fingerprint timeoutMs=1234 editorMode=batchmode lifecycleState=compiling blockingReason=compile canAcceptExecutionRequests=false observed",
            lines[3]);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        Assert.False(outputJson.RootElement.TryGetProperty("event", out _));
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
    public async Task Start_WhenFormatIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new StubDaemonStartService(DaemonStartExecutionResult.Success(CreateSuccessOutput()));
        var command = new DaemonStartCommand(service, CommandResultTestWriter.Create());

        CommandExecutionState.Reset();
        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.StartAsync(
            format: "yaml",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);
        Assert.Equal(string.Empty, standardError);
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
    public async Task Start_WhenOnStartupBlockedIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new StubDaemonStartService(DaemonStartExecutionResult.Success(CreateSuccessOutput()));
        var command = new DaemonStartCommand(service, CommandResultTestWriter.Create());

        CommandExecutionState.Reset();
        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.StartAsync(
            onStartupBlocked: "unsupported",
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
            StartupPhase: ContractLiteralCodec.ToValue(DaemonDiagnosisStartupPhase.EndpointRegistration),
            ActionRequired: DaemonDiagnosisActionRequiredValues.InspectUnityLog,
            PrimaryDiagnostic: new DaemonPrimaryDiagnosticOutput(
                Kind: DaemonDiagnosisPrimaryDiagnosticKindValues.Compiler,
                Code: "CS0103",
                File: "Assets/Foo.cs",
                Line: 12,
                Column: 34,
                Message: "The name 'MissingType' does not exist in the current context"));
        var service = new StubDaemonStartService(DaemonStartExecutionResult.Failure(
            ExecutionError.Timeout("registration timeout", ExecutionErrorCodes.IpcTimeout),
            DaemonStartFailureExecutionOutput.Create(
                DaemonStatusKind.NotRunning,
                1234,
                startup: null,
                diagnosis)));
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
    [Trait("Size", "Small")]
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
        var service = new StubDaemonStartService(DaemonStartExecutionResult.Failure(
            ExecutionError.InternalError("Unity startup is blocked.", DaemonErrorCodes.DaemonStartupBlocked),
            DaemonStartFailureExecutionOutput.Create(
                DaemonStatusKind.NotRunning,
                1234,
                startup,
                diagnosis)));
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
        JsonGoldenFileAssert.Matches(CliOutputGoldenFiles.GetPath("daemon", "start-startup-blocked.json"), standardOutput);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenEndpointRegistrationTimesOut_NormalizesFinalWaitThenRetryToUnknown ()
    {
        var startup = new DaemonStartupObservation(
            StartupStatus: ContractLiteralCodec.ToValue(DaemonStartupStatus.Timeout),
            StartupBlockingReason: ContractLiteralCodec.ToValue(DaemonStartupBlockingReason.EndpointNotRegistered),
            LaunchAttemptId: "20260312_040500Z_00abcdef",
            ProcessAction: ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Terminated),
            RetryDisposition: ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.WaitThenRetry));
        var service = new StubDaemonStartService(DaemonStartExecutionResult.Failure(
            ExecutionError.Timeout("endpoint registration timeout", ExecutionErrorCodes.IpcTimeout),
            DaemonStartFailureExecutionOutput.Create(
                DaemonStatusKind.NotRunning,
                1234,
                startup,
                diagnosis: null)));
        var command = new DaemonStartCommand(service, CommandResultTestWriter.Create());

        CommandExecutionState.Reset();
        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.StartAsync(
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.ToolError, exitCode);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
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
        JsonGoldenFileAssert.Matches(CliOutputGoldenFiles.GetPath("daemon", "start-endpoint-timeout.json"), standardOutput);
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
        var service = new StubDaemonStartService(DaemonStartExecutionResult.Failure(
            ExecutionError.InternalError("transient startup failure", DaemonErrorCodes.DaemonStartupBlocked),
            DaemonStartFailureExecutionOutput.Create(
                DaemonStatusKind.NotRunning,
                1234,
                startup,
                diagnosis: null)));
        var command = new DaemonStartCommand(service, CommandResultTestWriter.Create());

        CommandExecutionState.Reset();
        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.StartAsync(
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.ToolError, exitCode);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        JsonAssert.For(outputJson.RootElement.GetProperty("payload"))
            .HasString("retryDisposition", ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.RetryImmediately))
            .HasBoolean("safeToRetryImmediately", true)
            .HasProperty("startup", startupJson => startupJson
                .HasString("retryDisposition", ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.RetryImmediately)));
    }

    private static DaemonStartExecutionOutput CreateSuccessOutput (
        string lifecycleState = IpcEditorLifecycleStateCodec.Ready,
        string? blockingReason = null,
        bool canAcceptExecutionRequests = true)
    {
        return new DaemonStartExecutionOutput(
            StartStatus: DaemonStartStatus.Started,
            DaemonStatus: DaemonStatusKind.Running,
            TimeoutMilliseconds: 1234,
            Session: new DaemonSessionOutput(
                ProjectFingerprint: "fingerprint",
                IssuedAtUtc: new DateTimeOffset(2026, 03, 12, 1, 2, 3, TimeSpan.Zero),
                EditorMode: "batchmode",
                OwnerKind: "cli",
                CanShutdownProcess: true,
                EndpointTransportKind: "namedPipe",
                EndpointAddress: "ucli-daemon-endpoint",
                ProcessId: 1234,
                ProcessStartedAtUtc: new DateTimeOffset(2026, 03, 12, 1, 2, 0, TimeSpan.Zero),
                OwnerProcessId: 5678),
            LifecycleState: lifecycleState,
            BlockingReason: blockingReason,
            CanAcceptExecutionRequests: canAcceptExecutionRequests);
    }

    private static async ValueTask EmitSampleDaemonStartProgressAsync (
        ICommandProgressSink? progressSink,
        CancellationToken cancellationToken)
    {
        Assert.NotNull(progressSink);
        await progressSink!.OnEntryAsync(
                ContractLiteralCodec.ToValue(DaemonStartProgressEvent.Started),
                CreateProgressEntry(result: null, startStatus: null, daemonStatus: null, errorCode: null),
                cancellationToken)
            .ConfigureAwait(false);
        await progressSink.OnEntryAsync(
                ContractLiteralCodec.ToValue(DaemonStartProgressEvent.Completed),
                CreateProgressEntry(ContractLiteralCodec.ToValue(CommandProgressResult.Succeeded), "started", "running", errorCode: null),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static DaemonStartProgressEntry CreateProgressEntry (
        string? result,
        string? startStatus,
        string? daemonStatus,
        string? errorCode)
    {
        return new DaemonStartProgressEntry(
            ProjectFingerprint: "fingerprint",
            TimeoutMilliseconds: 1234,
            EditorMode: "batchmode",
            OnStartupBlocked: "auto",
            Result: result,
            StartStatus: startStatus,
            DaemonStatus: daemonStatus,
            ErrorCode: errorCode);
    }

    private static async ValueTask EmitSampleSupervisorProgressAsync (
        ICommandProgressSink? progressSink,
        CancellationToken cancellationToken)
    {
        Assert.NotNull(progressSink);
        await progressSink!.OnEntryAsync(
                ContractLiteralCodec.ToValue(DaemonStartProgressEvent.WaitingForEndpoint),
                new DaemonStartStartupObservationProgressEntry(
                    ContractLiteralCodec.ToValue(DaemonStartProgressPayloadKind.StartupObservation),
                    "fingerprint",
                    1234,
                    "batchmode",
                    "auto",
                    "attempt-1",
                    "cli",
                    true,
                    1234,
                    new DateTimeOffset(2026, 03, 12, 1, 2, 0, TimeSpan.Zero),
                    "waitingForEndpoint",
                    null,
                    "endpointRegistration",
                    null,
                    null,
                    null),
                cancellationToken)
            .ConfigureAwait(false);
        await progressSink.OnEntryAsync(
                ContractLiteralCodec.ToValue(DaemonStartProgressEvent.BlockerDetected),
                new DaemonStartStartupObservationProgressEntry(
                    ContractLiteralCodec.ToValue(DaemonStartProgressPayloadKind.StartupObservation),
                    "fingerprint",
                    1234,
                    "batchmode",
                    "auto",
                    "attempt-1",
                    "cli",
                    true,
                    1234,
                    new DateTimeOffset(2026, 03, 12, 1, 2, 0, TimeSpan.Zero),
                    ContractLiteralCodec.ToValue(DaemonStartupStatus.Blocked),
                    ContractLiteralCodec.ToValue(DaemonStartupBlockingReason.Compile),
                    "endpointRegistration",
                    ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.RetryAfterFix),
                    "Unity startup is blocked.",
                    DaemonErrorCodes.DaemonStartupBlocked.Value),
                cancellationToken)
            .ConfigureAwait(false);
        await progressSink.OnEntryAsync(
                ContractLiteralCodec.ToValue(DaemonStartProgressEvent.EndpointRegistered),
                new DaemonStartStartupObservationProgressEntry(
                    ContractLiteralCodec.ToValue(DaemonStartProgressPayloadKind.StartupObservation),
                    "fingerprint",
                    1234,
                    "batchmode",
                    "auto",
                    "attempt-1",
                    "cli",
                    true,
                    1234,
                    new DateTimeOffset(2026, 03, 12, 1, 2, 0, TimeSpan.Zero),
                    null,
                    null,
                    null,
                    null,
                    null,
                    null),
                cancellationToken)
            .ConfigureAwait(false);
        await progressSink.OnEntryAsync(
                ContractLiteralCodec.ToValue(DaemonStartProgressEvent.LifecycleObserved),
                new DaemonStartLifecycleSnapshotProgressEntry(
                    ContractLiteralCodec.ToValue(DaemonStartProgressPayloadKind.LifecycleSnapshot),
                    "fingerprint",
                    1234,
                    "batchmode",
                    "auto",
                    IpcEditorLifecycleStateCodec.Compiling,
                    IpcEditorBlockingReasonCodec.Compile,
                    CanAcceptExecutionRequests: false),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static DaemonDiagnosisOutput CreateDiagnosis (string reason)
    {
        return new DaemonDiagnosisOutput(
            Reason: reason,
            Message: "startup diagnosis",
            ReportedBy: DaemonDiagnosisReportedByValues.Cli,
            IsInferred: true,
            UpdatedAtUtc: new DateTimeOffset(2026, 03, 12, 4, 5, 6, TimeSpan.Zero),
            ProcessId: 1234,
            EditorInstancePath: "/repo/UnityProject/Library/EditorInstance.json");
    }

    private sealed class StubDaemonStartService : IDaemonStartService
    {
        private readonly DaemonStartExecutionResult result;

        private readonly Func<ICommandProgressSink?, CancellationToken, ValueTask>? progressHandler;

        public StubDaemonStartService (
            DaemonStartExecutionResult result,
            Func<ICommandProgressSink?, CancellationToken, ValueTask>? progressHandler = null)
        {
            this.result = result;
            this.progressHandler = progressHandler;
        }

        public int CallCount { get; private set; }

        public string? LastProjectPath { get; private set; }

        public int? LastTimeoutMilliseconds { get; private set; }

        public DaemonEditorMode? LastEditorMode { get; private set; }

        public DaemonStartupBlockedProcessPolicy LastOnStartupBlocked { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public ValueTask<DaemonStartExecutionResult> StartAsync (
            string? projectPath,
            int? timeoutMilliseconds,
            DaemonEditorMode? editorMode,
            DaemonStartupBlockedProcessPolicy onStartupBlocked,
            ICommandProgressSink? progressSink = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastProjectPath = projectPath;
            LastTimeoutMilliseconds = timeoutMilliseconds;
            LastEditorMode = editorMode;
            LastOnStartupBlocked = onStartupBlocked;
            LastCancellationToken = cancellationToken;
            if (progressHandler != null)
            {
                return StartCoreAsync(progressSink, cancellationToken);
            }

            return ValueTask.FromResult(result);
        }

        private async ValueTask<DaemonStartExecutionResult> StartCoreAsync (
            ICommandProgressSink? progressSink,
            CancellationToken cancellationToken)
        {
            await progressHandler!(progressSink, cancellationToken).ConfigureAwait(false);
            return result;
        }
    }
}
