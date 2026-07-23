using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Project;

#nullable enable annotations

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Stores recoverable IPC operation records under the project-local uCLI state directory. </summary>
    internal sealed class FileRecoverableIpcOperationStore : IRecoverableIpcOperationStore
    {
        private const int SchemaVersion = 1;
        private const long MaxRecordFileBytes = 1024 * 1024;
        private const int MaxMaintenanceRecordsPerRun = 128;
        private const string RecordFileName = "operation.json";
        private const string OperationLockFileName = "mutations.lock";

        private static readonly TimeSpan CompletedRecordTtl = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan PendingRecordTtl = TimeSpan.FromHours(24);
        private static readonly TimeSpan MaintenanceInterval = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan OperationLockAcquireTimeout = TimeSpan.FromSeconds(5);
        private static readonly long MaintenanceIntervalTimestampTicks =
            checked((long)(MaintenanceInterval.TotalSeconds * Stopwatch.Frequency));

        private readonly AbsolutePath operationsDirectoryPath;
        private readonly AbsolutePath operationLockPath;
        private readonly ProjectFingerprint projectFingerprint;
        private readonly int hostProcessId;
        private readonly Guid hostEditorInstanceId;
        private readonly SemaphoreSlim ioGate = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim maintenanceGate = new SemaphoreSlim(1, 1);

        private long nextMaintenanceTimestamp;
        private int maintenanceScheduled;
        private string? maintenanceFailureMessage;
        private string maintenanceCursorDirectoryName;

        private FileRecoverableIpcOperationStore (
            AbsolutePath operationsDirectoryPath,
            ProjectFingerprint projectFingerprint,
            int hostProcessId,
            Guid hostEditorInstanceId)
        {
            if (hostEditorInstanceId == Guid.Empty)
            {
                throw new ArgumentException("Host Editor instance id must not be empty.", nameof(hostEditorInstanceId));
            }

            this.operationsDirectoryPath =
                operationsDirectoryPath ?? throw new ArgumentNullException(nameof(operationsDirectoryPath));
            operationLockPath = ContainedPath.Create(
                operationsDirectoryPath,
                RootRelativePath.Parse(OperationLockFileName)).Target;
            this.projectFingerprint = projectFingerprint ?? throw new ArgumentNullException(nameof(projectFingerprint));
            this.hostProcessId = hostProcessId;
            this.hostEditorInstanceId = hostEditorInstanceId;
            nextMaintenanceTimestamp = Stopwatch.GetTimestamp() + MaintenanceIntervalTimestampTicks;
        }

        /// <summary> Creates a file-backed store for the Unity project served by the IPC host. </summary>
        /// <param name="projectIdentity"> The Unity project identity served by the host. </param>
        /// <param name="hostEditorInstanceId"> The non-empty Editor process identity captured for this host generation. </param>
        /// <returns> A store scoped to the supplied project and host generation.</returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="projectIdentity" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="hostEditorInstanceId" /> is empty. </exception>
        public static FileRecoverableIpcOperationStore Create (
            UnityHostProjectIdentity projectIdentity,
            Guid hostEditorInstanceId)
        {
            if (projectIdentity == null)
            {
                throw new ArgumentNullException(nameof(projectIdentity));
            }

            var storageRoot = UcliStoragePathResolver.ResolveStorageRoot(projectIdentity.ProjectPath);
            var projectDirectory = UcliStoragePathResolver.ResolveProjectDirectory(
                storageRoot,
                projectIdentity.ProjectFingerprint);
            var operationsDirectory = ContainedPath.Create(
                projectDirectory,
                RootRelativePath.Parse(UcliStoragePathNames.IpcOperationsDirectoryName)).Target;
            using var process = Process.GetCurrentProcess();
            return new FileRecoverableIpcOperationStore(
                operationsDirectory,
                projectIdentity.ProjectFingerprint,
                process.Id,
                hostEditorInstanceId);
        }

        /// <inheritdoc />
        public ValueTask<RecoverableIpcOperationReadResult> ReadAsync (
            UnityIpcMethod method,
            Guid requestId,
            Sha256Digest requestPayloadHash,
            CancellationToken cancellationToken)
        {
            if (!ContractLiteralCodec.IsDefined(method))
            {
                throw new ArgumentOutOfRangeException(nameof(method), method, "Unity IPC method must be defined.");
            }

            EnsureRequestId(requestId);

            if (requestPayloadHash == null)
            {
                throw new ArgumentNullException(nameof(requestPayloadHash));
            }

            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<RecoverableIpcOperationReadResult>(Task.Run(
                () => ReadSerializedAsync(
                    method,
                    requestId,
                    requestPayloadHash,
                    cancellationToken),
                cancellationToken));
        }

        /// <inheritdoc />
        public ValueTask<RecoverableIpcOperationStoreResult> WritePendingAsync (
            UnityIpcMethod method,
            Guid requestId,
            Sha256Digest requestPayloadHash,
            DateTimeOffset startedAtUtc,
            JsonElement recoveryPayload,
            CancellationToken cancellationToken)
        {
            if (!ContractLiteralCodec.IsDefined(method))
            {
                throw new ArgumentOutOfRangeException(nameof(method), method, "Unity IPC method must be defined.");
            }

            EnsureRequestId(requestId);

            if (requestPayloadHash == null)
            {
                throw new ArgumentNullException(nameof(requestPayloadHash));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var record = new RecoverableIpcOperationRecord
            {
                SchemaVersion = SchemaVersion,
                ProjectFingerprint = projectFingerprint,
                Method = method,
                RequestId = requestId,
                RequestPayloadHash = requestPayloadHash,
                HostProcessId = hostProcessId,
                HostEditorInstanceId = hostEditorInstanceId,
                State = RecoverableIpcOperationState.Pending,
                StartedAtUtc = startedAtUtc,
                RecoveryPayload = recoveryPayload.Clone(),
            };
            return WriteRecordOffMainThreadAsync(record, cancellationToken);
        }

        /// <inheritdoc />
        public ValueTask<RecoverableIpcOperationStoreResult> WriteCompletedAsync (
            UnityIpcMethod method,
            Guid requestId,
            Sha256Digest requestPayloadHash,
            DateTimeOffset startedAtUtc,
            DateTimeOffset completedAtUtc,
            JsonElement recoveryPayload,
            IpcResponse response,
            CancellationToken cancellationToken)
        {
            if (!ContractLiteralCodec.IsDefined(method))
            {
                throw new ArgumentOutOfRangeException(nameof(method), method, "Unity IPC method must be defined.");
            }

            EnsureRequestId(requestId);

            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            if (response.RequestId != requestId)
            {
                throw new ArgumentException(
                    "Response request id must match the recoverable operation request id.",
                    nameof(response));
            }

            if (requestPayloadHash == null)
            {
                throw new ArgumentNullException(nameof(requestPayloadHash));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var record = new RecoverableIpcOperationRecord
            {
                SchemaVersion = SchemaVersion,
                ProjectFingerprint = projectFingerprint,
                Method = method,
                RequestId = requestId,
                RequestPayloadHash = requestPayloadHash,
                HostProcessId = hostProcessId,
                HostEditorInstanceId = hostEditorInstanceId,
                State = RecoverableIpcOperationState.Completed,
                StartedAtUtc = startedAtUtc,
                CompletedAtUtc = completedAtUtc,
                RecoveryPayload = recoveryPayload.Clone(),
                Response = response,
            };
            return WriteRecordOffMainThreadAsync(record, cancellationToken);
        }

        /// <summary> Runs one bounded maintenance pass. Production callers schedule this outside request execution. </summary>
        internal ValueTask<RecoverableIpcOperationStoreResult> PurgeExpiredRecordsAsync (
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<RecoverableIpcOperationStoreResult>(Task.Run(
                () => PurgeExpiredRecordsSerializedAsync(nowUtc, cancellationToken),
                cancellationToken));
        }

        /// <inheritdoc />
        public string? ConsumeMaintenanceFailure ()
        {
            return Interlocked.Exchange(ref maintenanceFailureMessage, null);
        }

        private async Task<RecoverableIpcOperationReadResult> ReadSerializedAsync (
            UnityIpcMethod method,
            Guid requestId,
            Sha256Digest requestPayloadHash,
            CancellationToken cancellationToken)
        {
            await ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                try
                {
                    var path = ResolveRecordPath(requestId);
                    if (!File.Exists(path.Value))
                    {
                        return RecoverableIpcOperationReadResult.Missing();
                    }

                    EnsureReadableRecordPath(path);
                    var record = JsonSerializer.Deserialize<RecoverableIpcOperationRecord>(
                        await ReadRecordTextAsync(path, cancellationToken).ConfigureAwait(false),
                        IpcJsonSerializerOptions.Default);
                    if (!IsValidRecord(record, method, requestId, requestPayloadHash, out var errorMessage))
                    {
                        return RecoverableIpcOperationReadResult.Failure(errorMessage);
                    }

                    return RecoverableIpcOperationReadResult.Success(record);
                }
                catch (Exception exception) when (
                    exception is IOException
                    or UnauthorizedAccessException
                    or JsonException
                    or ArgumentException)
                {
                    return RecoverableIpcOperationReadResult.Failure(exception.Message);
                }
            }
            finally
            {
                ioGate.Release();
                RequestMaintenance();
            }
        }

        private ValueTask<RecoverableIpcOperationStoreResult> WriteRecordOffMainThreadAsync (
            RecoverableIpcOperationRecord record,
            CancellationToken cancellationToken)
        {
            return new ValueTask<RecoverableIpcOperationStoreResult>(Task.Run(
                () => WriteRecordSerializedAsync(record, cancellationToken),
                cancellationToken));
        }

        private async Task<RecoverableIpcOperationStoreResult> WriteRecordSerializedAsync (
            RecoverableIpcOperationRecord record,
            CancellationToken cancellationToken)
        {
            await ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                try
                {
                    using var operationLock = await FileExclusiveLock.AcquireAsync(
                            operationLockPath,
                            OperationLockAcquireTimeout,
                            cancellationToken)
                        .ConfigureAwait(false);
                    var path = ResolveRecordPath(record.RequestId);
                    if (!path.TryGetParent(out var directoryPath))
                    {
                        throw new InvalidOperationException($"Recoverable IPC operation directory path could not be resolved: {path}");
                    }

                    FileSystemAccessBoundary.EnsureSecureDirectory(directoryPath);
                    var json = JsonSerializer.Serialize(record, IpcJsonSerializerOptions.Default) + Environment.NewLine;
                    if (Encoding.UTF8.GetByteCount(json) > MaxRecordFileBytes)
                    {
                        throw new IOException($"Recoverable IPC operation record exceeds the maximum size: {path}");
                    }

                    await FileUtilities.WriteAllTextAtomicallyAsync(path, json, cancellationToken).ConfigureAwait(false);
                    FileSystemAccessBoundary.EnsureSecureFile(path);
                    return RecoverableIpcOperationStoreResult.Success();
                }
                catch (Exception exception) when (
                    exception is IOException
                    or UnauthorizedAccessException
                    or JsonException
                    or InvalidOperationException
                    or ArgumentException
                    or TimeoutException)
                {
                    return RecoverableIpcOperationStoreResult.Failure(exception.Message);
                }
            }
            finally
            {
                ioGate.Release();
                RequestMaintenance();
            }
        }

        private async Task<RecoverableIpcOperationStoreResult> PurgeExpiredRecordsSerializedAsync (
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken)
        {
            await maintenanceGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await PurgeExpiredRecordsCoreAsync(nowUtc, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                maintenanceGate.Release();
            }
        }

        private async Task<RecoverableIpcOperationStoreResult> PurgeExpiredRecordsCoreAsync (
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken)
        {
            try
            {
                if (!Directory.Exists(operationsDirectoryPath.Value))
                {
                    return RecoverableIpcOperationStoreResult.Success();
                }

                if (IsReparsePoint(operationsDirectoryPath))
                {
                    return RecoverableIpcOperationStoreResult.Failure(
                        $"Recoverable IPC operations directory must not be a reparse point: {operationsDirectoryPath}");
                }

                var operationDirectoryPaths = Directory
                    .EnumerateDirectories(operationsDirectoryPath.Value)
                    .Select(AbsolutePath.Parse)
                    .Where(path => TryGetOwnedOperationRequestId(path, out _))
                    .OrderBy(path => Path.GetFileName(path.Value), StringComparer.Ordinal)
                    .ToArray();
                if (operationDirectoryPaths.Length == 0)
                {
                    maintenanceCursorDirectoryName = null;
                    return RecoverableIpcOperationStoreResult.Success();
                }

                var startIndex = ResolveMaintenanceStartIndex(operationDirectoryPaths);
                var processedRecordCount = Math.Min(MaxMaintenanceRecordsPerRun, operationDirectoryPaths.Length);
                for (var offset = 0; offset < processedRecordCount; offset++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var operationDirectoryPath = operationDirectoryPaths[
                        (startIndex + offset) % operationDirectoryPaths.Length];
                    maintenanceCursorDirectoryName = Path.GetFileName(operationDirectoryPath.Value);
                    await PurgeOperationDirectoryAsync(
                            operationDirectoryPath,
                            nowUtc,
                            cancellationToken)
                        .ConfigureAwait(false);

                    // Do not let best-effort maintenance continuously reacquire the request I/O gate.
                    await Task.Yield();
                }

                return RecoverableIpcOperationStoreResult.Success();
            }
            catch (Exception exception) when (
                exception is IOException
                or UnauthorizedAccessException
                or JsonException
                or TimeoutException)
            {
                return RecoverableIpcOperationStoreResult.Failure(exception.Message);
            }
        }

        private async Task PurgeOperationDirectoryAsync (
            AbsolutePath operationDirectoryPath,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken)
        {
            await ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using var operationLock = await FileExclusiveLock.AcquireAsync(
                        operationLockPath,
                        OperationLockAcquireTimeout,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!TryGetOwnedOperationRequestId(operationDirectoryPath, out var requestId))
                {
                    return;
                }

                var recordPath = ContainedPath.Create(
                    operationDirectoryPath,
                    RootRelativePath.Parse(RecordFileName)).Target;
                if (!File.Exists(recordPath.Value))
                {
                    TryDeleteEmptyOwnedDirectory(operationDirectoryPath, requestId);
                    return;
                }

                if (!ShouldPurgeRecordFile(recordPath, requestId, nowUtc))
                {
                    return;
                }

                FileUtilities.EnsureRegularFile(recordPath, "Recoverable IPC operation record");
                File.Delete(recordPath.Value);
                TryDeleteEmptyOwnedDirectory(operationDirectoryPath, requestId);
            }
            finally
            {
                ioGate.Release();
            }
        }

        private int ResolveMaintenanceStartIndex (AbsolutePath[] operationDirectoryPaths)
        {
            if (string.IsNullOrWhiteSpace(maintenanceCursorDirectoryName))
            {
                return 0;
            }

            var nextIndex = Array.FindIndex(
                operationDirectoryPaths,
                path => string.Compare(
                    Path.GetFileName(path.Value),
                    maintenanceCursorDirectoryName,
                    StringComparison.Ordinal) > 0);
            return nextIndex >= 0 ? nextIndex : 0;
        }

        private void RequestMaintenance ()
        {
            var nowTimestamp = Stopwatch.GetTimestamp();
            if (nowTimestamp < Volatile.Read(ref nextMaintenanceTimestamp)
                || Interlocked.CompareExchange(ref maintenanceScheduled, 1, 0) != 0)
            {
                return;
            }

            _ = Task.Run(RunScheduledMaintenanceAsync);
        }

        private async Task RunScheduledMaintenanceAsync ()
        {
            try
            {
                var result = await PurgeExpiredRecordsSerializedAsync(
                        DateTimeOffset.UtcNow,
                        CancellationToken.None)
                    .ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    Interlocked.Exchange(ref maintenanceFailureMessage, result.ErrorMessage);
                }
            }
            catch (Exception exception)
            {
                // Maintenance is best effort. A later main-thread dispatch consumes this message.
                Interlocked.Exchange(ref maintenanceFailureMessage, exception.Message);
            }
            finally
            {
                Volatile.Write(
                    ref nextMaintenanceTimestamp,
                    Stopwatch.GetTimestamp() + MaintenanceIntervalTimestampTicks);
                Volatile.Write(ref maintenanceScheduled, 0);
            }
        }

        private AbsolutePath ResolveRecordPath (Guid requestId)
        {
            var operationRelativePath = RootRelativePath.Parse(
                $"{StoragePathSegmentCodec.EncodeGuid(requestId, nameof(requestId))}/{RecordFileName}");
            return ContainedPath.Create(
                operationsDirectoryPath,
                operationRelativePath).Target;
        }

        private bool IsValidRecord (
            RecoverableIpcOperationRecord record,
            UnityIpcMethod method,
            Guid requestId,
            Sha256Digest requestPayloadHash,
            out string errorMessage)
        {
            // NOTE: Operation records are scoped to the current Editor host. This prevents
            // stale pending/completed records from an older daemon process from being replayed.
            if (record == null
                || record.SchemaVersion != SchemaVersion
                || record.ProjectFingerprint != projectFingerprint
                || record.Method != method
                || record.RequestId != requestId
                || record.RequestPayloadHash != requestPayloadHash
                || record.HostProcessId != hostProcessId
                || record.HostEditorInstanceId != hostEditorInstanceId)
            {
                errorMessage = "Recoverable IPC operation record identity is invalid.";
                return false;
            }

            if (!record.HasState)
            {
                errorMessage = record.HasPersistedState
                    ? $"Recoverable IPC operation state is unsupported: {record.UnsupportedPersistedState}."
                    : "Recoverable IPC operation state is missing.";
                return false;
            }

            if (record.State == RecoverableIpcOperationState.Pending)
            {
                if (record.RecoveryPayload.ValueKind == JsonValueKind.Undefined)
                {
                    errorMessage = "Recoverable IPC operation pending payload is missing.";
                    return false;
                }

                errorMessage = null;
                return true;
            }

            if (record.State == RecoverableIpcOperationState.Completed)
            {
                if (record.Response == null || !record.CompletedAtUtc.HasValue)
                {
                    errorMessage = "Recoverable IPC operation completed response is missing.";
                    return false;
                }

                if (record.Response.RequestId != requestId)
                {
                    errorMessage = "Recoverable IPC operation completed response identity is invalid.";
                    return false;
                }

                errorMessage = null;
                return true;
            }

            errorMessage = $"Recoverable IPC operation state is unsupported: {record.State}.";
            return false;
        }

        private static bool TryReadRecordFile (
            AbsolutePath recordPath,
            out RecoverableIpcOperationRecord record)
        {
            try
            {
                record = JsonSerializer.Deserialize<RecoverableIpcOperationRecord>(
                    ReadRecordText(recordPath),
                    IpcJsonSerializerOptions.Default);
                return record != null;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                record = null;
                return false;
            }
        }

        private static string ReadRecordText (AbsolutePath recordPath)
        {
            using (var stream = FileUtilities.OpenReopenSafeReadStream(recordPath))
            using (var memoryStream = new MemoryStream())
            {
                // NOTE: Keep the in-loop byte count even after the initial length check.
                // Operation records can change between metadata reads and stream reads.
                if (stream.Length > MaxRecordFileBytes)
                {
                    throw new IOException($"Recoverable IPC operation record exceeds the maximum size: {recordPath}");
                }

                var buffer = new byte[8192];
                long totalBytesRead = 0;
                while (true)
                {
                    var bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    totalBytesRead += bytesRead;
                    if (totalBytesRead > MaxRecordFileBytes)
                    {
                        throw new IOException($"Recoverable IPC operation record exceeds the maximum size: {recordPath}");
                    }

                    memoryStream.Write(buffer, 0, bytesRead);
                }

                return Encoding.UTF8.GetString(memoryStream.ToArray());
            }
        }

        private static async Task<string> ReadRecordTextAsync (
            AbsolutePath recordPath,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (var stream = FileUtilities.OpenReopenSafeReadStream(recordPath))
            using (var memoryStream = new MemoryStream())
            {
                if (stream.Length > MaxRecordFileBytes)
                {
                    throw new IOException($"Recoverable IPC operation record exceeds the maximum size: {recordPath}");
                }

                var buffer = new byte[8192];
                long totalBytesRead = 0;
                while (true)
                {
                    var bytesRead = await stream.ReadAsync(
                        buffer,
                        0,
                        buffer.Length,
                        cancellationToken).ConfigureAwait(false);
                    if (bytesRead == 0)
                    {
                        return Encoding.UTF8.GetString(memoryStream.ToArray());
                    }

                    totalBytesRead += bytesRead;
                    if (totalBytesRead > MaxRecordFileBytes)
                    {
                        throw new IOException($"Recoverable IPC operation record exceeds the maximum size: {recordPath}");
                    }

                    memoryStream.Write(buffer, 0, bytesRead);
                }
            }
        }

        private void EnsureReadableRecordPath (AbsolutePath path)
        {
            if (Directory.Exists(operationsDirectoryPath.Value)
                && IsReparsePoint(operationsDirectoryPath))
            {
                throw new IOException($"Recoverable IPC operations directory must not be a reparse point: {operationsDirectoryPath}");
            }

            if (path.TryGetParent(out var operationDirectoryPath)
                && Directory.Exists(operationDirectoryPath.Value)
                && IsReparsePoint(operationDirectoryPath))
            {
                throw new IOException(
                    $"Recoverable IPC operation directory must not be a reparse point: {operationDirectoryPath}");
            }
        }

        private static bool IsReparsePoint (AbsolutePath path)
        {
            try
            {
                return (File.GetAttributes(path.Value) & FileAttributes.ReparsePoint) != 0;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return true;
            }
        }

        private bool ShouldPurgeRecordFile (
            AbsolutePath recordPath,
            Guid requestId,
            DateTimeOffset nowUtc)
        {
            if (!TryReadRecordFile(recordPath, out var record)
                || !IsOwnedRecord(record, requestId))
            {
                return false;
            }

            return record.State == RecoverableIpcOperationState.Completed
                ? nowUtc - record.CompletedAtUtc.Value > CompletedRecordTtl
                : nowUtc - record.StartedAtUtc > PendingRecordTtl;
        }

        private bool IsOwnedRecord (
            RecoverableIpcOperationRecord record,
            Guid requestId)
        {
            if (record == null
                || record.SchemaVersion != SchemaVersion
                || record.ProjectFingerprint != projectFingerprint
                || !ContractLiteralCodec.IsDefined(record.Method)
                || record.RequestId != requestId
                || record.RequestPayloadHash == null
                || record.HostProcessId != hostProcessId
                || record.HostEditorInstanceId != hostEditorInstanceId
                || !record.HasState
                || record.RecoveryPayload.ValueKind == JsonValueKind.Undefined)
            {
                return false;
            }

            return (record.State == RecoverableIpcOperationState.Pending
                    && !record.CompletedAtUtc.HasValue
                    && record.Response == null)
                || (record.State == RecoverableIpcOperationState.Completed
                    && record.CompletedAtUtc.HasValue
                    && record.Response != null
                    && record.Response.RequestId == requestId);
        }

        private static bool TryGetOwnedOperationRequestId (
            AbsolutePath directoryPath,
            out Guid requestId)
        {
            var directoryName = Path.GetFileName(directoryPath.Value);
            return StoragePathSegmentCodec.TryDecodeNonEmptyGuid(directoryName, out requestId)
                && Directory.Exists(directoryPath.Value)
                && !IsReparsePoint(directoryPath);
        }

        private static void TryDeleteEmptyOwnedDirectory (
            AbsolutePath directoryPath,
            Guid requestId)
        {
            try
            {
                if (TryGetOwnedOperationRequestId(directoryPath, out var currentRequestId)
                    && currentRequestId == requestId
                    && Directory.GetFileSystemEntries(directoryPath.Value).Length == 0)
                {
                    Directory.Delete(directoryPath.Value, recursive: false);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }

        private static void EnsureRequestId (Guid requestId)
        {
            if (requestId == Guid.Empty)
            {
                throw new ArgumentException("Request id must not be empty.", nameof(requestId));
            }
        }

    }
}
