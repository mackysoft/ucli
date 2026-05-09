using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;

/// <summary> Represents the result of reading a Unity Editor instance marker. </summary>
/// <param name="Marker"> The marker when one was loaded; otherwise <see langword="null" />. </param>
/// <param name="Error"> The structured read error when reading failed; otherwise <see langword="null" />. </param>
internal sealed record UnityEditorInstanceMarkerReadResult (
    UnityEditorInstanceMarker? Marker,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether the marker read operation succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Gets a value indicating whether a marker exists. </summary>
    public bool Exists => Marker is not null;

    /// <summary> Creates a successful marker read result. </summary>
    public static UnityEditorInstanceMarkerReadResult Success (UnityEditorInstanceMarker? marker)
    {
        return new UnityEditorInstanceMarkerReadResult(marker, null);
    }

    /// <summary> Creates a failed marker read result. </summary>
    public static UnityEditorInstanceMarkerReadResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new UnityEditorInstanceMarkerReadResult(null, error);
    }
}
