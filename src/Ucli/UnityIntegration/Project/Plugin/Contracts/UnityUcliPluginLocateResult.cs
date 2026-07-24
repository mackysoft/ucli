using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.UnityIntegration.Project.Plugin.Contracts;

/// <summary> Represents the result of locating the uCLI Unity plugin marker. </summary>
/// <param name="Status"> The locate status. </param>
/// <param name="MarkerPath"> The resolved marker path when exactly one valid marker was found. </param>
/// <param name="ProtocolVersion"> The marker protocol version when one valid marker was found. </param>
/// <param name="MarkerPaths"> The discovered marker paths related to the result. </param>
/// <param name="Error"> The structured error when marker lookup failed. </param>
internal sealed record UnityUcliPluginLocateResult (
    UnityUcliPluginLocateStatus Status,
    AbsolutePath? MarkerPath,
    int? ProtocolVersion,
    IReadOnlyList<AbsolutePath> MarkerPaths,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether exactly one valid marker was found. </summary>
    public bool IsSuccess => Status == UnityUcliPluginLocateStatus.Found
        && MarkerPath is not null
        && ProtocolVersion is not null
        && Error is null;

    /// <summary> Creates a successful locate result. </summary>
    /// <param name="markerPath"> The resolved marker path. </param>
    /// <param name="protocolVersion"> The marker protocol version. </param>
    /// <returns> The successful result. </returns>
    public static UnityUcliPluginLocateResult Found (
        AbsolutePath markerPath,
        int protocolVersion)
    {
        ArgumentNullException.ThrowIfNull(markerPath);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(protocolVersion, 0);

        return new UnityUcliPluginLocateResult(
            Status: UnityUcliPluginLocateStatus.Found,
            MarkerPath: markerPath,
            ProtocolVersion: protocolVersion,
            MarkerPaths:
            [
                markerPath,
            ],
            Error: null);
    }

    /// <summary> Creates a not-found locate result. </summary>
    /// <param name="error"> The structured failure error. </param>
    /// <returns> The failed result. </returns>
    public static UnityUcliPluginLocateResult NotFound (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new UnityUcliPluginLocateResult(
            Status: UnityUcliPluginLocateStatus.NotFound,
            MarkerPath: null,
            ProtocolVersion: null,
            MarkerPaths: Array.Empty<AbsolutePath>(),
            Error: error);
    }

    /// <summary> Creates a multiple-found locate result. </summary>
    /// <param name="markerPaths"> The conflicting marker paths. </param>
    /// <param name="error"> The structured failure error. </param>
    /// <returns> The failed result. </returns>
    public static UnityUcliPluginLocateResult MultipleFound (
        IReadOnlyList<AbsolutePath> markerPaths,
        ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(markerPaths);
        ArgumentNullException.ThrowIfNull(error);

        return new UnityUcliPluginLocateResult(
            Status: UnityUcliPluginLocateStatus.MultipleFound,
            MarkerPath: null,
            ProtocolVersion: null,
            MarkerPaths: markerPaths,
            Error: error);
    }

    /// <summary> Creates an invalid-marker locate result. </summary>
    /// <param name="markerPath"> The offending marker path. </param>
    /// <param name="error"> The structured failure error. </param>
    /// <returns> The failed result. </returns>
    public static UnityUcliPluginLocateResult InvalidMarker (
        AbsolutePath markerPath,
        ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(markerPath);
        ArgumentNullException.ThrowIfNull(error);

        return new UnityUcliPluginLocateResult(
            Status: UnityUcliPluginLocateStatus.InvalidMarker,
            MarkerPath: null,
            ProtocolVersion: null,
            MarkerPaths:
            [
                markerPath,
            ],
            Error: error);
    }
}
