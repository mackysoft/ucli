using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class FileRecoverableIpcOperationStoreTests
    {
        private static readonly ProjectFingerprint ProjectFingerprint =
            ProjectFingerprintTestFactory.Create("project-fingerprint");

        private static readonly Guid EditorInstanceId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static readonly Guid OtherEditorInstanceId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        private static readonly Sha256Digest RequestPayloadHash = Sha256Digest.Parse(
            "cda34040abc54e9b351b66c6ecbc9708cf2c70996b0805553b3854bdce80d94b");

        private static readonly Sha256Digest OtherRequestPayloadHash = Sha256Digest.Parse(
            "43b7a6583da442557792fe0e75ad8e950b6ee17b6d6aa48001e3f4be38f08c90");

        [Test]
        [Category("Size.Small")]
        public void Create_WhenHostEditorInstanceIdIsEmpty_ThrowsArgumentException ()
        {
            var exception = Assert.Throws<ArgumentException>(() => FileRecoverableIpcOperationStore.Create(
                new IpcProjectIdentity(Path.GetTempPath(), ProjectFingerprint, "6000.1.4f1"),
                Guid.Empty));

            Assert.That(exception.ParamName, Is.EqualTo("hostEditorInstanceId"));
        }

        [Test]
        [Category("Size.Small")]
        public void State_WhenValueIsUndefined_ThrowsArgumentOutOfRangeException ()
        {
            var record = new RecoverableIpcOperationRecord();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => record.State = (RecoverableIpcOperationState)999);
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator WritePendingAsync_ThenReadAsync_RestoresPendingRecord () => UniTask.ToCoroutine(async () =>
        {
            var projectPath = CreateTemporaryProjectPath();
            try
            {
                var store = CreateStore(projectPath);
                var startedAtUtc = DateTimeOffset.UtcNow;
                var payload = IpcPayloadCodec.SerializeToElement(new { before = "snapshot" });
                var requestId = new Guid("7b6d4c17-1b8e-4f28-a2e6-123456789abc");

                var writeResult = await store.WritePendingAsync(
                    UnityIpcMethod.PlayEnter,
                    requestId,
                    RequestPayloadHash,
                    startedAtUtc,
                    payload,
                    CancellationToken.None);
                var readResult = await store.ReadAsync(
                    UnityIpcMethod.PlayEnter,
                    requestId,
                    RequestPayloadHash,
                    CancellationToken.None);

                Assert.That(writeResult.IsSuccess, Is.True, writeResult.ErrorMessage);
                Assert.That(readResult.IsSuccess, Is.True, readResult.ErrorMessage);
                Assert.That(readResult.Record, Is.Not.Null);
                Assert.That(readResult.Record.State, Is.EqualTo(RecoverableIpcOperationState.Pending));
                var recordPath = FindOperationRecordPath(projectPath);
                var recordText = ReadOperationRecordText(projectPath);
                using var recordDocument = JsonDocument.Parse(recordText);
                Assert.That(recordText, Does.Contain("\"state\":\"pending\""));
                Assert.That(recordDocument.RootElement.GetProperty("schemaVersion").GetInt32(), Is.EqualTo(1));
                Assert.That(
                    recordDocument.RootElement.GetProperty("requestId").GetString(),
                    Is.EqualTo("7b6d4c17-1b8e-4f28-a2e6-123456789abc"));
                Assert.That(
                    recordDocument.RootElement.GetProperty("hostEditorInstanceId").GetGuid(),
                    Is.EqualTo(EditorInstanceId));
                Assert.That(
                    Path.GetFileName(Path.GetDirectoryName(recordPath)),
                    Is.EqualTo("fdmko5orhp7ih8n628q5cu4qng"));
                Assert.That(readResult.Record.ProjectFingerprint, Is.EqualTo(ProjectFingerprint));
                Assert.That(readResult.Record.Method, Is.EqualTo(UnityIpcMethod.PlayEnter));
                Assert.That(readResult.Record.RequestId, Is.EqualTo(requestId));
                Assert.That(readResult.Record.RequestPayloadHash, Is.EqualTo(RequestPayloadHash));
                Assert.That(readResult.Record.HostEditorInstanceId, Is.EqualTo(EditorInstanceId));
                Assert.That(readResult.Record.StartedAtUtc, Is.EqualTo(startedAtUtc));
                Assert.That(readResult.Record.RecoveryPayload.GetRawText(), Is.EqualTo(payload.GetRawText()));
                Assert.That(FindOperationRecordPath(projectPath), Does.Contain(UcliStoragePathNames.IpcOperationsDirectoryName));
            }
            finally
            {
                DeleteDirectoryIfExists(projectPath);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ReadAsync_WhenMethodDiffersForSameRequestId_RejectsRecordIdentity () => UniTask.ToCoroutine(async () =>
        {
            var projectPath = CreateTemporaryProjectPath();
            try
            {
                var store = CreateStore(projectPath);
                var requestId = Guid.NewGuid();
                var writeResult = await store.WritePendingAsync(
                    UnityIpcMethod.PlayEnter,
                    requestId,
                    RequestPayloadHash,
                    DateTimeOffset.UtcNow,
                    IpcPayloadCodec.SerializeToElement(new { before = "snapshot" }),
                    CancellationToken.None);
                Assert.That(writeResult.IsSuccess, Is.True, writeResult.ErrorMessage);

                var readResult = await store.ReadAsync(
                    UnityIpcMethod.PlayExit,
                    requestId,
                    RequestPayloadHash,
                    CancellationToken.None);

                Assert.That(readResult.IsSuccess, Is.False);
                Assert.That(readResult.Record, Is.Null);
                Assert.That(readResult.ErrorMessage, Does.Contain("identity"));
            }
            finally
            {
                DeleteDirectoryIfExists(projectPath);
            }
        });

        [Test]
        [Category("Size.Small")]
        public void WriteCompletedAsync_WhenResponseRequestIdDiffers_ThrowsArgumentException ()
        {
            var projectPath = CreateTemporaryProjectPath();
            try
            {
                var store = CreateStore(projectPath);
                var requestId = Guid.NewGuid();
                var response = CreateSuccessResponse(Guid.NewGuid());

                var exception = Assert.Throws<ArgumentException>(() => store.WriteCompletedAsync(
                    UnityIpcMethod.Compile,
                    requestId,
                    RequestPayloadHash,
                    DateTimeOffset.UtcNow.AddSeconds(-1),
                    DateTimeOffset.UtcNow,
                    IpcPayloadCodec.SerializeToElement(new { runId = Guid.Parse("00000000-0000-0000-0000-000000000613") }),
                    response,
                    CancellationToken.None));

                Assert.That(exception.ParamName, Is.EqualTo("response"));
            }
            finally
            {
                DeleteDirectoryIfExists(projectPath);
            }
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator WriteCompletedAsync_ThenReadAsync_RestoresCompletedResponse () => UniTask.ToCoroutine(async () =>
        {
            var projectPath = CreateTemporaryProjectPath();
            try
            {
                var store = CreateStore(projectPath);
                var requestId = Guid.NewGuid();
                var response = CreateSuccessResponse(requestId);

                var writeResult = await store.WriteCompletedAsync(
                    UnityIpcMethod.Compile,
                    requestId,
                    RequestPayloadHash,
                    DateTimeOffset.UtcNow.AddSeconds(-1),
                    DateTimeOffset.UtcNow,
                    IpcPayloadCodec.SerializeToElement(new { runId = Guid.Parse("00000000-0000-0000-0000-000000000613") }),
                    response,
                    CancellationToken.None);
                var readResult = await store.ReadAsync(
                    UnityIpcMethod.Compile,
                    requestId,
                    RequestPayloadHash,
                    CancellationToken.None);

                Assert.That(writeResult.IsSuccess, Is.True, writeResult.ErrorMessage);
                Assert.That(readResult.IsSuccess, Is.True, readResult.ErrorMessage);
                Assert.That(readResult.Record, Is.Not.Null);
                Assert.That(readResult.Record.State, Is.EqualTo(RecoverableIpcOperationState.Completed));
                Assert.That(ReadOperationRecordText(projectPath), Does.Contain("\"state\":\"completed\""));
                Assert.That(readResult.Record.Response, Is.Not.Null);
                Assert.That(readResult.Record.Response.RequestId, Is.EqualTo(response.RequestId));
                Assert.That(readResult.Record.Response.Payload.GetRawText(), Is.EqualTo(response.Payload.GetRawText()));
            }
            finally
            {
                DeleteDirectoryIfExists(projectPath);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ReadAsync_WhenHostEditorInstanceIdIsMalformed_RejectsRecord () => UniTask.ToCoroutine(async () =>
        {
            var projectPath = CreateTemporaryProjectPath();
            try
            {
                var store = CreateStore(projectPath);
                var requestId = Guid.NewGuid();
                var writeResult = await store.WritePendingAsync(
                    UnityIpcMethod.PlayEnter,
                    requestId,
                    RequestPayloadHash,
                    DateTimeOffset.UtcNow,
                    IpcPayloadCodec.SerializeToElement(new { before = "snapshot" }),
                    CancellationToken.None);
                Assert.That(writeResult.IsSuccess, Is.True, writeResult.ErrorMessage);
                RewriteOperationRecordText(
                    projectPath,
                    text => text.Replace(
                        $"\"hostEditorInstanceId\":\"{EditorInstanceId:D}\"",
                        "\"hostEditorInstanceId\":\"editor-instance\"",
                        StringComparison.Ordinal));

                var readResult = await store.ReadAsync(
                    UnityIpcMethod.PlayEnter,
                    requestId,
                    RequestPayloadHash,
                    CancellationToken.None);

                Assert.That(readResult.IsSuccess, Is.False);
                Assert.That(readResult.Record, Is.Null);
                Assert.That(readResult.ErrorMessage, Does.Contain("Guid"));
                Assert.That(readResult.ErrorMessage, Does.Contain("hostEditorInstanceId"));
            }
            finally
            {
                DeleteDirectoryIfExists(projectPath);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ReadAsync_WhenHostEditorInstanceIdIsEmpty_RejectsRecord () => UniTask.ToCoroutine(async () =>
        {
            var projectPath = CreateTemporaryProjectPath();
            try
            {
                var store = CreateStore(projectPath);
                var requestId = Guid.NewGuid();
                var writeResult = await store.WritePendingAsync(
                    UnityIpcMethod.PlayEnter,
                    requestId,
                    RequestPayloadHash,
                    DateTimeOffset.UtcNow,
                    IpcPayloadCodec.SerializeToElement(new { before = "snapshot" }),
                    CancellationToken.None);
                Assert.That(writeResult.IsSuccess, Is.True, writeResult.ErrorMessage);
                RewriteOperationRecord(projectPath, record => record.HostEditorInstanceId = Guid.Empty);

                var readResult = await store.ReadAsync(
                    UnityIpcMethod.PlayEnter,
                    requestId,
                    RequestPayloadHash,
                    CancellationToken.None);

                Assert.That(readResult.IsSuccess, Is.False);
                Assert.That(readResult.Record, Is.Null);
                Assert.That(readResult.ErrorMessage, Does.Contain("identity"));
            }
            finally
            {
                DeleteDirectoryIfExists(projectPath);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ReadAsync_WhenCompletedResponseRequestIdDiffers_RejectsRecord () => UniTask.ToCoroutine(async () =>
        {
            var projectPath = CreateTemporaryProjectPath();
            try
            {
                var store = CreateStore(projectPath);
                var requestId = Guid.NewGuid();
                var writeResult = await store.WriteCompletedAsync(
                    UnityIpcMethod.Compile,
                    requestId,
                    RequestPayloadHash,
                    DateTimeOffset.UtcNow.AddSeconds(-1),
                    DateTimeOffset.UtcNow,
                    IpcPayloadCodec.SerializeToElement(new { runId = Guid.Parse("00000000-0000-0000-0000-000000000613") }),
                    CreateSuccessResponse(requestId),
                    CancellationToken.None);
                Assert.That(writeResult.IsSuccess, Is.True, writeResult.ErrorMessage);
                RewriteOperationRecord(
                    projectPath,
                    record => record.Response = CreateSuccessResponse(Guid.NewGuid()));

                var readResult = await store.ReadAsync(
                    UnityIpcMethod.Compile,
                    requestId,
                    RequestPayloadHash,
                    CancellationToken.None);

                Assert.That(readResult.IsSuccess, Is.False);
                Assert.That(readResult.Record, Is.Null);
                Assert.That(readResult.ErrorMessage, Does.Contain("response identity"));
            }
            finally
            {
                DeleteDirectoryIfExists(projectPath);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ReadAsync_WhenRecordStateIsUnsupported_RejectsRecord () => UniTask.ToCoroutine(async () =>
        {
            var projectPath = CreateTemporaryProjectPath();
            try
            {
                var store = CreateStore(projectPath);
                var requestId = Guid.NewGuid();
                var writeResult = await store.WritePendingAsync(
                    UnityIpcMethod.PlayEnter,
                    requestId,
                    RequestPayloadHash,
                    DateTimeOffset.UtcNow,
                    IpcPayloadCodec.SerializeToElement(new { before = "snapshot" }),
                    CancellationToken.None);
                Assert.That(writeResult.IsSuccess, Is.True, writeResult.ErrorMessage);
                RewriteOperationRecordText(
                    projectPath,
                    text => text.Replace("\"state\":\"pending\"", "\"state\":\"unsupported\"", StringComparison.Ordinal));

                var readResult = await store.ReadAsync(
                    UnityIpcMethod.PlayEnter,
                    requestId,
                    RequestPayloadHash,
                    CancellationToken.None);

                Assert.That(readResult.IsSuccess, Is.False);
                Assert.That(readResult.Record, Is.Null);
                Assert.That(readResult.ErrorMessage, Does.Contain("unsupported"));
            }
            finally
            {
                DeleteDirectoryIfExists(projectPath);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ReadAsync_WhenHostProcessIdDiffers_RejectsRecord () => UniTask.ToCoroutine(async () =>
        {
            var projectPath = CreateTemporaryProjectPath();
            try
            {
                var store = CreateStore(projectPath);
                var requestId = Guid.NewGuid();
                var writeResult = await store.WritePendingAsync(
                    UnityIpcMethod.PlayEnter,
                    requestId,
                    RequestPayloadHash,
                    DateTimeOffset.UtcNow,
                    IpcPayloadCodec.SerializeToElement(new { before = "snapshot" }),
                    CancellationToken.None);
                Assert.That(writeResult.IsSuccess, Is.True, writeResult.ErrorMessage);
                RewriteOperationRecord(projectPath, record => record.HostProcessId++);

                var readResult = await store.ReadAsync(
                    UnityIpcMethod.PlayEnter,
                    requestId,
                    RequestPayloadHash,
                    CancellationToken.None);

                Assert.That(readResult.IsSuccess, Is.False);
                Assert.That(readResult.Record, Is.Null);
                Assert.That(readResult.ErrorMessage, Does.Contain("identity"));
            }
            finally
            {
                DeleteDirectoryIfExists(projectPath);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ReadAsync_WhenHostEditorInstanceDiffers_RejectsRecord () => UniTask.ToCoroutine(async () =>
        {
            var projectPath = CreateTemporaryProjectPath();
            try
            {
                var store = CreateStore(projectPath);
                var requestId = Guid.NewGuid();
                var writeResult = await store.WriteCompletedAsync(
                    UnityIpcMethod.Compile,
                    requestId,
                    RequestPayloadHash,
                    DateTimeOffset.UtcNow.AddSeconds(-1),
                    DateTimeOffset.UtcNow,
                    IpcPayloadCodec.SerializeToElement(new { runId = Guid.Parse("00000000-0000-0000-0000-000000000613") }),
                    CreateSuccessResponse(requestId),
                    CancellationToken.None);
                Assert.That(writeResult.IsSuccess, Is.True, writeResult.ErrorMessage);
                RewriteOperationRecord(projectPath, record => record.HostEditorInstanceId = OtherEditorInstanceId);

                var readResult = await store.ReadAsync(
                    UnityIpcMethod.Compile,
                    requestId,
                    RequestPayloadHash,
                    CancellationToken.None);

                Assert.That(readResult.IsSuccess, Is.False);
                Assert.That(readResult.Record, Is.Null);
                Assert.That(readResult.ErrorMessage, Does.Contain("identity"));
            }
            finally
            {
                DeleteDirectoryIfExists(projectPath);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ReadAsync_WhenRequestPayloadHashDiffers_RejectsRecord () => UniTask.ToCoroutine(async () =>
        {
            var projectPath = CreateTemporaryProjectPath();
            try
            {
                var store = CreateStore(projectPath);
                var requestId = Guid.NewGuid();
                var writeResult = await store.WriteCompletedAsync(
                    UnityIpcMethod.PlayEnter,
                    requestId,
                    RequestPayloadHash,
                    DateTimeOffset.UtcNow.AddSeconds(-1),
                    DateTimeOffset.UtcNow,
                    IpcPayloadCodec.SerializeToElement(new { before = "snapshot" }),
                    CreateSuccessResponse(requestId),
                    CancellationToken.None);
                Assert.That(writeResult.IsSuccess, Is.True, writeResult.ErrorMessage);

                var readResult = await store.ReadAsync(
                    UnityIpcMethod.PlayEnter,
                    requestId,
                    OtherRequestPayloadHash,
                    CancellationToken.None);

                Assert.That(readResult.IsSuccess, Is.False);
                Assert.That(readResult.Record, Is.Null);
                Assert.That(readResult.ErrorMessage, Does.Contain("identity"));
            }
            finally
            {
                DeleteDirectoryIfExists(projectPath);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PurgeExpiredRecordsAsync_RemovesExpiredCompletedRecord () => UniTask.ToCoroutine(async () =>
        {
            var projectPath = CreateTemporaryProjectPath();
            try
            {
                var store = CreateStore(projectPath);
                var nowUtc = DateTimeOffset.UtcNow;
                var requestId = Guid.NewGuid();
                var writeResult = await store.WriteCompletedAsync(
                    UnityIpcMethod.Compile,
                    requestId,
                    RequestPayloadHash,
                    nowUtc.AddMinutes(-12),
                    nowUtc.AddMinutes(-11),
                    IpcPayloadCodec.SerializeToElement(new { runId = Guid.Parse("00000000-0000-0000-0000-000000000613") }),
                    CreateSuccessResponse(requestId),
                    CancellationToken.None);
                Assert.That(writeResult.IsSuccess, Is.True, writeResult.ErrorMessage);

                var purgeResult = await store.PurgeExpiredRecordsAsync(nowUtc, CancellationToken.None);
                var readResult = await store.ReadAsync(
                    UnityIpcMethod.Compile,
                    requestId,
                    RequestPayloadHash,
                    CancellationToken.None);

                Assert.That(purgeResult.IsSuccess, Is.True, purgeResult.ErrorMessage);
                Assert.That(readResult.IsSuccess, Is.True, readResult.ErrorMessage);
                Assert.That(readResult.Record, Is.Null);
            }
            finally
            {
                DeleteDirectoryIfExists(projectPath);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PurgeExpiredRecordsAsync_RemovesExpiredPendingRecord () => UniTask.ToCoroutine(async () =>
        {
            var projectPath = CreateTemporaryProjectPath();
            try
            {
                var store = CreateStore(projectPath);
                var nowUtc = DateTimeOffset.UtcNow;
                var requestId = Guid.NewGuid();
                var writeResult = await store.WritePendingAsync(
                    UnityIpcMethod.PlayEnter,
                    requestId,
                    RequestPayloadHash,
                    nowUtc.AddHours(-25),
                    IpcPayloadCodec.SerializeToElement(new { before = "snapshot" }),
                    CancellationToken.None);
                Assert.That(writeResult.IsSuccess, Is.True, writeResult.ErrorMessage);

                var purgeResult = await store.PurgeExpiredRecordsAsync(nowUtc, CancellationToken.None);
                var readResult = await store.ReadAsync(
                    UnityIpcMethod.PlayEnter,
                    requestId,
                    RequestPayloadHash,
                    CancellationToken.None);

                Assert.That(purgeResult.IsSuccess, Is.True, purgeResult.ErrorMessage);
                Assert.That(readResult.IsSuccess, Is.True, readResult.ErrorMessage);
                Assert.That(readResult.Record, Is.Null);
            }
            finally
            {
                DeleteDirectoryIfExists(projectPath);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PurgeExpiredRecordsAsync_RemovesExpiredInvalidRecord () => UniTask.ToCoroutine(async () =>
        {
            var projectPath = CreateTemporaryProjectPath();
            try
            {
                var store = CreateStore(projectPath);
                var nowUtc = DateTimeOffset.UtcNow;
                var requestId = Guid.NewGuid();
                var writeResult = await store.WritePendingAsync(
                    UnityIpcMethod.PlayEnter,
                    requestId,
                    RequestPayloadHash,
                    nowUtc,
                    IpcPayloadCodec.SerializeToElement(new { before = "snapshot" }),
                    CancellationToken.None);
                Assert.That(writeResult.IsSuccess, Is.True, writeResult.ErrorMessage);
                var recordPath = FindOperationRecordPath(projectPath);
                File.WriteAllText(recordPath, "{");
                File.SetLastWriteTimeUtc(recordPath, nowUtc.AddHours(-25).UtcDateTime);

                var purgeResult = await store.PurgeExpiredRecordsAsync(nowUtc, CancellationToken.None);

                Assert.That(purgeResult.IsSuccess, Is.True, purgeResult.ErrorMessage);
                Assert.That(File.Exists(recordPath), Is.False);
            }
            finally
            {
                DeleteDirectoryIfExists(projectPath);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PurgeExpiredRecordsAsync_WhenFirstBatchRemains_AdvancesToNextBatch () => UniTask.ToCoroutine(async () =>
        {
            var projectPath = CreateTemporaryProjectPath();
            try
            {
                var store = CreateStore(projectPath);
                var operationsDirectoryPath = ResolveOperationsDirectoryPath(projectPath);
                Directory.CreateDirectory(operationsDirectoryPath);
                string expiredRecordPath = null;
                for (var i = 0; i <= 128; i++)
                {
                    var operationDirectoryPath = Path.Combine(operationsDirectoryPath, i.ToString("D3"));
                    Directory.CreateDirectory(operationDirectoryPath);
                    var recordPath = Path.Combine(operationDirectoryPath, "operation.json");
                    File.WriteAllText(recordPath, "{");
                    if (i == 128)
                    {
                        expiredRecordPath = recordPath;
                        File.SetLastWriteTimeUtc(recordPath, DateTime.UtcNow.AddHours(-25));
                    }
                }

                var firstResult = await store.PurgeExpiredRecordsAsync(DateTimeOffset.UtcNow, CancellationToken.None);
                Assert.That(firstResult.IsSuccess, Is.True, firstResult.ErrorMessage);
                Assert.That(File.Exists(expiredRecordPath), Is.True);

                var secondResult = await store.PurgeExpiredRecordsAsync(DateTimeOffset.UtcNow, CancellationToken.None);
                Assert.That(secondResult.IsSuccess, Is.True, secondResult.ErrorMessage);
                Assert.That(File.Exists(expiredRecordPath), Is.False);
            }
            finally
            {
                DeleteDirectoryIfExists(projectPath);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PurgeExpiredRecordsAsync_WhenMaintenanceIsQueued_DoesNotBlockRequestIo () => UniTask.ToCoroutine(async () =>
        {
            var projectPath = CreateTemporaryProjectPath();
            try
            {
                var store = CreateStore(projectPath);
                var maintenanceGate = GetPrivateField<SemaphoreSlim>(store, "maintenanceGate");
                var requestId = Guid.NewGuid();
                await maintenanceGate.WaitAsync(CancellationToken.None);
                Task<RecoverableIpcOperationStoreResult> purgeTask = null;
                try
                {
                    purgeTask = store
                        .PurgeExpiredRecordsAsync(DateTimeOffset.UtcNow, CancellationToken.None)
                        .AsTask();
                    var writeTask = store.WritePendingAsync(
                            UnityIpcMethod.PlayEnter,
                            requestId,
                            RequestPayloadHash,
                            DateTimeOffset.UtcNow,
                            IpcPayloadCodec.SerializeToElement(new { before = "snapshot" }),
                            CancellationToken.None)
                        .AsTask();

                    var writeResult = await TestAwaiter.WaitAsync(
                        writeTask,
                        "recoverable request write while maintenance is queued",
                        TimeSpan.FromSeconds(5));

                    Assert.That(writeResult.IsSuccess, Is.True, writeResult.ErrorMessage);
                    Assert.That(purgeTask.IsCompleted, Is.False);
                }
                finally
                {
                    maintenanceGate.Release();
                }

                var purgeResult = await TestAwaiter.WaitAsync(
                    purgeTask,
                    "queued recoverable maintenance pass",
                    TimeSpan.FromSeconds(5));
                Assert.That(purgeResult.IsSuccess, Is.True, purgeResult.ErrorMessage);
            }
            finally
            {
                DeleteDirectoryIfExists(projectPath);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PurgeExpiredRecordsAsync_ReleasesRequestIoGateBetweenRecords () => UniTask.ToCoroutine(async () =>
        {
            var projectPath = CreateTemporaryProjectPath();
            try
            {
                var store = CreateStore(projectPath);
                var existingRequestId = Guid.NewGuid();
                var newRequestId = Guid.NewGuid();
                var initialWriteResult = await store.WritePendingAsync(
                    UnityIpcMethod.PlayEnter,
                    existingRequestId,
                    RequestPayloadHash,
                    DateTimeOffset.UtcNow,
                    IpcPayloadCodec.SerializeToElement(new { before = "existing" }),
                    CancellationToken.None);
                Assert.That(initialWriteResult.IsSuccess, Is.True, initialWriteResult.ErrorMessage);

                var operationsDirectoryPath = ResolveOperationsDirectoryPath(projectPath);
                var invalidRecordText = new string(' ', 64 * 1024);
                for (var i = 0; i < 128; i++)
                {
                    var operationDirectoryPath = Path.Combine(operationsDirectoryPath, i.ToString("D3"));
                    Directory.CreateDirectory(operationDirectoryPath);
                    File.WriteAllText(Path.Combine(operationDirectoryPath, "operation.json"), invalidRecordText);
                }

                var ioGate = GetPrivateField<SemaphoreSlim>(store, "ioGate");
                await ioGate.WaitAsync(CancellationToken.None);
                var ioGateHeld = true;
                try
                {
                    var purgeTask = store
                        .PurgeExpiredRecordsAsync(DateTimeOffset.UtcNow, CancellationToken.None)
                        .AsTask();
                    await WaitUntilAsync(
                        () => GetPrivateField<string>(store, "maintenanceCursorDirectoryName") != null,
                        TimeSpan.FromSeconds(5));

                    var readTask = store.ReadAsync(
                            UnityIpcMethod.PlayEnter,
                            existingRequestId,
                            RequestPayloadHash,
                            CancellationToken.None)
                        .AsTask();
                    var writeTask = store.WritePendingAsync(
                            UnityIpcMethod.PlayExit,
                            newRequestId,
                            RequestPayloadHash,
                            DateTimeOffset.UtcNow,
                            IpcPayloadCodec.SerializeToElement(new { before = "new" }),
                            CancellationToken.None)
                        .AsTask();

                    // Give both request tasks time to queue behind the maintenance record currently holding the gate.
                    await Task.Delay(TimeSpan.FromMilliseconds(50));

                    var completionSequence = 0;
                    var purgeCompletionOrder = 0;
                    var readCompletionOrder = 0;
                    var writeCompletionOrder = 0;
                    var purgeCompletionTask = purgeTask.ContinueWith(
                        _ => purgeCompletionOrder = Interlocked.Increment(ref completionSequence),
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                    var readCompletionTask = readTask.ContinueWith(
                        _ => readCompletionOrder = Interlocked.Increment(ref completionSequence),
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                    var writeCompletionTask = writeTask.ContinueWith(
                        _ => writeCompletionOrder = Interlocked.Increment(ref completionSequence),
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);

                    ioGate.Release();
                    ioGateHeld = false;

                    await TestAwaiter.WaitAsync(
                        Task.WhenAll(purgeTask, readTask, writeTask),
                        "interleaved recoverable maintenance and request I/O",
                        TimeSpan.FromSeconds(10));
                    await Task.WhenAll(purgeCompletionTask, readCompletionTask, writeCompletionTask);

                    var purgeResult = await purgeTask;
                    var readResult = await readTask;
                    var writeResult = await writeTask;
                    Assert.That(purgeResult.IsSuccess, Is.True, purgeResult.ErrorMessage);
                    Assert.That(readResult.IsSuccess, Is.True, readResult.ErrorMessage);
                    Assert.That(readResult.Record, Is.Not.Null);
                    Assert.That(writeResult.IsSuccess, Is.True, writeResult.ErrorMessage);
                    Assert.That(readCompletionOrder, Is.LessThan(purgeCompletionOrder));
                    Assert.That(writeCompletionOrder, Is.LessThan(purgeCompletionOrder));
                }
                finally
                {
                    if (ioGateHeld)
                    {
                        ioGate.Release();
                    }
                }
            }
            finally
            {
                DeleteDirectoryIfExists(projectPath);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ReadAsync_WhenRecordExceedsMaximumSize_RejectsRecord () => UniTask.ToCoroutine(async () =>
        {
            var projectPath = CreateTemporaryProjectPath();
            try
            {
                var store = CreateStore(projectPath);
                var requestId = Guid.NewGuid();
                var writeResult = await store.WritePendingAsync(
                    UnityIpcMethod.PlayEnter,
                    requestId,
                    RequestPayloadHash,
                    DateTimeOffset.UtcNow,
                    IpcPayloadCodec.SerializeToElement(new { before = "snapshot" }),
                    CancellationToken.None);
                Assert.That(writeResult.IsSuccess, Is.True, writeResult.ErrorMessage);
                var recordPath = FindOperationRecordPath(projectPath);
                File.WriteAllText(recordPath, new string(' ', (1024 * 1024) + 1));

                var readResult = await store.ReadAsync(
                    UnityIpcMethod.PlayEnter,
                    requestId,
                    RequestPayloadHash,
                    CancellationToken.None);

                Assert.That(readResult.IsSuccess, Is.False);
                Assert.That(readResult.Record, Is.Null);
                Assert.That(readResult.ErrorMessage, Does.Contain("maximum size"));
            }
            finally
            {
                DeleteDirectoryIfExists(projectPath);
            }
        });

        private static FileRecoverableIpcOperationStore CreateStore (string projectPath)
        {
            return FileRecoverableIpcOperationStore.Create(
                new IpcProjectIdentity(
                    projectPath,
                    ProjectFingerprint,
                    "6000.1.4f1"),
                EditorInstanceId);
        }

        private static T GetPrivateField<T> (object instance, string fieldName)
        {
            var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Private field was not found: {fieldName}");
            return (T)field.GetValue(instance);
        }

        private static async Task WaitUntilAsync (Func<bool> predicate, TimeSpan timeout)
        {
            var stopwatch = Stopwatch.StartNew();
            while (!predicate())
            {
                Assert.That(
                    stopwatch.Elapsed,
                    Is.LessThan(timeout),
                    $"Condition was not satisfied within {timeout}.");
                await Task.Delay(TimeSpan.FromMilliseconds(10));
            }
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

        private static string ResolveOperationsDirectoryPath (string projectPath)
        {
            var storageRoot = UcliStoragePathResolver.ResolveStorageRoot(projectPath);
            var projectDirectory = UcliStoragePathResolver.ResolveProjectDirectory(
                storageRoot,
                ProjectFingerprint);
            return Path.Combine(projectDirectory, UcliStoragePathNames.IpcOperationsDirectoryName);
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

        private static IpcResponse CreateSuccessResponse (Guid requestId)
        {
            return new IpcResponse(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: requestId,
                status: IpcResponseStatus.Ok,
                payload: IpcPayloadCodec.SerializeToElement(new { ok = true }),
                errors: Array.Empty<IpcError>());
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
