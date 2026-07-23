using System.Text.Json;
using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Observation;

/// <summary> Implements filesystem-backed daemon lifecycle observation persistence. </summary>
internal sealed class DaemonLifecycleStore : IDaemonLifecycleStore
{
    /// <inheritdoc />
    public async ValueTask<DaemonLifecycleObservationReadResult> ReadAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var path = UcliStoragePathResolver.ResolveDaemonLifecyclePath(storageRoot, projectFingerprint);

        string? json;
        try
        {
            json = await FileUtilities.ReadAllTextOrNullAsync(path, cancellationToken).ConfigureAwait(false);
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
        catch (JsonException exception)
        {
            return DaemonLifecycleObservationReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon lifecycle JSON is invalid: {path}. Field={exception.Path ?? "$"}. {exception.Message}"));
        }
        catch (ArgumentException exception)
        {
            return DaemonLifecycleObservationReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon lifecycle JSON is invalid: {path}. {exception.Message}"));
        }

        if (!TryCreateObservation(contract, path, out var observation, out var validationError))
        {
            return DaemonLifecycleObservationReadResult.Failure(validationError!);
        }

        return DaemonLifecycleObservationReadResult.Success(observation);
    }

    /// <inheritdoc />
    public ValueTask<DaemonLifecycleStoreOperationResult> DeleteAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var path = UcliStoragePathResolver.ResolveDaemonLifecyclePath(storageRoot, projectFingerprint);

        try
        {
            FileUtilities.DeleteIfExists(path);
            return ValueTask.FromResult(DaemonLifecycleStoreOperationResult.Success());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ValueTask.FromResult(DaemonLifecycleStoreOperationResult.Failure(ExecutionError.InternalError(
                $"Failed to delete daemon lifecycle file: {path}. {exception.Message}")));
        }
    }

    private static bool TryCreateObservation (
        DaemonLifecycleJsonContract contract,
        AbsolutePath path,
        out DaemonLifecycleObservation? observation,
        out ExecutionError? error)
    {
        observation = null;
        error = null;

        if (contract.ProcessId is not int processId)
        {
            error = ExecutionError.InvalidArgument($"Daemon lifecycle processId is invalid: {path}.");
            return false;
        }

        if (contract.ProcessStartedAtUtc is not DateTimeOffset processStartedAtUtc)
        {
            error = ExecutionError.InvalidArgument($"Daemon lifecycle processStartedAtUtc is invalid: {path}.");
            return false;
        }

        if (contract.State is not UnityEditorStateSnapshot state)
        {
            error = ExecutionError.InvalidArgument($"Daemon lifecycle state is invalid: {path}.");
            return false;
        }

        if (!TryValidatePrimaryDiagnostic(contract.PrimaryDiagnostic, path, out var primaryDiagnostic, out error))
        {
            return false;
        }

        if (contract.ObservedAtUtc is not DateTimeOffset observedAtUtc)
        {
            error = ExecutionError.InvalidArgument($"Daemon lifecycle observedAtUtc is invalid: {path}.");
            return false;
        }

        if (contract.EditorInstanceId is not Guid editorInstanceId)
        {
            error = ExecutionError.InvalidArgument($"Daemon lifecycle editorInstanceId is invalid: {path}.");
            return false;
        }

        observation = new DaemonLifecycleObservation(
            processId: processId,
            processStartedAtUtc: processStartedAtUtc,
            state: state,
            observedAtUtc: observedAtUtc,
            actionRequired: contract.ActionRequired,
            primaryDiagnostic: primaryDiagnostic,
            serverVersion: StringValueNormalizer.TrimToNull(contract.ServerVersion),
            editorInstanceId: editorInstanceId,
            recoveryLease: contract.RecoveryLease);
        return true;
    }

    private static bool TryValidatePrimaryDiagnostic (
        IpcPrimaryDiagnostic? primaryDiagnostic,
        AbsolutePath path,
        out IpcPrimaryDiagnostic? normalizedDiagnostic,
        out ExecutionError? error)
    {
        normalizedDiagnostic = null;
        if (primaryDiagnostic is null)
        {
            error = null;
            return true;
        }

        if (!primaryDiagnostic.Kind.HasValue)
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
            Kind: primaryDiagnostic.Kind.Value,
            Code: StringValueNormalizer.TrimToNull(primaryDiagnostic.Code),
            File: StringValueNormalizer.TrimToNull(primaryDiagnostic.File),
            Line: primaryDiagnostic.Line,
            Column: primaryDiagnostic.Column,
            Message: StringValueNormalizer.TrimToNull(primaryDiagnostic.Message));
        error = null;
        return true;
    }

}
