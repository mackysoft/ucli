using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.UnityIntegration.Project.Plugin.Cache;

/// <summary> Represents one plugin-marker cache write or delete result. </summary>
internal sealed record UnityUcliPluginMarkerCacheStoreOperationResult (ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether the cache operation succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates a successful cache-operation result. </summary>
    /// <returns> The successful cache-operation result. </returns>
    public static UnityUcliPluginMarkerCacheStoreOperationResult Success ()
    {
        return new UnityUcliPluginMarkerCacheStoreOperationResult((ExecutionError?)null);
    }

    /// <summary> Creates a failed cache-operation result. </summary>
    /// <param name="error"> The structured cache-operation error. </param>
    /// <returns> The failed cache-operation result. </returns>
    public static UnityUcliPluginMarkerCacheStoreOperationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new UnityUcliPluginMarkerCacheStoreOperationResult(error);
    }
}