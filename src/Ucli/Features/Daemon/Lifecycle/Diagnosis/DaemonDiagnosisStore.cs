using System.Text.Json;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;

/// <summary> Implements orchestration for filesystem-backed daemon diagnosis persistence. </summary>
internal sealed class DaemonDiagnosisStore : IDaemonDiagnosisStore
{
    /// <summary> Initializes a new instance of the <see cref="DaemonDiagnosisStore" /> class with default dependencies. </summary>
    public DaemonDiagnosisStore ()
    {
    }

    /// <inheritdoc />
    public async ValueTask<DaemonDiagnosisReadResult> ReadAsync (
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
            json = await FileUtilities.ReadAllTextOrNullAsync(diagnosisPath, cancellationToken).ConfigureAwait(false);
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
            ReportedBy: contract.ReportedBy!,
            IsInferred: contract.IsInferred!.Value,
            UpdatedAtUtc: contract.UpdatedAtUtc,
            ProcessId: contract.ProcessId,
            EditorInstancePath: StringValueNormalizer.TrimToNull(contract.EditorInstancePath),
            SessionIssuedAtUtc: contract.SessionIssuedAtUtc,
            ProcessStartedAtUtc: contract.ProcessStartedAtUtc,
            UnityLogPath: StringValueNormalizer.TrimToNull(contract.UnityLogPath),
            StartupPhase: StringValueNormalizer.TrimToNull(contract.StartupPhase),
            ActionRequired: StringValueNormalizer.TrimToNull(contract.ActionRequired),
            PrimaryDiagnostic: contract.PrimaryDiagnostic is null
                ? null
                : new DaemonPrimaryDiagnostic(
                    Kind: StringValueNormalizer.TrimToNull(contract.PrimaryDiagnostic.Kind)!,
                    Code: StringValueNormalizer.TrimToNull(contract.PrimaryDiagnostic.Code),
                    File: StringValueNormalizer.TrimToNull(contract.PrimaryDiagnostic.File),
                    Line: contract.PrimaryDiagnostic.Line,
                    Column: contract.PrimaryDiagnostic.Column,
                    Message: StringValueNormalizer.TrimToNull(contract.PrimaryDiagnostic.Message)));
        return DaemonDiagnosisReadResult.Success(diagnosis);
    }

    /// <inheritdoc />
    public async ValueTask<DaemonDiagnosisStoreOperationResult> WriteAsync (
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
            ReportedBy: diagnosis.ReportedBy,
            IsInferred: diagnosis.IsInferred,
            UpdatedAtUtc: diagnosis.UpdatedAtUtc,
            ProcessId: diagnosis.ProcessId,
            EditorInstancePath: diagnosis.EditorInstancePath,
            SessionIssuedAtUtc: diagnosis.SessionIssuedAtUtc,
            ProcessStartedAtUtc: diagnosis.ProcessStartedAtUtc,
            UnityLogPath: diagnosis.UnityLogPath,
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
            var diagnosisDirectoryPath = Path.GetDirectoryName(diagnosisPath)
                ?? throw new InvalidOperationException($"Daemon diagnosis directory path could not be resolved: {diagnosisPath}");
            FileSystemAccessBoundary.EnsureSecureDirectory(diagnosisDirectoryPath);
            await FileUtilities.WriteAllTextAtomicallyAsync(diagnosisPath, json, cancellationToken).ConfigureAwait(false);
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
    public async ValueTask<DaemonDiagnosisStoreOperationResult> DeleteAsync (
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

        if (string.IsNullOrWhiteSpace(contract.ReportedBy)
            || !DaemonDiagnosisReportedByValues.IsSupported(contract.ReportedBy))
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

        if (!TryValidateOptionalStartupPhase(contract.StartupPhase, diagnosisPath, out error))
        {
            return false;
        }

        if (!TryValidateOptionalActionRequired(contract.ActionRequired, diagnosisPath, out error))
        {
            return false;
        }

        if (!TryValidatePrimaryDiagnostic(contract.PrimaryDiagnostic, diagnosisPath, out error))
        {
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

        if (string.IsNullOrWhiteSpace(diagnosis.ReportedBy)
            || !DaemonDiagnosisReportedByValues.IsSupported(diagnosis.ReportedBy))
        {
            error = ExecutionError.InvalidArgument($"Daemon diagnosis reportedBy is invalid: {diagnosisPath}");
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

        if (!TryValidateOptionalStartupPhase(diagnosis.StartupPhase, diagnosisPath, out error))
        {
            return false;
        }

        if (!TryValidateOptionalActionRequired(diagnosis.ActionRequired, diagnosisPath, out error))
        {
            return false;
        }

        if (!TryValidatePrimaryDiagnostic(diagnosis.PrimaryDiagnostic, diagnosisPath, out error))
        {
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryValidateOptionalStartupPhase (
        string? startupPhase,
        string diagnosisPath,
        out ExecutionError? error)
    {
        if (StringValueNormalizer.TrimToNull(startupPhase) is not string normalizedStartupPhase)
        {
            error = null;
            return true;
        }

        if (!DaemonDiagnosisStartupPhaseValues.IsSupported(normalizedStartupPhase))
        {
            error = ExecutionError.InvalidArgument($"Daemon diagnosis startupPhase is invalid: {diagnosisPath}");
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryValidateOptionalActionRequired (
        string? actionRequired,
        string diagnosisPath,
        out ExecutionError? error)
    {
        if (StringValueNormalizer.TrimToNull(actionRequired) is not string normalizedActionRequired)
        {
            error = null;
            return true;
        }

        if (!DaemonDiagnosisActionRequiredValues.IsSupported(normalizedActionRequired))
        {
            error = ExecutionError.InvalidArgument($"Daemon diagnosis actionRequired is invalid: {diagnosisPath}");
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryValidatePrimaryDiagnostic (
        DaemonPrimaryDiagnostic? primaryDiagnostic,
        string diagnosisPath,
        out ExecutionError? error)
    {
        if (primaryDiagnostic is null)
        {
            error = null;
            return true;
        }

        return TryValidatePrimaryDiagnosticFields(
            primaryDiagnostic.Kind,
            primaryDiagnostic.Line,
            primaryDiagnostic.Column,
            diagnosisPath,
            out error);
    }

    private static bool TryValidatePrimaryDiagnostic (
        DaemonDiagnosisPrimaryDiagnosticJsonContract? primaryDiagnostic,
        string diagnosisPath,
        out ExecutionError? error)
    {
        if (primaryDiagnostic is null)
        {
            error = null;
            return true;
        }

        return TryValidatePrimaryDiagnosticFields(
            primaryDiagnostic.Kind,
            primaryDiagnostic.Line,
            primaryDiagnostic.Column,
            diagnosisPath,
            out error);
    }

    private static bool TryValidatePrimaryDiagnosticFields (
        string? kind,
        int? line,
        int? column,
        string diagnosisPath,
        out ExecutionError? error)
    {
        if (!StringValueNormalizer.TryTrimToNonEmpty(kind, out var normalizedKind)
            || !DaemonDiagnosisPrimaryDiagnosticKindValues.IsSupported(normalizedKind))
        {
            error = ExecutionError.InvalidArgument($"Daemon diagnosis primaryDiagnostic.kind is invalid: {diagnosisPath}");
            return false;
        }

        if (line is <= 0)
        {
            error = ExecutionError.InvalidArgument($"Daemon diagnosis primaryDiagnostic.line is invalid: {diagnosisPath}");
            return false;
        }

        if (column is <= 0)
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
