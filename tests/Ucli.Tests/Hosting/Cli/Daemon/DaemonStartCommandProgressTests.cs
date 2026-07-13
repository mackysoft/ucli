using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Daemon;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.DaemonStartCommandTestSupport;

namespace MackySoft.Ucli.Tests;

public sealed class DaemonStartCommandProgressTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Start_WithJsonFormat_WritesProgressEntriesToStandardErrorAndFinalCommandResultToStandardOutput ()
    {
        var expectedProjectFingerprint = ProjectFingerprintTestFactory.Create("fingerprint").ToString();
        var service = new RecordingDaemonStartService(
            DaemonStartExecutionResult.Success(CreateSuccessOutput(
                lifecycleState: IpcEditorLifecycleStateCodec.Compiling,
                blockingReason: IpcEditorBlockingReasonCodec.Compile,
                canAcceptExecutionRequests: false)),
            EmitSampleDaemonStartProgressAsync);
        var command = new DaemonStartCommand(service, CommandResultTestWriter.Create());

        CommandExecutionState.Reset();
        var result = await CommandResultCapture.ExecuteWithErrorAsync(() => command.StartAsync(
            format: "json",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        var lines = result.StdErr.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        using var startedEntry = JsonDocument.Parse(lines[0]);
        using var completedEntry = JsonDocument.Parse(lines[1]);
        JsonAssert.For(startedEntry.RootElement)
            .HasString("command", UcliCommandNames.DaemonStart)
            .HasString("event", ContractLiteralCodec.ToValue(DaemonStartProgressEvent.Started))
            .HasInt32("sequence", 1);
        JsonAssert.For(startedEntry.RootElement.GetProperty("payload"))
            .HasString("projectFingerprint", expectedProjectFingerprint)
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
        JsonGoldenFileAssert.Matches(CliOutputGoldenFiles.GetPath("daemon", "start-compiling-success.json"), result.StdOut);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WithJsonFormat_WritesSupervisorProgressPayloadsToStandardErrorOnly ()
    {
        var expectedProjectFingerprint = ProjectFingerprintTestFactory.Create("fingerprint").ToString();
        var service = new RecordingDaemonStartService(
            DaemonStartExecutionResult.Success(CreateSuccessOutput(
                lifecycleState: IpcEditorLifecycleStateCodec.Compiling,
                blockingReason: IpcEditorBlockingReasonCodec.Compile,
                canAcceptExecutionRequests: false)),
            EmitSampleSupervisorProgressAsync);
        var command = new DaemonStartCommand(service, CommandResultTestWriter.Create());

        CommandExecutionState.Reset();
        var result = await CommandResultCapture.ExecuteWithErrorAsync(() => command.StartAsync(
            format: "json",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        var lines = result.StdErr.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
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
            .HasString("projectFingerprint", expectedProjectFingerprint)
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
            .HasString("projectFingerprint", expectedProjectFingerprint)
            .HasInt32("processId", 1234);
        JsonAssert.For(lifecycleEntry.RootElement)
            .HasString("event", ContractLiteralCodec.ToValue(DaemonStartProgressEvent.LifecycleObserved))
            .HasInt32("sequence", 4);
        JsonAssert.For(lifecycleEntry.RootElement.GetProperty("payload"))
            .HasString("payloadKind", "lifecycleSnapshot")
            .HasString("lifecycleState", IpcEditorLifecycleStateCodec.Compiling)
            .HasString("blockingReason", IpcEditorBlockingReasonCodec.Compile)
            .HasBoolean("canAcceptExecutionRequests", false);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        JsonAssert.For(outputJson.RootElement)
            .HasString("command", UcliCommandNames.DaemonStart)
            .HasValueKind("payload", JsonValueKind.Object);
        Assert.False(outputJson.RootElement.TryGetProperty("event", out _));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Start_WithDefaultOrTextFormat_WritesSingleLineProgressEntriesToStandardError ()
    {
        var expectedProjectFingerprint = ProjectFingerprintTestFactory.Create("fingerprint").ToString();
        foreach (var format in new string?[] { null, "text" })
        {
            var service = new RecordingDaemonStartService(
                DaemonStartExecutionResult.Success(CreateSuccessOutput(
                    lifecycleState: IpcEditorLifecycleStateCodec.Compiling,
                    blockingReason: IpcEditorBlockingReasonCodec.Compile,
                    canAcceptExecutionRequests: false)),
                EmitSampleDaemonStartProgressAsync);
            var command = new DaemonStartCommand(service, CommandResultTestWriter.Create());

            CommandExecutionState.Reset();
            var result = await CommandResultCapture.ExecuteWithErrorAsync(() => command.StartAsync(
                format: format,
                cancellationToken: CancellationToken.None));

            Assert.Equal((int)CliExitCode.Success, result.ExitCode);
            var lines = result.StdErr.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(2, lines.Length);
            Assert.Equal($"daemon start workflow project={expectedProjectFingerprint} timeoutMs=1234 started", lines[0]);
            Assert.Equal($"daemon start workflow project={expectedProjectFingerprint} timeoutMs=1234 result=succeeded startStatus=started daemonStatus=running completed", lines[1]);
            JsonGoldenFileAssert.Matches(CliOutputGoldenFiles.GetPath("daemon", "start-compiling-success.json"), result.StdOut);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WithTextFormat_WhenProgressEventHasUnknownStartedSuffix_RendersStartedStatus ()
    {
        var expectedProjectFingerprint = ProjectFingerprintTestFactory.Create("fingerprint").ToString();
        var service = new RecordingDaemonStartService(
            DaemonStartExecutionResult.Success(CreateSuccessOutput()),
            EmitUnknownStartedDaemonStartProgressAsync);
        var command = new DaemonStartCommand(service, CommandResultTestWriter.Create());

        CommandExecutionState.Reset();
        var result = await CommandResultCapture.ExecuteWithErrorAsync(() => command.StartAsync(
            format: "text",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        var line = Assert.Single(result.StdErr.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        Assert.Equal($"daemon start daemon.start.future.started project={expectedProjectFingerprint} timeoutMs=1234 started", line);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WithTextFormat_WritesSupervisorProgressPayloadsToStandardError ()
    {
        var expectedProjectFingerprint = ProjectFingerprintTestFactory.Create("fingerprint").ToString();
        var service = new RecordingDaemonStartService(
            DaemonStartExecutionResult.Success(CreateSuccessOutput(
                lifecycleState: IpcEditorLifecycleStateCodec.Compiling,
                blockingReason: IpcEditorBlockingReasonCodec.Compile,
                canAcceptExecutionRequests: false)),
            EmitSampleSupervisorProgressAsync);
        var command = new DaemonStartCommand(service, CommandResultTestWriter.Create());

        CommandExecutionState.Reset();
        var result = await CommandResultCapture.ExecuteWithErrorAsync(() => command.StartAsync(
            format: "text",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        var lines = result.StdErr.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(4, lines.Length);
        Assert.Equal(
            $"daemon start endpoint project={expectedProjectFingerprint} timeoutMs=1234 editorMode=batchmode owner=cli canShutdownProcess=true pid=1234 launchAttempt=attempt-1 startupStatus=waitingForEndpoint startupPhase=endpointRegistration waiting",
            lines[0]);
        Assert.Equal(
            $"daemon start blocker project={expectedProjectFingerprint} timeoutMs=1234 editorMode=batchmode owner=cli canShutdownProcess=true pid=1234 launchAttempt=attempt-1 startupStatus=blocked startupBlockingReason=compile startupPhase=endpointRegistration retryDisposition=retryAfterFix errorCode=DAEMON_STARTUP_BLOCKED detected",
            lines[1]);
        Assert.Equal(
            $"daemon start endpoint project={expectedProjectFingerprint} timeoutMs=1234 editorMode=batchmode owner=cli canShutdownProcess=true pid=1234 launchAttempt=attempt-1 registered",
            lines[2]);
        Assert.Equal(
            $"daemon start lifecycle project={expectedProjectFingerprint} timeoutMs=1234 editorMode=batchmode lifecycleState=compiling blockingReason=compile canAcceptExecutionRequests=false observed",
            lines[3]);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.False(outputJson.RootElement.TryGetProperty("event", out _));
    }
}
