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
        ProjectFingerprint projectFingerprint,
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

        var contract = ToContract(launchAttempt);
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
        ProjectFingerprint projectFingerprint,
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
            .ThenByDescending(static entry => entry.LaunchAttemptId)
            .FirstOrDefault();
        return DaemonLaunchAttemptReadResult.Success(latestFailure?.LaunchAttempt);
    }

    /// <inheritdoc />
    public async ValueTask<DaemonLaunchAttemptStoreOperationResult> PruneAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
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
                .ThenByDescending(static entry => entry.LaunchAttemptId)
                .ThenByDescending(static entry => entry.DirectoryPath, StringComparer.Ordinal)
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
        ProjectFingerprint projectFingerprint,
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

            var launchAttemptIdPathSegment = Path.GetFileName(attemptDirectory);
            if (!Guid.TryParseExact(launchAttemptIdPathSegment, "N", out var launchAttemptId)
                || launchAttemptId == Guid.Empty
                || !string.Equals(launchAttemptIdPathSegment, launchAttemptId.ToString("N"), StringComparison.Ordinal))
            {
                if (failOnInvalidPayload)
                {
                    return LaunchAttemptDirectoryEntriesReadResult.Failure(ExecutionError.InvalidArgument(
                        $"Daemon launch-attempt directory name is invalid: {attemptDirectory}"));
                }

                entries.Add(new LaunchAttemptDirectoryEntry(
                    attemptDirectory,
                    null,
                    GetLastWriteTimeUtc(attemptDirectory),
                    null));
                continue;
            }

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

            var readResult = await ReadOneAsync(
                    attemptDirectory,
                    diagnosisPath,
                    launchAttemptId,
                    cancellationToken)
                .ConfigureAwait(false);
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
                    null));
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
        Guid directoryLaunchAttemptId,
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
                $"Daemon launch-attempt JSON is invalid: {diagnosisPath}. Field={exception.Path ?? "$"}. {exception.Message}"));
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

        if (contract.LaunchAttemptId != directoryLaunchAttemptId)
        {
            return DaemonLaunchAttemptReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon launch-attempt launchAttemptId does not match its directory: {diagnosisPath}"));
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
            StartupBlockingReason: ContractLiteralCodec.ToValue(launchAttempt.StartupBlockingReason),
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
            LaunchAttemptId: contract.LaunchAttemptId!.Value,
            StartedAtUtc: contract.StartedAtUtc,
            UpdatedAtUtc: contract.UpdatedAtUtc,
            StartupStatus: ParseRequiredLiteral<DaemonStartupStatus>(contract.StartupStatus!),
            StartupBlockingReason: ParseRequiredLiteral<DaemonStartupBlockingReason>(contract.StartupBlockingReason!),
            RetryDisposition: ParseRequiredLiteral<DaemonStartupRetryDisposition>(contract.RetryDisposition!),
            ProcessAction: ParseRequiredLiteral<DaemonStartupProcessAction>(contract.ProcessAction!),
            EditorMode: ParseOptionalLiteral<DaemonEditorMode>(contract.EditorMode),
            ProcessId: contract.ProcessId,
            ProcessStartedAtUtc: contract.ProcessStartedAtUtc,
            UnityLogPath: StringValueNormalizer.TrimToNull(contract.UnityLogPath),
            ArtifactPath: artifactPath,
            Diagnosis: new DaemonDiagnosis(
                Reason: diagnosis.Reason!.Value,
                Message: diagnosis.Message!,
                ReportedBy: diagnosis.ReportedBy!.Value,
                IsInferred: diagnosis.IsInferred!.Value,
                UpdatedAtUtc: diagnosis.UpdatedAtUtc,
                ProcessId: diagnosis.ProcessId,
                EditorInstancePath: StringValueNormalizer.TrimToNull(diagnosis.EditorInstancePath),
                SessionIssuedAtUtc: diagnosis.SessionIssuedAtUtc,
                ProcessStartedAtUtc: diagnosis.ProcessStartedAtUtc,
                UnityLogPath: StringValueNormalizer.TrimToNull(diagnosis.UnityLogPath),
                StartupPhase: diagnosis.StartupPhase,
                ActionRequired: diagnosis.ActionRequired,
                PrimaryDiagnostic: diagnosis.PrimaryDiagnostic is null
                    ? null
                    : new DaemonPrimaryDiagnostic(
                        Kind: diagnosis.PrimaryDiagnostic.Kind!.Value,
                        Code: StringValueNormalizer.TrimToNull(diagnosis.PrimaryDiagnostic.Code),
                        File: StringValueNormalizer.TrimToNull(diagnosis.PrimaryDiagnostic.File),
                        Line: diagnosis.PrimaryDiagnostic.Line,
                        Column: diagnosis.PrimaryDiagnostic.Column,
                        Message: StringValueNormalizer.TrimToNull(diagnosis.PrimaryDiagnostic.Message))));
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

        if (!contract.LaunchAttemptId.HasValue)
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt launchAttemptId is invalid: {diagnosisPath}");
            return false;
        }

        if (contract.Diagnosis is null)
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt diagnosis is invalid: {diagnosisPath}");
            return false;
        }

        if (!ContractLiteralCodec.TryParse(contract.StartupStatus, out DaemonStartupStatus startupStatus)
            || startupStatus is not (DaemonStartupStatus.Blocked
                or DaemonStartupStatus.Timeout
                or DaemonStartupStatus.Failed
                or DaemonStartupStatus.Completed))
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt startupStatus is invalid: {diagnosisPath}");
            return false;
        }

        if (StringValueNormalizer.TrimToNull(contract.StartupBlockingReason) is not string startupBlockingReasonLiteral
            || !ContractLiteralCodec.TryParse<DaemonStartupBlockingReason>(startupBlockingReasonLiteral, out _))
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt startupBlockingReason is invalid: {diagnosisPath}");
            return false;
        }

        if (!ContractLiteralCodec.TryParse(contract.RetryDisposition, out DaemonStartupRetryDisposition _))
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt retryDisposition is invalid: {diagnosisPath}");
            return false;
        }

        if (!ContractLiteralCodec.TryParse(contract.ProcessAction, out DaemonStartupProcessAction _))
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt processAction is invalid: {diagnosisPath}");
            return false;
        }

        if (!TryParseOptionalLiteral(contract.EditorMode, out DaemonEditorMode? _))
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt editorMode is invalid: {diagnosisPath}");
            return false;
        }

        if (contract.StartedAtUtc == default || contract.StartedAtUtc.Offset != TimeSpan.Zero)
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt startedAtUtc is invalid: {diagnosisPath}");
            return false;
        }

        if (contract.UpdatedAtUtc == default || contract.UpdatedAtUtc.Offset != TimeSpan.Zero)
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt updatedAtUtc is invalid: {diagnosisPath}");
            return false;
        }

        if (contract.UpdatedAtUtc < contract.StartedAtUtc)
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt updatedAtUtc is invalid: {diagnosisPath}");
            return false;
        }

        if (contract.ProcessId is <= 0)
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt processId is invalid: {diagnosisPath}");
            return false;
        }

        if (contract.ProcessStartedAtUtc.HasValue
            && (contract.ProcessStartedAtUtc.Value == default
                || contract.ProcessStartedAtUtc.Value.Offset != TimeSpan.Zero))
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt processStartedAtUtc is invalid: {diagnosisPath}");
            return false;
        }

        if (contract.ProcessStartedAtUtc > contract.UpdatedAtUtc)
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt processStartedAtUtc is invalid: {diagnosisPath}");
            return false;
        }

        return TryValidateDiagnosis(contract.Diagnosis, diagnosisPath, out error);
    }

    private static bool TryValidateDiagnosis (
        DaemonDiagnosisJsonContract diagnosis,
        string diagnosisPath,
        out ExecutionError? error)
    {
        if (!diagnosis.Reason.HasValue)
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt diagnosis.reason is invalid: {diagnosisPath}");
            return false;
        }

        if (!StringValueNormalizer.TryTrimToNonEmpty(diagnosis.Message, out _))
        {
            error = ExecutionError.InvalidArgument($"Daemon launch-attempt diagnosis.message is invalid: {diagnosisPath}");
            return false;
        }

        if (!diagnosis.ReportedBy.HasValue)
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

        return TryValidatePrimaryDiagnostic(diagnosis.PrimaryDiagnostic, diagnosisPath, out error);
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

        if (!primaryDiagnostic.Kind.HasValue)
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
        Guid? LaunchAttemptId,
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
