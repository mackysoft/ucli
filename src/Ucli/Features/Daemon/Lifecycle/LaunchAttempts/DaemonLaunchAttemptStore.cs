using System.Text.Json;
using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.LaunchAttempts;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.LaunchAttempts;

/// <summary> Implements filesystem-backed daemon launch-attempt artifact persistence. </summary>
internal sealed class DaemonLaunchAttemptStore : IDaemonLaunchAttemptStore
{
    private const int SchemaVersion = 1;

    /// <inheritdoc />
    public async ValueTask<DaemonLaunchAttemptStoreOperationResult> WriteFailureAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        DaemonLaunchAttempt launchAttempt,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(launchAttempt);

        var attemptDirectory = UcliStoragePathResolver.ResolveLaunchAttemptDirectory(
            storageRoot,
            projectFingerprint,
            launchAttempt.LaunchAttemptId);
        var diagnosisPath = UcliStoragePathResolver.ResolveLaunchAttemptStartupDiagnosisPath(
            storageRoot,
            projectFingerprint,
            launchAttempt.LaunchAttemptId);

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
            FileSystemAccessBoundary.EnsureSecureDirectory(attemptDirectory);
            await FileUtilities.WriteAllTextAtomicallyAsync(diagnosisPath, json, cancellationToken).ConfigureAwait(false);
            return DaemonLaunchAttemptStoreOperationResult.Success();
        }
        catch (Exception exception) when (IsIoFailure(exception))
        {
            return DaemonLaunchAttemptStoreOperationResult.Failure(ExecutionError.InternalError(
                $"Failed to write daemon launch-attempt file: {diagnosisPath}. {exception.Message}"));
        }
    }

    /// <inheritdoc />
    public async ValueTask<DaemonLaunchAttemptReadResult> ReadLastFailureAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var attemptsDirectory = UcliStoragePathResolver.ResolveLaunchAttemptsDirectory(
            storageRoot,
            projectFingerprint);

        if (!Directory.Exists(attemptsDirectory.Value))
        {
            return DaemonLaunchAttemptReadResult.Success(null);
        }

        var entriesResult = await ReadDirectoryEntriesAsync(
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
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        int keepCount,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentOutOfRangeException.ThrowIfNegative(keepCount);

        var attemptsDirectory = UcliStoragePathResolver.ResolveLaunchAttemptsDirectory(
            storageRoot,
            projectFingerprint);

        if (!Directory.Exists(attemptsDirectory.Value))
        {
            return DaemonLaunchAttemptStoreOperationResult.Success();
        }

        var entriesResult = await ReadDirectoryEntriesAsync(
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
                .Skip(keepCount)
                .ToArray();
            var deletedCount = 0;
            foreach (var entry in attemptsToDelete)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DeleteOwnedLaunchAttemptDirectory(entry.DirectoryPath);
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
        AbsolutePath attemptsDirectory,
        bool failOnInvalidPayload,
        CancellationToken cancellationToken)
    {
        try
        {
            FileSystemAccessBoundary.EnsureSecureDirectory(attemptsDirectory);
        }
        catch (Exception exception) when (IsIoFailure(exception))
        {
            return LaunchAttemptDirectoryEntriesReadResult.Failure(ExecutionError.InternalError(
                $"Daemon launch-attempt directory is unsafe: {attemptsDirectory}. {exception.Message}"));
        }

        string[] attemptDirectories;
        try
        {
            attemptDirectories = Directory.EnumerateDirectories(attemptsDirectory.Value).ToArray();
        }
        catch (Exception exception) when (IsIoFailure(exception))
        {
            return LaunchAttemptDirectoryEntriesReadResult.Failure(ExecutionError.InternalError(
                $"Failed to enumerate daemon launch-attempt directories: {attemptsDirectory}. {exception.Message}"));
        }

        var entries = new List<LaunchAttemptDirectoryEntry>();
        foreach (var attemptDirectoryValue in attemptDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!AbsolutePath.TryParse(
                    attemptDirectoryValue,
                    out var absoluteAttemptDirectory,
                    out _)
                || !ContainedPath.TryCreate(
                    attemptsDirectory,
                    absoluteAttemptDirectory,
                    out var attemptDirectory,
                    out _))
            {
                return LaunchAttemptDirectoryEntriesReadResult.Failure(ExecutionError.InternalError(
                    $"Daemon launch-attempt directory is outside its owned boundary: {attemptDirectoryValue}"));
            }

            var launchAttemptIdPathSegment = Path.GetFileName(attemptDirectory.Target.Value);
            if (!StoragePathSegmentCodec.TryDecodeNonEmptyGuid(launchAttemptIdPathSegment, out var launchAttemptId))
            {
                if (failOnInvalidPayload)
                {
                    return LaunchAttemptDirectoryEntriesReadResult.Failure(ExecutionError.InvalidArgument(
                        $"Daemon launch-attempt directory name is invalid: {attemptDirectory}"));
                }

                continue;
            }

            try
            {
                EnsureSafeLaunchAttemptDirectory(attemptDirectory);
            }
            catch (Exception exception) when (IsIoFailure(exception))
            {
                return LaunchAttemptDirectoryEntriesReadResult.Failure(ExecutionError.InternalError(
                    $"Daemon launch-attempt directory is unsafe: {attemptDirectory}. {exception.Message}"));
            }

            var diagnosisPath = ContainedPath.Create(
                attemptDirectory.Target,
                RootRelativePath.Parse(UcliStoragePathNames.StartupDiagnosisFileName));

            var readResult = await ReadOneAsync(
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
                    GetLastWriteTimeUtc(attemptDirectory.Target),
                    null));
                continue;
            }

            var updatedAtUtc = readResult.LaunchAttempt?.UpdatedAtUtc
                ?? GetLastWriteTimeUtc(attemptDirectory.Target);
            entries.Add(new LaunchAttemptDirectoryEntry(
                attemptDirectory,
                launchAttemptId,
                updatedAtUtc,
                readResult.LaunchAttempt));
        }

        return LaunchAttemptDirectoryEntriesReadResult.Success(entries);
    }

    private static async ValueTask<DaemonLaunchAttemptReadResult> ReadOneAsync (
        ContainedPath diagnosisPath,
        Guid directoryLaunchAttemptId,
        CancellationToken cancellationToken)
    {
        try
        {
            EnsureSafeStartupDiagnosisFile(diagnosisPath);
        }
        catch (Exception exception) when (IsIoFailure(exception))
        {
            return DaemonLaunchAttemptReadResult.Failure(ExecutionError.InternalError(
                $"Daemon launch-attempt file is unsafe: {diagnosisPath}. {exception.Message}"));
        }

        string? json;
        try
        {
            json = await FileUtilities.ReadAllTextOrNullAsync(
                    diagnosisPath.Target,
                    cancellationToken)
                .ConfigureAwait(false);
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

        return ValidateAndMap(contract, diagnosisPath.Target, directoryLaunchAttemptId);
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
            UnityLogPath: launchAttempt.UnityLogPath?.Value,
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
    }

    private static DaemonLaunchAttemptReadResult ValidateAndMap (
        DaemonLaunchAttemptJsonContract contract,
        AbsolutePath diagnosisPath,
        Guid directoryLaunchAttemptId)
    {
        if (contract.SchemaVersion != SchemaVersion)
        {
            return DaemonLaunchAttemptReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon launch-attempt schemaVersion is invalid: {diagnosisPath}"));
        }

        if (!contract.LaunchAttemptId.HasValue)
        {
            return DaemonLaunchAttemptReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon launch-attempt launchAttemptId is invalid: {diagnosisPath}"));
        }

        if (contract.Diagnosis is null)
        {
            return DaemonLaunchAttemptReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon launch-attempt diagnosis is invalid: {diagnosisPath}"));
        }

        if (!ContractLiteralCodec.TryParse(contract.StartupStatus, out DaemonStartupStatus startupStatus)
            || startupStatus is not (DaemonStartupStatus.Blocked
                or DaemonStartupStatus.Timeout
                or DaemonStartupStatus.Failed
                or DaemonStartupStatus.Completed))
        {
            return DaemonLaunchAttemptReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon launch-attempt startupStatus is invalid: {diagnosisPath}"));
        }

        if (!ContractLiteralCodec.TryParse(
                contract.StartupBlockingReason,
                out DaemonStartupBlockingReason startupBlockingReason))
        {
            return DaemonLaunchAttemptReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon launch-attempt startupBlockingReason is invalid: {diagnosisPath}"));
        }

        if (!ContractLiteralCodec.TryParse(contract.RetryDisposition, out DaemonStartupRetryDisposition retryDisposition))
        {
            return DaemonLaunchAttemptReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon launch-attempt retryDisposition is invalid: {diagnosisPath}"));
        }

        if (!ContractLiteralCodec.TryParse(contract.ProcessAction, out DaemonStartupProcessAction processAction))
        {
            return DaemonLaunchAttemptReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon launch-attempt processAction is invalid: {diagnosisPath}"));
        }

        DaemonEditorMode? editorMode = null;
        if (contract.EditorMode is not null)
        {
            if (!ContractLiteralCodec.TryParse(contract.EditorMode, out DaemonEditorMode parsedEditorMode))
            {
                return DaemonLaunchAttemptReadResult.Failure(ExecutionError.InvalidArgument(
                    $"Daemon launch-attempt editorMode is invalid: {diagnosisPath}"));
            }

            editorMode = parsedEditorMode;
        }

        if (contract.StartedAtUtc == default || contract.StartedAtUtc.Offset != TimeSpan.Zero)
        {
            return DaemonLaunchAttemptReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon launch-attempt startedAtUtc is invalid: {diagnosisPath}"));
        }

        if (contract.UpdatedAtUtc == default || contract.UpdatedAtUtc.Offset != TimeSpan.Zero)
        {
            return DaemonLaunchAttemptReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon launch-attempt updatedAtUtc is invalid: {diagnosisPath}"));
        }

        if (contract.UpdatedAtUtc < contract.StartedAtUtc)
        {
            return DaemonLaunchAttemptReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon launch-attempt updatedAtUtc is invalid: {diagnosisPath}"));
        }

        if (contract.ProcessId is <= 0)
        {
            return DaemonLaunchAttemptReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon launch-attempt processId is invalid: {diagnosisPath}"));
        }

        if (contract.ProcessStartedAtUtc.HasValue
            && (contract.ProcessStartedAtUtc.Value == default
                || contract.ProcessStartedAtUtc.Value.Offset != TimeSpan.Zero))
        {
            return DaemonLaunchAttemptReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon launch-attempt processStartedAtUtc is invalid: {diagnosisPath}"));
        }

        if (contract.ProcessStartedAtUtc > contract.UpdatedAtUtc)
        {
            return DaemonLaunchAttemptReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon launch-attempt processStartedAtUtc is invalid: {diagnosisPath}"));
        }

        if (!TryValidateDiagnosis(contract.Diagnosis, diagnosisPath, out var diagnosisError))
        {
            return DaemonLaunchAttemptReadResult.Failure(diagnosisError!);
        }

        if (!TryParseOptionalAbsolutePath(contract.UnityLogPath, out var unityLogPath))
        {
            return DaemonLaunchAttemptReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon launch-attempt unityLogPath is invalid: {diagnosisPath}"));
        }

        if (!TryParseOptionalAbsolutePath(contract.Diagnosis.EditorInstancePath, out var editorInstancePath))
        {
            return DaemonLaunchAttemptReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon launch-attempt diagnosis.editorInstancePath is invalid: {diagnosisPath}"));
        }

        if (!TryParseOptionalAbsolutePath(contract.Diagnosis.UnityLogPath, out var diagnosisUnityLogPath))
        {
            return DaemonLaunchAttemptReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon launch-attempt diagnosis.unityLogPath is invalid: {diagnosisPath}"));
        }

        if (contract.LaunchAttemptId != directoryLaunchAttemptId)
        {
            return DaemonLaunchAttemptReadResult.Failure(ExecutionError.InvalidArgument(
                $"Daemon launch-attempt launchAttemptId does not match its directory: {diagnosisPath}"));
        }

        var diagnosis = contract.Diagnosis;
        return DaemonLaunchAttemptReadResult.Success(new DaemonLaunchAttempt(
            LaunchAttemptId: contract.LaunchAttemptId.Value,
            StartedAtUtc: contract.StartedAtUtc,
            UpdatedAtUtc: contract.UpdatedAtUtc,
            StartupStatus: startupStatus,
            StartupBlockingReason: startupBlockingReason,
            RetryDisposition: retryDisposition,
            ProcessAction: processAction,
            EditorMode: editorMode,
            ProcessId: contract.ProcessId,
            ProcessStartedAtUtc: contract.ProcessStartedAtUtc,
            UnityLogPath: unityLogPath,
            ArtifactPath: diagnosisPath,
            Diagnosis: new DaemonDiagnosis(
                Reason: diagnosis.Reason!.Value,
                Message: diagnosis.Message!,
                ReportedBy: diagnosis.ReportedBy!.Value,
                IsInferred: diagnosis.IsInferred!.Value,
                UpdatedAtUtc: diagnosis.UpdatedAtUtc,
                ProcessId: diagnosis.ProcessId,
                EditorInstancePath: editorInstancePath,
                SessionIssuedAtUtc: diagnosis.SessionIssuedAtUtc,
                ProcessStartedAtUtc: diagnosis.ProcessStartedAtUtc,
                UnityLogPath: diagnosisUnityLogPath,
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
                        Message: StringValueNormalizer.TrimToNull(diagnosis.PrimaryDiagnostic.Message)))));
    }

    private static bool TryValidateDiagnosis (
        DaemonDiagnosisJsonContract diagnosis,
        AbsolutePath diagnosisPath,
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

    private static void EnsureSafeLaunchAttemptDirectory (
        ContainedPath attemptDirectory)
    {
        if ((File.GetAttributes(attemptDirectory.Target.Value) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Launch-attempt deletion target must not be a reparse point: {attemptDirectory}");
        }
    }

    private static void DeleteOwnedLaunchAttemptDirectory (
        ContainedPath attemptDirectory)
    {
        EnsureSafeLaunchAttemptDirectory(attemptDirectory);

        var diagnosisPath = ContainedPath.Create(
            attemptDirectory.Target,
            RootRelativePath.Parse(UcliStoragePathNames.StartupDiagnosisFileName)).Target;
        using var entries = Directory.EnumerateFileSystemEntries(
                attemptDirectory.Target.Value)
            .GetEnumerator();
        if (!entries.MoveNext())
        {
            Directory.Delete(attemptDirectory.Target.Value, recursive: false);
            return;
        }

        if (!AbsolutePath.TryParse(entries.Current, out var entryPath, out _)
            || !entryPath.IsSameAs(diagnosisPath)
            || entries.MoveNext())
        {
            throw new IOException(
                $"Launch-attempt deletion target must contain only its owned startup diagnosis file: {attemptDirectory}");
        }

        FileUtilities.EnsureRegularFile(diagnosisPath, "Launch-attempt startup diagnosis file");
        File.Delete(diagnosisPath.Value);
        Directory.Delete(attemptDirectory.Target.Value, recursive: false);
    }

    private static void EnsureSafeStartupDiagnosisFile (
        ContainedPath diagnosisPath)
    {
        if (!File.Exists(diagnosisPath.Target.Value))
        {
            return;
        }

        if ((File.GetAttributes(diagnosisPath.Target.Value) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Launch-attempt diagnosis file must not be a reparse point: {diagnosisPath}");
        }
    }

    private static DateTimeOffset GetLastWriteTimeUtc (AbsolutePath path)
    {
        return new DateTimeOffset(Directory.GetLastWriteTimeUtc(path.Value), TimeSpan.Zero);
    }

    private static bool IsIoFailure (Exception exception)
    {
        return exception is IOException
            or UnauthorizedAccessException;
    }

    private sealed record LaunchAttemptDirectoryEntry (
        ContainedPath DirectoryPath,
        Guid LaunchAttemptId,
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
