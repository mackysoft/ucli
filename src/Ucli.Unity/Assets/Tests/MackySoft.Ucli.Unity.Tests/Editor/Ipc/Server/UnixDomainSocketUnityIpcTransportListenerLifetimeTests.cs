using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnixDomainSocketUnityIpcTransportListenerLifetimeTests
    {
        private const int MaximumActiveConnections = 32;

        private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

        private static readonly TimeSpan ConnectionDrainTimeout = TimeSpan.FromSeconds(1);

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Run_WhenReservedGenerationFailsValidation_ReleasesReservation () => UniTask.ToCoroutine(async () =>
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                return;
            }

            var fallbackPath = new UnixSocketFallbackPath(
                Path.GetTempPath(),
                UnixSocketFallbackPurpose.Daemon,
                Guid.NewGuid().ToString("N"));
            var socketDirectoryPath = fallbackPath.DirectoryPath;
            var address = fallbackPath.SocketPath;
            var listener = new UnixDomainSocketUnityIpcTransportListener(
                NoOpDaemonLogger.Instance,
                new IpcEndpoint(IpcTransportKind.UnixDomainSocket, address),
                MaximumActiveConnections,
                ConnectionDrainTimeout);
            using var cancellationTokenSource = new CancellationTokenSource();

            try
            {
                listener.ReserveRun(cancellationTokenSource.Token);
                ArgumentException validationException = null;
                try
                {
                    await listener.RunAsync(
                        string.Empty,
                        new NoOpConnectionHandler(),
                        () => { },
                        _ => { },
                        cancellationTokenSource.Token);
                }
                catch (ArgumentException exception)
                {
                    validationException = exception;
                }

                Assert.That(validationException, Is.Not.Null);

                listener.ReserveRun(cancellationTokenSource.Token);
                listener.Release();
                await listener.RunAsync(
                    address,
                    new NoOpConnectionHandler(),
                    () => { },
                    _ => { },
                    cancellationTokenSource.Token);
            }
            finally
            {
                listener.Release();
                if (Directory.Exists(socketDirectoryPath))
                {
                    Directory.Delete(socketDirectoryPath, recursive: true);
                }
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Run_WhenReservedGenerationIsReleasedBeforeBackgroundEntry_DoesNotStartAndAllowsSuccessor () => UniTask.ToCoroutine(async () =>
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                return;
            }

            var fallbackPath = new UnixSocketFallbackPath(
                Path.GetTempPath(),
                UnixSocketFallbackPurpose.Daemon,
                Guid.NewGuid().ToString("N"));
            var socketDirectoryPath = fallbackPath.DirectoryPath;
            var address = fallbackPath.SocketPath;
            var listener = new UnixDomainSocketUnityIpcTransportListener(
                NoOpDaemonLogger.Instance,
                new IpcEndpoint(IpcTransportKind.UnixDomainSocket, address),
                MaximumActiveConnections,
                ConnectionDrainTimeout);
            using var releasedCancellationTokenSource = new CancellationTokenSource();
            using var successorCancellationTokenSource = new CancellationTokenSource();
            var releasedGenerationStarted = false;
            var successorStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task releasedRunTask = null;
            Task successorRunTask = null;

            try
            {
                listener.ReserveRun(releasedCancellationTokenSource.Token);
                listener.Release();

                releasedRunTask = listener.RunAsync(
                    address,
                    new NoOpConnectionHandler(),
                    () => releasedGenerationStarted = true,
                    _ => { },
                    releasedCancellationTokenSource.Token);
                await TestAwaiter.WaitAsync(
                    releasedRunTask,
                    "Released pre-entry Unix socket generation",
                    SignalWaitTimeout);

                listener.ReserveRun(successorCancellationTokenSource.Token);
                successorRunTask = listener.RunAsync(
                    address,
                    new NoOpConnectionHandler(),
                    () => successorStarted.TrySetResult(true),
                    _ => { },
                    successorCancellationTokenSource.Token);
                await TestAwaiter.WaitAsync(
                    successorStarted.Task,
                    "Successor after released pre-entry Unix socket generation",
                    SignalWaitTimeout);

                Assert.That(releasedGenerationStarted, Is.False);
            }
            finally
            {
                releasedCancellationTokenSource.Cancel();
                successorCancellationTokenSource.Cancel();
                listener.Release();
                if (releasedRunTask != null)
                {
                    await WaitForListenerStopAsync(
                        releasedRunTask,
                        "Released pre-entry Unix socket generation cleanup");
                }

                if (successorRunTask != null)
                {
                    await WaitForListenerStopAsync(
                        successorRunTask,
                        "Successor after pre-entry Unix socket release cleanup");
                }

                if (Directory.Exists(socketDirectoryPath))
                {
                    Directory.Delete(socketDirectoryPath, recursive: true);
                }
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Run_WhenReleasedGenerationCompletesAfterSameListenerRestarts_DoesNotDeleteRestartedSocket () => UniTask.ToCoroutine(async () =>
        {
            await AssertReleasedGenerationDoesNotDeleteRestartedSocketAsync(useSeparateListenerInstance: false);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Run_WhenReleasedGenerationCompletesAfterSeparateListenerRestarts_DoesNotDeleteRestartedSocket () => UniTask.ToCoroutine(async () =>
        {
            await AssertReleasedGenerationDoesNotDeleteRestartedSocketAsync(useSeparateListenerInstance: true);
        });

        [UnityTest]
        [Category("Size.Medium")]
        public IEnumerator Run_WhenIndependentOwnerHoldsEndpointLock_FailsBeforeUnlinkingAndStartsAfterOwnerExit () => UniTask.ToCoroutine(async () =>
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                return;
            }

            var fallbackPath = new UnixSocketFallbackPath(
                Path.GetTempPath(),
                UnixSocketFallbackPurpose.Daemon,
                Guid.NewGuid().ToString("N"));
            var socketDirectoryPath = fallbackPath.DirectoryPath;
            var address = fallbackPath.SocketPath;
            var lockPath = UnixDomainSocketUnityIpcTransportListener.ResolveEndpointOwnershipLockPath(address);
            var lockDirectoryPath = Path.GetDirectoryName(lockPath);
            Assert.That(lockDirectoryPath, Is.Not.Null);
            Directory.CreateDirectory(socketDirectoryPath);
            Directory.CreateDirectory(lockDirectoryPath!);
            File.WriteAllText(address, "active-owner-residue");

            var listener = new UnixDomainSocketUnityIpcTransportListener(
                NoOpDaemonLogger.Instance,
                new IpcEndpoint(IpcTransportKind.UnixDomainSocket, address),
                MaximumActiveConnections,
                ConnectionDrainTimeout);
            using var firstCancellationTokenSource = new CancellationTokenSource();
            using var restartedCancellationTokenSource = new CancellationTokenSource();
            Task firstRunTask = null;
            Task restartedRunTask = null;
            EndpointOwnershipLockOwner independentOwner = null;
            try
            {
                independentOwner = await EndpointOwnershipLockOwner.AcquireAsync(lockPath);
                var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                firstRunTask = listener.RunAsync(
                    address,
                    new NoOpConnectionHandler(),
                    () => firstStarted.TrySetResult(true),
                    _ => { },
                    firstCancellationTokenSource.Token);
                var firstOutcome = await TestAwaiter.WaitAsync(
                    Task.WhenAny(firstRunTask, firstStarted.Task),
                    "Contended Unix socket listener outcome",
                    SignalWaitTimeout);
                TimeoutException ownershipException = null;
                if (ReferenceEquals(firstOutcome, firstRunTask))
                {
                    var completedRunTask = firstRunTask;
                    try
                    {
                        await completedRunTask;
                    }
                    catch (TimeoutException exception)
                    {
                        ownershipException = exception;
                    }
                    finally
                    {
                        firstRunTask = null;
                    }
                }

                firstCancellationTokenSource.Cancel();
                listener.Release();
                if (firstRunTask != null)
                {
                    await WaitForListenerStopAsync(firstRunTask, "Contended Unix socket listener cleanup");
                }

                Assert.That(ownershipException, Is.Not.Null);
                Assert.That(File.ReadAllText(address), Is.EqualTo("active-owner-residue"));

                await independentOwner.ExitUnexpectedlyAsync();
                independentOwner = null;

                var restarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                restartedRunTask = listener.RunAsync(
                    address,
                    new NoOpConnectionHandler(),
                    () => restarted.TrySetResult(true),
                    _ => { },
                    restartedCancellationTokenSource.Token);
                await TestAwaiter.WaitAsync(
                    restarted.Task,
                    "Unix socket listener start after ownership lock release",
                    SignalWaitTimeout);

                Assert.That(File.Exists(address), Is.True);
            }
            finally
            {
                if (independentOwner != null)
                {
                    await independentOwner.ReleaseAsync();
                }

                firstCancellationTokenSource.Cancel();
                restartedCancellationTokenSource.Cancel();
                listener.Release();
                if (firstRunTask != null)
                {
                    await WaitForListenerStopAsync(firstRunTask, "Contended Unix socket listener final cleanup");
                }

                if (restartedRunTask != null)
                {
                    await WaitForListenerStopAsync(restartedRunTask, "Restarted Unix socket listener final cleanup");
                }

                if (Directory.Exists(socketDirectoryPath))
                {
                    Directory.Delete(socketDirectoryPath, recursive: true);
                }

                if (Directory.Exists(lockDirectoryPath!))
                {
                    Directory.Delete(lockDirectoryPath!, recursive: true);
                }
            }
        });

        private static async Task AssertReleasedGenerationDoesNotDeleteRestartedSocketAsync (bool useSeparateListenerInstance)
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                return;
            }

            var fallbackPath = new UnixSocketFallbackPath(
                Path.GetTempPath(),
                UnixSocketFallbackPurpose.Daemon,
                Guid.NewGuid().ToString("N"));
            var socketDirectoryPath = fallbackPath.DirectoryPath;
            var address = fallbackPath.SocketPath;
            var firstListener = new UnixDomainSocketUnityIpcTransportListener(
                NoOpDaemonLogger.Instance,
                new IpcEndpoint(IpcTransportKind.UnixDomainSocket, address),
                MaximumActiveConnections,
                ConnectionDrainTimeout);
            var restartedListener = useSeparateListenerInstance
                ? new UnixDomainSocketUnityIpcTransportListener(
                    NoOpDaemonLogger.Instance,
                    new IpcEndpoint(IpcTransportKind.UnixDomainSocket, address),
                    MaximumActiveConnections,
                    ConnectionDrainTimeout)
                : firstListener;
            var firstConnectionHandler = new ReleasableConnectionHandler();
            var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var restarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var firstCancellationTokenSource = new CancellationTokenSource();
            using var restartedCancellationTokenSource = new CancellationTokenSource();
            Task firstRunTask = null;
            Task restartedRunTask = null;
            Socket firstClient = null;

            try
            {
                firstRunTask = firstListener.RunAsync(
                    address,
                    firstConnectionHandler,
                    () => firstStarted.TrySetResult(true),
                    _ => { },
                    firstCancellationTokenSource.Token);
                await TestAwaiter.WaitAsync(firstStarted.Task, "First Unix socket listener start", SignalWaitTimeout);

                firstClient = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                await TestAwaiter.WaitAsync(
                    firstClient.ConnectAsync(new UnixDomainSocketEndPoint(address)),
                    "First Unix socket client connection",
                    SignalWaitTimeout);
                await TestAwaiter.WaitAsync(
                    firstConnectionHandler.ConnectionObserved,
                    "First Unix socket connection handling",
                    SignalWaitTimeout);

                firstCancellationTokenSource.Cancel();
                firstListener.Release();

                restartedRunTask = restartedListener.RunAsync(
                    address,
                    new NoOpConnectionHandler(),
                    () => restarted.TrySetResult(true),
                    _ => { },
                    restartedCancellationTokenSource.Token);
                await TestAwaiter.WaitAsync(restarted.Task, "Restarted Unix socket listener start", SignalWaitTimeout);
                Assert.That(File.Exists(address), Is.True);

                firstConnectionHandler.Complete();
                await WaitForListenerStopAsync(firstRunTask, "Released Unix socket listener completion");

                Assert.That(File.Exists(address), Is.True);
            }
            finally
            {
                firstConnectionHandler.Complete();
                firstClient?.Dispose();
                firstCancellationTokenSource.Cancel();
                restartedCancellationTokenSource.Cancel();
                firstListener.Release();
                if (!ReferenceEquals(restartedListener, firstListener))
                {
                    restartedListener.Release();
                }

                if (firstRunTask != null)
                {
                    await WaitForListenerStopAsync(firstRunTask, "First Unix socket listener cleanup");
                }

                if (restartedRunTask != null)
                {
                    await WaitForListenerStopAsync(restartedRunTask, "Restarted Unix socket listener cleanup");
                }
            }

            Assert.That(File.Exists(address), Is.False);
            try
            {
                Assert.That(Directory.Exists(socketDirectoryPath), Is.True);
            }
            finally
            {
                if (Directory.Exists(socketDirectoryPath))
                {
                    Directory.Delete(socketDirectoryPath, recursive: true);
                }
            }
        }

        private static async Task WaitForListenerStopAsync (
            Task listenerTask,
            string description)
        {
            try
            {
                await TestAwaiter.WaitAsync(listenerTask, description, SignalWaitTimeout);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private sealed class EndpointOwnershipLockOwner
        {
            private const string LockCommandPath = "/usr/bin/lockf";

            private const string EchoCommandPath = "/bin/cat";

            private const string ReadySignal = "LOCKED";

            private readonly Process process;

            private readonly FileStream lockStream;

            private int released;

            private EndpointOwnershipLockOwner (
                Process process,
                FileStream lockStream)
            {
                this.process = process;
                this.lockStream = lockStream;
            }

            public static async Task<EndpointOwnershipLockOwner> AcquireAsync (string lockPath)
            {
                if (!File.Exists(LockCommandPath) || !File.Exists(EchoCommandPath))
                {
                    var lockStream = new FileStream(
                        lockPath,
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.None);
                    lockStream.Lock(0, 1);
                    return new EndpointOwnershipLockOwner(null, lockStream);
                }

                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = LockCommandPath,
                    Arguments = $"-t 0 {QuoteProcessArgument(lockPath)} {EchoCommandPath}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }) ?? throw new InvalidOperationException("Endpoint ownership lock child process could not be started.");
                try
                {
                    await process.StandardInput.WriteLineAsync(ReadySignal);
                    await process.StandardInput.FlushAsync();
                    var observedSignal = await TestAwaiter.WaitAsync(
                        process.StandardOutput.ReadLineAsync(),
                        "Endpoint ownership lock child process readiness",
                        SignalWaitTimeout);
                    if (!string.Equals(observedSignal, ReadySignal, StringComparison.Ordinal))
                    {
                        var error = await process.StandardError.ReadToEndAsync();
                        throw new InvalidOperationException(
                            $"Endpoint ownership lock child process did not acquire the lock. Error={error}");
                    }

                    return new EndpointOwnershipLockOwner(process, null);
                }
                catch
                {
                    TerminateProcess(process);
                    throw;
                }
            }

            public async Task ReleaseAsync ()
            {
                await ReleaseAsync(exitProcessUnexpectedly: false);
            }

            public async Task ExitUnexpectedlyAsync ()
            {
                await ReleaseAsync(exitProcessUnexpectedly: true);
            }

            private async Task ReleaseAsync (bool exitProcessUnexpectedly)
            {
                if (Interlocked.Exchange(ref released, 1) != 0)
                {
                    return;
                }

                if (lockStream != null)
                {
                    try
                    {
                        lockStream.Unlock(0, 1);
                    }
                    finally
                    {
                        lockStream.Dispose();
                    }
                }

                if (process != null)
                {
                    if (exitProcessUnexpectedly)
                    {
                        process.Kill();
                    }
                    else
                    {
                        process.StandardInput.Close();
                    }

                    await TestAwaiter.WaitAsync(
                        Task.Run(() => TerminateProcess(process)),
                        "Endpoint ownership lock child process release",
                        SignalWaitTimeout);
                }
            }

            private static string QuoteProcessArgument (string argument)
            {
                if (argument.IndexOf('"') >= 0)
                {
                    throw new ArgumentException("Process argument must not contain a quote.", nameof(argument));
                }

                return $"\"{argument}\"";
            }

            private static void TerminateProcess (Process process)
            {
                try
                {
                    if (!process.HasExited && !process.WaitForExit((int)SignalWaitTimeout.TotalMilliseconds / 2))
                    {
                        process.Kill();
                        process.WaitForExit((int)SignalWaitTimeout.TotalMilliseconds / 2);
                    }
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        private sealed class ReleasableConnectionHandler : IUnityIpcConnectionHandler
        {
            private readonly TaskCompletionSource<bool> connectionObserved =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> completion =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task ConnectionObserved => connectionObserved.Task;

            public void Complete ()
            {
                completion.TrySetResult(true);
            }

            public async Task<UnityIpcConnectionHandleResult> HandleAsync (
                Stream stream,
                CancellationToken cancellationToken = default)
            {
                connectionObserved.TrySetResult(true);
                await completion.Task;
                return UnityIpcConnectionHandleResult.NoTerminalResponse;
            }
        }

        private sealed class NoOpConnectionHandler : IUnityIpcConnectionHandler
        {
            public Task<UnityIpcConnectionHandleResult> HandleAsync (
                Stream stream,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(UnityIpcConnectionHandleResult.NoTerminalResponse);
            }
        }
    }
}
