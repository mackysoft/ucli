using System.Text.Json;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Session;

/// <summary> Implements orchestration for filesystem-backed daemon session persistence. </summary>
internal sealed class DaemonSessionStore : IDaemonSessionStore
{
    private static readonly TimeSpan SessionLockAcquireTimeout = TimeSpan.FromSeconds(1);

    /// <summary> Reads daemon session metadata for one project fingerprint. </summary>
    /// <param name="storageRoot"> The storage root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon session read result. </returns>
    public async ValueTask<DaemonSessionReadResult> ReadAsync (
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
            json = await FileUtilities.ReadAllTextOrNullAsync(sessionPath, cancellationToken).ConfigureAwait(false);
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
            return DaemonSessionReadResult.Missing();
        }

        var artifactIdentity = DaemonSessionArtifactIdentity.Create(json);

        DaemonSessionJsonContract contract;
        try
        {
            contract = DaemonSessionJsonContractSerializer.Deserialize(json)
                ?? throw new JsonException("Daemon session JSON root must be an object.");
        }
        catch (JsonException exception)
        {
            return DaemonSessionReadResult.Invalid(ExecutionError.InvalidArgument(
                    $"Daemon session JSON is invalid: {sessionPath}. {exception.Message}"),
                invalidEvidence: null,
                artifactIdentity);
        }
        catch (ArgumentException exception)
        {
            return DaemonSessionReadResult.Invalid(ExecutionError.InvalidArgument(
                    $"Daemon session JSON is invalid: {sessionPath}. {exception.Message}"),
                invalidEvidence: null,
                artifactIdentity);
        }
        catch (Exception exception)
        {
            return DaemonSessionReadResult.Failure(ExecutionError.InternalError(
                $"Failed to deserialize daemon session JSON: {sessionPath}. {exception.Message}"),
                DaemonSessionReadFailureKind.InternalFailure,
                artifactIdentity);
        }

        if (!DaemonSessionContractMapper.TryCreate(
                contract,
                projectFingerprint,
                sessionPath,
                out var session,
                out var validationError))
        {
            return DaemonSessionReadResult.Invalid(
                validationError!,
                new DaemonInvalidSessionEvidence(contract),
                artifactIdentity);
        }

        return DaemonSessionReadResult.Found(session, artifactIdentity);
    }

    /// <summary> Writes daemon session metadata to local storage. </summary>
    /// <param name="storageRoot"> The storage root path. </param>
    /// <param name="session"> The daemon session metadata to persist. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon session storage operation result. </returns>
    public async ValueTask<DaemonSessionStoreOperationResult> WriteAsync (
        string storageRoot,
        DaemonSession session,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(session);

        string sessionPath;
        string sessionLockPath;
        try
        {
            sessionPath = UcliStoragePathResolver.ResolveSessionPath(storageRoot, session.ProjectFingerprint);
            sessionLockPath = UcliStoragePathResolver.ResolveDaemonSessionLockPath(
                storageRoot,
                session.ProjectFingerprint);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return DaemonSessionStoreOperationResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon session path is invalid. {exception.Message}"));
        }

        string json;
        try
        {
            json = DaemonSessionJsonContractSerializer.Serialize(
                    DaemonSessionContractMapper.ToContract(session))
                + Environment.NewLine;
        }
        catch (Exception exception)
        {
            return DaemonSessionStoreOperationResult.Failure(ExecutionError.InternalError(
                $"Failed to serialize daemon session JSON. {exception.Message}"));
        }

        try
        {
            using var sessionLock = await FileExclusiveLock.AcquireAsync(
                    sessionLockPath,
                    SessionLockAcquireTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
            var sessionDirectoryPath = Path.GetDirectoryName(sessionPath)
                ?? throw new InvalidOperationException($"Daemon session directory path could not be resolved: {sessionPath}");
            FileSystemAccessBoundary.EnsureSecureDirectory(sessionDirectoryPath);
            await FileUtilities.WriteAllTextAtomicallyAsync(sessionPath, json, cancellationToken).ConfigureAwait(false);
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
    public async ValueTask<DaemonSessionStoreOperationResult> DeleteAsync (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string sessionPath;
        string sessionLockPath;
        try
        {
            sessionPath = UcliStoragePathResolver.ResolveSessionPath(storageRoot, projectFingerprint);
            sessionLockPath = UcliStoragePathResolver.ResolveDaemonSessionLockPath(
                storageRoot,
                projectFingerprint);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return DaemonSessionStoreOperationResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon session path is invalid. {exception.Message}"));
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var sessionLock = await FileExclusiveLock.AcquireAsync(
                    sessionLockPath,
                    SessionLockAcquireTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
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
            or UnauthorizedAccessException
            or TimeoutException;
    }
}
