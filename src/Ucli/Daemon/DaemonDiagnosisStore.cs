using System.Text.Json;
using MackySoft.Ucli.Contracts.Paths;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Daemon;

/// <summary> Implements orchestration for filesystem-backed daemon diagnosis persistence. </summary>
internal sealed class DaemonDiagnosisStore : IDaemonDiagnosisStore
{
    /// <summary> Initializes a new instance of the <see cref="DaemonDiagnosisStore" /> class with default dependencies. </summary>
    public DaemonDiagnosisStore ()
    {
    }

    /// <inheritdoc />
    public async ValueTask<DaemonDiagnosisReadResult> Read (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string diagnosisPath;
        try
        {
            diagnosisPath = UcliStoragePathResolver.ResolveDaemonDiagnosisPath(storageRoot, projectFingerprint);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return DaemonDiagnosisReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon diagnosis path is invalid. {exception.Message}"));
        }

        string? json;
        try
        {
            json = await FileUtilities.ReadAllTextOrNull(diagnosisPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return DaemonDiagnosisReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon diagnosis path is invalid: {diagnosisPath}. {exception.Message}"));
        }
        catch (Exception exception) when (IsIoFailure(exception))
        {
            return DaemonDiagnosisReadResult.Failure(ExecutionError.InternalError(
                $"Failed to read daemon diagnosis file: {diagnosisPath}. {exception.Message}"));
        }

        if (json == null)
        {
            return DaemonDiagnosisReadResult.Success(null);
        }

        DaemonDiagnosisJsonContract contract;
        try
        {
            contract = DaemonDiagnosisJsonContractSerializer.Deserialize(json)
                ?? throw new JsonException("Daemon diagnosis JSON is null.");
        }
        catch (JsonException exception)
        {
            return DaemonDiagnosisReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon diagnosis JSON is invalid: {diagnosisPath}. {exception.Message}"));
        }
        catch (ArgumentException exception)
        {
            return DaemonDiagnosisReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon diagnosis JSON is invalid: {diagnosisPath}. {exception.Message}"));
        }
        catch (Exception exception)
        {
            return DaemonDiagnosisReadResult.Failure(ExecutionError.InternalError(
                $"Failed to deserialize daemon diagnosis JSON: {diagnosisPath}. {exception.Message}"));
        }

        if (!TryValidate(contract, diagnosisPath, out var validationError))
        {
            return DaemonDiagnosisReadResult.Failure(validationError!);
        }

        var diagnosis = new DaemonDiagnosis(
            Reason: contract.Reason!,
            Message: contract.Message!,
            UpdatedAtUtc: contract.UpdatedAtUtc,
            ProcessId: contract.ProcessId,
            SessionIssuedAtUtc: contract.SessionIssuedAtUtc);
        return DaemonDiagnosisReadResult.Success(diagnosis);
    }

    /// <inheritdoc />
    public async ValueTask<DaemonDiagnosisStoreOperationResult> Write (
        string storageRoot,
        string projectFingerprint,
        DaemonDiagnosis diagnosis,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(diagnosis);

        string diagnosisPath;
        try
        {
            diagnosisPath = UcliStoragePathResolver.ResolveDaemonDiagnosisPath(storageRoot, projectFingerprint);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return DaemonDiagnosisStoreOperationResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon diagnosis path is invalid. {exception.Message}"));
        }

        if (!TryValidate(diagnosis, diagnosisPath, out var validationError))
        {
            return DaemonDiagnosisStoreOperationResult.Failure(validationError!);
        }

        var contract = new DaemonDiagnosisJsonContract(
            Reason: diagnosis.Reason,
            Message: diagnosis.Message,
            UpdatedAtUtc: diagnosis.UpdatedAtUtc,
            ProcessId: diagnosis.ProcessId,
            SessionIssuedAtUtc: diagnosis.SessionIssuedAtUtc);

        string json;
        try
        {
            json = DaemonDiagnosisJsonContractSerializer.Serialize(contract) + Environment.NewLine;
        }
        catch (Exception exception)
        {
            return DaemonDiagnosisStoreOperationResult.Failure(ExecutionError.InternalError(
                $"Failed to serialize daemon diagnosis JSON. {exception.Message}"));
        }

        try
        {
            await FileUtilities.WriteAllTextAtomically(diagnosisPath, json, cancellationToken).ConfigureAwait(false);
            return DaemonDiagnosisStoreOperationResult.Success();
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return DaemonDiagnosisStoreOperationResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon diagnosis path is invalid: {diagnosisPath}. {exception.Message}"));
        }
        catch (Exception exception) when (IsIoFailure(exception))
        {
            return DaemonDiagnosisStoreOperationResult.Failure(ExecutionError.InternalError(
                $"Failed to write daemon diagnosis file: {diagnosisPath}. {exception.Message}"));
        }
    }

    /// <inheritdoc />
    public async ValueTask<DaemonDiagnosisStoreOperationResult> Delete (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string diagnosisPath;
        try
        {
            diagnosisPath = UcliStoragePathResolver.ResolveDaemonDiagnosisPath(storageRoot, projectFingerprint);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return DaemonDiagnosisStoreOperationResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon diagnosis path is invalid. {exception.Message}"));
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            FileUtilities.DeleteIfExists(diagnosisPath);
            return DaemonDiagnosisStoreOperationResult.Success();
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return DaemonDiagnosisStoreOperationResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon diagnosis path is invalid: {diagnosisPath}. {exception.Message}"));
        }
        catch (Exception exception) when (IsIoFailure(exception))
        {
            return DaemonDiagnosisStoreOperationResult.Failure(ExecutionError.InternalError(
                $"Failed to delete daemon diagnosis file: {diagnosisPath}. {exception.Message}"));
        }
    }

    private static bool TryValidate (
        DaemonDiagnosisJsonContract contract,
        string diagnosisPath,
        out ExecutionError? error)
    {
        if (!StringValueNormalizer.TryTrimToNonEmpty(contract.Reason, out _))
        {
            error = ExecutionError.InvalidArgument($"Daemon diagnosis reason is invalid: {diagnosisPath}");
            return false;
        }

        if (!StringValueNormalizer.TryTrimToNonEmpty(contract.Message, out _))
        {
            error = ExecutionError.InvalidArgument($"Daemon diagnosis message is invalid: {diagnosisPath}");
            return false;
        }

        if (contract.UpdatedAtUtc == default)
        {
            error = ExecutionError.InvalidArgument($"Daemon diagnosis updatedAtUtc is invalid: {diagnosisPath}");
            return false;
        }

        if (contract.SessionIssuedAtUtc == default)
        {
            error = ExecutionError.InvalidArgument($"Daemon diagnosis sessionIssuedAtUtc is invalid: {diagnosisPath}");
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryValidate (
        DaemonDiagnosis diagnosis,
        string diagnosisPath,
        out ExecutionError? error)
    {
        if (!StringValueNormalizer.TryTrimToNonEmpty(diagnosis.Reason, out _))
        {
            error = ExecutionError.InvalidArgument($"Daemon diagnosis reason is invalid: {diagnosisPath}");
            return false;
        }

        if (!StringValueNormalizer.TryTrimToNonEmpty(diagnosis.Message, out _))
        {
            error = ExecutionError.InvalidArgument($"Daemon diagnosis message is invalid: {diagnosisPath}");
            return false;
        }

        if (diagnosis.UpdatedAtUtc == default)
        {
            error = ExecutionError.InvalidArgument($"Daemon diagnosis updatedAtUtc is invalid: {diagnosisPath}");
            return false;
        }

        if (diagnosis.SessionIssuedAtUtc == default)
        {
            error = ExecutionError.InvalidArgument($"Daemon diagnosis sessionIssuedAtUtc is invalid: {diagnosisPath}");
            return false;
        }

        error = null;
        return true;
    }

    private static bool IsIoFailure (Exception exception)
    {
        return exception is IOException
            or UnauthorizedAccessException;
    }

}
