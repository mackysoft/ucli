using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Storage;

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

        private static readonly TimeSpan CompletedRecordTtl = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan PendingRecordTtl = TimeSpan.FromHours(24);
        private static readonly TimeSpan InvalidRecordTtl = TimeSpan.FromHours(24);
        private static readonly TimeSpan MaintenanceInterval = TimeSpan.FromMinutes(1);
        private static readonly long MaintenanceIntervalTimestampTicks =
            checked((long)(MaintenanceInterval.TotalSeconds * Stopwatch.Frequency));

        private readonly string operationsDirectoryPath;
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
            string operationsDirectoryPath,
            ProjectFingerprint projectFingerprint,
            int hostProcessId,
            Guid hostEditorInstanceId)
        {
            if (string.IsNullOrWhiteSpace(operationsDirectoryPath))
            {
                throw new ArgumentException("Operations directory path must not be empty.", nameof(operationsDirectoryPath));
            }

            if (hostEditorInstanceId == Guid.Empty)
            {
                throw new ArgumentException("Host Editor instance id must not be empty.", nameof(hostEditorInstanceId));
            }

            this.operationsDirectoryPath = operationsDirectoryPath;
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
            IpcProjectIdentity projectIdentity,
            Guid hostEditorInstanceId)
        {
            if (projectIdentity == null)
            {
                throw new ArgumentNullException(nameof(projectIdentity));
            }

            var storageRoot = UcliStoragePathResolver.ResolveStorageRoot(projectIdentity.ProjectPath);
            var fingerprintDirectory = UcliStoragePathResolver.ResolveFingerprintDirectory(
                storageRoot,
                projectIdentity.ProjectFingerprint);
            using var process = Process.GetCurrentProcess();
            return new FileRecoverableIpcOperationStore(
                Path.Combine(fingerprintDirectory, UcliStoragePathNames.IpcOperationsDirectoryName),
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

            if (requestPayloadHash == null)
            {
                throw new ArgumentNullException(nameof(requestPayloadHash));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var record = new RecoverableIpcOperationRecord
            {
                SchemaVersion = SchemaVersion,
                ProjectFingerprint = projectFingerprint,
                Method = ContractLiteralCodec.ToValue(method),
                RequestId = requestId,
                RequestPayloadHash = requestPayloadHash.ToString(),
                HostProcessId = hostProcessId,
                HostEditorInstanceId = hostEditorInstanceId.ToString("N"),
                State = RecoverableIpcOperationState.Pending,
                StartedAtUtc = startedAtUtc,
                RecoveryPayload = recoveryPayload.Clone(),
            };
            return WriteRecordOffMainThreadAsync(method, record, cancellationToken);
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
                Method = ContractLiteralCodec.ToValue(method),
                RequestId = requestId,
                RequestPayloadHash = requestPayloadHash.ToString(),
                HostProcessId = hostProcessId,
                HostEditorInstanceId = hostEditorInstanceId.ToString("N"),
                State = RecoverableIpcOperationState.Completed,
                StartedAtUtc = startedAtUtc,
                CompletedAtUtc = completedAtUtc,
                RecoveryPayload = recoveryPayload.Clone(),
                Response = response,
            };
            return WriteRecordOffMainThreadAsync(method, record, cancellationToken);
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
                    var path = ResolveRecordPath(method, requestId);
                    if (!File.Exists(path))
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
            UnityIpcMethod method,
            RecoverableIpcOperationRecord record,
            CancellationToken cancellationToken)
        {
            return new ValueTask<RecoverableIpcOperationStoreResult>(Task.Run(
                () => WriteRecordSerializedAsync(method, record, cancellationToken),
                cancellationToken));
        }

        private async Task<RecoverableIpcOperationStoreResult> WriteRecordSerializedAsync (
            UnityIpcMethod method,
            RecoverableIpcOperationRecord record,
            CancellationToken cancellationToken)
        {
            await ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                try
                {
                    var path = ResolveRecordPath(method, record.RequestId);
                    var directoryPath = Path.GetDirectoryName(path);
                    if (string.IsNullOrWhiteSpace(directoryPath))
                    {
                        throw new InvalidOperationException($"Recoverable IPC operation directory path could not be resolved: {path}");
                    }

                    FileSystemAccessBoundary.EnsureSecureDirectory(directoryPath);
                    var json = JsonSerializer.Serialize(record, IpcJsonSerializerOptions.Default) + Environment.NewLine;
                    if (Encoding.UTF8.GetByteCount(json) > MaxRecordFileBytes)
                    {
                        throw new IOException($"Recoverable IPC operation record exceeds the maximum size: {path}");
                    }

                    EnsureWritableRecordFile(path);
                    await FileUtilities.WriteAllTextAtomicallyAsync(path, json, cancellationToken).ConfigureAwait(false);
                    FileSystemAccessBoundary.EnsureSecureFile(path);
                    return RecoverableIpcOperationStoreResult.Success();
                }
                catch (Exception exception) when (
                    exception is IOException
                    or UnauthorizedAccessException
                    or JsonException
                    or InvalidOperationException
                    or ArgumentException)
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
                if (!Directory.Exists(operationsDirectoryPath))
                {
                    return RecoverableIpcOperationStoreResult.Success();
                }

                if (IsReparsePoint(operationsDirectoryPath))
                {
                    return RecoverableIpcOperationStoreResult.Failure(
                        $"Recoverable IPC operations directory must not be a reparse point: {operationsDirectoryPath}");
                }

                var operationDirectoryPaths = Directory
                    .EnumerateDirectories(operationsDirectoryPath)
                    .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
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
                    var operationDirectoryPath = operationDirectoryPaths[(startIndex + offset) % operationDirectoryPaths.Length];
                    maintenanceCursorDirectoryName = Path.GetFileName(operationDirectoryPath);
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
                or JsonException)
            {
                return RecoverableIpcOperationStoreResult.Failure(exception.Message);
            }
        }

        private async Task PurgeOperationDirectoryAsync (
            string operationDirectoryPath,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken)
        {
            await ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (IsReparsePoint(operationDirectoryPath))
                {
                    return;
                }

                var recordPath = Path.Combine(operationDirectoryPath, RecordFileName);
                if (!File.Exists(recordPath))
                {
                    TryDeleteEmptyDirectory(operationDirectoryPath);
                    return;
                }

                if (!ShouldPurgeRecordFile(recordPath, nowUtc))
                {
                    return;
                }

                FileUtilities.DeleteIfExists(recordPath);
                TryDeleteEmptyDirectory(operationDirectoryPath);
            }
            finally
            {
                ioGate.Release();
            }
        }

        private int ResolveMaintenanceStartIndex (string[] operationDirectoryPaths)
        {
            if (string.IsNullOrWhiteSpace(maintenanceCursorDirectoryName))
            {
                return 0;
            }

            var nextIndex = Array.FindIndex(
                operationDirectoryPaths,
                path => string.Compare(
                    Path.GetFileName(path),
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

        private string ResolveRecordPath (
            UnityIpcMethod method,
            Guid requestId)
        {
            if (!ContractLiteralCodec.IsDefined(method))
            {
                throw new ArgumentOutOfRangeException(nameof(method), method, "Unity IPC method must be defined.");
            }

            if (requestId == Guid.Empty)
            {
                throw new ArgumentException("Request id must not be empty.", nameof(requestId));
            }

            var identity = string.Concat(
                projectFingerprint.ToString(),
                "\n",
                ContractLiteralCodec.ToValue(method),
                "\n",
                requestId.ToString("D"));
            var operationKey = Sha256Digest.Compute(Encoding.UTF8.GetBytes(identity));
            return Path.Combine(operationsDirectoryPath, operationKey.ToString(), RecordFileName);
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
                || !string.Equals(record.Method, ContractLiteralCodec.ToValue(method), StringComparison.Ordinal)
                || record.RequestId != requestId
                || !Sha256Digest.TryParse(record.RequestPayloadHash, out var storedRequestPayloadHash)
                || storedRequestPayloadHash != requestPayloadHash
                || record.HostProcessId != hostProcessId
                || record.HostEditorInstanceId == null
                || record.HostEditorInstanceId.Length != 32
                || !Guid.TryParseExact(record.HostEditorInstanceId, "N", out var storedHostEditorInstanceId)
                || storedHostEditorInstanceId == Guid.Empty
                || storedHostEditorInstanceId != hostEditorInstanceId)
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
            string recordPath,
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

        private static string ReadRecordText (string recordPath)
        {
            EnsureReadableRecordFile(recordPath);
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
            string recordPath,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureReadableRecordFile(recordPath);
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

        private static void EnsureReadableRecordFile (string path)
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"Recoverable IPC operation record must not be a reparse point: {path}");
            }

            if ((attributes & FileAttributes.Directory) != 0)
            {
                throw new IOException($"Recoverable IPC operation record must not be a directory: {path}");
            }
        }

        private void EnsureReadableRecordPath (string path)
        {
            if (Directory.Exists(operationsDirectoryPath)
                && IsReparsePoint(operationsDirectoryPath))
            {
                throw new IOException($"Recoverable IPC operations directory must not be a reparse point: {operationsDirectoryPath}");
            }

            var operationDirectoryPath = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(operationDirectoryPath)
                && Directory.Exists(operationDirectoryPath)
                && IsReparsePoint(operationDirectoryPath))
            {
                throw new IOException($"Recoverable IPC operation directory must not be a reparse point: {operationDirectoryPath}");
            }
        }

        private static void EnsureWritableRecordFile (string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                return;
            }

            EnsureReadableRecordFile(path);
        }

        private static bool IsReparsePoint (string path)
        {
            try
            {
                return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return true;
            }
        }

        private static bool IsExpiredCompletedRecord (
            RecoverableIpcOperationRecord record,
            DateTimeOffset nowUtc)
        {
            return record != null
                && record.HasState
                && record.State == RecoverableIpcOperationState.Completed
                && record.CompletedAtUtc.HasValue
                && nowUtc - record.CompletedAtUtc.Value > CompletedRecordTtl;
        }

        private static bool IsExpiredPendingRecord (
            RecoverableIpcOperationRecord record,
            DateTimeOffset nowUtc)
        {
            return record != null
                && record.HasState
                && record.State == RecoverableIpcOperationState.Pending
                && nowUtc - record.StartedAtUtc > PendingRecordTtl;
        }

        private static bool ShouldPurgeRecordFile (
            string recordPath,
            DateTimeOffset nowUtc)
        {
            if (!TryReadRecordFile(recordPath, out var record))
            {
                return IsRecordFileOlderThan(recordPath, nowUtc, InvalidRecordTtl);
            }

            if (IsExpiredCompletedRecord(record, nowUtc)
                || IsExpiredPendingRecord(record, nowUtc))
            {
                return true;
            }

            if (!record.HasState || !IsSupportedState(record.State))
            {
                return IsRecordFileOlderThan(recordPath, nowUtc, InvalidRecordTtl);
            }

            return false;
        }

        private static bool IsSupportedState (RecoverableIpcOperationState state)
        {
            return state is RecoverableIpcOperationState.Pending or RecoverableIpcOperationState.Completed;
        }

        private static bool IsRecordFileOlderThan (
            string recordPath,
            DateTimeOffset nowUtc,
            TimeSpan ttl)
        {
            try
            {
                var lastWriteUtc = new DateTimeOffset(File.GetLastWriteTimeUtc(recordPath), TimeSpan.Zero);
                return nowUtc - lastWriteUtc > ttl;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static void TryDeleteEmptyDirectory (string directoryPath)
        {
            try
            {
                if (Directory.Exists(directoryPath)
                    && Directory.GetFileSystemEntries(directoryPath).Length == 0)
                {
                    Directory.Delete(directoryPath);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
