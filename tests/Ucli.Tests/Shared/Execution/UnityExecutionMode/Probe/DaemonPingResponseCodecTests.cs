using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Execution.Mode;

public sealed class DaemonPingResponseCodecTests
{
    private const string ProjectFingerprintWireValue = "6c997854157f1c37233ad17eacb2ee2357866912f2d6ac6d62f1ec477b95f262";

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
        const string expectedUnityVersion = " 2022.3.5f1 ";

        var response = CreateResponse(
            IpcProtocol.StatusOk,
            Array.Empty<IpcError>(),
            IpcUnityEditorObservationTestFactory.Create(
                serverVersion: expectedServerVersion,
                unityVersion: expectedUnityVersion,
                projectFingerprint: ProjectFingerprintTestFactory.Create("project-fingerprint")));

        var result = DaemonPingResponseCodec.TryDecodePayload(response, out var payload, out var error);

        Assert.True(result);
        Assert.NotNull(payload);
        Assert.Equal(expectedServerVersion, payload.ServerVersion);
        Assert.Equal(DaemonEditorMode.Batchmode, payload.State.EditorMode);
        Assert.Equal(expectedUnityVersion, payload.UnityVersion);
        Assert.Equal(IpcCompileState.Ready, payload.State.CompileState);
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
            CreateWirePayload(serverVersion: " "));

        var result = DaemonPingResponseCodec.TryDecodePayload(response, out var payload, out var error);

        Assert.False(result);
        Assert.Null(payload);
        Assert.NotNull(error);
        Assert.Contains("payload is invalid", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDecodePayload_WhenCompileStateIsMissing_ReturnsFalse ()
    {
        var response = CreateResponse(
            IpcProtocol.StatusOk,
            Array.Empty<IpcError>(),
                CreateWirePayload(includeCompileState: false));

        var result = DaemonPingResponseCodec.TryDecodePayload(response, out var payload, out var error);

        Assert.False(result);
        Assert.Null(payload);
        Assert.NotNull(error);
        Assert.Contains("payload is invalid", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDecodePayload_WhenProjectFingerprintIsMissing_ReturnsFalse ()
    {
        var response = CreateResponse(
            IpcProtocol.StatusOk,
            Array.Empty<IpcError>(),
            CreateWirePayload(projectFingerprint: null));

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
                unityVersion = "2022.3.5f1",
                projectFingerprint = ProjectFingerprintWireValue,
                state = new
                {
                    runtime = "batchmode",
                    lifecycleState = "ready",
                    compileState = "ready",
                    generations = new
                    {
                        compileGeneration = 0,
                        domainReloadGeneration = 0,
                        assetRefreshGeneration = 0,
                        playModeGeneration = 0,
                    },
                    playMode = new
                    {
                        state = "stopped",
                        transition = "none",
                        isPlaying = false,
                        isPlayingOrWillChangePlaymode = false,
                    },
                },
                observedAtUtc = "2026-05-21T00:00:00+00:00",
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
            ProjectFingerprintTestFactory.Create("project-fingerprint"),
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
            ProjectFingerprintTestFactory.Create("different-fingerprint"),
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

    private static IpcUnityEditorObservation CreatePayload ()
    {
        return IpcUnityEditorObservationTestFactory.Create(
            serverVersion: "0.5.0",
            unityVersion: "2022.3.5f1",
            projectFingerprint: ProjectFingerprintTestFactory.Create("project-fingerprint"));
    }

    private static object CreateWirePayload (
        string serverVersion = "0.5.0",
        string? projectFingerprint = ProjectFingerprintWireValue,
        bool includeCompileState = true)
    {
        var state = includeCompileState
            ? (object)new
            {
                editorMode = "batchmode",
                lifecycleState = "ready",
                compileState = "ready",
                generations = CreateGenerationWirePayload(),
                playMode = CreatePlayModeWirePayload(),
            }
            : new
            {
                editorMode = "batchmode",
                lifecycleState = "ready",
                generations = CreateGenerationWirePayload(),
                playMode = CreatePlayModeWirePayload(),
            };
        return new
        {
            serverVersion,
            unityVersion = "2022.3.5f1",
            projectFingerprint,
            state,
            observedAtUtc = "2026-05-21T00:00:00+00:00",
        };
    }

    private static object CreateGenerationWirePayload ()
    {
        return new
        {
            compileGeneration = 0,
            domainReloadGeneration = 0,
            assetRefreshGeneration = 0,
            playModeGeneration = 0,
        };
    }

    private static object CreatePlayModeWirePayload ()
    {
        return new
        {
            state = "stopped",
            transition = "none",
            isPlaying = false,
            isPlayingOrWillChangePlaymode = false,
        };
    }
}
