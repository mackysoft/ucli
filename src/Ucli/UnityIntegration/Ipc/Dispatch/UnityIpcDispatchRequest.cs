using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

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
    public UnityIpcDispatchRequest (
        string method,
        JsonElement payload,
        IReadOnlyList<string>? allowedStartupLifecycleStates = null,
        bool isRecoverable = false,
        TimeSpan? recoverableResponseAttemptTimeout = null,
        string responseMode = IpcResponseModes.Single)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        if (recoverableResponseAttemptTimeout.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(recoverableResponseAttemptTimeout.Value, TimeSpan.Zero);
        }

        if (!IpcResponseModes.IsDefined(responseMode))
        {
            throw new ArgumentException($"Unsupported IPC response mode: {responseMode}.", nameof(responseMode));
        }

        Method = method;
        Payload = payload;
        AllowedStartupLifecycleStates = allowedStartupLifecycleStates ?? Array.Empty<string>();
        IsRecoverable = isRecoverable;
        RecoverableResponseAttemptTimeout = recoverableResponseAttemptTimeout;
        ResponseMode = responseMode;
    }

    /// <summary> Gets the IPC method name. </summary>
    public string Method { get; }

    /// <summary> Gets the IPC payload element. </summary>
    public JsonElement Payload { get; }

    /// <summary> Gets lifecycle states where this request may be dispatched before normal readiness. </summary>
    public IReadOnlyList<string> AllowedStartupLifecycleStates { get; }

    /// <summary> Gets whether daemon dispatch may replay this request with the same request id after endpoint recovery. </summary>
    public bool IsRecoverable { get; }

    /// <summary> Gets the response wait timeout for one recoverable dispatch attempt before retrying with the same request id. </summary>
    public TimeSpan? RecoverableResponseAttemptTimeout { get; }

    /// <summary> Gets the IPC response framing mode requested for this dispatch. </summary>
    public string ResponseMode { get; }

    /// <summary> Creates a copy of this request with a different response framing mode. </summary>
    /// <param name="responseMode"> The response framing mode. </param>
    /// <returns> The copied request. </returns>
    public UnityIpcDispatchRequest WithResponseMode (string responseMode)
    {
        return new UnityIpcDispatchRequest(
            Method,
            Payload,
            AllowedStartupLifecycleStates,
            IsRecoverable,
            RecoverableResponseAttemptTimeout,
            responseMode);
    }
}
