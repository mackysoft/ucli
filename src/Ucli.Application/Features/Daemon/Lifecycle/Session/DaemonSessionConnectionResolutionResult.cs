using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

/// <summary> Represents the result of resolving daemon IPC connection values from persisted session metadata. </summary>
/// <param name="Connection"> The resolved daemon IPC connection values when successful; otherwise <see langword="null" />. </param>
/// <param name="Error"> The structured error when resolution fails; otherwise <see langword="null" />. </param>
internal sealed record DaemonSessionConnectionResolutionResult (
    DaemonSessionConnection? Connection,
    ExecutionError? Error)
{
    /// <summary> Gets the message used when daemon session metadata is not present. </summary>
    public const string SessionNotAvailableMessage = "Daemon session is not available.";

    /// <summary> Gets a value indicating whether connection resolution succeeded. </summary>
    public bool IsSuccess => Connection is not null && Error is null;

    /// <summary> Gets a value indicating whether connection resolution failed because session metadata is missing. </summary>
    public bool IsSessionNotAvailable =>
        Error is not null
        && Error.Kind == ExecutionErrorKind.InvalidArgument
        && string.Equals(Error.Message, SessionNotAvailableMessage, StringComparison.Ordinal);

    /// <summary> Creates a successful connection-resolution result. </summary>
    /// <param name="connection"> The resolved daemon IPC connection values. </param>
    /// <returns> The successful connection-resolution result. </returns>
    public static DaemonSessionConnectionResolutionResult Success (DaemonSessionConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return new DaemonSessionConnectionResolutionResult(connection, null);
    }

    /// <summary> Creates a failed connection-resolution result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed connection-resolution result. </returns>
    public static DaemonSessionConnectionResolutionResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonSessionConnectionResolutionResult(null, error);
    }

    /// <summary> Creates a connection-resolution result that indicates daemon session metadata is missing. </summary>
    /// <returns> The connection-resolution result that indicates session metadata is unavailable. </returns>
    public static DaemonSessionConnectionResolutionResult SessionNotAvailable ()
    {
        return Failure(ExecutionError.InvalidArgument(SessionNotAvailableMessage));
    }
}
