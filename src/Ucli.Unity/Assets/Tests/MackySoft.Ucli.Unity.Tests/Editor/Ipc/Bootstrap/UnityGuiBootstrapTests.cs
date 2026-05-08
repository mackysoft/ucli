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
                var registration = await UnityGuiSessionPersistence.Write(
                    storageRoot,
                    "fingerprint",
                    new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-gui-bootstrap-tests"),
                    UnityGuiBootstrapSessionOptions.Create(null),
                    CancellationToken.None);
                var server = new SpyUnityIpcServer();
                var logCapture = new SpyDisposable();
                var serviceProvider = new SpyServiceProvider();

                await UnityGuiBootstrap.CleanupFailedStart(
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
                var registration = await UnityGuiSessionPersistence.Write(
                    storageRoot,
                    "fingerprint",
                    new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-gui-bootstrap-tests"),
                    UnityGuiBootstrapSessionOptions.Create(null),
                    CancellationToken.None);

                await UnityGuiBootstrap.CleanupFailedStart(
                    registration,
                    server: null,
                    unityLogCaptureService: null,
                    serviceProvider: new ThrowingServiceProvider(),
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
                var registration = await UnityGuiSessionPersistence.Write(
                    storageRoot,
                    "fingerprint",
                    new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-gui-bootstrap-tests"),
                    UnityGuiBootstrapSessionOptions.Create(null),
                    CancellationToken.None);
                var server = new ThrowingUnityIpcServer();
                var logCapture = new SpyDisposable();
                var serviceProvider = new SpyServiceProvider();

                await UnityGuiBootstrap.CleanupFailedStart(
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
            public int StopCallCount { get; private set; }

            public Task Start (
                IpcEndpoint endpoint,
                CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task Stop (CancellationToken cancellationToken = default)
            {
                StopCallCount++;
                return Task.CompletedTask;
            }

            public Task WaitForTermination (CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class ThrowingUnityIpcServer : IUnityIpcServer
        {
            public int StopCallCount { get; private set; }

            public Task Start (
                IpcEndpoint endpoint,
                CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task Stop (CancellationToken cancellationToken = default)
            {
                StopCallCount++;
                throw new InvalidOperationException("stop failed");
            }

            public Task WaitForTermination (CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class SpyDisposable : IDisposable
        {
            public int DisposeCallCount { get; private set; }

            public void Dispose ()
            {
                DisposeCallCount++;
            }
        }

        private sealed class ThrowingServiceProvider : IServiceProvider, IDisposable
        {
            public object GetService (Type serviceType)
            {
                return null;
            }

            public void Dispose ()
            {
                throw new InvalidOperationException("dispose failed");
            }
        }

        private sealed class SpyServiceProvider : IServiceProvider, IDisposable
        {
            public int DisposeCallCount { get; private set; }

            public object GetService (Type serviceType)
            {
                return null;
            }

            public void Dispose ()
            {
                DisposeCallCount++;
            }
        }
    }
}
