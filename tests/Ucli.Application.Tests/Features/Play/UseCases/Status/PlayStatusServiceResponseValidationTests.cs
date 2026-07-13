using MackySoft.Ucli.Application.Features.Play.UseCases.Status;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Play.PlayStatusServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Play;

public sealed class PlayStatusServiceResponseValidationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenResponseProjectFingerprintDiffers_ReturnsMismatchFailure ()
    {
        var otherProjectFingerprint = ProjectFingerprintTestFactory.Create("other-project-fingerprint");
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResultTestFactory.Found(CreatePlaySession()));
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(CreateStatusResponse(
            projectFingerprint: otherProjectFingerprint))));
        var service = CreateService(PlayProjectContext, sessionStore, requestExecutor);

        var result = await service.ExecuteAsync(new PlayStatusCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("projectFingerprint mismatch", error.Message, StringComparison.Ordinal);
        Assert.Contains(PlayProjectContext.UnityProject.ProjectFingerprint.ToString(), error.Message, StringComparison.Ordinal);
        Assert.Contains(otherProjectFingerprint.ToString(), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPlayModeSnapshotIsInvalid_ReturnsStateUnknown ()
    {
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResultTestFactory.Found(CreatePlaySession()));
        var statusResponse = CreateStatusResponse(playMode: new IpcPlayModeSnapshot(
            State: "invalid",
            Transition: "none",
            IsPlaying: false,
            IsPlayingOrWillChangePlaymode: false,
            Generation: "2"));
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(statusResponse)));
        var service = CreateService(PlayProjectContext, sessionStore, requestExecutor);

        var result = await service.ExecuteAsync(new PlayStatusCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(PlayModeErrorCodes.PlayModeStateUnknown, error.Code);
    }
}
