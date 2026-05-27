using System.Globalization;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Play.UseCases.Status;
using MackySoft.Ucli.Application.Shared.CommandContracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Play;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class PlayStatusCommandTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Status_MapsOptionsToServiceInputAndCancellationToken ()
    {
        var service = new StubPlayStatusService((_, _) => ValueTask.FromResult(PlayStatusExecutionResult.Success(CreateOutput())));
        var command = new PlayStatusCommand(service, CommandResultTestWriter.Create());
        using var cancellationTokenSource = new CancellationTokenSource();

        await StandardOutputCapture.ExecuteAsync(() => command.StatusAsync(
            projectPath: "/repo/UnityProject",
            timeout: "1234",
            cancellationToken: cancellationTokenSource.Token));

        Assert.Equal(cancellationTokenSource.Token, service.CapturedCancellationToken);
        var input = Assert.IsType<PlayStatusCommandInput>(service.CapturedInput);
        Assert.Equal("/repo/UnityProject", input.ProjectPath);
        Assert.Equal(1234, input.TimeoutMilliseconds);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Status_WhenTimeoutIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new StubPlayStatusService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new PlayStatusCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.StatusAsync(
            timeout: "abc",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);
        Assert.Null(service.CapturedInput);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.PlayStatus,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Status_WhenServiceSucceeds_EmitsPlayStatusEnvelopeAndFlatPayload ()
    {
        var service = new StubPlayStatusService((_, _) => ValueTask.FromResult(PlayStatusExecutionResult.Success(CreateOutput())));
        var command = new PlayStatusCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.StatusAsync(
            timeout: "1000",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.PlayStatus,
            IpcProtocol.StatusOk,
            (int)CliExitCode.Success);
        JsonAssert.For(outputJson.RootElement.GetProperty("payload"))
            .HasProperty("project", project => project
                .HasString("projectPath", "/repo/UnityProject")
                .HasString("projectFingerprint", "project-fingerprint")
                .HasString("unityVersion", "6000.1.4f1"))
            .HasString("daemonStatus", "running")
            .HasString("serverVersion", "0.5.0")
            .HasString("editorMode", "gui")
            .HasString("lifecycleState", "ready")
            .IsNull("blockingReason")
            .HasString("compileState", "ready")
            .HasString("compileGeneration", "12")
            .HasString("domainReloadGeneration", "7")
            .HasBoolean("canAcceptExecutionRequests", true)
            .HasProperty("playMode", playMode => playMode
                .HasString("state", "stopped")
                .HasString("transition", "none")
                .HasBoolean("isPlaying", false)
                .HasBoolean("isPlayingOrWillChangePlaymode", false)
                .HasString("generation", "2"))
            .HasInt32("timeoutMilliseconds", 1000);
    }

    private static PlayStatusExecutionOutput CreateOutput ()
    {
        return new PlayStatusExecutionOutput(
            Project: new ProjectIdentityInfo(
                ProjectPath: "/repo/UnityProject",
                ProjectFingerprint: "project-fingerprint",
                UnityVersion: "6000.1.4f1"),
            DaemonStatus: DaemonStatusKind.Running,
            ServerVersion: "0.5.0",
            EditorMode: "gui",
            LifecycleState: "ready",
            BlockingReason: null,
            CompileState: "ready",
            CompileGeneration: "12",
            DomainReloadGeneration: "7",
            CanAcceptExecutionRequests: true,
            ObservedAtUtc: DateTimeOffset.Parse("2026-05-21T00:00:00+00:00", CultureInfo.InvariantCulture),
            ActionRequired: null,
            PrimaryDiagnostic: null,
            PlayMode: new PlayModeSnapshotOutput(
                State: "stopped",
                Transition: "none",
                IsPlaying: false,
                IsPlayingOrWillChangePlaymode: false,
                Generation: "2"),
            TimeoutMilliseconds: 1000);
    }

    private sealed class StubPlayStatusService : IPlayStatusService
    {
        private readonly Func<PlayStatusCommandInput, CancellationToken, ValueTask<PlayStatusExecutionResult>> handler;

        public StubPlayStatusService (Func<PlayStatusCommandInput, CancellationToken, ValueTask<PlayStatusExecutionResult>> handler)
        {
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public PlayStatusCommandInput? CapturedInput { get; private set; }

        public CancellationToken CapturedCancellationToken { get; private set; }

        public ValueTask<PlayStatusExecutionResult> ExecuteAsync (
            PlayStatusCommandInput input,
            CancellationToken cancellationToken = default)
        {
            CapturedInput = input;
            CapturedCancellationToken = cancellationToken;
            return handler(input, cancellationToken);
        }
    }
}
