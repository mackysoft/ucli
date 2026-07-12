using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Logs;

public sealed class IpcUnityConsoleClearResponseCodecTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryDecode_WhenResponseIsSuccessful_ReturnsSuccess ()
    {
        var response = CreateResponse(
            status: IpcProtocol.StatusOk,
            errors: Array.Empty<IpcError>(),
            payload: new IpcUnityConsoleClearResponse());

        var result = IpcUnityConsoleClearResponseCodec.TryDecode(response, out var error);

        Assert.True(result);
        Assert.Null(error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDecode_WhenResponseContainsInvalidArgumentError_ReturnsInvalidArgument ()
    {
        var response = CreateResponse(
            status: IpcProtocol.StatusError,
            errors:
            [
                new IpcError(UcliCoreErrorCodes.InvalidArgument, "GUI Editor daemon is required.", null),
            ],
            payload: new { });

        var result = IpcUnityConsoleClearResponseCodec.TryDecode(response, out var error);

        Assert.False(result);
        var executionError = Assert.IsType<ExecutionError>(error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, executionError.Kind);
        Assert.Contains("GUI Editor daemon", executionError.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDecode_WhenPayloadIsNull_ReturnsInternalError ()
    {
        var response = CreateResponse(
            status: IpcProtocol.StatusOk,
            errors: Array.Empty<IpcError>(),
            payload: null);

        var result = IpcUnityConsoleClearResponseCodec.TryDecode(response, out var error);

        Assert.False(result);
        var executionError = Assert.IsType<ExecutionError>(error);
        Assert.Equal(ExecutionErrorKind.InternalError, executionError.Kind);
        Assert.Contains("payload is invalid", executionError.Message, StringComparison.Ordinal);
    }

    private static IpcResponse CreateResponse (
        string status,
        IReadOnlyList<IpcError> errors,
        object? payload)
    {
        return new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: Guid.NewGuid(),
            status: status,
            payload: IpcPayloadCodec.SerializeToElement(payload),
            errors: errors);
    }
}
