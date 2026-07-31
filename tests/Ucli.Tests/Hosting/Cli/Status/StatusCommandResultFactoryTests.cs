using System.Text.Json;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Status;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Tests;

public sealed class StatusCommandResultFactoryTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithSuccessResult_ReturnsOkEnvelopeWithPayload ()
    {
        var executionResult = StatusExecutionResult.Success(
            new StatusExecutionOutput(
                DaemonStatus: DaemonStatusKind.Running,
                UnityVersion: "6000.1.4f1",
                ServerVersion: "0.5.0",
                LifecycleState: UnityEditorLifecycleState.Busy,
                BlockingReason: UnityEditorBlockingReason.Busy,
                CompileState: UnityEditorCompileState.Ready,
                Generations: new UnityEditorGenerationSnapshot(12, 7, 0, 2),
                CanAcceptExecutionRequests: false,
                EditorMode: UnityEditorMode.Batchmode,
                TimeoutMilliseconds: 1234,
                ObservedAtUtc: null,
                ActionRequired: null,
                PrimaryDiagnostic: null,
                PlayMode: new UnityEditorPlayModeSnapshot(
                    State: UnityEditorPlayModeState.Stopped,
                    Transition: UnityEditorPlayModeTransition.None,
                    IsPlaying: false,
                    IsPlayingOrWillChangePlaymode: false)));

        var result = StatusCommandResultFactory.Create(executionResult);

        Assert.Equal(UcliCommandNames.Status, result.Command);
        Assert.Equal(CommandResultStatus.Ok, result.Status);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Empty(result.Errors);

        var payload = JsonSerializer.SerializeToElement(
            result.Payload,
            CliOutputJsonSerializerOptions.Default);
        JsonAssert.For(payload)
            .HasString("daemonStatus", "running")
            .HasString("unityVersion", "6000.1.4f1")
            .HasString("serverVersion", "0.5.0")
            .HasString("lifecycleState", "busy")
            .HasString("blockingReason", "busy")
            .HasString("compileState", "ready")
            .HasProperty("generations", generations => generations
                .HasInt32("compileGeneration", 12)
                .HasInt32("domainReloadGeneration", 7)
                .HasInt32("assetRefreshGeneration", 0)
                .HasInt32("playModeGeneration", 2))
            .HasBoolean("canAcceptExecutionRequests", false)
            .HasString("editorMode", "batchmode")
            .HasProperty("playMode", playMode => playMode
                .HasString("state", "stopped")
                .HasString("transition", "none")
                .HasBoolean("isPlaying", false)
                .HasBoolean("isPlayingOrWillChangePlaymode", false))
            .HasInt32("timeoutMilliseconds", 1234);
        Assert.False(payload.TryGetProperty("runtime", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithFailureResult_ReturnsErrorEnvelope ()
    {
        var executionResult = StatusExecutionResult.Failure(ExecutionError.InvalidArgument("timeout is invalid."));

        var result = StatusCommandResultFactory.Create(executionResult);

        Assert.Equal(UcliCommandNames.Status, result.Command);
        Assert.Equal(CommandResultStatus.Error, result.Status);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.Errors[0].Code);
        Assert.Equal("timeout is invalid.", result.Errors[0].Message);
    }
}
