using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.UnityIntegration.Project.Plugin.Marker;

/// <summary> Represents the result of enumerating uCLI Unity plugin marker candidates. </summary>
internal sealed record UnityUcliPluginMarkerDiscoveryResult (
    IReadOnlyList<string>? MarkerPaths,
    string? Path,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether marker enumeration succeeded. </summary>
    public bool IsSuccess => MarkerPaths is not null && Error is null;

    /// <summary> Creates a successful marker enumeration result. </summary>
    /// <param name="markerPaths"> The enumerated marker paths. </param>
    /// <returns> The successful result. </returns>
    public static UnityUcliPluginMarkerDiscoveryResult Success (IReadOnlyList<string> markerPaths)
    {
        ArgumentNullException.ThrowIfNull(markerPaths);
        return new UnityUcliPluginMarkerDiscoveryResult(markerPaths, null, null);
    }

    /// <summary> Creates a failed marker enumeration result. </summary>
    /// <param name="path"> The offending path. </param>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed result. </returns>
    public static UnityUcliPluginMarkerDiscoveryResult Failure (
        string path,
        ExecutionError error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(error);
        return new UnityUcliPluginMarkerDiscoveryResult(null, path, error);
    }
}
