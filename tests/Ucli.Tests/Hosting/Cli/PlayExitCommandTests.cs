using System.Globalization;
using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Play.Common.Contracts;
using MackySoft.Ucli.Application.Features.Play.UseCases.Exit;
using MackySoft.Ucli.Application.Shared.CommandContracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Play;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class PlayExitCommandTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Exit_MapsOptionsToServiceInputAndCancellationToken ()
    {
        var service = new StubPlayExitService((_, _) => ValueTask.FromResult(PlayExitExecutionResult.Success(CreateOutput())));
        var command = new PlayExitCommand(service, CommandResultTestWriter.Create());
        using var cancellationTokenSource = new CancellationTokenSource();

        await StandardOutputCapture.ExecuteAsync(() => command.ExitAsync(
            projectPath: "/repo/UnityProject",
            timeout: "1234",
            cancellationToken: cancellationTokenSource.Token));

        Assert.Equal(cancellationTokenSource.Token, service.CapturedCancellationToken);
        var input = Assert.IsType<PlayExitCommandInput>(service.CapturedInput);
        Assert.Equal("/repo/UnityProject", input.ProjectPath);
        Assert.Equal(1234, input.TimeoutMilliseconds);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Exit_WhenTimeoutIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new StubPlayExitService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new PlayExitCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.ExitAsync(
            timeout: "abc",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);
        Assert.Null(service.CapturedInput);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.PlayExit,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Exit_WhenServiceSucceeds_EmitsPlayExitEnvelopeAndPayloadWithoutOpResults ()
    {
        var service = new StubPlayExitService((_, _) => ValueTask.FromResult(PlayExitExecutionResult.Success(CreateOutput())));
        var command = new PlayExitCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.ExitAsync(
            timeout: "1000",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.PlayExit,
            IpcProtocol.StatusOk,
            (int)CliExitCode.Success);
        JsonAssert.For(outputJson.RootElement.GetProperty("payload"))
            .HasProperty("project", project => project
                .HasString("projectPath", "/repo/UnityProject")
                .HasString("projectFingerprint", "project-fingerprint")
                .HasString("unityVersion", "6000.1.4f1"))
            .HasString("daemonStatus", "running")
            .HasString("editorMode", "gui")
            .HasString("lifecycleState", IpcEditorLifecycleStateCodec.Ready)
            .HasValueKind("blockingReason", JsonValueKind.Null)
            .HasBoolean("canAcceptExecutionRequests", true)
            .HasProperty("playMode", playMode => playMode
                .HasString("state", IpcPlayModeStateNames.Stopped)
                .HasString("transition", IpcPlayModeTransitionNames.None)
                .HasBoolean("isPlaying", false)
                .HasBoolean("isPlayingOrWillChangePlaymode", false)
                .HasString("generation", "3"))
            .HasProperty("transition", transition => transition
                .HasString("transition", IpcPlayTransitionCommandNames.Exit)
                .HasString("result", IpcPlayTransitionResultNames.Exited)
                .HasProperty("before", _ => { })
                .HasProperty("after", _ => { }))
            .HasInt32("timeoutMilliseconds", 1000);
        var transitionPayload = outputJson.RootElement.GetProperty("payload").GetProperty("transition");
        Assert.False(transitionPayload.TryGetProperty("observed", out _));
        Assert.False(transitionPayload.TryGetProperty("applicationState", out _));
        Assert.False(transitionPayload.TryGetProperty("until", out _));
        Assert.False(outputJson.RootElement.GetProperty("payload").TryGetProperty("opResults", out _));
        Assert.DoesNotContain("\"touched\"", standardOutput, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Exit_WhenServiceReturnsTransitionFailure_EmitsErrorEnvelopeWithObservedTransitionPayload ()
    {
        var output = CreateOutput(IpcPlayTransitionResultNames.Timeout, includeAfter: false);
        var failure = ApplicationFailure.Timeout(
            "Unity Play Mode exit timed out after 1000 milliseconds.",
            PlayModeErrorCodes.PlayModeTransitionTimeout);
        var service = new StubPlayExitService((_, _) => ValueTask.FromResult(PlayExitExecutionResult.Failure(failure, output)));
        var command = new PlayExitCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.ExitAsync(
            timeout: "1000",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.ToolError, exitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.PlayExit,
            IpcProtocol.StatusError,
            (int)CliExitCode.ToolError);
        CommandResultAssert.HasSingleError(outputJson.RootElement, PlayModeErrorCodes.PlayModeTransitionTimeout);
        JsonAssert.For(outputJson.RootElement.GetProperty("payload").GetProperty("transition"))
            .HasString("result", IpcPlayTransitionResultNames.Timeout)
            .HasString("applicationState", IpcPlayApplicationStateNames.Indeterminate)
            .HasProperty("observed", _ => { });
        Assert.False(outputJson.RootElement.GetProperty("payload").GetProperty("transition").TryGetProperty("after", out _));
        Assert.False(outputJson.RootElement.GetProperty("payload").TryGetProperty("opResults", out _));
        Assert.DoesNotContain("\"touched\"", standardOutput, StringComparison.Ordinal);
    }

    private static PlayExitExecutionOutput CreateOutput (
        string result = IpcPlayTransitionResultNames.Exited,
        bool includeAfter = true,
        string applicationState = IpcPlayApplicationStateNames.Indeterminate)
    {
        var before = CreateSnapshot(
            IpcEditorLifecycleStateCodec.Playmode,
            IpcEditorBlockingReasonCodec.PlayMode,
            false,
            CreatePlayMode(IpcPlayModeStateNames.Playing, IpcPlayModeTransitionNames.None, true, true, "2"));
        var current = CreateSnapshot(
            IpcEditorLifecycleStateCodec.Ready,
            null,
            true,
            CreatePlayMode(IpcPlayModeStateNames.Stopped, IpcPlayModeTransitionNames.None, false, false, "3"));
        var transition = new PlayExitTransitionOutput(
            Transition: IpcPlayTransitionCommandNames.Exit,
            Result: result,
            Before: CreateSnapshotOutput(before),
            After: null,
            Observed: null,
            ApplicationState: null);
        if (includeAfter)
        {
            transition = transition with
            {
                After = CreateSnapshotOutput(current),
            };
        }
        else
        {
            transition = transition with
            {
                Observed = CreateSnapshotOutput(current),
                ApplicationState = applicationState,
            };
        }

        return new PlayExitExecutionOutput(
            Project: new ProjectIdentityInfo(
                ProjectPath: "/repo/UnityProject",
                ProjectFingerprint: "project-fingerprint",
                UnityVersion: "6000.1.4f1"),
            DaemonStatus: DaemonStatusKind.Running,
            ServerVersion: "0.5.0",
            EditorMode: "gui",
            LifecycleState: IpcEditorLifecycleStateCodec.Ready,
            BlockingReason: null,
            CompileState: "ready",
            CompileGeneration: "12",
            DomainReloadGeneration: "7",
            CanAcceptExecutionRequests: true,
            ObservedAtUtc: DateTimeOffset.Parse("2026-05-21T00:00:00+00:00", CultureInfo.InvariantCulture),
            ActionRequired: null,
            PrimaryDiagnostic: null,
            PlayMode: new PlayModeSnapshotOutput(
                State: IpcPlayModeStateNames.Stopped,
                Transition: IpcPlayModeTransitionNames.None,
                IsPlaying: false,
                IsPlayingOrWillChangePlaymode: false,
                Generation: "3"),
            Transition: transition,
            TimeoutMilliseconds: 1000);
    }

    private static PlayLifecycleSnapshotOutput CreateSnapshotOutput (IpcPlayLifecycleSnapshot snapshot)
    {
        return new PlayLifecycleSnapshotOutput(
            ServerVersion: snapshot.ServerVersion,
            EditorMode: snapshot.EditorMode,
            UnityVersion: snapshot.UnityVersion,
            ProjectFingerprint: snapshot.ProjectFingerprint,
            LifecycleState: snapshot.LifecycleState,
            BlockingReason: snapshot.BlockingReason,
            CompileState: snapshot.CompileState,
            CompileGeneration: snapshot.CompileGeneration,
            DomainReloadGeneration: snapshot.DomainReloadGeneration,
            CanAcceptExecutionRequests: snapshot.CanAcceptExecutionRequests,
            ObservedAtUtc: snapshot.ObservedAtUtc,
            ActionRequired: snapshot.ActionRequired,
            PrimaryDiagnostic: null,
            PlayMode: new PlayModeSnapshotOutput(
                State: snapshot.PlayMode!.State,
                Transition: snapshot.PlayMode.Transition,
                IsPlaying: snapshot.PlayMode.IsPlaying,
                IsPlayingOrWillChangePlaymode: snapshot.PlayMode.IsPlayingOrWillChangePlaymode,
                Generation: snapshot.PlayMode.Generation));
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

    private sealed class StubPlayExitService : IPlayExitService
    {
        private readonly Func<PlayExitCommandInput, CancellationToken, ValueTask<PlayExitExecutionResult>> handler;

        public StubPlayExitService (Func<PlayExitCommandInput, CancellationToken, ValueTask<PlayExitExecutionResult>> handler)
        {
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public PlayExitCommandInput? CapturedInput { get; private set; }

        public CancellationToken CapturedCancellationToken { get; private set; }

        public ValueTask<PlayExitExecutionResult> ExecuteAsync (
            PlayExitCommandInput input,
            CancellationToken cancellationToken = default)
        {
            CapturedInput = input;
            CapturedCancellationToken = cancellationToken;
            return handler(input, cancellationToken);
        }
    }
}
