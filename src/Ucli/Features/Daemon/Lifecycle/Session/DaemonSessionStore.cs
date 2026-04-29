using System.Text.Json;
using MackySoft.Ucli.Contracts.Paths;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Features.Daemon.UseCases.Stop;
using MackySoft.Ucli.Shared.Foundation;
using MackySoft.Ucli.Shared.Storage;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Session;

/// <summary> Implements orchestration for filesystem-backed daemon session persistence. </summary>
internal sealed class DaemonSessionStore : IDaemonSessionStore
{
    private readonly IDaemonSessionSerializer sessionSerializer;

    private readonly IDaemonSessionValidator sessionValidator;

    /// <summary> Initializes a new instance of the <see cref="DaemonSessionStore" /> class with default dependencies. </summary>
    public DaemonSessionStore ()
        : this(
            new DaemonSessionJsonSerializer(),
            new DaemonSessionValidator())
    {
    }

    /// <summary> Initializes a new instance of the <see cref="DaemonSessionStore" /> class. </summary>
    /// <param name="sessionSerializer"> The daemon session serializer dependency. </param>
    /// <param name="sessionValidator"> The daemon session validator dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonSessionStore (
        IDaemonSessionSerializer sessionSerializer,
        IDaemonSessionValidator sessionValidator)
    {
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
            sessionPath = UcliStoragePathResolver.ResolveSessionPath(storageRoot, projectFingerprint);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return DaemonSessionReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon session path is invalid. {exception.Message}"),
                DaemonSessionReadFailureKind.PathInvalid);
        }

        string? json;
        try
        {
            json = await FileUtilities.ReadAllTextOrNull(sessionPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return DaemonSessionReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon session path is invalid: {sessionPath}. {exception.Message}"),
                DaemonSessionReadFailureKind.PathInvalid);
        }
        catch (Exception exception) when (IsIoFailure(exception))
        {
            return DaemonSessionReadResult.Failure(ExecutionError.InternalError(
                $"Failed to read daemon session file: {sessionPath}. {exception.Message}"),
                DaemonSessionReadFailureKind.IoFailure);
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
                $"Daemon session JSON is invalid: {sessionPath}. {exception.Message}"),
                DaemonSessionReadFailureKind.InvalidSession);
        }
        catch (ArgumentException exception)
        {
            return DaemonSessionReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon session JSON is invalid: {sessionPath}. {exception.Message}"),
                DaemonSessionReadFailureKind.InvalidSession);
        }
        catch (Exception exception)
        {
            return DaemonSessionReadResult.Failure(ExecutionError.InternalError(
                $"Failed to deserialize daemon session JSON: {sessionPath}. {exception.Message}"),
                DaemonSessionReadFailureKind.InternalFailure);
        }

        if (!sessionValidator.TryValidate(session, sessionPath, out var validationError))
        {
            return DaemonSessionReadResult.Failure(
                validationError!,
                DaemonSessionReadFailureKind.InvalidSession,
                session);
        }

        if (!string.Equals(session.ProjectFingerprint, projectFingerprint, StringComparison.Ordinal))
        {
            return DaemonSessionReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon session projectFingerprint mismatch. Requested={projectFingerprint}, Actual={session.ProjectFingerprint}. {sessionPath}"),
                DaemonSessionReadFailureKind.InvalidSession,
                session);
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
            sessionPath = UcliStoragePathResolver.ResolveSessionPath(storageRoot, session.ProjectFingerprint);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
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
            var sessionDirectoryPath = Path.GetDirectoryName(sessionPath)
                ?? throw new InvalidOperationException($"Daemon session directory path could not be resolved: {sessionPath}");
            FileSystemAccessBoundary.EnsureSecureDirectory(sessionDirectoryPath);
            await FileUtilities.WriteAllTextAtomically(sessionPath, json, cancellationToken).ConfigureAwait(false);
            FileSystemAccessBoundary.EnsureSecureFile(sessionPath);
            return DaemonSessionStoreOperationResult.Success();
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
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
            sessionPath = UcliStoragePathResolver.ResolveSessionPath(storageRoot, projectFingerprint);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return DaemonSessionStoreOperationResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon session path is invalid. {exception.Message}"));
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            FileUtilities.DeleteIfExists(sessionPath);
            return DaemonSessionStoreOperationResult.Success();
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
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

    /// <summary> Determines whether one exception indicates filesystem I/O failure. </summary>
    /// <param name="exception"> The exception to classify. </param>
    /// <returns> <see langword="true" /> when exception indicates I/O failure; otherwise <see langword="false" />. </returns>
    private static bool IsIoFailure (Exception exception)
    {
        return exception is IOException
            or UnauthorizedAccessException;
    }

}
