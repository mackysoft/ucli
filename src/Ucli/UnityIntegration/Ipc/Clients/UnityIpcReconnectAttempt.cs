using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Clients;

/// <summary>
/// Reports whether one IPC provider proved that it still owns the host of a persisted
/// Lifecycle Execution start.
/// </summary>
internal sealed record UnityIpcReconnectAttempt
{
    private UnityIpcReconnectAttempt (
        bool isOwned,
        UnityRequestExecutionResult? result)
    {
        if (isOwned != (result is not null))
        {
            throw new ArgumentException(
                "An owned reconnect attempt must contain exactly one execution result.",
                nameof(result));
        }

        IsOwned = isOwned;
        Result = result;
    }

    /// <summary> Gets whether this provider proved ownership of the persisted execution host. </summary>
    public bool IsOwned { get; }

    /// <summary> Gets the completed request result when ownership was proved. </summary>
    public UnityRequestExecutionResult? Result { get; }

    /// <summary> Creates an attempt for a provider that has no matching existing host endpoint. </summary>
    public static UnityIpcReconnectAttempt NotOwned ()
    {
        return new UnityIpcReconnectAttempt(
            isOwned: false,
            result: null);
    }

    /// <summary> Creates an attempt completed through the provider that proved host ownership. </summary>
    public static UnityIpcReconnectAttempt Owned (
        UnityRequestExecutionResult result)
    {
        return new UnityIpcReconnectAttempt(
            isOwned: true,
            result ?? throw new ArgumentNullException(nameof(result)));
    }
}
