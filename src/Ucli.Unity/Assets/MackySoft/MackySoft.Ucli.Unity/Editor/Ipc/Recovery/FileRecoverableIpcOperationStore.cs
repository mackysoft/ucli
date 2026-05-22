using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Cryptography;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Stores recoverable IPC operation records under the project-local uCLI state directory. </summary>
    internal sealed class FileRecoverableIpcOperationStore : IRecoverableIpcOperationStore
    {
        private const int SchemaVersion = 1;
        private const long MaxRecordFileBytes = 1024 * 1024;
        private const int TemporaryRecordTokenLength = 12;
        private const string RecordFileName = "operation.json";

        private static readonly TimeSpan CompletedRecordTtl = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan PendingRecordTtl = TimeSpan.FromHours(24);
        private static readonly TimeSpan InvalidRecordTtl = TimeSpan.FromHours(24);

        private readonly string operationsDirectoryPath;
        private readonly string projectFingerprint;
        private readonly int hostProcessId;
        private readonly string hostEditorInstanceId;

        private FileRecoverableIpcOperationStore (
            string operationsDirectoryPath,
            string projectFingerprint,
            int hostProcessId,
            string hostEditorInstanceId)
        {
            if (string.IsNullOrWhiteSpace(operationsDirectoryPath))
            {
                throw new ArgumentException("Operations directory path must not be empty.", nameof(operationsDirectoryPath));
            }

            if (string.IsNullOrWhiteSpace(projectFingerprint))
            {
                throw new ArgumentException("Project fingerprint must not be empty.", nameof(projectFingerprint));
            }

            this.operationsDirectoryPath = operationsDirectoryPath;
            this.projectFingerprint = projectFingerprint;
            this.hostProcessId = hostProcessId;
            this.hostEditorInstanceId = hostEditorInstanceId;
        }

        /// <summary> Creates a file-backed store for the Unity project served by the IPC host. </summary>
        public static FileRecoverableIpcOperationStore Create (IpcProjectIdentity projectIdentity)
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
                UnityEditorProcessIdentity.GetEditorInstanceId());
        }

        /// <inheritdoc />
        public bool TryRead (
            string method,
            string requestId,
            string requestPayloadHash,
            out RecoverableIpcOperationRecord record,
            out string errorMessage)
        {
            record = null;
            if (string.IsNullOrWhiteSpace(requestPayloadHash))
            {
                errorMessage = "Request payload hash must not be empty.";
                return false;
            }

            try
            {
                var path = ResolveRecordPath(method, requestId);
                if (!File.Exists(path))
                {
                    errorMessage = null;
                    return false;
                }

                EnsureReadableRecordPath(path);
                record = JsonSerializer.Deserialize<RecoverableIpcOperationRecord>(
                    ReadRecordText(path),
                    IpcJsonSerializerOptions.Default);
                if (!IsValidRecord(record, method, requestId, requestPayloadHash, out errorMessage))
                {
                    record = null;
                    return false;
                }

                errorMessage = null;
                return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
            {
                errorMessage = exception.Message;
                return false;
            }
        }

        /// <inheritdoc />
        public bool TryWritePending (
            string method,
            string requestId,
            string requestPayloadHash,
            DateTimeOffset startedAtUtc,
            JsonElement recoveryPayload,
            out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(requestPayloadHash))
            {
                errorMessage = "Request payload hash must not be empty.";
                return false;
            }

            var record = new RecoverableIpcOperationRecord
            {
                SchemaVersion = SchemaVersion,
                ProjectFingerprint = projectFingerprint,
                Method = method,
                RequestId = requestId,
                RequestPayloadHash = requestPayloadHash,
                HostProcessId = hostProcessId,
                HostEditorInstanceId = hostEditorInstanceId,
                State = RecoverableIpcOperationStateNames.Pending,
                StartedAtUtc = startedAtUtc,
                RecoveryPayload = recoveryPayload.Clone(),
            };
            return TryWriteRecord(record, out errorMessage);
        }

        /// <inheritdoc />
        public bool TryWriteCompleted (
            string method,
            string requestId,
            string requestPayloadHash,
            DateTimeOffset startedAtUtc,
            DateTimeOffset completedAtUtc,
            JsonElement recoveryPayload,
            IpcResponse response,
            out string errorMessage)
        {
            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            if (string.IsNullOrWhiteSpace(requestPayloadHash))
            {
                errorMessage = "Request payload hash must not be empty.";
                return false;
            }

            var record = new RecoverableIpcOperationRecord
            {
                SchemaVersion = SchemaVersion,
                ProjectFingerprint = projectFingerprint,
                Method = method,
                RequestId = requestId,
                RequestPayloadHash = requestPayloadHash,
                HostProcessId = hostProcessId,
                HostEditorInstanceId = hostEditorInstanceId,
                State = RecoverableIpcOperationStateNames.Completed,
                StartedAtUtc = startedAtUtc,
                CompletedAtUtc = completedAtUtc,
                RecoveryPayload = recoveryPayload.Clone(),
                Response = response,
            };
            return TryWriteRecord(record, out errorMessage);
        }

        /// <inheritdoc />
        public bool TryPurgeExpiredRecords (
            DateTimeOffset nowUtc,
            out string errorMessage)
        {
            try
            {
                if (!Directory.Exists(operationsDirectoryPath))
                {
                    errorMessage = null;
                    return true;
                }

                if (IsReparsePoint(operationsDirectoryPath))
                {
                    errorMessage = $"Recoverable IPC operations directory must not be a reparse point: {operationsDirectoryPath}";
                    return false;
                }

                foreach (var operationDirectoryPath in Directory.EnumerateDirectories(operationsDirectoryPath))
                {
                    if (IsReparsePoint(operationDirectoryPath))
                    {
                        continue;
                    }

                    var recordPath = Path.Combine(operationDirectoryPath, RecordFileName);
                    if (!File.Exists(recordPath))
                    {
                        TryDeleteEmptyDirectory(operationDirectoryPath);
                        continue;
                    }

                    if (!ShouldPurgeRecordFile(recordPath, nowUtc))
                    {
                        continue;
                    }

                    FileUtilities.DeleteIfExists(recordPath);
                    TryDeleteEmptyDirectory(operationDirectoryPath);
                }

                errorMessage = null;
                return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                errorMessage = exception.Message;
                return false;
            }
        }

        private bool TryWriteRecord (
            RecoverableIpcOperationRecord record,
            out string errorMessage)
        {
            try
            {
                TryPurgeExpiredRecords(DateTimeOffset.UtcNow, out _);
                var path = ResolveRecordPath(record.Method, record.RequestId);
                var directoryPath = Path.GetDirectoryName(path);
                if (string.IsNullOrWhiteSpace(directoryPath))
                {
                    throw new InvalidOperationException($"Recoverable IPC operation directory path could not be resolved: {path}");
                }

                FileSystemAccessBoundary.EnsureSecureDirectory(directoryPath);
                var json = JsonSerializer.Serialize(record, IpcJsonSerializerOptions.Default) + Environment.NewLine;
                WriteAllTextAtomically(path, json);
                errorMessage = null;
                return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or ArgumentException)
            {
                errorMessage = exception.Message;
                return false;
            }
        }

        private string ResolveRecordPath (
            string method,
            string requestId)
        {
            if (string.IsNullOrWhiteSpace(method))
            {
                throw new ArgumentException("Method must not be empty.", nameof(method));
            }

            if (string.IsNullOrWhiteSpace(requestId))
            {
                throw new ArgumentException("Request id must not be empty.", nameof(requestId));
            }

            var identity = string.Concat(projectFingerprint, "\n", method, "\n", requestId);
            var operationKey = Sha256LowerHex.Compute(Encoding.UTF8.GetBytes(identity));
            return Path.Combine(operationsDirectoryPath, operationKey, RecordFileName);
        }

        private bool IsValidRecord (
            RecoverableIpcOperationRecord record,
            string method,
            string requestId,
            string requestPayloadHash,
            out string errorMessage)
        {
            // NOTE: Operation records are scoped to the current Editor host. This prevents
            // stale pending/completed records from an older daemon process from being replayed.
            if (record == null
                || record.SchemaVersion != SchemaVersion
                || !string.Equals(record.ProjectFingerprint, projectFingerprint, StringComparison.Ordinal)
                || !string.Equals(record.Method, method, StringComparison.Ordinal)
                || !string.Equals(record.RequestId, requestId, StringComparison.Ordinal)
                || !string.Equals(record.RequestPayloadHash, requestPayloadHash, StringComparison.Ordinal)
                || record.HostProcessId != hostProcessId
                || !string.Equals(record.HostEditorInstanceId, hostEditorInstanceId, StringComparison.Ordinal))
            {
                errorMessage = "Recoverable IPC operation record identity is invalid.";
                return false;
            }

            if (string.Equals(record.State, RecoverableIpcOperationStateNames.Pending, StringComparison.Ordinal))
            {
                if (record.RecoveryPayload.ValueKind == JsonValueKind.Undefined)
                {
                    errorMessage = "Recoverable IPC operation pending payload is missing.";
                    return false;
                }

                errorMessage = null;
                return true;
            }

            if (string.Equals(record.State, RecoverableIpcOperationStateNames.Completed, StringComparison.Ordinal))
            {
                if (record.Response == null || !record.CompletedAtUtc.HasValue)
                {
                    errorMessage = "Recoverable IPC operation completed response is missing.";
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
            using (var stream = new FileStream(recordPath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192))
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

        private static void WriteAllTextAtomically (
            string path,
            string contents)
        {
            var directoryPath = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new InvalidOperationException($"Recoverable IPC operation directory path could not be resolved: {path}");
            }

            // NOTE: The temporary file must remain beside the target record so replace is same-volume,
            // but its name must stay short. Windows CI project paths can sit close to MAX_PATH, and
            // appending ".tmp.<guid>" to "operation.json" can exceed that budget before persistence.
            var temporaryName = ".tmp-" + Guid.NewGuid().ToString("N").Substring(0, TemporaryRecordTokenLength);
            var temporaryPath = Path.Combine(directoryPath, temporaryName);
            try
            {
                EnsureWritableRecordFile(path);
                File.WriteAllText(temporaryPath, contents);
                FileSystemAccessBoundary.EnsureSecureFile(temporaryPath);
                ReplaceFile(temporaryPath, path);
                FileSystemAccessBoundary.EnsureSecureFile(path);
            }
            finally
            {
                FileUtilities.DeleteIfExists(temporaryPath);
            }
        }

        private static void ReplaceFile (
            string temporaryPath,
            string path)
        {
            try
            {
                File.Replace(temporaryPath, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            catch (FileNotFoundException)
            {
                MoveOrReplaceWhenCreatedConcurrently(temporaryPath, path);
            }
            catch (IOException) when (!File.Exists(path))
            {
                MoveOrReplaceWhenCreatedConcurrently(temporaryPath, path);
            }
        }

        private static void MoveOrReplaceWhenCreatedConcurrently (
            string temporaryPath,
            string path)
        {
            try
            {
                File.Move(temporaryPath, path);
            }
            catch (IOException) when (File.Exists(path))
            {
                EnsureWritableRecordFile(path);
                File.Replace(temporaryPath, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
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
                && string.Equals(record.State, RecoverableIpcOperationStateNames.Completed, StringComparison.Ordinal)
                && record.CompletedAtUtc.HasValue
                && nowUtc - record.CompletedAtUtc.Value > CompletedRecordTtl;
        }

        private static bool IsExpiredPendingRecord (
            RecoverableIpcOperationRecord record,
            DateTimeOffset nowUtc)
        {
            return record != null
                && string.Equals(record.State, RecoverableIpcOperationStateNames.Pending, StringComparison.Ordinal)
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

            if (!string.Equals(record.State, RecoverableIpcOperationStateNames.Pending, StringComparison.Ordinal)
                && !string.Equals(record.State, RecoverableIpcOperationStateNames.Completed, StringComparison.Ordinal))
            {
                return IsRecordFileOlderThan(recordPath, nowUtc, InvalidRecordTtl);
            }

            return false;
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
