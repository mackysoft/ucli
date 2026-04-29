using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.UnityIntegration.Project.Plugin.Cache;

/// <summary> Represents one plugin-marker cache read result. </summary>
internal sealed record UnityUcliPluginMarkerCacheReadResult (
    UnityUcliPluginMarkerCache? Cache,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether the cache read succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates a successful cache-read result. </summary>
    /// <param name="cache"> The persisted cache when present; otherwise <see langword="null" />. </param>
    /// <returns> The successful cache-read result. </returns>
    public static UnityUcliPluginMarkerCacheReadResult Success (UnityUcliPluginMarkerCache? cache)
    {
        return new UnityUcliPluginMarkerCacheReadResult(cache, null);
    }

    /// <summary> Creates a failed cache-read result. </summary>
    /// <param name="error"> The structured read error. </param>
    /// <returns> The failed cache-read result. </returns>
    public static UnityUcliPluginMarkerCacheReadResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new UnityUcliPluginMarkerCacheReadResult(null, error);
    }
}
