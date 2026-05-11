using System.Text.Json;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.LaunchAttempts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Startup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Startup;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.LaunchAttempts;

/// <summary> Implements filesystem-backed daemon launch-attempt artifact persistence. </summary>
internal sealed class DaemonLaunchAttemptStore : IDaemonLaunchAttemptStore
{
    private const int SchemaVersion = 1;

    /// <inheritdoc />
    public async ValueTask<DaemonLaunchAttemptStoreOperationResult> WriteFailureAsync (
        string storageRoot,
        string projectFingerprint,
        DaemonLaunchAttempt launchAttempt,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(launchAttempt);

        string diagnosisPath;
        try
        {
            diagnosisPath = UcliStoragePathResolver.ResolveLaunchAttemptStartupDiagnosisPath(
                storageRoot,
                projectFingerprint,
                launchAttempt.LaunchAttemptId);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return DaemonLaunchAttemptStoreOperationResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon launch-attempt path is invalid. {exception.Message}"));
        }

        var normalizedLaunchAttempt = launchAttempt with
        {
            ArtifactPath = diagnosisPath,
        };
        if (!TryValidate(normalizedLaunchAttempt, diagnosisPath, out var validationError))
        {
            return DaemonLaunchAttemptStoreOperationResult.Failure(validationError!);
        }

        var contract = ToContract(normalizedLaunchAttempt);
        string json;
        try
        {
            json = DaemonLaunchAttemptJsonContractSerializer.Serialize(contract) + Environment.NewLine;
        }
        catch (Exception exception)
        {
            return DaemonLaunchAttemptStoreOperationResult.Failure(ExecutionError.InternalError(
                $"Failed to serialize daemon launch-attempt JSON. {exception.Message}"));
        }

        try
        {
            var directoryPath = Path.GetDirectoryName(diagnosisPath)
                ?? throw new InvalidOperationException($"Daemon launch-attempt directory path could not be resolved: {diagnosisPath}");
            FileSystemAccessBoundary.EnsureSecureDirectory(directoryPath);
            await FileUtilities.WriteAllTextAtomicallyAsync(diagnosisPath, json, cancellationToken).ConfigureAwait(false);
            return DaemonLaunchAttemptStoreOperationResult.Success();
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return DaemonLaunchAttemptStoreOperationResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon launch-attempt path is invalid: {diagnosisPath}. {exception.Message}"));
        }
        catch (Exception exception) when (IsIoFailure(exception))
        {
            return DaemonLaunchAttemptStoreOperationResult.Failure(ExecutionError.InternalError(
                $"Failed to write daemon launch-attempt file: {diagnosisPath}. {exception.Message}"));
        }
    }

    /// <inheritdoc />
    public async ValueTask<DaemonLaunchAttemptReadResult> ReadLastFailureAsync (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string attemptsDirectory;
        try
        {
            attemptsDirectory = UcliStoragePathResolver.ResolveLaunchAttemptsDirectory(storageRoot, projectFingerprint);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return DaemonLaunchAttemptReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon launch-attempt path is invalid. {exception.Message}"));
        }

        if (!Directory.Exists(attemptsDirectory))
        {
            return DaemonLaunchAttemptReadResult.Success(null);
        }

        var candidates = Directory.EnumerateDirectories(attemptsDirectory)
            .OrderByDescending(static path => Directory.GetLastWriteTimeUtc(path))
            .ThenByDescending(static path => Path.GetFileName(path), StringComparer.Ordinal)
            .ToArray();
        foreach (var attemptDirectory in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var launchAttemptId = Path.GetFileName(attemptDirectory);
            string diagnosisPath;
            try
            {
                diagnosisPath = UcliStoragePathResolver.ResolveLaunchAttemptStartupDiagnosisPath(
                    storageRoot,
                    projectFingerprint,
                    launchAttemptId);
            }
            catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
            {
                return DaemonLaunchAttemptReadResult.Failure(ExecutionError.InvalidArgument(
                    $"Daemon launch-attempt path is invalid. {exception.Message}"));
            }

            var readResult = await ReadOneAsync(diagnosisPath, cancellationToken).ConfigureAwait(false);
            if (!readResult.IsSuccess)
            {
                return readResult;
            }

            if (readResult.Exists && IsPublicFailureStatus(readResult.LaunchAttempt!.StartupStatus))
            {
                return readResult;
            }
        }

        return DaemonLaunchAttemptReadResult.Success(null);
    }

