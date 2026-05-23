using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class FileRecoverableIpcOperationStoreTests
    {
        private const string ProjectFingerprint = "project-fingerprint";
        private const string RequestPayloadHash = "request-payload-hash";

        [TearDown]
        public void TearDown ()
        {
            UnityEditorSessionStateStore.SetEditorInstanceIdForTests(null);
        }

        [Test]
        [Category("Size.Small")]
        public void TryWritePending_ThenTryRead_RestoresPendingRecord ()
        {
            var projectPath = CreateTemporaryProjectPath();
            try
            {
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-pending");
                var store = CreateStore(projectPath);
                var startedAtUtc = DateTimeOffset.UtcNow;
                var payload = IpcPayloadCodec.SerializeToElement(new { before = "snapshot" });

                var writeResult = store.TryWritePending(
                    IpcMethodNames.PlayEnter,
                    "req-pending",
                    RequestPayloadHash,
                    startedAtUtc,
                    payload,
                    out var writeError);
                var readResult = store.TryRead(
                    IpcMethodNames.PlayEnter,
                    "req-pending",
                    RequestPayloadHash,
                    out var record,
                    out var readError);

                Assert.That(writeResult, Is.True, writeError);
                Assert.That(readResult, Is.True, readError);
                Assert.That(record.State, Is.EqualTo(RecoverableIpcOperationState.Pending));
                Assert.That(ReadOperationRecordText(projectPath), Does.Contain("\"state\":\"pending\""));
                Assert.That(record.ProjectFingerprint, Is.EqualTo(ProjectFingerprint));
                Assert.That(record.Method, Is.EqualTo(IpcMethodNames.PlayEnter));
                Assert.That(record.RequestId, Is.EqualTo("req-pending"));
                Assert.That(record.RequestPayloadHash, Is.EqualTo(RequestPayloadHash));
                Assert.That(record.HostEditorInstanceId, Is.EqualTo("editor-instance-pending"));
                Assert.That(record.StartedAtUtc, Is.EqualTo(startedAtUtc));
                Assert.That(record.RecoveryPayload.GetRawText(), Is.EqualTo(payload.GetRawText()));
                Assert.That(FindOperationRecordPath(projectPath), Does.Contain(UcliStoragePathNames.IpcOperationsDirectoryName));
            }
            finally
            {
                DeleteDirectoryIfExists(projectPath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void TryWriteCompleted_ThenTryRead_RestoresCompletedResponse ()
        {
            var projectPath = CreateTemporaryProjectPath();
            try
            {
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-completed");
                var store = CreateStore(projectPath);
                var response = CreateSuccessResponse("req-completed");

                var writeResult = store.TryWriteCompleted(
                    IpcMethodNames.Compile,
                    "req-completed",
                    RequestPayloadHash,
                    DateTimeOffset.UtcNow.AddSeconds(-1),
                    DateTimeOffset.UtcNow,
                    IpcPayloadCodec.SerializeToElement(new { runId = "run-1" }),
                    response,
                    out var writeError);
                var readResult = store.TryRead(
                    IpcMethodNames.Compile,
                    "req-completed",
                    RequestPayloadHash,
                    out var record,
                    out var readError);

                Assert.That(writeResult, Is.True, writeError);
                Assert.That(readResult, Is.True, readError);
                Assert.That(record.State, Is.EqualTo(RecoverableIpcOperationState.Completed));
                Assert.That(ReadOperationRecordText(projectPath), Does.Contain("\"state\":\"completed\""));
                Assert.That(record.Response, Is.Not.Null);
                Assert.That(record.Response.RequestId, Is.EqualTo(response.RequestId));
                Assert.That(record.Response.Payload.GetRawText(), Is.EqualTo(response.Payload.GetRawText()));
            }
            finally
            {
                DeleteDirectoryIfExists(projectPath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void TryRead_WhenRecordIdentityIsInvalid_RejectsRecord ()
        {
            var projectPath = CreateTemporaryProjectPath();
            try
            {
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-invalid-record");
                var store = CreateStore(projectPath);
                Assert.That(store.TryWritePending(
                    IpcMethodNames.PlayEnter,
                    "req-invalid",
                    RequestPayloadHash,
                    DateTimeOffset.UtcNow,
                    IpcPayloadCodec.SerializeToElement(new { before = "snapshot" }),
                    out var writeError), Is.True, writeError);
                RewriteOperationRecord(projectPath, record => record.ProjectFingerprint = "other-project");

                var readResult = store.TryRead(
                    IpcMethodNames.PlayEnter,
                    "req-invalid",
                    RequestPayloadHash,
                    out var readRecord,
                    out var readError);

                Assert.That(readResult, Is.False);
                Assert.That(readRecord, Is.Null);
                Assert.That(readError, Does.Contain("identity"));
            }
            finally
            {
                DeleteDirectoryIfExists(projectPath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void TryRead_WhenRecordStateIsUnsupported_RejectsRecord ()
        {
            var projectPath = CreateTemporaryProjectPath();
            try
            {
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-unsupported-state");
                var store = CreateStore(projectPath);
                Assert.That(store.TryWritePending(
                    IpcMethodNames.PlayEnter,
                    "req-unsupported-state",
                    RequestPayloadHash,
                    DateTimeOffset.UtcNow,
                    IpcPayloadCodec.SerializeToElement(new { before = "snapshot" }),
                    out var writeError), Is.True, writeError);
                RewriteOperationRecordText(
                    projectPath,
                    text => text.Replace("\"state\":\"pending\"", "\"state\":\"unsupported\"", StringComparison.Ordinal));

                var readResult = store.TryRead(
                    IpcMethodNames.PlayEnter,
                    "req-unsupported-state",
                    RequestPayloadHash,
                    out var readRecord,
                    out var readError);

                Assert.That(readResult, Is.False);
                Assert.That(readRecord, Is.Null);
                Assert.That(readError, Does.Contain("unsupported"));
            }
            finally
            {
                DeleteDirectoryIfExists(projectPath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void TryRead_WhenHostProcessIdDiffers_RejectsRecord ()
        {
            var projectPath = CreateTemporaryProjectPath();
            try
            {
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-host-process");
                var store = CreateStore(projectPath);
                Assert.That(store.TryWritePending(
                    IpcMethodNames.PlayEnter,
                    "req-host-process-id",
                    RequestPayloadHash,
                    DateTimeOffset.UtcNow,
                    IpcPayloadCodec.SerializeToElement(new { before = "snapshot" }),
                    out var writeError), Is.True, writeError);
                RewriteOperationRecord(projectPath, record => record.HostProcessId++);

                var readResult = store.TryRead(
                    IpcMethodNames.PlayEnter,
                    "req-host-process-id",
                    RequestPayloadHash,
                    out var readRecord,
                    out var readError);

                Assert.That(readResult, Is.False);
                Assert.That(readRecord, Is.Null);
                Assert.That(readError, Does.Contain("identity"));
            }
            finally
            {
                DeleteDirectoryIfExists(projectPath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void TryRead_WhenHostEditorInstanceDiffers_RejectsRecord ()
        {
            var projectPath = CreateTemporaryProjectPath();
            try
            {
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-host-1");
                var store = CreateStore(projectPath);
                Assert.That(store.TryWriteCompleted(
                    IpcMethodNames.Compile,
                    "req-host-editor-instance",
                    RequestPayloadHash,
                    DateTimeOffset.UtcNow.AddSeconds(-1),
                    DateTimeOffset.UtcNow,
                    IpcPayloadCodec.SerializeToElement(new { runId = "run-1" }),
                    CreateSuccessResponse("req-host-editor-instance"),
                    out var writeError), Is.True, writeError);
                RewriteOperationRecord(projectPath, record => record.HostEditorInstanceId = "editor-instance-host-2");

                var readResult = store.TryRead(
                    IpcMethodNames.Compile,
                    "req-host-editor-instance",
                    RequestPayloadHash,
                    out var readRecord,
                    out var readError);

                Assert.That(readResult, Is.False);
                Assert.That(readRecord, Is.Null);
                Assert.That(readError, Does.Contain("identity"));
            }
            finally
            {
                DeleteDirectoryIfExists(projectPath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void TryRead_WhenRequestPayloadHashDiffers_RejectsRecord ()
        {
            var projectPath = CreateTemporaryProjectPath();
            try
            {
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-hash");
                var store = CreateStore(projectPath);
                Assert.That(store.TryWriteCompleted(
                    IpcMethodNames.PlayEnter,
                    "req-hash-mismatch",
                    RequestPayloadHash,
                    DateTimeOffset.UtcNow.AddSeconds(-1),
                    DateTimeOffset.UtcNow,
                    IpcPayloadCodec.SerializeToElement(new { before = "snapshot" }),
                    CreateSuccessResponse("req-hash-mismatch"),
                    out var writeError), Is.True, writeError);

                var readResult = store.TryRead(
                    IpcMethodNames.PlayEnter,
                    "req-hash-mismatch",
                    "other-request-payload-hash",
                    out var record,
                    out var readError);

                Assert.That(readResult, Is.False);
                Assert.That(record, Is.Null);
                Assert.That(readError, Does.Contain("identity"));
            }
            finally
            {
                DeleteDirectoryIfExists(projectPath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void TryPurgeExpiredRecords_RemovesExpiredCompletedRecord ()
        {
            var projectPath = CreateTemporaryProjectPath();
            try
            {
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-purge");
                var store = CreateStore(projectPath);
                var nowUtc = DateTimeOffset.UtcNow;
                Assert.That(store.TryWriteCompleted(
                    IpcMethodNames.Compile,
                    "req-expired",
                    RequestPayloadHash,
                    nowUtc.AddMinutes(-12),
                    nowUtc.AddMinutes(-11),
                    IpcPayloadCodec.SerializeToElement(new { runId = "run-1" }),
                    CreateSuccessResponse("req-expired"),
                    out var writeError), Is.True, writeError);

                var purgeResult = store.TryPurgeExpiredRecords(nowUtc, out var purgeError);
                var readResult = store.TryRead(
                    IpcMethodNames.Compile,
                    "req-expired",
                    RequestPayloadHash,
                    out var record,
                    out var readError);

                Assert.That(purgeResult, Is.True, purgeError);
                Assert.That(readResult, Is.False, readError);
                Assert.That(record, Is.Null);
            }
            finally
            {
                DeleteDirectoryIfExists(projectPath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void TryPurgeExpiredRecords_RemovesExpiredPendingRecord ()
        {
            var projectPath = CreateTemporaryProjectPath();
            try
            {
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-purge-pending");
                var store = CreateStore(projectPath);
                var nowUtc = DateTimeOffset.UtcNow;
                Assert.That(store.TryWritePending(
                    IpcMethodNames.PlayEnter,
                    "req-expired-pending",
                    RequestPayloadHash,
                    nowUtc.AddHours(-25),
                    IpcPayloadCodec.SerializeToElement(new { before = "snapshot" }),
                    out var writeError), Is.True, writeError);

                var purgeResult = store.TryPurgeExpiredRecords(nowUtc, out var purgeError);
                var readResult = store.TryRead(
                    IpcMethodNames.PlayEnter,
                    "req-expired-pending",
                    RequestPayloadHash,
                    out var record,
                    out var readError);

                Assert.That(purgeResult, Is.True, purgeError);
                Assert.That(readResult, Is.False, readError);
                Assert.That(record, Is.Null);
            }
            finally
            {
                DeleteDirectoryIfExists(projectPath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void TryPurgeExpiredRecords_RemovesExpiredInvalidRecord ()
        {
            var projectPath = CreateTemporaryProjectPath();
            try
            {
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-purge-invalid");
                var store = CreateStore(projectPath);
                var nowUtc = DateTimeOffset.UtcNow;
                Assert.That(store.TryWritePending(
                    IpcMethodNames.PlayEnter,
                    "req-expired-invalid",
                    RequestPayloadHash,
                    nowUtc,
                    IpcPayloadCodec.SerializeToElement(new { before = "snapshot" }),
                    out var writeError), Is.True, writeError);
                var recordPath = FindOperationRecordPath(projectPath);
                File.WriteAllText(recordPath, "{");
                File.SetLastWriteTimeUtc(recordPath, nowUtc.AddHours(-25).UtcDateTime);

                var purgeResult = store.TryPurgeExpiredRecords(nowUtc, out var purgeError);

                Assert.That(purgeResult, Is.True, purgeError);
                Assert.That(File.Exists(recordPath), Is.False);
            }
            finally
            {
                DeleteDirectoryIfExists(projectPath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void TryRead_WhenRecordExceedsMaximumSize_RejectsRecord ()
        {
            var projectPath = CreateTemporaryProjectPath();
            try
            {
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-large-record");
                var store = CreateStore(projectPath);
                Assert.That(store.TryWritePending(
                    IpcMethodNames.PlayEnter,
                    "req-large-record",
                    RequestPayloadHash,
                    DateTimeOffset.UtcNow,
                    IpcPayloadCodec.SerializeToElement(new { before = "snapshot" }),
                    out var writeError), Is.True, writeError);
                var recordPath = FindOperationRecordPath(projectPath);
                File.WriteAllText(recordPath, new string(' ', 1024 * 1024 + 1));

                var readResult = store.TryRead(
                    IpcMethodNames.PlayEnter,
                    "req-large-record",
                    RequestPayloadHash,
                    out var record,
                    out var readError);

                Assert.That(readResult, Is.False);
                Assert.That(record, Is.Null);
                Assert.That(readError, Does.Contain("maximum size"));
            }
            finally
            {
                DeleteDirectoryIfExists(projectPath);
            }
        }

        private static FileRecoverableIpcOperationStore CreateStore (string projectPath)
        {
            return FileRecoverableIpcOperationStore.Create(new IpcProjectIdentity(
                projectPath,
                ProjectFingerprint,
                "6000.1.4f1"));
        }

        private static string CreateTemporaryProjectPath ()
        {
            var path = Path.Combine(Path.GetTempPath(), $"ucli-recoverable-ipc-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return path;
        }

        private static string FindOperationRecordPath (string projectPath)
        {
            return Directory
                .GetFiles(projectPath, "operation.json", SearchOption.AllDirectories)
                .Single();
        }

        private static string ReadOperationRecordText (string projectPath)
        {
            return File.ReadAllText(FindOperationRecordPath(projectPath));
        }

        private static void RewriteOperationRecord (
            string projectPath,
            Action<RecoverableIpcOperationRecord> update)
        {
            var recordPath = FindOperationRecordPath(projectPath);
            var record = JsonSerializer.Deserialize<RecoverableIpcOperationRecord>(
                File.ReadAllText(recordPath),
                IpcJsonSerializerOptions.Default);
            update(record);
            File.WriteAllText(
                recordPath,
                JsonSerializer.Serialize(record, IpcJsonSerializerOptions.Default));
        }

        private static void RewriteOperationRecordText (
            string projectPath,
            Func<string, string> update)
        {
            var recordPath = FindOperationRecordPath(projectPath);
            File.WriteAllText(recordPath, update(File.ReadAllText(recordPath)));
        }

        private static IpcResponse CreateSuccessResponse (string requestId)
        {
            return new IpcResponse(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: requestId,
                Status: IpcProtocol.StatusOk,
                Payload: IpcPayloadCodec.SerializeToElement(new { ok = true }),
                Errors: Array.Empty<IpcError>());
        }

        private static void DeleteDirectoryIfExists (string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch (Exception exception)
            {
                TestContext.WriteLine($"Temporary recoverable IPC test directory cleanup failed: {path}. {exception.Message}");
            }
        }
    }
}
