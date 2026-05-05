using MackySoft.Ucli.Application.Features.Testing.Run.Execution;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests;

public sealed class IpcDaemonTestRunResponseCodecTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryDecode_WithValidResponse_ReturnsTrueAndExitCode ()
    {
        var response = CreateResponse(
            status: IpcProtocol.StatusOk,
            errors: [],
            payload: new IpcTestRunResponse(2));

        var success = IpcDaemonTestRunResponseCodec.TryDecode(response, out var exitCode, out var errorCode, out var errorMessage);

        Assert.True(success);
        Assert.Equal(2, exitCode);
        Assert.Null(errorCode);
        Assert.Null(errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDecode_WithPayloadMissingExitCode_ReturnsFalse ()
    {
        var response = CreateResponse(
            status: IpcProtocol.StatusOk,
            errors: [],
            payload: new
            {
                unknown = true,
            });

        var success = IpcDaemonTestRunResponseCodec.TryDecode(response, out _, out var errorCode, out var errorMessage);

        Assert.False(success);
        Assert.Null(errorCode);
        Assert.Contains("payload is invalid", errorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDecode_WithUnsupportedExitCode_ReturnsFalse ()
    {
        var response = CreateResponse(
            status: IpcProtocol.StatusOk,
            errors: [],
            payload: new IpcTestRunResponse(1));

        var success = IpcDaemonTestRunResponseCodec.TryDecode(response, out _, out var errorCode, out var errorMessage);

        Assert.False(success);
        Assert.Null(errorCode);
        Assert.Contains("unsupported exit code", errorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDecode_WithErrorStatusAndErrorPayload_ReturnsFalse ()
    {
        var response = CreateResponse(
            status: IpcProtocol.StatusError,
            errors:
            [
                new IpcError(IpcErrorCodes.InvalidArgument, "invalid", null),
            ],
            payload: new IpcTestRunResponse(0));

        var success = IpcDaemonTestRunResponseCodec.TryDecode(response, out _, out var errorCode, out var errorMessage);

        Assert.False(success);
        Assert.Equal(IpcErrorCodes.InvalidArgument, errorCode);
        Assert.Contains(IpcErrorCodes.InvalidArgument, errorMessage, StringComparison.Ordinal);
    }

    private static IpcResponse CreateResponse (
        string status,
        IReadOnlyList<IpcError> errors,
        object payload)
    {
        return new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: "req-test-run",
            Status: status,
            Payload: IpcPayloadCodec.SerializeToElement(payload),
            Errors: errors);
    }
}
