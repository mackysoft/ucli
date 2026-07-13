using System.Text.Json;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.LaunchAttempts;
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

        var entriesResult = await ReadDirectoryEntriesAsync(
                storageRoot,
                projectFingerprint,
                attemptsDirectory,
                failOnInvalidPayload: true,
                cancellationToken)
            .ConfigureAwait(false);
        if (!entriesResult.IsSuccess)
        {
            return DaemonLaunchAttemptReadResult.Failure(entriesResult.Error!);
        }

        var latestFailure = entriesResult.Entries
            .Where(static entry => HasPublicFailureStartupStatus(entry))
            .OrderByDescending(static entry => entry.LaunchAttempt!.UpdatedAtUtc)
            .ThenByDescending(static entry => entry.LaunchAttemptId, StringComparer.Ordinal)
            .FirstOrDefault();
        return DaemonLaunchAttemptReadResult.Success(latestFailure?.LaunchAttempt);
    }

    /// <inheritdoc />
    public async ValueTask<DaemonLaunchAttemptStoreOperationResult> PruneAsync (
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
            return DaemonLaunchAttemptStoreOperationResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon launch-attempt path is invalid. {exception.Message}"));
        }

        if (!Directory.Exists(attemptsDirectory))
        {
            return DaemonLaunchAttemptStoreOperationResult.Success();
        }

        var entriesResult = await ReadDirectoryEntriesAsync(
                storageRoot,
                projectFingerprint,
                attemptsDirectory,
                failOnInvalidPayload: false,
                cancellationToken)
            .ConfigureAwait(false);
        if (!entriesResult.IsSuccess)
        {
            return DaemonLaunchAttemptStoreOperationResult.Failure(entriesResult.Error!);
        }

        try
        {
            var attemptsToDelete = entriesResult.Entries
                .OrderByDescending(static entry => entry.UpdatedAtUtc)
                .ThenByDescending(static entry => entry.LaunchAttemptId, StringComparer.Ordinal)
                .Skip(keepCount)
                .ToArray();
            var deletedCount = 0;
            foreach (var entry in attemptsToDelete)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Directory.Delete(entry.DirectoryPath, recursive: true);
                deletedCount++;
            }

            return DaemonLaunchAttemptStoreOperationResult.Success(deletedCount);
        }
        catch (Exception exception) when (IsIoFailure(exception))
        {
            return DaemonLaunchAttemptStoreOperationResult.Failure(ExecutionError.InternalError(
                $"Failed to prune daemon launch-attempt artifacts: {attemptsDirectory}. {exception.Message}"));
        }
    }

    private static async ValueTask<LaunchAttemptDirectoryEntriesReadResult> ReadDirectoryEntriesAsync (
        string storageRoot,
        string projectFingerprint,
        string attemptsDirectory,
        bool failOnInvalidPayload,
        CancellationToken cancellationToken)
    {
        try
        {
            FileSystemAccessBoundary.EnsureSecureDirectory(attemptsDirectory);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return LaunchAttemptDirectoryEntriesReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon launch-attempt path is invalid: {attemptsDirectory}. {exception.Message}"));
        }
        catch (Exception exception) when (IsIoFailure(exception))
        {
            return LaunchAttemptDirectoryEntriesReadResult.Failure(ExecutionError.InternalError(
                $"Daemon launch-attempt directory is unsafe: {attemptsDirectory}. {exception.Message}"));
        }

        string[] attemptDirectories;
        try
        {
            attemptDirectories = Directory.EnumerateDirectories(attemptsDirectory).ToArray();
        }
        catch (Exception exception) when (IsIoFailure(exception))
        {
            return LaunchAttemptDirectoryEntriesReadResult.Failure(ExecutionError.InternalError(
                $"Failed to enumerate daemon launch-attempt directories: {attemptsDirectory}. {exception.Message}"));
        }

        var entries = new List<LaunchAttemptDirectoryEntry>();
        foreach (var attemptDirectory in attemptDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                EnsureSafeLaunchAttemptDirectory(attemptsDirectory, attemptDirectory);
            }
            catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
            {
                return LaunchAttemptDirectoryEntriesReadResult.Failure(ExecutionError.InvalidArgument(
                    $"Daemon launch-attempt path is invalid: {attemptDirectory}. {exception.Message}"));
            }
            catch (Exception exception) when (IsIoFailure(exception))
            {
                return LaunchAttemptDirectoryEntriesReadResult.Failure(ExecutionError.InternalError(
                    $"Daemon launch-attempt directory is unsafe: {attemptDirectory}. {exception.Message}"));
            }

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
                return LaunchAttemptDirectoryEntriesReadResult.Failure(ExecutionError.InvalidArgument(
                    $"Daemon launch-attempt path is invalid. {exception.Message}"));
            }

            var readResult = await ReadOneAsync(attemptDirectory, diagnosisPath, cancellationToken).ConfigureAwait(false);
            if (!readResult.IsSuccess)
            {
                if (failOnInvalidPayload)
                {
                    return LaunchAttemptDirectoryEntriesReadResult.Failure(readResult.Error!);
                }

                entries.Add(new LaunchAttemptDirectoryEntry(
                    attemptDirectory,
                    launchAttemptId,
                    GetLastWriteTimeUtc(attemptDirectory),
                    LaunchAttempt: null));
                continue;
            }

            var updatedAtUtc = readResult.LaunchAttempt?.UpdatedAtUtc ?? GetLastWriteTimeUtc(attemptDirectory);
            entries.Add(new LaunchAttemptDirectoryEntry(
                attemptDirectory,
                launchAttemptId,
                updatedAtUtc,
                readResult.LaunchAttempt));
        }

        return LaunchAttemptDirectoryEntriesReadResult.Success(entries);
    }

    private static async ValueTask<DaemonLaunchAttemptReadResult> ReadOneAsync (
        string attemptDirectory,
        string diagnosisPath,
        CancellationToken cancellationToken)
    {
        try
        {
            EnsureSafeStartupDiagnosisFile(attemptDirectory, diagnosisPath);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return DaemonLaunchAttemptReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon launch-attempt path is invalid: {diagnosisPath}. {exception.Message}"));
        }
        catch (Exception exception) when (IsIoFailure(exception))
        {
            return DaemonLaunchAttemptReadResult.Failure(ExecutionError.InternalError(
                $"Daemon launch-attempt file is unsafe: {diagnosisPath}. {exception.Message}"));
        }

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
            StartupStatus: ContractLiteralCodec.ToValue(launchAttempt.StartupStatus),
            StartupBlockingReason: launchAttempt.StartupBlockingReason.HasValue
                ? ContractLiteralCodec.ToValue(launchAttempt.StartupBlockingReason.Value)
                : null,
            RetryDisposition: ContractLiteralCodec.ToValue(launchAttempt.RetryDisposition),
            ProcessAction: ContractLiteralCodec.ToValue(launchAttempt.ProcessAction),
            EditorMode: launchAttempt.EditorMode.HasValue
                ? ContractLiteralCodec.ToValue(launchAttempt.EditorMode.Value)
                : null,
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
            StartupPhase: diagnosis.StartupPhase.HasValue
                ? ContractLiteralCodec.ToValue(diagnosis.StartupPhase.Value)
                : null,
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
            StartupStatus: ParseRequiredLiteral<DaemonStartupStatus>(contract.StartupStatus!),
            StartupBlockingReason: ParseOptionalLiteral<DaemonStartupBlockingReason>(contract.StartupBlockingReason),
            RetryDisposition: ParseRequiredLiteral<DaemonStartupRetryDisposition>(contract.RetryDisposition!),
            ProcessAction: ParseRequiredLiteral<DaemonStartupProcessAction>(contract.ProcessAction!),
            EditorMode: ParseOptionalLiteral<DaemonEditorMode>(contract.EditorMode),
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
                StartupPhase: ParseOptionalLiteral<DaemonDiagnosisStartupPhase>(diagnosis.StartupPhase),
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
            launchAttempt.StartupBlockingReason,
            launchAttempt.RetryDisposition,
            launchAttempt.ProcessAction,
            launchAttempt.EditorMode,
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

        if (!TryReadPersistedTerminalStartupStatus(contract.StartupStatus, out var startupStatus))
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt startupStatus is invalid: {diagnosisPath}");
            return false;
        }

        if (StringValueNormalizer.TrimToNull(contract.StartupBlockingReason) is not string startupBlockingReasonLiteral
            || !ContractLiteralCodec.TryParse<DaemonStartupBlockingReason>(startupBlockingReasonLiteral, out var startupBlockingReason))
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt startupBlockingReason is invalid: {diagnosisPath}");
            return false;
        }

        if (!ContractLiteralCodec.TryParse(contract.RetryDisposition, out DaemonStartupRetryDisposition retryDisposition))
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt retryDisposition is invalid: {diagnosisPath}");
            return false;
        }

        if (!ContractLiteralCodec.TryParse(contract.ProcessAction, out DaemonStartupProcessAction processAction))
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt processAction is invalid: {diagnosisPath}");
            return false;
        }

        if (!TryParseOptionalLiteral(contract.EditorMode, out DaemonEditorMode? editorMode))
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt editorMode is invalid: {diagnosisPath}");
            return false;
        }

        if (!TryParseOptionalLiteral(contract.Diagnosis.StartupPhase, out DaemonDiagnosisStartupPhase? startupPhase))
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt diagnosis.startupPhase is invalid: {diagnosisPath}");
            return false;
        }

        return TryValidateFields(
            contract.StartedAtUtc,
            contract.UpdatedAtUtc,
            startupStatus,
            startupBlockingReason,
            retryDisposition,
            processAction,
            editorMode,
            ToDiagnosisForValidation(contract.Diagnosis, startupPhase),
            diagnosisPath,
            out error);
    }

    private static bool TryValidateFields (
        DateTimeOffset startedAtUtc,
        DateTimeOffset updatedAtUtc,
        DaemonStartupStatus startupStatus,
        DaemonStartupBlockingReason? startupBlockingReason,
        DaemonStartupRetryDisposition retryDisposition,
        DaemonStartupProcessAction processAction,
        DaemonEditorMode? editorMode,
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

        if (!IsPersistedTerminalStartupStatus(startupStatus))
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt startupStatus is invalid: {diagnosisPath}");
            return false;
        }

        if (!startupBlockingReason.HasValue
            || !ContractLiteralCodec.IsDefined(startupBlockingReason.Value))
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt startupBlockingReason is invalid: {diagnosisPath}");
            return false;
        }

        if (!ContractLiteralCodec.IsDefined(retryDisposition))
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt retryDisposition is invalid: {diagnosisPath}");
            return false;
        }

        if (!ContractLiteralCodec.IsDefined(processAction))
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt processAction is invalid: {diagnosisPath}");
            return false;
        }

        if (editorMode.HasValue && !ContractLiteralCodec.IsDefined(editorMode.Value))
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt editorMode is invalid: {diagnosisPath}");
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

    private static DaemonDiagnosis? ToDiagnosisForValidation (
        DaemonDiagnosisJsonContract diagnosis,
        DaemonDiagnosisStartupPhase? startupPhase)
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
            StartupPhase: startupPhase,
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

        if (!StringValueNormalizer.TryTrimToNonEmpty(diagnosis.ReportedBy, out var reportedBy)
            || !DaemonDiagnosisReportedByValues.IsSupported(reportedBy))
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
        DaemonDiagnosisStartupPhase? startupPhase,
        string diagnosisPath,
        out ExecutionError? error)
    {
        if (!startupPhase.HasValue)
        {
            error = null;
            return true;
        }

        if (!ContractLiteralCodec.IsDefined(startupPhase.Value))
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt diagnosis.startupPhase is invalid: {diagnosisPath}");
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
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt diagnosis.actionRequired is invalid: {diagnosisPath}");
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

        if (!StringValueNormalizer.TryTrimToNonEmpty(primaryDiagnostic.Kind, out var normalizedKind)
            || !DaemonDiagnosisPrimaryDiagnosticKindValues.IsSupported(normalizedKind))
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt diagnosis.primaryDiagnostic.kind is invalid: {diagnosisPath}");
            return false;
        }

        if (primaryDiagnostic.Line is <= 0)
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt diagnosis.primaryDiagnostic.line is invalid: {diagnosisPath}");
            return false;
        }

        if (primaryDiagnostic.Column is <= 0)
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt diagnosis.primaryDiagnostic.column is invalid: {diagnosisPath}");
            return false;
        }

        error = null;
        return true;
    }

    private static bool HasPublicFailureStartupStatus (LaunchAttemptDirectoryEntry entry)
    {
        if (entry.LaunchAttempt is null)
        {
            return false;
        }

        return entry.LaunchAttempt.StartupStatus is DaemonStartupStatus.Blocked
            or DaemonStartupStatus.Timeout
            or DaemonStartupStatus.Failed;
    }

    private static bool TryReadPersistedTerminalStartupStatus (
        string? startupStatus,
        out DaemonStartupStatus status)
    {
        if (!ContractLiteralCodec.TryParse(startupStatus, out status))
        {
            return false;
        }

        return IsPersistedTerminalStartupStatus(status);
    }

    private static bool IsPersistedTerminalStartupStatus (DaemonStartupStatus status)
    {
        return status is DaemonStartupStatus.Blocked
            or DaemonStartupStatus.Timeout
            or DaemonStartupStatus.Failed
            or DaemonStartupStatus.Completed;
    }

    private static TEnum ParseRequiredLiteral<TEnum> (string literal)
        where TEnum : struct, Enum
    {
        return ContractLiteralCodec.TryParse(literal, out TEnum value)
            ? value
            : throw new InvalidOperationException($"Validated {typeof(TEnum).Name} literal could not be parsed.");
    }

    private static TEnum? ParseOptionalLiteral<TEnum> (string? literal)
        where TEnum : struct, Enum
    {
        if (StringValueNormalizer.TrimToNull(literal) is not string normalizedLiteral)
        {
            return null;
        }

        return ParseRequiredLiteral<TEnum>(normalizedLiteral);
    }

    private static bool TryParseOptionalLiteral<TEnum> (
        string? literal,
        out TEnum? value)
        where TEnum : struct, Enum
    {
        if (StringValueNormalizer.TrimToNull(literal) is not string normalizedLiteral)
        {
            value = null;
            return true;
        }

        if (ContractLiteralCodec.TryParse(normalizedLiteral, out TEnum parsedValue))
        {
            value = parsedValue;
            return true;
        }

        value = null;
        return false;
    }

    private static void EnsureSafeLaunchAttemptDirectory (
        string attemptsDirectory,
        string attemptDirectory)
    {
        if (!PathIdentity.IsChildPath(attemptsDirectory, attemptDirectory))
        {
            throw new IOException($"Launch-attempt deletion target must remain under launch-attempts directory: {attemptDirectory}");
        }

        if ((File.GetAttributes(attemptDirectory) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Launch-attempt deletion target must not be a reparse point: {attemptDirectory}");
        }
    }

    private static void EnsureSafeStartupDiagnosisFile (
        string attemptDirectory,
        string diagnosisPath)
    {
        if (!PathIdentity.IsChildPath(attemptDirectory, diagnosisPath))
        {
            throw new IOException($"Launch-attempt diagnosis file must remain under attempt directory: {diagnosisPath}");
        }

        if (!File.Exists(diagnosisPath))
        {
            return;
        }

        if ((File.GetAttributes(diagnosisPath) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Launch-attempt diagnosis file must not be a reparse point: {diagnosisPath}");
        }
    }

    private static DateTimeOffset GetLastWriteTimeUtc (string path)
    {
        return new DateTimeOffset(Directory.GetLastWriteTimeUtc(path), TimeSpan.Zero);
    }

    private static bool IsIoFailure (Exception exception)
    {
        return exception is IOException
            or UnauthorizedAccessException;
    }

    private sealed record LaunchAttemptDirectoryEntry (
        string DirectoryPath,
        string LaunchAttemptId,
        DateTimeOffset UpdatedAtUtc,
        DaemonLaunchAttempt? LaunchAttempt);

    private sealed record LaunchAttemptDirectoryEntriesReadResult (
        IReadOnlyList<LaunchAttemptDirectoryEntry> Entries,
        ExecutionError? Error)
    {
        public bool IsSuccess => Error is null;

        public static LaunchAttemptDirectoryEntriesReadResult Success (IReadOnlyList<LaunchAttemptDirectoryEntry> entries)
        {
            ArgumentNullException.ThrowIfNull(entries);
            return new LaunchAttemptDirectoryEntriesReadResult(entries, null);
        }

        public static LaunchAttemptDirectoryEntriesReadResult Failure (ExecutionError error)
        {
            ArgumentNullException.ThrowIfNull(error);
            return new LaunchAttemptDirectoryEntriesReadResult(Array.Empty<LaunchAttemptDirectoryEntry>(), error);
        }
    }
}
