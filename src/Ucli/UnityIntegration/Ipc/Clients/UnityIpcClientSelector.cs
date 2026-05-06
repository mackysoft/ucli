using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Clients;

/// <summary> Selects one Unity IPC client by resolved execution target. </summary>
internal sealed class UnityIpcClientSelector
{
    private readonly IReadOnlyDictionary<UnityExecutionTarget, IUnityIpcClient> clientsByTarget;

    /// <summary> Initializes a new instance of the <see cref="UnityIpcClientSelector" /> class. </summary>
    /// <param name="unityIpcClients"> The registered Unity IPC clients. </param>
    public UnityIpcClientSelector (IEnumerable<IUnityIpcClient> unityIpcClients)
    {
        ArgumentNullException.ThrowIfNull(unityIpcClients);

        var clients = new Dictionary<UnityExecutionTarget, IUnityIpcClient>();
        foreach (var unityIpcClient in unityIpcClients)
        {
            if (unityIpcClient == null)
            {
                throw new ArgumentException("Unity IPC clients must not contain null entries.", nameof(unityIpcClients));
            }

            if (!clients.TryAdd(unityIpcClient.Target, unityIpcClient))
            {
                throw new InvalidOperationException($"Multiple Unity IPC clients were registered for '{unityIpcClient.Target}'.");
            }
        }

        clientsByTarget = clients;
    }

    /// <summary> Selects the client registered for the specified execution target. </summary>
    /// <param name="target"> The resolved execution target. </param>
    /// <returns> The matching Unity IPC client. </returns>
    public IUnityIpcClient Select (UnityExecutionTarget target)
    {
        if (!clientsByTarget.TryGetValue(target, out var client))
        {
            throw new InvalidOperationException($"Unity IPC client for target '{target}' is not registered.");
        }

        return client;
    }
}
