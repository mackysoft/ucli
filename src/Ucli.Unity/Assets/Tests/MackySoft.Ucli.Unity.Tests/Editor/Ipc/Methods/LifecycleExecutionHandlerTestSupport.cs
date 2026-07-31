using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Execution.Lifecycle;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    internal static class LifecycleExecutionHandlerTestSupport
    {
        public static TTerminalRecord ReadTerminalRecord<TTerminalRecord> (
            FileLifecycleExecutionStore executionStore,
            LifecycleExecutionKind kind,
            Guid executionId)
            where TTerminalRecord : LifecycleExecutionTerminalRecord
        {
            var path = executionStore.Paths.ResolveTerminalRecordPath(
                kind,
                executionId).Target;
            return JsonSerializer.Deserialize<TTerminalRecord>(
                    ReadGuardedBytes(path),
                    IpcJsonSerializerOptions.Default)
                ?? throw new AssertionException(
                    $"{typeof(TTerminalRecord).Name} was empty.");
        }

        public static byte[] ReadGuardedBytes (AbsolutePath path)
        {
            var contents = FileUtilities
                .ReadAllBytesOrNullAsync(path, CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();
            return contents?.ToArray()
                ?? throw new AssertionException(
                    $"Guarded test file was not found: {path.Value}");
        }

        public static string ReadGuardedText (AbsolutePath path)
        {
            return FileUtilities.ReadAllTextOrNull(path)
                ?? throw new AssertionException(
                    $"Guarded test file was not found: {path.Value}");
        }

        public static void WriteGuardedText (
            AbsolutePath path,
            string contents)
        {
            FileUtilities.WriteAllTextAtomically(path, contents);
        }

        public static ValueTask WriteGuardedTextAsync (
            AbsolutePath path,
            string contents,
            CancellationToken cancellationToken)
        {
            return FileUtilities.WriteAllTextAtomicallyAsync(
                path,
                contents,
                cancellationToken);
        }

        public static bool GuardedFileExists (AbsolutePath path)
        {
            return FileUtilities.FileExists(path);
        }

        public static void DeleteGuardedFileIfExists (AbsolutePath path)
        {
            FileUtilities.DeleteIfExists(path);
        }

        internal sealed class TemporaryStorageScope : IDisposable
        {
            private TemporaryStorageScope (string rootPath)
            {
                RootPath = rootPath;
            }

            public string RootPath { get; }

            public static TemporaryStorageScope Create ()
            {
                var rootPath = Path.Combine(
                    Path.GetTempPath(),
                    "ucli-lifecycle-execution-handler-tests",
                    Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(rootPath);
                return new TemporaryStorageScope(rootPath);
            }

            public FileLifecycleExecutionStore CreateExecutionStore (
                ProjectFingerprint projectFingerprint)
            {
                return new FileLifecycleExecutionStore(
                    AbsolutePath.Parse(RootPath),
                    projectFingerprint);
            }

            public void Dispose ()
            {
                if (Directory.Exists(RootPath))
                {
                    Directory.Delete(RootPath, recursive: true);
                }
            }
        }

        internal sealed class MutableUnityEditorReadinessGate :
            IUnityEditorReadinessGate
        {
            public MutableUnityEditorReadinessGate (
                UnityEditorRuntimeObservation snapshot)
            {
                Snapshot = snapshot;
            }

            public UnityEditorRuntimeObservation Snapshot { get; set; }

            public int CaptureObservationCallCount { get; private set; }

            public UnityEditorRuntimeObservation CaptureObservation ()
            {
                CaptureObservationCallCount++;
                return Snapshot;
            }

            public Task<UnityEditorExecutionReadinessResult>
                EnsureExecutionReadyAsync (
                    bool failFast,
                    CancellationToken cancellationToken = default,
                    bool allowPlayMode = false)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(
                    UnityEditorExecutionReadinessResult.Ready(Snapshot));
            }
        }

        internal sealed class StubServerVersionProvider :
            IServerVersionProvider
        {
            private readonly string version;

            public StubServerVersionProvider (string version)
            {
                this.version = version;
            }

            public string GetVersion ()
            {
                return version;
            }
        }

        internal sealed class RecordingUnityAssetRefreshController :
            IUnityAssetRefreshController
        {
            private readonly Action refresh;

            public RecordingUnityAssetRefreshController (Action refresh)
            {
                this.refresh = refresh
                    ?? throw new ArgumentNullException(nameof(refresh));
            }

            public void Refresh ()
            {
                refresh();
            }
        }
    }
}
