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
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;
using UnityEngine.TestTools;

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

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator WritePendingAsync_ThenReadAsync_RestoresPendingRecord () => UniTask.ToCoroutine(async () =>
        {
            var projectPath = CreateTemporaryProjectPath();
            try
            {
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-pending");
                var store = CreateStore(projectPath);
                var startedAtUtc = DateTimeOffset.UtcNow;
                var payload = IpcPayloadCodec.SerializeToElement(new { before = "snapshot" });

                var writeResult = await store.WritePendingAsync(
                    IpcMethodNames.PlayEnter,
                    "req-pending",
                    RequestPayloadHash,
                    startedAtUtc,
                    payload,
                    CancellationToken.None);
                var readResult = await store.ReadAsync(
                    IpcMethodNames.PlayEnter,
                    "req-pending",
                    RequestPayloadHash,
                    CancellationToken.None);

                Assert.That(writeResult.IsSuccess, Is.True, writeResult.ErrorMessage);
                Assert.That(readResult.IsSuccess, Is.True, readResult.ErrorMessage);
                Assert.That(readResult.Record, Is.Not.Null);
                Assert.That(readResult.Record.State, Is.EqualTo(RecoverableIpcOperationState.Pending));
                Assert.That(ReadOperationRecordText(projectPath), Does.Contain("\"state\":\"pending\""));
                Assert.That(readResult.Record.ProjectFingerprint, Is.EqualTo(ProjectFingerprint));
                Assert.That(readResult.Record.Method, Is.EqualTo(IpcMethodNames.PlayEnter));
                Assert.That(readResult.Record.RequestId, Is.EqualTo("req-pending"));
                Assert.That(readResult.Record.RequestPayloadHash, Is.EqualTo(RequestPayloadHash));
                Assert.That(readResult.Record.HostEditorInstanceId, Is.EqualTo("editor-instance-pending"));
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
        public IEnumerator WriteCompletedAsync_ThenReadAsync_RestoresCompletedResponse () => UniTask.ToCoroutine(async () =>
        {
            var projectPath = CreateTemporaryProjectPath();
            try
            {
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-completed");
                var store = CreateStore(projectPath);
                var response = CreateSuccessResponse("req-completed");

                var writeResult = await store.WriteCompletedAsync(
                    IpcMethodNames.Compile,
                    "req-completed",
                    RequestPayloadHash,
                    DateTimeOffset.UtcNow.AddSeconds(-1),
                    DateTimeOffset.UtcNow,
                    IpcPayloadCodec.SerializeToElement(new { runId = "run-1" }),
                    response,
                    CancellationToken.None);
                var readResult = await store.ReadAsync(
                    IpcMethodNames.Compile,
                    "req-completed",
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
        public IEnumerator ReadAsync_WhenRecordIdentityIsInvalid_RejectsRecord () => UniTask.ToCoroutine(async () =>
        {
            var projectPath = CreateTemporaryProjectPath();
            try
            {
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-invalid-record");
                var store = CreateStore(projectPath);
                var writeResult = await store.WritePendingAsync(
                    IpcMethodNames.PlayEnter,
                    "req-invalid",
                    RequestPayloadHash,
                    DateTimeOffset.UtcNow,
                    IpcPayloadCodec.SerializeToElement(new { before = "snapshot" }),
                    CancellationToken.None);
                Assert.That(writeResult.IsSuccess, Is.True, writeResult.ErrorMessage);
                RewriteOperationRecord(projectPath, record => record.ProjectFingerprint = "other-project");

                var readResult = await store.ReadAsync(
                    IpcMethodNames.PlayEnter,
                    "req-invalid",
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
        public IEnumerator ReadAsync_WhenRecordStateIsUnsupported_RejectsRecord () => UniTask.ToCoroutine(async () =>
        {
            var projectPath = CreateTemporaryProjectPath();
            try
            {
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-unsupported-state");
                var store = CreateStore(projectPath);
                var writeResult = await store.WritePendingAsync(
                    IpcMethodNames.PlayEnter,
                    "req-unsupported-state",
                    RequestPayloadHash,
                    DateTimeOffset.UtcNow,
                    IpcPayloadCodec.SerializeToElement(new { before = "snapshot" }),
                    CancellationToken.None);
                Assert.That(writeResult.IsSuccess, Is.True, writeResult.ErrorMessage);
                RewriteOperationRecordText(
                    projectPath,
                    text => text.Replace("\"state\":\"pending\"", "\"state\":\"unsupported\"", StringComparison.Ordinal));

                var readResult = await store.ReadAsync(
                    IpcMethodNames.PlayEnter,
                    "req-unsupported-state",
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
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-host-process");
                var store = CreateStore(projectPath);
                var writeResult = await store.WritePendingAsync(
                    IpcMethodNames.PlayEnter,
                    "req-host-process-id",
                    RequestPayloadHash,
                    DateTimeOffset.UtcNow,
                    IpcPayloadCodec.SerializeToElement(new { before = "snapshot" }),
                    CancellationToken.None);
                Assert.That(writeResult.IsSuccess, Is.True, writeResult.ErrorMessage);
                RewriteOperationRecord(projectPath, record => record.HostProcessId++);

                var readResult = await store.ReadAsync(
                    IpcMethodNames.PlayEnter,
                    "req-host-process-id",
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
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-host-1");
                var store = CreateStore(projectPath);
                var writeResult = await store.WriteCompletedAsync(
                    IpcMethodNames.Compile,
                    "req-host-editor-instance",
                    RequestPayloadHash,
                    DateTimeOffset.UtcNow.AddSeconds(-1),
                    DateTimeOffset.UtcNow,
                    IpcPayloadCodec.SerializeToElement(new { runId = "run-1" }),
                    CreateSuccessResponse("req-host-editor-instance"),
                    CancellationToken.None);
                Assert.That(writeResult.IsSuccess, Is.True, writeResult.ErrorMessage);
                RewriteOperationRecord(projectPath, record => record.HostEditorInstanceId = "editor-instance-host-2");

                var readResult = await store.ReadAsync(
                    IpcMethodNames.Compile,
                    "req-host-editor-instance",
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
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-hash");
                var store = CreateStore(projectPath);
                var writeResult = await store.WriteCompletedAsync(
                    IpcMethodNames.PlayEnter,
                    "req-hash-mismatch",
                    RequestPayloadHash,
                    DateTimeOffset.UtcNow.AddSeconds(-1),
                    DateTimeOffset.UtcNow,
                    IpcPayloadCodec.SerializeToElement(new { before = "snapshot" }),
                    CreateSuccessResponse("req-hash-mismatch"),
                    CancellationToken.None);
                Assert.That(writeResult.IsSuccess, Is.True, writeResult.ErrorMessage);

                var readResult = await store.ReadAsync(
                    IpcMethodNames.PlayEnter,
                    "req-hash-mismatch",
                    "other-request-payload-hash",
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
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-purge");
                var store = CreateStore(projectPath);
                var nowUtc = DateTimeOffset.UtcNow;
                var writeResult = await store.WriteCompletedAsync(
                    IpcMethodNames.Compile,
                    "req-expired",
                    RequestPayloadHash,
                    nowUtc.AddMinutes(-12),
                    nowUtc.AddMinutes(-11),
                    IpcPayloadCodec.SerializeToElement(new { runId = "run-1" }),
                    CreateSuccessResponse("req-expired"),
                    CancellationToken.None);
                Assert.That(writeResult.IsSuccess, Is.True, writeResult.ErrorMessage);

                var purgeResult = await store.PurgeExpiredRecordsAsync(nowUtc, CancellationToken.None);
                var readResult = await store.ReadAsync(
                    IpcMethodNames.Compile,
                    "req-expired",
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
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-purge-pending");
                var store = CreateStore(projectPath);
                var nowUtc = DateTimeOffset.UtcNow;
                var writeResult = await store.WritePendingAsync(
                    IpcMethodNames.PlayEnter,
                    "req-expired-pending",
                    RequestPayloadHash,
                    nowUtc.AddHours(-25),
                    IpcPayloadCodec.SerializeToElement(new { before = "snapshot" }),
                    CancellationToken.None);
                Assert.That(writeResult.IsSuccess, Is.True, writeResult.ErrorMessage);

                var purgeResult = await store.PurgeExpiredRecordsAsync(nowUtc, CancellationToken.None);
                var readResult = await store.ReadAsync(
                    IpcMethodNames.PlayEnter,
                    "req-expired-pending",
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
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-purge-invalid");
                var store = CreateStore(projectPath);
                var nowUtc = DateTimeOffset.UtcNow;
                var writeResult = await store.WritePendingAsync(
                    IpcMethodNames.PlayEnter,
                    "req-expired-invalid",
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
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-purge-cursor");
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
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-maintenance-gate");
                var store = CreateStore(projectPath);
                var maintenanceGate = GetPrivateField<SemaphoreSlim>(store, "maintenanceGate");
                await maintenanceGate.WaitAsync(CancellationToken.None);
                Task<RecoverableIpcOperationStoreResult> purgeTask = null;
                try
                {
                    purgeTask = store
                        .PurgeExpiredRecordsAsync(DateTimeOffset.UtcNow, CancellationToken.None)
                        .AsTask();
                    var writeTask = store.WritePendingAsync(
                            IpcMethodNames.PlayEnter,
                            "req-while-maintenance-queued",
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
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-maintenance-interleave");
                var store = CreateStore(projectPath);
                var initialWriteResult = await store.WritePendingAsync(
                    IpcMethodNames.PlayEnter,
                    "req-existing-during-maintenance",
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
                            IpcMethodNames.PlayEnter,
                            "req-existing-during-maintenance",
                            RequestPayloadHash,
                            CancellationToken.None)
                        .AsTask();
                    var writeTask = store.WritePendingAsync(
                            IpcMethodNames.PlayExit,
                            "req-new-during-maintenance",
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
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-large-record");
                var store = CreateStore(projectPath);
                var writeResult = await store.WritePendingAsync(
                    IpcMethodNames.PlayEnter,
                    "req-large-record",
                    RequestPayloadHash,
                    DateTimeOffset.UtcNow,
                    IpcPayloadCodec.SerializeToElement(new { before = "snapshot" }),
                    CancellationToken.None);
                Assert.That(writeResult.IsSuccess, Is.True, writeResult.ErrorMessage);
                var recordPath = FindOperationRecordPath(projectPath);
                File.WriteAllText(recordPath, new string(' ', (1024 * 1024) + 1));

                var readResult = await store.ReadAsync(
                    IpcMethodNames.PlayEnter,
                    "req-large-record",
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
            return FileRecoverableIpcOperationStore.Create(new IpcProjectIdentity(
                projectPath,
                ProjectFingerprint,
                "6000.1.4f1"));
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
            var fingerprintDirectory = UcliStoragePathResolver.ResolveFingerprintDirectory(
                storageRoot,
                ProjectFingerprint);
            return Path.Combine(fingerprintDirectory, UcliStoragePathNames.IpcOperationsDirectoryName);
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
