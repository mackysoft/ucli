using System.Text.Json;
using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Daemon;

/// <summary> Implements orchestration for filesystem-backed daemon session persistence. </summary>
internal sealed class DaemonSessionStore : IDaemonSessionStore
{
    private readonly IDaemonSessionFileAccess sessionFileAccess;

    private readonly IDaemonSessionSerializer sessionSerializer;

    private readonly IDaemonSessionValidator sessionValidator;

    /// <summary> Initializes a new instance of the <see cref="DaemonSessionStore" /> class with default dependencies. </summary>
    public DaemonSessionStore ()
        : this(
            new DaemonSessionFileAccess(),
            new DaemonSessionJsonSerializer(),
            new DaemonSessionValidator())
    {
    }

    /// <summary> Initializes a new instance of the <see cref="DaemonSessionStore" /> class. </summary>
    /// <param name="sessionFileAccess"> The daemon session file-access dependency. </param>
    /// <param name="sessionSerializer"> The daemon session serializer dependency. </param>
    /// <param name="sessionValidator"> The daemon session validator dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonSessionStore (
        IDaemonSessionFileAccess sessionFileAccess,
        IDaemonSessionSerializer sessionSerializer,
        IDaemonSessionValidator sessionValidator)
    {
        this.sessionFileAccess = sessionFileAccess ?? throw new ArgumentNullException(nameof(sessionFileAccess));
        this.sessionSerializer = sessionSerializer ?? throw new ArgumentNullException(nameof(sessionSerializer));
        this.sessionValidator = sessionValidator ?? throw new ArgumentNullException(nameof(sessionValidator));
    }

    /// <summary> Reads daemon session metadata for one project fingerprint. </summary>
    /// <param name="storageRoot"> The storage root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon session read result. </returns>
    public async ValueTask<DaemonSessionReadResult> Read (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string sessionPath;
        try
        {
            sessionPath = DaemonStoragePathResolver.ResolveSessionPath(storageRoot, projectFingerprint);
        }
        catch (Exception exception) when (IsPathFormatException(exception))
        {
            return DaemonSessionReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon session path is invalid. {exception.Message}"));
        }

        string? json;
        try
        {
            json = await sessionFileAccess.ReadOrNull(sessionPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsPathFormatException(exception))
        {
            return DaemonSessionReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon session path is invalid: {sessionPath}. {exception.Message}"));
        }
        catch (Exception exception) when (IsIoFailure(exception))
        {
            return DaemonSessionReadResult.Failure(ExecutionError.InternalError(
                $"Failed to read daemon session file: {sessionPath}. {exception.Message}"));
        }

        if (json == null)
        {
            return DaemonSessionReadResult.Success(null);
        }

        DaemonSession session;
        try
        {
            session = sessionSerializer.Deserialize(json);
        }
        catch (JsonException exception)
        {
            return DaemonSessionReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon session JSON is invalid: {sessionPath}. {exception.Message}"));
        }
        catch (ArgumentException exception)
        {
            return DaemonSessionReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon session JSON is invalid: {sessionPath}. {exception.Message}"));
        }
        catch (Exception exception)
        {
            return DaemonSessionReadResult.Failure(ExecutionError.InternalError(
                $"Failed to deserialize daemon session JSON: {sessionPath}. {exception.Message}"));
        }

        if (!sessionValidator.TryValidate(session, sessionPath, out var validationError))
        {
            return DaemonSessionReadResult.Failure(validationError!);
        }

        if (!string.Equals(session.ProjectFingerprint, projectFingerprint, StringComparison.Ordinal))
        {
            return DaemonSessionReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon session projectFingerprint mismatch. Requested={projectFingerprint}, Actual={session.ProjectFingerprint}. {sessionPath}"));
        }

        return DaemonSessionReadResult.Success(session);
    }

    /// <summary> Writes daemon session metadata to local storage. </summary>
    /// <param name="storageRoot"> The storage root path. </param>
    /// <param name="session"> The daemon session metadata to persist. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon session storage operation result. </returns>
    public async ValueTask<DaemonSessionStoreOperationResult> Write (
        string storageRoot,
        DaemonSession session,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(session);

        string sessionPath;
        try
        {
            sessionPath = DaemonStoragePathResolver.ResolveSessionPath(storageRoot, session.ProjectFingerprint);
        }
        catch (Exception exception) when (IsPathFormatException(exception))
        {
            return DaemonSessionStoreOperationResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon session path is invalid. {exception.Message}"));
        }

        if (!sessionValidator.TryValidate(session, sessionPath, out var validationError))
        {
            return DaemonSessionStoreOperationResult.Failure(validationError!);
        }

        string json;
        try
        {
            json = sessionSerializer.Serialize(session) + Environment.NewLine;
        }
        catch (Exception exception)
        {
            return DaemonSessionStoreOperationResult.Failure(ExecutionError.InternalError(
                $"Failed to serialize daemon session JSON. {exception.Message}"));
        }

        try
        {
            await sessionFileAccess.WriteAtomically(sessionPath, json, cancellationToken).ConfigureAwait(false);
            return DaemonSessionStoreOperationResult.Success();
        }
        catch (Exception exception) when (IsPathFormatException(exception))
        {
            return DaemonSessionStoreOperationResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon session path is invalid: {sessionPath}. {exception.Message}"));
        }
        catch (Exception exception) when (IsIoFailure(exception))
        {
            return DaemonSessionStoreOperationResult.Failure(ExecutionError.InternalError(
                $"Failed to write daemon session file: {sessionPath}. {exception.Message}"));
        }
    }

    /// <summary> Deletes daemon session metadata for one project fingerprint. </summary>
    /// <param name="storageRoot"> The storage root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon session storage operation result. </returns>
    public async ValueTask<DaemonSessionStoreOperationResult> Delete (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string sessionPath;
        try
        {
            sessionPath = DaemonStoragePathResolver.ResolveSessionPath(storageRoot, projectFingerprint);
        }
        catch (Exception exception) when (IsPathFormatException(exception))
        {
            return DaemonSessionStoreOperationResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon session path is invalid. {exception.Message}"));
        }

        try
        {
            await sessionFileAccess.DeleteIfExists(sessionPath, cancellationToken).ConfigureAwait(false);
            return DaemonSessionStoreOperationResult.Success();
        }
        catch (Exception exception) when (IsPathFormatException(exception))
        {
            return DaemonSessionStoreOperationResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon session path is invalid: {sessionPath}. {exception.Message}"));
        }
        catch (Exception exception) when (IsIoFailure(exception))
        {
            return DaemonSessionStoreOperationResult.Failure(ExecutionError.InternalError(
                $"Failed to delete daemon session file: {sessionPath}. {exception.Message}"));
        }
    }

    /// <summary> Determines whether one exception indicates invalid path format usage. </summary>
    /// <param name="exception"> The exception to classify. </param>
    /// <returns> <see langword="true" /> when exception indicates invalid path formatting; otherwise <see langword="false" />. </returns>
    private static bool IsPathFormatException (Exception exception)
    {
        return exception is ArgumentException
            or NotSupportedException
            or PathTooLongException;
    }

    /// <summary> Determines whether one exception indicates filesystem I/O failure. </summary>
    /// <param name="exception"> The exception to classify. </param>
    /// <returns> <see langword="true" /> when exception indicates I/O failure; otherwise <see langword="false" />. </returns>
    private static bool IsIoFailure (Exception exception)
    {
        return exception is IOException
            or UnauthorizedAccessException;
    }
}
