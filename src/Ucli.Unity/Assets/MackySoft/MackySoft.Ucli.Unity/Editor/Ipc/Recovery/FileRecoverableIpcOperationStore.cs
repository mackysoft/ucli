using System;
using System.IO;
using System.Text;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Cryptography;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Stores recoverable IPC operation records under the project-local uCLI state directory. </summary>
    internal sealed class FileRecoverableIpcOperationStore : IRecoverableIpcOperationStore
    {
        private const int SchemaVersion = 1;
        private const string RecordFileName = "operation.json";

        private static readonly TimeSpan CompletedRecordTtl = TimeSpan.FromMinutes(10);

        private readonly string operationsDirectoryPath;
        private readonly string projectFingerprint;

        private FileRecoverableIpcOperationStore (
            string operationsDirectoryPath,
            string projectFingerprint)
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
            return new FileRecoverableIpcOperationStore(
                Path.Combine(fingerprintDirectory, UcliStoragePathNames.IpcOperationsDirectoryName),
                projectIdentity.ProjectFingerprint);
        }

        /// <inheritdoc />
        public bool TryRead (
            string method,
            string requestId,
            out RecoverableIpcOperationRecord record,
            out string errorMessage)
        {
            record = null;
            try
            {
                var path = ResolveRecordPath(method, requestId);
                if (!File.Exists(path))
                {
                    errorMessage = null;
                    return false;
                }

                record = JsonSerializer.Deserialize<RecoverableIpcOperationRecord>(
                    File.ReadAllText(path),
                    IpcJsonSerializerOptions.Default);
                if (!IsValidRecord(record, method, requestId, out errorMessage))
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
            DateTimeOffset startedAtUtc,
            JsonElement recoveryPayload,
            out string errorMessage)
        {
            var record = new RecoverableIpcOperationRecord
            {
                SchemaVersion = SchemaVersion,
                ProjectFingerprint = projectFingerprint,
                Method = method,
                RequestId = requestId,
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

            var record = new RecoverableIpcOperationRecord
            {
                SchemaVersion = SchemaVersion,
                ProjectFingerprint = projectFingerprint,
                Method = method,
                RequestId = requestId,
                State = RecoverableIpcOperationStateNames.Completed,
                StartedAtUtc = startedAtUtc,
                CompletedAtUtc = completedAtUtc,
                RecoveryPayload = recoveryPayload.Clone(),
                Response = response,
            };
            return TryWriteRecord(record, out errorMessage);
        }

        /// <inheritdoc />
        public bool TryPurgeExpiredCompletedRecords (
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

                foreach (var operationDirectoryPath in Directory.EnumerateDirectories(operationsDirectoryPath))
                {
                    var recordPath = Path.Combine(operationDirectoryPath, RecordFileName);
                    if (!File.Exists(recordPath))
                    {
                        continue;
                    }

                    if (!TryReadRecordFile(recordPath, out var record)
                        || !IsExpiredCompletedRecord(record, nowUtc))
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
                TryPurgeExpiredCompletedRecords(DateTimeOffset.UtcNow, out _);
                var path = ResolveRecordPath(record.Method, record.RequestId);
                var directoryPath = Path.GetDirectoryName(path);
                if (string.IsNullOrWhiteSpace(directoryPath))
                {
                    throw new InvalidOperationException($"Recoverable IPC operation directory path could not be resolved: {path}");
                }

                Directory.CreateDirectory(directoryPath);
                var json = JsonSerializer.Serialize(record, IpcJsonSerializerOptions.Default) + Environment.NewLine;
                FileUtilities.WriteAllTextAtomically(path, json);
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
            out string errorMessage)
        {
            if (record == null
                || record.SchemaVersion != SchemaVersion
                || !string.Equals(record.ProjectFingerprint, projectFingerprint, StringComparison.Ordinal)
                || !string.Equals(record.Method, method, StringComparison.Ordinal)
                || !string.Equals(record.RequestId, requestId, StringComparison.Ordinal))
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
                    File.ReadAllText(recordPath),
                    IpcJsonSerializerOptions.Default);
                return record != null;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                record = null;
                return false;
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
