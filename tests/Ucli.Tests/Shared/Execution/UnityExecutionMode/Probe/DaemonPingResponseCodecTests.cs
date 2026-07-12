using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Execution.Mode;

public sealed class DaemonPingResponseCodecTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryValidateSuccessResponse_WhenResponseIsOk_ReturnsTrue ()
    {
        var response = CreateResponse(IpcProtocol.StatusOk, Array.Empty<IpcError>(), CreatePayload());

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
                new IpcError(UcliCoreErrorCodes.InvalidArgument, "invalid request", null),
            ],
            null);

        var result = DaemonPingResponseCodec.TryValidateSuccessResponse(response, out var error);

        Assert.False(result);
        Assert.NotNull(error);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, error.ErrorCode!.Value);
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
            new IpcPingResponse(
                ServerVersion: expectedServerVersion,
                EditorMode: expectedRuntime,
                UnityVersion: expectedUnityVersion,
                ProjectFingerprint: " project-fingerprint ",
                CompileState: expectedCompileState));

        var result = DaemonPingResponseCodec.TryDecodePayload(response, out var payload, out var error);

        Assert.True(result);
        Assert.NotNull(payload);
        Assert.Equal(expectedServerVersion, payload.ServerVersion);
        Assert.Equal(expectedRuntime, payload.EditorMode);
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
            new IpcPingResponse(
                ServerVersion: " ",
                EditorMode: "batchmode",
                UnityVersion: "2022.3.5f1",
                ProjectFingerprint: "project-fingerprint",
                CompileState: "ready"));

        var result = DaemonPingResponseCodec.TryDecodePayload(response, out var payload, out var error);

        Assert.False(result);
        Assert.Null(payload);
        Assert.NotNull(error);
        Assert.Contains("required fields", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDecodePayload_WhenCompileStateIsMissing_ReturnsTrue ()
    {
        var response = CreateResponse(
            IpcProtocol.StatusOk,
            Array.Empty<IpcError>(),
            new
            {
                serverVersion = "0.5.0",
                editorMode = "batchmode",
                unityVersion = "2022.3.5f1",
                projectFingerprint = "project-fingerprint",
            });

        var result = DaemonPingResponseCodec.TryDecodePayload(response, out var payload, out var error);

        Assert.True(result);
        Assert.NotNull(payload);
        Assert.True(string.IsNullOrWhiteSpace(payload.CompileState));
        Assert.Null(error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDecodePayload_WhenProjectFingerprintIsMissing_ReturnsFalse ()
    {
        var response = CreateResponse(
            IpcProtocol.StatusOk,
            Array.Empty<IpcError>(),
            new
            {
                serverVersion = "0.5.0",
                editorMode = "batchmode",
                unityVersion = "2022.3.5f1",
                compileState = "ready",
            });

        var result = DaemonPingResponseCodec.TryDecodePayload(response, out var payload, out var error);

        Assert.False(result);
        Assert.Null(payload);
        Assert.NotNull(error);
        Assert.Contains("payload is invalid", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDecodePayload_WhenOnlyLegacyRuntimeFieldExists_ReturnsFalse ()
    {
        var response = CreateResponse(
            IpcProtocol.StatusOk,
            Array.Empty<IpcError>(),
            new
            {
                serverVersion = "0.5.0",
                runtime = "batchmode",
                unityVersion = "2022.3.5f1",
                projectFingerprint = "project-fingerprint",
                compileState = "ready",
            });

        var result = DaemonPingResponseCodec.TryDecodePayload(response, out var payload, out var error);

        Assert.False(result);
        Assert.Null(payload);
        Assert.NotNull(error);
        Assert.Contains("payload is invalid", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDecodePayloadForProject_WhenProjectFingerprintMatches_ReturnsTrue ()
    {
        var response = CreateResponse(IpcProtocol.StatusOk, Array.Empty<IpcError>(), CreatePayload());

        var result = DaemonPingResponseCodec.TryDecodePayloadForProject(
            response,
            "project-fingerprint",
            "Daemon ping",
            out var payload,
            out var error);

        Assert.True(result);
        Assert.NotNull(payload);
        Assert.Null(error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDecodePayloadForProject_WhenProjectFingerprintMismatches_ReturnsFalse ()
    {
        var response = CreateResponse(IpcProtocol.StatusOk, Array.Empty<IpcError>(), CreatePayload());

        var result = DaemonPingResponseCodec.TryDecodePayloadForProject(
            response,
            "different-fingerprint",
            "Daemon ping",
            out var payload,
            out var error);

        Assert.False(result);
        Assert.Null(payload);
        Assert.NotNull(error);
        Assert.Contains("projectFingerprint mismatch", error.Message, StringComparison.Ordinal);
    }

    private static IpcResponse CreateResponse (
        string status,
        IReadOnlyList<IpcError> errors,
        object? payload)
    {
        var payloadElement = payload is null
            ? IpcPayloadCodec.SerializeToElement(new { })
            : IpcPayloadCodec.SerializeToElement(payload);
        return new IpcResponse(IpcProtocol.CurrentVersion, Guid.NewGuid(), status, payloadElement, errors);
    }

    private static IpcPingResponse CreatePayload ()
    {
        return new IpcPingResponse(
            ServerVersion: "0.5.0",
            EditorMode: "batchmode",
            UnityVersion: "2022.3.5f1",
            ProjectFingerprint: "project-fingerprint",
            CompileState: "ready");
    }
}
