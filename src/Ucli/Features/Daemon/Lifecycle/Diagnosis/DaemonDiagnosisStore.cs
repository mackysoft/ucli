using System.Text.Json;
using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;

/// <summary> Implements orchestration for filesystem-backed daemon diagnosis persistence. </summary>
internal sealed class DaemonDiagnosisStore : IDaemonDiagnosisStore
{
    /// <inheritdoc />
    public async ValueTask<DaemonDiagnosisReadResult> ReadAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var diagnosisPath = UcliStoragePathResolver.ResolveDaemonDiagnosisPath(storageRoot, projectFingerprint);

        string? json;
        try
        {
            json = await FileUtilities.ReadAllTextOrNullAsync(diagnosisPath, cancellationToken).ConfigureAwait(false);
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
                $"Daemon diagnosis JSON is invalid: {diagnosisPath}. Field={exception.Path ?? "$"}. {exception.Message}"));
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

        if (!TryParseOptionalAbsolutePath(contract.EditorInstancePath, out var editorInstancePath))
        {
            return DaemonDiagnosisReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon diagnosis editorInstancePath is invalid: {diagnosisPath}"));
        }

        if (!TryParseOptionalAbsolutePath(contract.UnityLogPath, out var unityLogPath))
        {
            return DaemonDiagnosisReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon diagnosis unityLogPath is invalid: {diagnosisPath}"));
        }

        var diagnosis = new DaemonDiagnosis(
            Reason: contract.Reason!.Value,
            Message: contract.Message!,
            ReportedBy: contract.ReportedBy!.Value,
            IsInferred: contract.IsInferred!.Value,
            UpdatedAtUtc: contract.UpdatedAtUtc,
            ProcessId: contract.ProcessId,
            EditorInstancePath: editorInstancePath,
            SessionIssuedAtUtc: contract.SessionIssuedAtUtc,
            ProcessStartedAtUtc: contract.ProcessStartedAtUtc,
            UnityLogPath: unityLogPath,
            StartupPhase: contract.StartupPhase,
            ActionRequired: contract.ActionRequired,
            PrimaryDiagnostic: contract.PrimaryDiagnostic is null
                ? null
                : new DaemonPrimaryDiagnostic(
                    Kind: contract.PrimaryDiagnostic.Kind!.Value,
                    Code: StringValueNormalizer.TrimToNull(contract.PrimaryDiagnostic.Code),
                    File: StringValueNormalizer.TrimToNull(contract.PrimaryDiagnostic.File),
                    Line: contract.PrimaryDiagnostic.Line,
                    Column: contract.PrimaryDiagnostic.Column,
                    Message: StringValueNormalizer.TrimToNull(contract.PrimaryDiagnostic.Message)));
        return DaemonDiagnosisReadResult.Success(diagnosis);
    }

    /// <inheritdoc />
    public async ValueTask<DaemonDiagnosisStoreOperationResult> WriteAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        DaemonDiagnosis diagnosis,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(diagnosis);

        var diagnosisPath = UcliStoragePathResolver.ResolveDaemonDiagnosisPath(storageRoot, projectFingerprint);

        var contract = new DaemonDiagnosisJsonContract(
            Reason: diagnosis.Reason,
            Message: diagnosis.Message,
            ReportedBy: diagnosis.ReportedBy,
            IsInferred: diagnosis.IsInferred,
            UpdatedAtUtc: diagnosis.UpdatedAtUtc,
            ProcessId: diagnosis.ProcessId,
            EditorInstancePath: diagnosis.EditorInstancePath?.Value,
            SessionIssuedAtUtc: diagnosis.SessionIssuedAtUtc,
            ProcessStartedAtUtc: diagnosis.ProcessStartedAtUtc,
            UnityLogPath: diagnosis.UnityLogPath?.Value,
            StartupPhase: diagnosis.StartupPhase,
            ActionRequired: diagnosis.ActionRequired,
            PrimaryDiagnostic: diagnosis.PrimaryDiagnostic is null
                ? null
                : new DaemonDiagnosisPrimaryDiagnosticJsonContract(
                    Kind: diagnosis.PrimaryDiagnostic.Kind,
                    Code: diagnosis.PrimaryDiagnostic.Code,
                    File: diagnosis.PrimaryDiagnostic.File,
                    Line: diagnosis.PrimaryDiagnostic.Line,
                    Column: diagnosis.PrimaryDiagnostic.Column,
                    Message: diagnosis.PrimaryDiagnostic.Message));

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
            var diagnosisDirectoryPath = UcliStoragePathResolver.ResolveProjectDirectory(
                storageRoot,
                projectFingerprint);
            FileSystemAccessBoundary.EnsureSecureDirectory(diagnosisDirectoryPath);
            await FileUtilities.WriteAllTextAtomicallyAsync(diagnosisPath, json, cancellationToken).ConfigureAwait(false);
            return DaemonDiagnosisStoreOperationResult.Success();
        }
        catch (Exception exception) when (IsIoFailure(exception))
        {
            return DaemonDiagnosisStoreOperationResult.Failure(ExecutionError.InternalError(
                $"Failed to write daemon diagnosis file: {diagnosisPath}. {exception.Message}"));
        }
    }

    /// <inheritdoc />
    public async ValueTask<DaemonDiagnosisStoreOperationResult> DeleteAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var diagnosisPath = UcliStoragePathResolver.ResolveDaemonDiagnosisPath(storageRoot, projectFingerprint);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            FileUtilities.DeleteIfExists(diagnosisPath);
            return DaemonDiagnosisStoreOperationResult.Success();
        }
        catch (Exception exception) when (IsIoFailure(exception))
        {
            return DaemonDiagnosisStoreOperationResult.Failure(ExecutionError.InternalError(
                $"Failed to delete daemon diagnosis file: {diagnosisPath}. {exception.Message}"));
        }
    }

    private static bool TryValidate (
        DaemonDiagnosisJsonContract contract,
        AbsolutePath diagnosisPath,
        out ExecutionError? error)
    {
        if (!contract.Reason.HasValue)
        {
            error = ExecutionError.InvalidArgument($"Daemon diagnosis reason is invalid: {diagnosisPath}");
            return false;
        }

        if (!StringValueNormalizer.TryTrimToNonEmpty(contract.Message, out _))
        {
            error = ExecutionError.InvalidArgument($"Daemon diagnosis message is invalid: {diagnosisPath}");
            return false;
        }

        if (!contract.ReportedBy.HasValue)
        {
            error = ExecutionError.InvalidArgument($"Daemon diagnosis reportedBy is invalid: {diagnosisPath}");
            return false;
        }

        if (contract.IsInferred is null)
        {
            error = ExecutionError.InvalidArgument($"Daemon diagnosis isInferred is invalid: {diagnosisPath}");
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

        if (!TryValidatePrimaryDiagnostic(contract.PrimaryDiagnostic, diagnosisPath, out error))
        {
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryParseOptionalAbsolutePath (
        string? value,
        out AbsolutePath? path)
    {
        path = null;
        var normalizedValue = StringValueNormalizer.TrimToNull(value);
        return normalizedValue is null
            || AbsolutePath.TryParse(normalizedValue, out path, out _);
    }

    private static bool TryValidatePrimaryDiagnostic (
        DaemonDiagnosisPrimaryDiagnosticJsonContract? primaryDiagnostic,
        AbsolutePath diagnosisPath,
        out ExecutionError? error)
    {
        if (primaryDiagnostic is null)
        {
            error = null;
            return true;
        }

        if (!primaryDiagnostic.Kind.HasValue)
        {
            error = ExecutionError.InvalidArgument($"Daemon diagnosis primaryDiagnostic.kind is invalid: {diagnosisPath}");
            return false;
        }

        if (primaryDiagnostic.Line is <= 0)
        {
            error = ExecutionError.InvalidArgument($"Daemon diagnosis primaryDiagnostic.line is invalid: {diagnosisPath}");
            return false;
        }

        if (primaryDiagnostic.Column is <= 0)
        {
            error = ExecutionError.InvalidArgument($"Daemon diagnosis primaryDiagnostic.column is invalid: {diagnosisPath}");
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
