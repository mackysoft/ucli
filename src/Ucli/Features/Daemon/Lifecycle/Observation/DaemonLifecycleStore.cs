using System.Text.Json;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Observation;

/// <summary> Implements filesystem-backed daemon lifecycle observation persistence. </summary>
internal sealed class DaemonLifecycleStore : IDaemonLifecycleStore
{
    /// <inheritdoc />
    public async ValueTask<DaemonLifecycleObservationReadResult> ReadAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryResolvePath(storageRoot, projectFingerprint, out var path, out var pathError))
        {
            return DaemonLifecycleObservationReadResult.Failure(pathError!);
        }

        string? json;
        try
        {
            json = await FileUtilities.ReadAllTextOrNullAsync(path!, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return DaemonLifecycleObservationReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon lifecycle path is invalid: {path}. {exception.Message}"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return DaemonLifecycleObservationReadResult.Failure(ExecutionError.InternalError(
                $"Failed to read daemon lifecycle file: {path}. {exception.Message}"));
        }

        if (json == null)
        {
            return DaemonLifecycleObservationReadResult.Success(null);
        }

        DaemonLifecycleJsonContract contract;
        try
        {
            contract = DaemonLifecycleJsonContractSerializer.Deserialize(json)
                ?? throw new JsonException("Daemon lifecycle JSON is null.");
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            return DaemonLifecycleObservationReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon lifecycle JSON is invalid: {path}. {exception.Message}"));
        }

        if (!TryCreateObservation(contract, path!, out var observation, out var validationError))
        {
            return DaemonLifecycleObservationReadResult.Failure(validationError!);
        }

        return DaemonLifecycleObservationReadResult.Success(observation);
    }

    /// <inheritdoc />
    public ValueTask<DaemonLifecycleStoreOperationResult> DeleteAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryResolvePath(storageRoot, projectFingerprint, out var path, out var pathError))
        {
            return ValueTask.FromResult(DaemonLifecycleStoreOperationResult.Failure(pathError!));
        }

        try
        {
            FileUtilities.DeleteIfExists(path!);
            return ValueTask.FromResult(DaemonLifecycleStoreOperationResult.Success());
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return ValueTask.FromResult(DaemonLifecycleStoreOperationResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon lifecycle path is invalid: {path}. {exception.Message}")));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ValueTask.FromResult(DaemonLifecycleStoreOperationResult.Failure(ExecutionError.InternalError(
                $"Failed to delete daemon lifecycle file: {path}. {exception.Message}")));
        }
    }

    private static bool TryCreateObservation (
        DaemonLifecycleJsonContract contract,
        string path,
        out DaemonLifecycleObservation? observation,
        out ExecutionError? error)
    {
        observation = null;
        error = null;

        if (contract.ProcessId is not int processId || processId <= 0)
        {
            error = ExecutionError.InvalidArgument($"Daemon lifecycle processId is invalid: {path}.");
            return false;
        }

        if (contract.ProcessStartedAtUtc is not DateTimeOffset processStartedAtUtc || processStartedAtUtc == default)
        {
            error = ExecutionError.InvalidArgument($"Daemon lifecycle processStartedAtUtc is invalid: {path}.");
            return false;
        }

        if (contract.State is not UnityEditorStateSnapshot state)
        {
            error = ExecutionError.InvalidArgument($"Daemon lifecycle state is invalid: {path}.");
            return false;
        }

        var actionRequired = StringValueNormalizer.TrimToNull(contract.ActionRequired);
        if (actionRequired is not null && !DaemonDiagnosisActionRequiredValues.IsSupported(actionRequired))
        {
            error = ExecutionError.InvalidArgument($"Daemon lifecycle actionRequired is invalid: {path}.");
            return false;
        }

        if (!TryValidatePrimaryDiagnostic(contract.PrimaryDiagnostic, path, out var primaryDiagnostic, out error))
        {
            return false;
        }

        if (contract.ObservedAtUtc is not DateTimeOffset observedAtUtc || observedAtUtc == default)
        {
            error = ExecutionError.InvalidArgument($"Daemon lifecycle observedAtUtc is invalid: {path}.");
            return false;
        }

        if (contract.EditorInstanceId is not Guid editorInstanceId || editorInstanceId == Guid.Empty)
        {
            error = ExecutionError.InvalidArgument($"Daemon lifecycle editorInstanceId is invalid: {path}.");
            return false;
        }

        observation = new DaemonLifecycleObservation(
            processId: processId,
            processStartedAtUtc: processStartedAtUtc,
            state: state,
            observedAtUtc: observedAtUtc,
            actionRequired: actionRequired,
            primaryDiagnostic: primaryDiagnostic,
            serverVersion: StringValueNormalizer.TrimToNull(contract.ServerVersion),
            editorInstanceId: editorInstanceId);
        return true;
    }

    private static bool TryValidatePrimaryDiagnostic (
        IpcPrimaryDiagnostic? primaryDiagnostic,
        string path,
        out IpcPrimaryDiagnostic? normalizedDiagnostic,
        out ExecutionError? error)
    {
        normalizedDiagnostic = null;
        if (primaryDiagnostic is null)
        {
            error = null;
            return true;
        }

        if (!StringValueNormalizer.TryTrimToNonEmpty(primaryDiagnostic.Kind, out var kind)
            || !DaemonDiagnosisPrimaryDiagnosticKindValues.IsSupported(kind))
        {
            error = ExecutionError.InvalidArgument($"Daemon lifecycle primaryDiagnostic.kind is invalid: {path}.");
            return false;
        }

        if (primaryDiagnostic.Line is <= 0)
        {
            error = ExecutionError.InvalidArgument($"Daemon lifecycle primaryDiagnostic.line is invalid: {path}.");
            return false;
        }

        if (primaryDiagnostic.Column is <= 0)
        {
            error = ExecutionError.InvalidArgument($"Daemon lifecycle primaryDiagnostic.column is invalid: {path}.");
            return false;
        }

        normalizedDiagnostic = new IpcPrimaryDiagnostic(
            Kind: kind,
            Code: StringValueNormalizer.TrimToNull(primaryDiagnostic.Code),
            File: StringValueNormalizer.TrimToNull(primaryDiagnostic.File),
            Line: primaryDiagnostic.Line,
            Column: primaryDiagnostic.Column,
            Message: StringValueNormalizer.TrimToNull(primaryDiagnostic.Message));
        error = null;
        return true;
    }

    private static bool TryResolvePath (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        out string? path,
        out ExecutionError? error)
    {
        try
        {
            path = UcliStoragePathResolver.ResolveDaemonLifecyclePath(storageRoot, projectFingerprint);
            error = null;
            return true;
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            path = null;
            error = ExecutionError.InvalidArgument($"Daemon lifecycle path is invalid. {exception.Message}");
            return false;
        }
    }
}
