using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;

/// <summary> Represents one IPC method dispatch request after application payload conversion. </summary>
internal sealed record UnityIpcDispatchRequest
{
    /// <summary> Initializes a new instance of the <see cref="UnityIpcDispatchRequest" /> class. </summary>
    /// <param name="method"> The IPC method name. </param>
    /// <param name="payload"> The IPC payload element. </param>
    /// <param name="allowedStartupLifecycleStates"> Lifecycle states where the request may be dispatched before normal readiness. </param>
    /// <param name="isRecoverable"> Whether daemon dispatch may replay this request with the same request id after endpoint recovery. </param>
    /// <param name="recoverableResponseAttemptTimeout"> The bounded response wait for one recoverable dispatch attempt, or <see langword="null" /> to use the full remaining timeout. </param>
    /// <param name="responseMode"> The IPC response framing mode. </param>
    /// <param name="dispatchTimeoutPayloadTransformer"> Optional method-owned transformer that projects the final dispatch timeout into the payload. </param>
    /// <param name="oneshotActiveBuildProfilePath"> The optional project-relative Unity Build Profile path that oneshot launch should pass to Unity <c>-activeBuildProfile</c>. </param>
    public UnityIpcDispatchRequest (
        string method,
        JsonElement payload,
        IReadOnlyList<IpcEditorLifecycleState>? allowedStartupLifecycleStates = null,
        bool isRecoverable = false,
        TimeSpan? recoverableResponseAttemptTimeout = null,
        IpcResponseMode responseMode = IpcResponseMode.Single,
        Func<JsonElement, TimeSpan, JsonElement>? dispatchTimeoutPayloadTransformer = null,
        string? oneshotActiveBuildProfilePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        if (recoverableResponseAttemptTimeout.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(recoverableResponseAttemptTimeout.Value, TimeSpan.Zero);
        }

        if (!ContractLiteralCodec.IsDefined(responseMode))
        {
            throw new ArgumentException($"Unsupported IPC response mode: {responseMode}.", nameof(responseMode));
        }

        if (oneshotActiveBuildProfilePath != null
            && !UnityAssetPathContract.IsNormalizedBuildProfileAssetPath(oneshotActiveBuildProfilePath))
        {
            throw new ArgumentException(
                "Oneshot active Build Profile path must be a normalized project-relative asset path under Assets and must not reference a .meta file.",
                nameof(oneshotActiveBuildProfilePath));
        }

        Method = method;
        Payload = payload;
        AllowedStartupLifecycleStates = allowedStartupLifecycleStates ?? Array.Empty<IpcEditorLifecycleState>();
        IsRecoverable = isRecoverable;
        RecoverableResponseAttemptTimeout = recoverableResponseAttemptTimeout;
        ResponseMode = responseMode;
        DispatchTimeoutPayloadTransformer = dispatchTimeoutPayloadTransformer;
        OneshotActiveBuildProfilePath = oneshotActiveBuildProfilePath;
    }

    /// <summary> Gets the IPC method name. </summary>
    public string Method { get; }

    /// <summary> Gets the IPC payload element. </summary>
    public JsonElement Payload { get; }

    /// <summary> Gets lifecycle states where this request may be dispatched before normal readiness. </summary>
    public IReadOnlyList<IpcEditorLifecycleState> AllowedStartupLifecycleStates { get; }

    /// <summary> Gets whether daemon dispatch may replay this request with the same request id after endpoint recovery. </summary>
    public bool IsRecoverable { get; }

    /// <summary> Gets the response wait timeout for one recoverable dispatch attempt before retrying with the same request id. </summary>
    public TimeSpan? RecoverableResponseAttemptTimeout { get; }

    /// <summary> Gets the IPC response framing mode requested for this dispatch. </summary>
    public IpcResponseMode ResponseMode { get; }

    /// <summary> Gets the optional Unity Build Profile path that oneshot launch should activate before uCLI bootstrap. </summary>
    public string? OneshotActiveBuildProfilePath { get; }

    private Func<JsonElement, TimeSpan, JsonElement>? DispatchTimeoutPayloadTransformer { get; }

    /// <summary> Creates a copy of this request with a different response framing mode. </summary>
    /// <param name="responseMode"> The response framing mode. </param>
    /// <returns> The copied request. </returns>
    public UnityIpcDispatchRequest WithResponseMode (IpcResponseMode responseMode)
    {
        return new UnityIpcDispatchRequest(
            Method,
            Payload,
            AllowedStartupLifecycleStates,
            IsRecoverable,
            RecoverableResponseAttemptTimeout,
            responseMode,
            DispatchTimeoutPayloadTransformer,
            OneshotActiveBuildProfilePath);
    }

    /// <summary> Creates the payload element for one concrete dispatch attempt. </summary>
    /// <param name="dispatchTimeout"> The final dispatch timeout budget when the method needs server-side cancellation. </param>
    /// <returns> The original or timeout-projected payload element. </returns>
    public JsonElement CreatePayload (TimeSpan? dispatchTimeout)
    {
        return dispatchTimeout.HasValue && DispatchTimeoutPayloadTransformer != null
            ? DispatchTimeoutPayloadTransformer(Payload, dispatchTimeout.Value)
            : Payload;
    }
}
