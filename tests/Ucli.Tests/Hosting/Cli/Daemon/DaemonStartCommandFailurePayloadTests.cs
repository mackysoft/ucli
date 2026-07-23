using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Application.Shared.Foundation;
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
            Reason: DaemonDiagnosisReason.GuiEndpointNotRegistered,
            Message: "GUI endpoint was not registered.",
            ReportedBy: DaemonDiagnosisReportedBy.Cli,
            IsInferred: true,
            UpdatedAtUtc: new DateTimeOffset(2026, 03, 12, 4, 5, 6, TimeSpan.Zero),
            ProcessId: 1234,
            EditorInstancePath: "/repo/UnityProject/Library/EditorInstance.json",
            ProcessStartedAtUtc: new DateTimeOffset(2026, 03, 12, 4, 5, 0, TimeSpan.Zero),
            UnityLogPath: "/repo/.ucli/local/projects/04hkaps9lf6uu0938ljojaudts0i6hb7h6lsrro14d2mf2dbpnng/unity.log",
            StartupPhase: DaemonDiagnosisStartupPhase.EndpointRegistration,
            ActionRequired: DaemonDiagnosisActionRequired.InspectUnityLog,
            PrimaryDiagnostic: new DaemonPrimaryDiagnosticOutput(
                Kind: DaemonDiagnosisPrimaryDiagnosticKind.Compiler,
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
        var command = new DaemonStartCommand(service, CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);

        CommandExecutionState.Reset();
        var result = await CommandResultCapture.ExecuteAsync(() => command.StartAsync(
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.DaemonStart,
            ContractLiteralCodec.ToValue(CommandResultStatus.Error),
            (int)CliExitCode.ToolError);
        CommandResultAssert.HasSingleError(outputJson.RootElement, ExecutionErrorCodes.IpcTimeout);
        var payload = outputJson.RootElement.GetProperty("payload");
        var diagnosisJson = payload.GetProperty("diagnosis");
        Assert.Equal(
            ContractLiteralCodec.ToValue(DaemonDiagnosisReason.GuiEndpointNotRegistered),
            diagnosisJson.GetProperty("reason").GetString());
        Assert.Equal("/repo/UnityProject/Library/EditorInstance.json", diagnosisJson.GetProperty("editorInstancePath").GetString());
        Assert.Equal("2026-03-12T04:05:00+00:00", diagnosisJson.GetProperty("processStartedAtUtc").GetString());
        Assert.Equal(
            "/repo/.ucli/local/projects/04hkaps9lf6uu0938ljojaudts0i6hb7h6lsrro14d2mf2dbpnng/unity.log",
            diagnosisJson.GetProperty("unityLogPath").GetString());
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonDiagnosisStartupPhase.EndpointRegistration), diagnosisJson.GetProperty("startupPhase").GetString());
        Assert.Equal(
            ContractLiteralCodec.ToValue(DaemonDiagnosisActionRequired.InspectUnityLog),
            diagnosisJson.GetProperty("actionRequired").GetString());
        Assert.True(diagnosisJson.GetProperty("isInferred").GetBoolean());
        var primaryDiagnosticJson = diagnosisJson.GetProperty("primaryDiagnostic");
        Assert.Equal(
            ContractLiteralCodec.ToValue(DaemonDiagnosisPrimaryDiagnosticKind.Compiler),
            primaryDiagnosticJson.GetProperty("kind").GetString());
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
        var diagnosis = CreateDiagnosis(DaemonDiagnosisReason.UnityScriptCompilationFailed);
        var artifactPath = AbsolutePath.Parse(Path.Combine(
            ProjectPathTestValues.RepositoryRoot,
            ".ucli",
            "local",
            "projects",
            "04hkaps9lf6uu0938ljojaudts0i6hb7h6lsrro14d2mf2dbpnng",
            "launch-attempts",
            "04hkaps9lf6uu0938ljojaudts",
            "startup-diagnosis.json"));
        var startup = new DaemonStartupObservation(
            StartupStatus: DaemonStartupStatus.Blocked,
            StartupBlockingReason: DaemonStartupBlockingReason.Compile,
            LaunchAttemptId: Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"),
            ProcessAction: DaemonStartupProcessAction.Kept,
            RetryDisposition: DaemonStartupRetryDisposition.RetryAfterFix,
            EditorMode: DaemonEditorMode.Batchmode,
            OwnerKind: DaemonSessionOwnerKind.Cli,
            CanShutdownProcess: true,
            ProcessId: 4321,
            StartedAtUtc: new DateTimeOffset(2026, 03, 12, 4, 5, 1, TimeSpan.Zero),
            ElapsedMilliseconds: 2500,
            ArtifactPath: artifactPath);
        var service = new RecordingDaemonStartService(DaemonStartExecutionResult.Failure(
            ExecutionError.InternalError("Unity startup is blocked.", DaemonErrorCodes.DaemonStartupBlocked),
            DaemonStartFailureExecutionOutput.Create(
                DaemonStatusKind.NotRunning,
                1234,
                startup,
                diagnosis)));
        var command = new DaemonStartCommand(service, CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);

        CommandExecutionState.Reset();
        var result = await CommandResultCapture.ExecuteAsync(() => command.StartAsync(
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.DaemonStart,
            ContractLiteralCodec.ToValue(CommandResultStatus.Error),
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
                .HasString("launchAttemptId", "01234567-89ab-cdef-0123-456789abcdef")
                .HasString("editorMode", "batchmode")
                .HasString("ownerKind", "cli")
                .HasBoolean("canShutdownProcess", true)
                .HasInt32("processId", 4321)
                .HasString("startedAtUtc", "2026-03-12T04:05:01+00:00")
                .HasInt32("elapsedMilliseconds", 2500)
                .HasString("processAction", ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Kept))
                .IsNull("processTermination")
                .HasString("artifactPath", artifactPath.Value)
                .HasString("retryDisposition", ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.RetryAfterFix)))
            .HasProperty("diagnosis", diagnosisJson => diagnosisJson
                .HasString("reason", ContractLiteralCodec.ToValue(DaemonDiagnosisReason.UnityScriptCompilationFailed)));
        Assert.False(payload.TryGetProperty("lifecycleState", out _));
        Assert.False(payload.TryGetProperty("blockingReason", out _));
        Assert.False(payload.TryGetProperty("canAcceptExecutionRequests", out _));
        Assert.False(payload.TryGetProperty("runtimeKind", out _));
        Assert.False(payload.GetProperty("startup").TryGetProperty("runtimeKind", out _));
        if (!OperatingSystem.IsWindows())
        {
            JsonGoldenFileAssert.Matches(CliOutputGoldenFiles.GetPath("daemon", "start-startup-blocked.json"), result.StdOut);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Start_WhenEndpointRegistrationTimesOut_NormalizesFinalWaitThenRetryToUnknown ()
    {
        var startup = new DaemonStartupObservation(
            StartupStatus: DaemonStartupStatus.Timeout,
            StartupBlockingReason: DaemonStartupBlockingReason.EndpointNotRegistered,
            LaunchAttemptId: Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"),
            ProcessAction: DaemonStartupProcessAction.Terminated,
            RetryDisposition: DaemonStartupRetryDisposition.WaitThenRetry,
            EditorMode: null,
            OwnerKind: null,
            CanShutdownProcess: null,
            ProcessId: null,
            StartedAtUtc: null,
            ElapsedMilliseconds: null,
            ArtifactPath: null);
        var service = new RecordingDaemonStartService(DaemonStartExecutionResult.Failure(
            ExecutionError.Timeout("endpoint registration timeout", ExecutionErrorCodes.IpcTimeout),
            DaemonStartFailureExecutionOutput.Create(
                DaemonStatusKind.NotRunning,
                1234,
                startup,
                diagnosis: null)));
        var command = new DaemonStartCommand(service, CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);

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
            StartupStatus: DaemonStartupStatus.Failed,
            StartupBlockingReason: DaemonStartupBlockingReason.Unknown,
            LaunchAttemptId: Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"),
            ProcessAction: DaemonStartupProcessAction.None,
            RetryDisposition: DaemonStartupRetryDisposition.RetryImmediately,
            EditorMode: null,
            OwnerKind: null,
            CanShutdownProcess: null,
            ProcessId: null,
            StartedAtUtc: null,
            ElapsedMilliseconds: null,
            ArtifactPath: null);
        var service = new RecordingDaemonStartService(DaemonStartExecutionResult.Failure(
            ExecutionError.InternalError("transient startup failure", DaemonErrorCodes.DaemonStartupBlocked),
            DaemonStartFailureExecutionOutput.Create(
                DaemonStatusKind.NotRunning,
                1234,
                startup,
                diagnosis: null)));
        var command = new DaemonStartCommand(service, CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);

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
