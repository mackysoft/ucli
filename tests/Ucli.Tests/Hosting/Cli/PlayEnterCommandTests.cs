using System.Globalization;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Play.UseCases.Enter;
using MackySoft.Ucli.Application.Shared.CommandContracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Play;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class PlayEnterCommandTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Enter_MapsOptionsToServiceInputAndCancellationToken ()
    {
        var service = new StubPlayEnterService((_, _) => ValueTask.FromResult(PlayEnterExecutionResult.Success(CreateOutput())));
        var command = new PlayEnterCommand(service, CommandResultTestWriter.Create());
        using var cancellationTokenSource = new CancellationTokenSource();

        await StandardOutputCapture.ExecuteAsync(() => command.EnterAsync(
            projectPath: "/repo/UnityProject",
            timeout: "1234",
            cancellationToken: cancellationTokenSource.Token));

        Assert.Equal(cancellationTokenSource.Token, service.CapturedCancellationToken);
        var input = Assert.IsType<PlayEnterCommandInput>(service.CapturedInput);
        Assert.Equal("/repo/UnityProject", input.ProjectPath);
        Assert.Equal(1234, input.TimeoutMilliseconds);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Enter_WhenTimeoutIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new StubPlayEnterService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new PlayEnterCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.EnterAsync(
            timeout: "abc",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);
        Assert.Null(service.CapturedInput);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.PlayEnter,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Enter_WhenServiceSucceeds_EmitsPlayEnterEnvelopeAndPayloadWithoutOpResults ()
    {
        var service = new StubPlayEnterService((_, _) => ValueTask.FromResult(PlayEnterExecutionResult.Success(CreateOutput())));
        var command = new PlayEnterCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.EnterAsync(
            timeout: "1000",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.PlayEnter,
            IpcProtocol.StatusOk,
            (int)CliExitCode.Success);
        JsonAssert.For(outputJson.RootElement.GetProperty("payload"))
            .HasProperty("project", project => project
                .HasString("projectPath", "/repo/UnityProject")
                .HasString("projectFingerprint", "project-fingerprint")
                .HasString("unityVersion", "6000.1.4f1"))
            .HasString("daemonStatus", "running")
            .HasString("editorMode", "gui")
            .HasString("lifecycleState", IpcEditorLifecycleStateCodec.Playmode)
            .HasString("blockingReason", IpcEditorBlockingReasonCodec.PlayMode)
            .HasBoolean("canAcceptExecutionRequests", false)
            .HasProperty("playMode", playMode => playMode
                .HasString("state", IpcPlayModeStateNames.Playing)
                .HasString("transition", IpcPlayModeTransitionNames.None)
                .HasBoolean("isPlaying", true)
                .HasBoolean("isPlayingOrWillChangePlaymode", true)
                .HasString("generation", "3"))
            .HasProperty("transition", transition => transition
                .HasString("transition", IpcPlayTransitionCommandNames.Enter)
                .HasString("result", IpcPlayTransitionResultNames.Entered)
                .HasProperty("before", _ => { })
                .HasProperty("after", _ => { }))
            .HasInt32("timeoutMilliseconds", 1000);
        Assert.False(outputJson.RootElement.GetProperty("payload").TryGetProperty("opResults", out _));
        Assert.DoesNotContain("\"touched\"", standardOutput, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Enter_WhenServiceReturnsTransitionFailure_EmitsErrorEnvelopeWithTransitionPayload ()
    {
        var output = CreateOutput(IpcPlayTransitionResultNames.Timeout, includeAfter: false);
        var failure = ApplicationFailure.Timeout(
            "Unity Play Mode enter timed out after 1000 milliseconds.",
            PlayModeErrorCodes.PlayModeTransitionTimeout);
        var service = new StubPlayEnterService((_, _) => ValueTask.FromResult(PlayEnterExecutionResult.Failure(failure, output)));
        var command = new PlayEnterCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.EnterAsync(
            timeout: "1000",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.ToolError, exitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.PlayEnter,
            IpcProtocol.StatusError,
            (int)CliExitCode.ToolError);
        CommandResultAssert.HasSingleError(outputJson.RootElement, PlayModeErrorCodes.PlayModeTransitionTimeout);
        JsonAssert.For(outputJson.RootElement.GetProperty("payload").GetProperty("transition"))
            .HasString("result", IpcPlayTransitionResultNames.Timeout)
            .HasString("applicationState", IpcPlayApplicationStateNames.Indeterminate)
            .HasProperty("observed", _ => { });
        Assert.False(outputJson.RootElement.GetProperty("payload").GetProperty("transition").TryGetProperty("after", out _));
    }

    private static PlayEnterExecutionOutput CreateOutput (
        string result = IpcPlayTransitionResultNames.Entered,
        bool includeAfter = true)
    {
        var before = CreateSnapshot(
            IpcEditorLifecycleStateCodec.Ready,
            null,
            true,
            CreatePlayMode(IpcPlayModeStateNames.Stopped, IpcPlayModeTransitionNames.None, false, false, "2"));
        var current = CreateSnapshot(
            IpcEditorLifecycleStateCodec.Playmode,
            IpcEditorBlockingReasonCodec.PlayMode,
            false,
            CreatePlayMode(IpcPlayModeStateNames.Playing, IpcPlayModeTransitionNames.None, true, true, "3"));
        var transition = new IpcPlayTransitionResult(
            IpcPlayTransitionCommandNames.Enter,
            result,
            before);
        if (includeAfter)
        {
            transition = transition with
            {
                After = current,
            };
        }
        else
        {
            transition = transition with
            {
                Observed = current,
                ApplicationState = IpcPlayApplicationStateNames.Indeterminate,
            };
        }

        return new PlayEnterExecutionOutput(
            Project: new ProjectIdentityInfo(
                ProjectPath: "/repo/UnityProject",
                ProjectFingerprint: "project-fingerprint",
                UnityVersion: "6000.1.4f1"),
            DaemonStatus: DaemonStatusKind.Running,
            ServerVersion: "0.5.0",
            EditorMode: "gui",
            LifecycleState: IpcEditorLifecycleStateCodec.Playmode,
            BlockingReason: IpcEditorBlockingReasonCodec.PlayMode,
            CompileState: "ready",
            CompileGeneration: "12",
            DomainReloadGeneration: "7",
            CanAcceptExecutionRequests: false,
            ObservedAtUtc: DateTimeOffset.Parse("2026-05-21T00:00:00+00:00", CultureInfo.InvariantCulture),
            ActionRequired: null,
            PrimaryDiagnostic: null,
            PlayMode: new PlayModeSnapshotOutput(
                State: IpcPlayModeStateNames.Playing,
                Transition: IpcPlayModeTransitionNames.None,
                IsPlaying: true,
                IsPlayingOrWillChangePlaymode: true,
                Generation: "3"),
            Transition: transition,
            TimeoutMilliseconds: 1000);
    }

    private static IpcPlayLifecycleSnapshot CreateSnapshot (
        string lifecycleState,
        string? blockingReason,
        bool canAcceptExecutionRequests,
        IpcPlayModeSnapshot playMode)
    {
        return new IpcPlayLifecycleSnapshot(
            ServerVersion: "0.5.0",
            EditorMode: DaemonEditorModeValues.Gui,
            UnityVersion: "6000.1.4f1",
            ProjectFingerprint: "project-fingerprint",
            LifecycleState: lifecycleState,
            BlockingReason: blockingReason,
            CompileState: "ready",
            CompileGeneration: "12",
            DomainReloadGeneration: "7",
            CanAcceptExecutionRequests: canAcceptExecutionRequests,
            ObservedAtUtc: DateTimeOffset.Parse("2026-05-21T00:00:00+00:00", CultureInfo.InvariantCulture),
            ActionRequired: null,
            PrimaryDiagnostic: null,
            PlayMode: playMode);
    }

    private static IpcPlayModeSnapshot CreatePlayMode (
        string state,
        string transition,
        bool isPlaying,
        bool isPlayingOrWillChangePlaymode,
        string generation)
    {
        return new IpcPlayModeSnapshot(
            State: state,
            Transition: transition,
            IsPlaying: isPlaying,
            IsPlayingOrWillChangePlaymode: isPlayingOrWillChangePlaymode,
            Generation: generation);
    }

    private sealed class StubPlayEnterService : IPlayEnterService
    {
        private readonly Func<PlayEnterCommandInput, CancellationToken, ValueTask<PlayEnterExecutionResult>> handler;

        public StubPlayEnterService (Func<PlayEnterCommandInput, CancellationToken, ValueTask<PlayEnterExecutionResult>> handler)
        {
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public PlayEnterCommandInput? CapturedInput { get; private set; }

        public CancellationToken CapturedCancellationToken { get; private set; }

        public ValueTask<PlayEnterExecutionResult> ExecuteAsync (
            PlayEnterCommandInput input,
            CancellationToken cancellationToken = default)
        {
            CapturedInput = input;
            CapturedCancellationToken = cancellationToken;
            return handler(input, cancellationToken);
        }
    }
}