    /// <inheritdoc />
    public ValueTask<DaemonLaunchAttemptStoreOperationResult> PruneAsync (
        string storageRoot,
        string projectFingerprint,
        int keepCount,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentOutOfRangeException.ThrowIfNegative(keepCount);

        string attemptsDirectory;
        try
        {
            attemptsDirectory = UcliStoragePathResolver.ResolveLaunchAttemptsDirectory(storageRoot, projectFingerprint);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return ValueTask.FromResult(DaemonLaunchAttemptStoreOperationResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon launch-attempt path is invalid. {exception.Message}")));
        }

        if (!Directory.Exists(attemptsDirectory))
        {
            return ValueTask.FromResult(DaemonLaunchAttemptStoreOperationResult.Success());
        }

        try
        {
            var attemptsToDelete = Directory.EnumerateDirectories(attemptsDirectory)
                .OrderByDescending(static path => Directory.GetLastWriteTimeUtc(path))
                .ThenByDescending(static path => Path.GetFileName(path), StringComparer.Ordinal)
                .Skip(keepCount)
                .ToArray();
            var deletedCount = 0;
            foreach (var attemptDirectory in attemptsToDelete)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Directory.Delete(attemptDirectory, recursive: true);
                deletedCount++;
            }

            return ValueTask.FromResult(DaemonLaunchAttemptStoreOperationResult.Success(deletedCount));
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return ValueTask.FromResult(DaemonLaunchAttemptStoreOperationResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon launch-attempt path is invalid: {attemptsDirectory}. {exception.Message}")));
        }
        catch (Exception exception) when (IsIoFailure(exception))
        {
            return ValueTask.FromResult(DaemonLaunchAttemptStoreOperationResult.Failure(ExecutionError.InternalError(
                $"Failed to prune daemon launch-attempt artifacts: {attemptsDirectory}. {exception.Message}")));
        }
    }

    private static async ValueTask<DaemonLaunchAttemptReadResult> ReadOneAsync (
        string diagnosisPath,
        CancellationToken cancellationToken)
    {
        string? json;
        try
        {
            json = await FileUtilities.ReadAllTextOrNullAsync(diagnosisPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return DaemonLaunchAttemptReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon launch-attempt path is invalid: {diagnosisPath}. {exception.Message}"));
        }
        catch (Exception exception) when (IsIoFailure(exception))
        {
            return DaemonLaunchAttemptReadResult.Failure(ExecutionError.InternalError(
                $"Failed to read daemon launch-attempt file: {diagnosisPath}. {exception.Message}"));
        }

        if (json is null)
        {
            return DaemonLaunchAttemptReadResult.Success(null);
        }

        DaemonLaunchAttemptJsonContract contract;
        try
        {
            contract = DaemonLaunchAttemptJsonContractSerializer.Deserialize(json)
                ?? throw new JsonException("Daemon launch-attempt JSON is null.");
        }
        catch (JsonException exception)
        {
            return DaemonLaunchAttemptReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon launch-attempt JSON is invalid: {diagnosisPath}. {exception.Message}"));
        }
        catch (ArgumentException exception)
        {
            return DaemonLaunchAttemptReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon launch-attempt JSON is invalid: {diagnosisPath}. {exception.Message}"));
        }

        if (!TryValidate(contract, diagnosisPath, out var validationError))
        {
            return DaemonLaunchAttemptReadResult.Failure(validationError!);
        }

        return DaemonLaunchAttemptReadResult.Success(ToModel(contract, diagnosisPath));
    }

    private static DaemonLaunchAttemptJsonContract ToContract (DaemonLaunchAttempt launchAttempt)
    {
        return new DaemonLaunchAttemptJsonContract(
            SchemaVersion: SchemaVersion,
            LaunchAttemptId: launchAttempt.LaunchAttemptId,
            StartedAtUtc: launchAttempt.StartedAtUtc,
            UpdatedAtUtc: launchAttempt.UpdatedAtUtc,
            StartupStatus: launchAttempt.StartupStatus,
            StartupBlockingReason: launchAttempt.StartupBlockingReason,
            RetryDisposition: launchAttempt.RetryDisposition,
            ProcessAction: launchAttempt.ProcessAction,
            EditorMode: launchAttempt.EditorMode,
            ProcessId: launchAttempt.ProcessId,
            ProcessStartedAtUtc: launchAttempt.ProcessStartedAtUtc,
            UnityLogPath: launchAttempt.UnityLogPath,
            Diagnosis: ToContract(launchAttempt.Diagnosis));
    }

    private static DaemonDiagnosisJsonContract ToContract (DaemonDiagnosis diagnosis)
    {
        return new DaemonDiagnosisJsonContract(
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
    }

    private static DaemonLaunchAttempt ToModel (
        DaemonLaunchAttemptJsonContract contract,
        string artifactPath)
    {
        var diagnosis = contract.Diagnosis!;
        return new DaemonLaunchAttempt(
            LaunchAttemptId: contract.LaunchAttemptId!,
            StartedAtUtc: contract.StartedAtUtc,
            UpdatedAtUtc: contract.UpdatedAtUtc,
            StartupStatus: contract.StartupStatus!,
            StartupBlockingReason: StringValueNormalizer.TrimToNull(contract.StartupBlockingReason),
            RetryDisposition: contract.RetryDisposition!,
            ProcessAction: contract.ProcessAction!,
            EditorMode: StringValueNormalizer.TrimToNull(contract.EditorMode),
            ProcessId: contract.ProcessId,
            ProcessStartedAtUtc: contract.ProcessStartedAtUtc,
            UnityLogPath: StringValueNormalizer.TrimToNull(contract.UnityLogPath),
            ArtifactPath: artifactPath,
            Diagnosis: new DaemonDiagnosis(
                Reason: diagnosis.Reason!,
                Message: diagnosis.Message!,
                ReportedBy: diagnosis.ReportedBy!,
                IsInferred: diagnosis.IsInferred!.Value,
                UpdatedAtUtc: diagnosis.UpdatedAtUtc,
                ProcessId: diagnosis.ProcessId,
                EditorInstancePath: StringValueNormalizer.TrimToNull(diagnosis.EditorInstancePath),
                SessionIssuedAtUtc: diagnosis.SessionIssuedAtUtc,
                ProcessStartedAtUtc: diagnosis.ProcessStartedAtUtc,
                UnityLogPath: StringValueNormalizer.TrimToNull(diagnosis.UnityLogPath),
                StartupPhase: StringValueNormalizer.TrimToNull(diagnosis.StartupPhase),
                ActionRequired: StringValueNormalizer.TrimToNull(diagnosis.ActionRequired),
                PrimaryDiagnostic: diagnosis.PrimaryDiagnostic is null
                    ? null
                    : new DaemonPrimaryDiagnostic(
                        Kind: StringValueNormalizer.TrimToNull(diagnosis.PrimaryDiagnostic.Kind)!,
                        Code: StringValueNormalizer.TrimToNull(diagnosis.PrimaryDiagnostic.Code),
                        File: StringValueNormalizer.TrimToNull(diagnosis.PrimaryDiagnostic.File),
                        Line: diagnosis.PrimaryDiagnostic.Line,
                        Column: diagnosis.PrimaryDiagnostic.Column,
                        Message: StringValueNormalizer.TrimToNull(diagnosis.PrimaryDiagnostic.Message))));
    }

    private static bool TryValidate (
        DaemonLaunchAttempt launchAttempt,
        string diagnosisPath,
        out ExecutionError? error)
    {
        if (!StringValueNormalizer.TryTrimToNonEmpty(launchAttempt.LaunchAttemptId, out _))
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt launchAttemptId is invalid: {diagnosisPath}");
            return false;
        }

        return TryValidateFields(
            launchAttempt.StartedAtUtc,
            launchAttempt.UpdatedAtUtc,
            launchAttempt.StartupStatus,
            launchAttempt.RetryDisposition,
            launchAttempt.ProcessAction,
            launchAttempt.Diagnosis,
            diagnosisPath,
            out error);
    }

    private static bool TryValidate (
        DaemonLaunchAttemptJsonContract contract,
        string diagnosisPath,
        out ExecutionError? error)
    {
        if (contract.SchemaVersion != SchemaVersion)
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt schemaVersion is invalid: {diagnosisPath}");
            return false;
        }

        if (!StringValueNormalizer.TryTrimToNonEmpty(contract.LaunchAttemptId, out _))
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt launchAttemptId is invalid: {diagnosisPath}");
            return false;
        }

        if (contract.Diagnosis is null)
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt diagnosis is invalid: {diagnosisPath}");
            return false;
        }

