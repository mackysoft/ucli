using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityGuiBootstrapTests
    {
        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator CleanupFailedStart_WhenSessionWasWritten_StopsServerAndDeletesSession () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            try
            {
                var registration = await UnityGuiSessionPersistence.WriteAsync(
                    storageRoot,
                    "fingerprint",
                    new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-gui-bootstrap-tests"),
                    UnityGuiBootstrapSessionOptions.Create(null),
                    CancellationToken.None);
                var server = new SpyUnityIpcServer();
                var logCapture = new SpyDisposable();
                var serviceProvider = new SpyServiceProvider();

                await UnityGuiBootstrap.CleanupFailedStartAsync(
                    registration,
                    server,
                    logCapture,
                    serviceProvider,
                    NoOpDaemonLogger.Instance);

                Assert.That(server.StopCallCount, Is.EqualTo(1));
                Assert.That(logCapture.DisposeCallCount, Is.EqualTo(1));
                Assert.That(serviceProvider.DisposeCallCount, Is.EqualTo(1));
                Assert.That(File.Exists(registration.SessionPath), Is.False);
            }
            finally
            {
                DeleteDirectory(storageRoot);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator CleanupFailedStart_WhenServiceProviderDisposeFails_StillDeletesSession () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            try
            {
                var registration = await UnityGuiSessionPersistence.WriteAsync(
                    storageRoot,
                    "fingerprint",
                    new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-gui-bootstrap-tests"),
                    UnityGuiBootstrapSessionOptions.Create(null),
                    CancellationToken.None);

                await UnityGuiBootstrap.CleanupFailedStartAsync(
                    registration,
                    server: null,
                    unityLogCaptureService: null,
                    serviceProvider: new SpyServiceProvider(throwOnDispose: true),
                    daemonLogger: NoOpDaemonLogger.Instance);

                Assert.That(File.Exists(registration.SessionPath), Is.False);
            }
            finally
            {
                DeleteDirectory(storageRoot);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator CleanupFailedStart_WhenServerStopFails_StillDisposesAndDeletesSession () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            try
            {
                var registration = await UnityGuiSessionPersistence.WriteAsync(
                    storageRoot,
                    "fingerprint",
                    new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-gui-bootstrap-tests"),
                    UnityGuiBootstrapSessionOptions.Create(null),
                    CancellationToken.None);
                var server = new SpyUnityIpcServer(throwOnStop: true);
                var logCapture = new SpyDisposable();
                var serviceProvider = new SpyServiceProvider();

                await UnityGuiBootstrap.CleanupFailedStartAsync(
                    registration,
                    server,
                    logCapture,
                    serviceProvider,
                    NoOpDaemonLogger.Instance);

                Assert.That(server.StopCallCount, Is.EqualTo(1));
                Assert.That(logCapture.DisposeCallCount, Is.EqualTo(1));
                Assert.That(serviceProvider.DisposeCallCount, Is.EqualTo(1));
                Assert.That(File.Exists(registration.SessionPath), Is.False);
            }
            finally
            {
                DeleteDirectory(storageRoot);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ReleaseResourcesForEditorLifecycleEvent_WhenServerStopDoesNotComplete_UsesSynchronousReleaseOnly () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            try
            {
                var registration = await UnityGuiSessionPersistence.WriteAsync(
                    storageRoot,
                    "fingerprint",
                    new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-gui-bootstrap-tests"),
                    UnityGuiBootstrapSessionOptions.Create(null),
                    CancellationToken.None);
                var incompleteStopSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var server = new SpyUnityIpcServer(stopTask: incompleteStopSource.Task);
                var logCapture = new SpyDisposable();
                var serviceProvider = new SpyServiceProvider();
                var callingThreadId = Thread.CurrentThread.ManagedThreadId;

                UnityGuiBootstrap.ReleaseResourcesForEditorLifecycleEvent(
                    registration,
                    server,
                    logCapture,
                    serviceProvider,
                    NoOpDaemonLogger.Instance,
                    deleteSession: true);

                Assert.That(server.StopCallCount, Is.EqualTo(0));
                Assert.That(server.ReleaseCallCount, Is.EqualTo(1));
                Assert.That(logCapture.DisposeCallCount, Is.EqualTo(1));
                Assert.That(logCapture.DisposeThreadId, Is.EqualTo(callingThreadId));
                Assert.That(serviceProvider.DisposeCallCount, Is.EqualTo(1));
                Assert.That(serviceProvider.DisposeThreadId, Is.EqualTo(callingThreadId));
                Assert.That(File.Exists(registration.SessionPath), Is.False);
            }
            finally
            {
                DeleteDirectory(storageRoot);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ReleaseResourcesForEditorLifecycleEvent_WhenServerWasNotCreated_DisposesServiceProviderOnCallingThread () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            try
            {
                var registration = await UnityGuiSessionPersistence.WriteAsync(
                    storageRoot,
                    "fingerprint",
                    new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-gui-bootstrap-tests"),
                    UnityGuiBootstrapSessionOptions.Create(null),
                    CancellationToken.None);
                var serviceProvider = new SpyServiceProvider();
                var callingThreadId = Thread.CurrentThread.ManagedThreadId;

                UnityGuiBootstrap.ReleaseResourcesForEditorLifecycleEvent(
                    registration,
                    server: null,
                    unityLogCaptureService: null,
                    serviceProvider,
                    NoOpDaemonLogger.Instance,
                    deleteSession: true);

                Assert.That(serviceProvider.DisposeCallCount, Is.EqualTo(1));
                Assert.That(serviceProvider.DisposeThreadId, Is.EqualTo(callingThreadId));
                Assert.That(File.Exists(registration.SessionPath), Is.False);
            }
            finally
            {
                DeleteDirectory(storageRoot);
            }
        });

        private static string CreateStorageRoot ()
        {
            return Path.Combine(Path.GetTempPath(), $"ucli-gui-bootstrap-tests-{Guid.NewGuid():N}");
        }

        private static void DeleteDirectory (string storageRoot)
        {
            if (Directory.Exists(storageRoot))
            {
                Directory.Delete(storageRoot, recursive: true);
            }
        }

        private sealed class SpyUnityIpcServer : IUnityIpcServer
        {
            private readonly bool throwOnStop;

            private readonly Task stopTask;

            public SpyUnityIpcServer (
                bool throwOnStop = false,
                Task stopTask = null)
            {
                this.throwOnStop = throwOnStop;
                this.stopTask = stopTask;
            }

            public int StopCallCount { get; private set; }

            public int ReleaseCallCount { get; private set; }

            public Task StartAsync (
                IpcEndpoint endpoint,
                CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task StopAsync (CancellationToken cancellationToken = default)
            {
                StopCallCount++;
                if (throwOnStop)
                {
                    throw new InvalidOperationException("stop failed");
                }

                return stopTask ?? Task.CompletedTask;
            }

            public void ReleaseForEditorLifecycleEvent ()
            {
                ReleaseCallCount++;
                if (throwOnStop)
                {
                    throw new InvalidOperationException("release failed");
                }
            }

            public Task WaitForTerminationAsync (CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class SpyDisposable : IDisposable
        {
            public int DisposeCallCount { get; private set; }

            public int DisposeThreadId { get; private set; }

            public void Dispose ()
            {
                DisposeCallCount++;
                DisposeThreadId = Thread.CurrentThread.ManagedThreadId;
            }
        }

        private sealed class SpyServiceProvider : IServiceProvider, IDisposable
        {
            private readonly bool throwOnDispose;

            public SpyServiceProvider (bool throwOnDispose = false)
            {
                this.throwOnDispose = throwOnDispose;
            }

            public int DisposeCallCount { get; private set; }

            public int DisposeThreadId { get; private set; }

            public object GetService (Type serviceType)
            {
                return null;
            }

            public void Dispose ()
            {
                DisposeCallCount++;
                DisposeThreadId = Thread.CurrentThread.ManagedThreadId;
                if (throwOnDispose)
                {
                    throw new InvalidOperationException("dispose failed");
                }
            }
        }
    }
}
