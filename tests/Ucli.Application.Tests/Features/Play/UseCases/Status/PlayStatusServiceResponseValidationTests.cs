using System.Text.Json.Nodes;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
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
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(CreatePlaySession()));
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(CreateStatusResponse(
            projectFingerprint: "other-project-fingerprint"))));
        var service = CreateService(PlayProjectContext, sessionStore, requestExecutor);

        var result = await service.ExecuteAsync(new PlayStatusCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("projectFingerprint mismatch", error.Message, StringComparison.Ordinal);
        Assert.Contains("project-fingerprint", error.Message, StringComparison.Ordinal);
        Assert.Contains("other-project-fingerprint", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPlayModeStateLiteralIsInvalid_ReturnsInvalidPayloadFailure ()
    {
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(CreatePlaySession()));
        var payload = JsonNode.Parse(IpcPayloadCodec.SerializeToElement(CreateStatusResponse()).GetRawText())!;
        payload["snapshot"]!["state"]!["playMode"]!["state"] = "invalid";
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(
            UnityRequestResponseTestFactory.Create(new IpcResponse(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "request-1",
                Status: IpcProtocol.StatusOk,
                Payload: IpcPayloadCodec.SerializeToElement(payload),
                Errors: []))));
        var service = CreateService(PlayProjectContext, sessionStore, requestExecutor);

        var result = await service.ExecuteAsync(new PlayStatusCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("Unity play status payload is invalid.", error.Message, StringComparison.Ordinal);
    }
}