        return TryValidateFields(
            contract.StartedAtUtc,
            contract.UpdatedAtUtc,
            contract.StartupStatus,
            contract.RetryDisposition,
            contract.ProcessAction,
            ToDiagnosisForValidation(contract.Diagnosis),
            diagnosisPath,
            out error);
    }

    private static bool TryValidateFields (
        DateTimeOffset startedAtUtc,
        DateTimeOffset updatedAtUtc,
        string? startupStatus,
        string? retryDisposition,
        string? processAction,
        DaemonDiagnosis? diagnosis,
        string diagnosisPath,
        out ExecutionError? error)
    {
        if (startedAtUtc == default)
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt startedAtUtc is invalid: {diagnosisPath}");
            return false;
        }

        if (updatedAtUtc == default)
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt updatedAtUtc is invalid: {diagnosisPath}");
            return false;
        }

        if (!IsSupportedStartupStatus(startupStatus))
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt startupStatus is invalid: {diagnosisPath}");
            return false;
        }

        if (!IsSupportedRetryDisposition(retryDisposition))
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt retryDisposition is invalid: {diagnosisPath}");
            return false;
        }

        if (!IsSupportedProcessAction(processAction))
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt processAction is invalid: {diagnosisPath}");
            return false;
        }

        if (diagnosis is null)
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt diagnosis is invalid: {diagnosisPath}");
            return false;
        }

        if (!TryValidateDiagnosis(diagnosis, diagnosisPath, out error))
        {
            return false;
        }

        error = null;
        return true;
    }

    private static DaemonDiagnosis? ToDiagnosisForValidation (DaemonDiagnosisJsonContract diagnosis)
    {
        if (diagnosis.IsInferred is null)
        {
            return null;
        }

        return new DaemonDiagnosis(
            Reason: diagnosis.Reason ?? string.Empty,
            Message: diagnosis.Message ?? string.Empty,
            ReportedBy: diagnosis.ReportedBy ?? string.Empty,
            IsInferred: diagnosis.IsInferred.Value,
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
                : new DaemonPrimaryDiagnostic(
                    Kind: diagnosis.PrimaryDiagnostic.Kind ?? string.Empty,
                    Code: diagnosis.PrimaryDiagnostic.Code,
                    File: diagnosis.PrimaryDiagnostic.File,
                    Line: diagnosis.PrimaryDiagnostic.Line,
                    Column: diagnosis.PrimaryDiagnostic.Column,
                    Message: diagnosis.PrimaryDiagnostic.Message));
    }

    private static bool TryValidateDiagnosis (
        DaemonDiagnosis diagnosis,
        string diagnosisPath,
        out ExecutionError? error)
    {
        if (!StringValueNormalizer.TryTrimToNonEmpty(diagnosis.Reason, out _))
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt diagnosis.reason is invalid: {diagnosisPath}");
            return false;
        }

        if (!StringValueNormalizer.TryTrimToNonEmpty(diagnosis.Message, out _))
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt diagnosis.message is invalid: {diagnosisPath}");
            return false;
        }

        if (!StringValueNormalizer.TryTrimToNonEmpty(diagnosis.ReportedBy, out _))
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt diagnosis.reportedBy is invalid: {diagnosisPath}");
            return false;
        }

        if (diagnosis.UpdatedAtUtc == default)
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt diagnosis.updatedAtUtc is invalid: {diagnosisPath}");
            return false;
        }

        if (diagnosis.SessionIssuedAtUtc == default)
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt diagnosis.sessionIssuedAtUtc is invalid: {diagnosisPath}");
            return false;
        }

        error = null;
        return true;
    }

    private static bool IsSupportedStartupStatus (string? startupStatus)
    {
        return startupStatus is DaemonStartupStatusValues.Blocked
            or DaemonStartupStatusValues.Timeout
            or DaemonStartupStatusValues.Failed
            or DaemonStartupStatusValues.Completed;
    }

    private static bool IsPublicFailureStatus (string startupStatus)
    {
        return startupStatus is DaemonStartupStatusValues.Blocked
            or DaemonStartupStatusValues.Timeout
            or DaemonStartupStatusValues.Failed;
    }

    private static bool IsSupportedRetryDisposition (string? retryDisposition)
    {
        return retryDisposition is DaemonStartupRetryDispositionValues.RetryImmediately
            or DaemonStartupRetryDispositionValues.WaitThenRetry
            or DaemonStartupRetryDispositionValues.RetryAfterFix
            or DaemonStartupRetryDispositionValues.ManualActionRequired
            or DaemonStartupRetryDispositionValues.DoNotRetry
            or DaemonStartupRetryDispositionValues.Unknown;
    }

    private static bool IsSupportedProcessAction (string? processAction)
    {
        return processAction is DaemonStartupProcessActionValues.None
            or DaemonStartupProcessActionValues.Kept
            or DaemonStartupProcessActionValues.Terminated
            or DaemonStartupProcessActionValues.Unknown;
    }

    private static bool IsIoFailure (Exception exception)
    {
        return exception is IOException
            or UnauthorizedAccessException;
    }
}
