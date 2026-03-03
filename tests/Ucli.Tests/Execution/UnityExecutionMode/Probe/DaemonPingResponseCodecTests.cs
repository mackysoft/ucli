using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Execution;

namespace MackySoft.Ucli.Tests.Execution.Mode;

public sealed class DaemonPingResponseCodecTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryValidateSuccessResponse_WhenResponseIsOk_ReturnsTrue ()
    {
        var response = CreateResponse(IpcProtocol.StatusOk, Array.Empty<IpcError>(), new IpcPingResponse("0.5.0", "batchmode", "2022.3.5f1", "ready"));

        var result = DaemonPingResponseCodec.TryValidateSuccessResponse(response, out var error);

        Assert.True(result);
        Assert.Null(error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidateSuccessResponse_WhenResponseHasErrorCode_ReturnsFalse ()
    {
        var response = CreateResponse(
            IpcProtocol.StatusError,
            [
                new IpcError(IpcErrorCodes.InvalidArgument, "invalid request", null),
            ],
            null);

        var result = DaemonPingResponseCodec.TryValidateSuccessResponse(response, out var error);

        Assert.False(result);
        Assert.NotNull(error);
        Assert.Equal(IpcErrorCodes.InvalidArgument, error.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDecodePayload_WhenPayloadIsValid_ReturnsTrue ()
    {
        const string expectedServerVersion = " 0.5.0 ";
        const string expectedRuntime = " batchmode ";
        const string expectedUnityVersion = " 2022.3.5f1 ";
        const string expectedCompileState = " ready ";

        var response = CreateResponse(
            IpcProtocol.StatusOk,
            Array.Empty<IpcError>(),
            new IpcPingResponse(expectedServerVersion, expectedRuntime, expectedUnityVersion, expectedCompileState));

        var result = DaemonPingResponseCodec.TryDecodePayload(response, out var payload, out var error);

        Assert.True(result);
        Assert.NotNull(payload);
        Assert.Equal(expectedServerVersion, payload.ServerVersion);
        Assert.Equal(expectedRuntime, payload.Runtime);
        Assert.Equal(expectedUnityVersion, payload.UnityVersion);
        Assert.Equal(expectedCompileState, payload.CompileState);
        Assert.Null(error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDecodePayload_WhenPayloadIsMissing_ReturnsFalse ()
    {
        var response = CreateResponse(IpcProtocol.StatusOk, Array.Empty<IpcError>(), null);

        var result = DaemonPingResponseCodec.TryDecodePayload(response, out var payload, out var error);

        Assert.False(result);
        Assert.Null(payload);
        Assert.NotNull(error);
        Assert.Contains("payload", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDecodePayload_WhenRequiredFieldIsWhitespace_ReturnsFalse ()
    {
        var response = CreateResponse(
            IpcProtocol.StatusOk,
            Array.Empty<IpcError>(),
            new IpcPingResponse(" ", "batchmode", "2022.3.5f1", "ready"));

        var result = DaemonPingResponseCodec.TryDecodePayload(response, out var payload, out var error);

        Assert.False(result);
        Assert.Null(payload);
        Assert.NotNull(error);
        Assert.Contains("required fields", error.Message, StringComparison.Ordinal);
    }

    private static IpcResponse CreateResponse (
        string status,
        IReadOnlyList<IpcError> errors,
        IpcPingResponse? payload)
    {
        var payloadElement = payload is null
            ? IpcPayloadCodec.SerializeToElement(new { })
            : IpcPayloadCodec.SerializeToElement(payload);
        return new IpcResponse(IpcProtocol.CurrentVersion, "req-1", status, payloadElement, errors);
    }
}