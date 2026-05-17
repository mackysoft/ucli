using System.Text.Json;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;

/// <summary> Represents one IPC method dispatch request after application payload conversion. </summary>
internal sealed record UnityIpcDispatchRequest
{
    /// <summary> Initializes a new instance of the <see cref="UnityIpcDispatchRequest" /> class. </summary>
    /// <param name="method"> The IPC method name. </param>
    /// <param name="payload"> The IPC payload element. </param>
    /// <param name="allowedStartupLifecycleStates"> Lifecycle states where the request may be dispatched before normal readiness. </param>
    public UnityIpcDispatchRequest (
        string method,
        JsonElement payload,
        IReadOnlyList<string>? allowedStartupLifecycleStates = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        Method = method;
        Payload = payload;
        AllowedStartupLifecycleStates = allowedStartupLifecycleStates ?? Array.Empty<string>();
    }

    /// <summary> Gets the IPC method name. </summary>
    public string Method { get; }

    /// <summary> Gets the IPC payload element. </summary>
    public JsonElement Payload { get; }

    /// <summary> Gets lifecycle states where this request may be dispatched before normal readiness. </summary>
    public IReadOnlyList<string> AllowedStartupLifecycleStates { get; }
}
