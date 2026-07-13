using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Play.UseCases.Status;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Foundation;
using static MackySoft.Ucli.Application.Tests.Play.PlayStatusServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Play;

public sealed class PlayStatusServiceSessionGateTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenProjectResolutionFails_ReturnsFailureWithoutSessionOrIpcCall ()
    {
        var expectedError = ExecutionError.InvalidArgument("Project resolution failed.");
        var sessionStore = new UnexpectedDaemonSessionStore();
        var requestExecutor = new UnexpectedUnityRequestExecutor();
        var service = CreateService(ProjectContextResolutionResult.Failure(expectedError), sessionStore, requestExecutor);

        var result = await service.ExecuteAsync(new PlayStatusCommandInput("/missing/project", null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Same(expectedError, result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenSessionIsMissing_ReturnsSessionNotAvailableWithoutIpcCall ()
    {
        var context = PlayProjectContext;
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Missing());
        var requestExecutor = new UnexpectedUnityRequestExecutor();
        var service = CreateService(context, sessionStore, requestExecutor);

        var result = await service.ExecuteAsync(new PlayStatusCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(PlayModeErrorCodes.PlayModeSessionNotAvailable, error.Code);
        DaemonSessionStoreAssert.SessionReadRequestedFor(sessionStore, context);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenRegisteredSessionIsBatchmode_ReturnsRequiresGuiEditorWithoutIpcCall ()
    {
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResultTestFactory.Found(
            DaemonSessionTestFactory.Create(
                editorMode: "batchmode",
                endpointAddress: PlaySessionEndpointAddress)));
        var requestExecutor = new UnexpectedUnityRequestExecutor();
        var service = CreateService(PlayProjectContext, sessionStore, requestExecutor);

        var result = await service.ExecuteAsync(new PlayStatusCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(PlayModeErrorCodes.PlayModeRequiresGuiEditor, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenSessionReadFails_ReturnsSessionReadErrorWithoutIpcCall ()
    {
        var expectedError = ExecutionError.InternalError("Failed to read daemon session.");
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Failure(
            expectedError,
            DaemonSessionReadFailureKind.Unknown));
        var requestExecutor = new UnexpectedUnityRequestExecutor();
        var service = CreateService(PlayProjectContext, sessionStore, requestExecutor);

        var result = await service.ExecuteAsync(new PlayStatusCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Same(expectedError, result.Error);
    }
}
