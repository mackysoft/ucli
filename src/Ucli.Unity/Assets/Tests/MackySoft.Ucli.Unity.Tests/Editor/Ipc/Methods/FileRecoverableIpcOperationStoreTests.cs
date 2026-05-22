using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class FileRecoverableIpcOperationStoreTests
    {
        private const string ProjectFingerprint = "project-fingerprint";

        [Test]
        [Category("Size.Small")]
        public void TryWritePending_ThenTryRead_RestoresPendingRecord ()
        {
            var projectPath = CreateTemporaryProjectPath();
            try
            {
                var store = CreateStore(projectPath);
                var startedAtUtc = DateTimeOffset.UtcNow;
                var payload = IpcPayloadCodec.SerializeToElement(new { before = "snapshot" });

                var writeResult = store.TryWritePending(
                    IpcMethodNames.PlayEnter,
                    "req-pending",
                    startedAtUtc,
                    payload,
                    out var writeError);
                var readResult = store.TryRead(
                    IpcMethodNames.PlayEnter,
                    "req-pending",
                    out var record,
                    out var readError);

                Assert.That(writeResult, Is.True, writeError);
                Assert.That(readResult, Is.True, readError);
                Assert.That(record.State, Is.EqualTo(RecoverableIpcOperationStateNames.Pending));
                Assert.That(record.ProjectFingerprint, Is.EqualTo(ProjectFingerprint));
                Assert.That(record.Method, Is.EqualTo(IpcMethodNames.PlayEnter));
                Assert.That(record.RequestId, Is.EqualTo("req-pending"));
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
                var store = CreateStore(projectPath);
                var response = CreateSuccessResponse("req-completed");

                var writeResult = store.TryWriteCompleted(
                    IpcMethodNames.Compile,
                    "req-completed",
                    DateTimeOffset.UtcNow.AddSeconds(-1),
                    DateTimeOffset.UtcNow,
                    IpcPayloadCodec.SerializeToElement(new { runId = "run-1" }),
                    response,
                    out var writeError);
                var readResult = store.TryRead(
                    IpcMethodNames.Compile,
                    "req-completed",
                    out var record,
                    out var readError);

                Assert.That(writeResult, Is.True, writeError);
                Assert.That(readResult, Is.True, readError);
                Assert.That(record.State, Is.EqualTo(RecoverableIpcOperationStateNames.Completed));
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
                var store = CreateStore(projectPath);
                Assert.That(store.TryWritePending(
                    IpcMethodNames.PlayEnter,
                    "req-invalid",
                    DateTimeOffset.UtcNow,
                    IpcPayloadCodec.SerializeToElement(new { before = "snapshot" }),
                    out var writeError), Is.True, writeError);
                var recordPath = FindOperationRecordPath(projectPath);
                var record = JsonSerializer.Deserialize<RecoverableIpcOperationRecord>(
                    File.ReadAllText(recordPath),
                    IpcJsonSerializerOptions.Default);
                record.ProjectFingerprint = "other-project";
                File.WriteAllText(
                    recordPath,
                    JsonSerializer.Serialize(record, IpcJsonSerializerOptions.Default));

                var readResult = store.TryRead(
                    IpcMethodNames.PlayEnter,
                    "req-invalid",
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
        public void TryPurgeExpiredCompletedRecords_RemovesExpiredCompletedRecord ()
        {
            var projectPath = CreateTemporaryProjectPath();
            try
            {
                var store = CreateStore(projectPath);
                var nowUtc = DateTimeOffset.UtcNow;
                Assert.That(store.TryWriteCompleted(
                    IpcMethodNames.Compile,
                    "req-expired",
                    nowUtc.AddMinutes(-12),
                    nowUtc.AddMinutes(-11),
                    IpcPayloadCodec.SerializeToElement(new { runId = "run-1" }),
                    CreateSuccessResponse("req-expired"),
                    out var writeError), Is.True, writeError);

                var purgeResult = store.TryPurgeExpiredCompletedRecords(nowUtc, out var purgeError);
                var readResult = store.TryRead(
                    IpcMethodNames.Compile,
                    "req-expired",
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
