using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Play.UseCases.Exit;
using static MackySoft.Ucli.Application.Tests.Play.PlayExitServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Play;

public sealed class PlayExitServiceSessionGateTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenSessionIsMissing_ReturnsSessionNotAvailableWithoutIpcCall ()
    {
        var context = PlayProjectContext;
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(null));
        var requestExecutor = new UnexpectedUnityRequestExecutor();
        var service = CreateService(context, sessionStore, requestExecutor);

        var result = await service.ExecuteAsync(new PlayExitCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        Assert.Equal(PlayModeErrorCodes.PlayModeSessionNotAvailable, result.Error!.Code);
        DaemonSessionStoreAssert.SessionReadRequestedFor(sessionStore, context);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenRegisteredSessionIsBatchmode_ReturnsRequiresGuiEditorWithoutIpcCall ()
    {
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(DaemonSessionTestFactory.CreateUserOwned(DaemonEditorMode.Batchmode, PlaySessionEndpointAddress)));
        var requestExecutor = new UnexpectedUnityRequestExecutor();
        var service = CreateService(PlayProjectContext, sessionStore, requestExecutor);

        var result = await service.ExecuteAsync(new PlayExitCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        Assert.Equal(PlayModeErrorCodes.PlayModeRequiresGuiEditor, result.Error!.Code);
    }
}
