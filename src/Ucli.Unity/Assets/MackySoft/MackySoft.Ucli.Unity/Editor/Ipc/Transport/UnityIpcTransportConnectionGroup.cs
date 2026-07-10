using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Tracks active transport connections owned by one listener instance. </summary>
    internal sealed class UnityIpcTransportConnectionGroup
    {
        private readonly object syncRoot = new object();

        private readonly IDaemonLogger daemonLogger;

        private readonly int maximumActiveConnections;

        private readonly List<ActiveConnection> activeConnections = new List<ActiveConnection>();

        private bool isReleased;

        /// <summary> Initializes a new instance of the <see cref="UnityIpcTransportConnectionGroup" /> class. </summary>
        /// <param name="daemonLogger"> The daemon logger dependency. </param>
        /// <param name="maximumActiveConnections"> The maximum number of accepted connections that may be handled concurrently. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="daemonLogger" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="maximumActiveConnections" /> is not positive. </exception>
        public UnityIpcTransportConnectionGroup (
            IDaemonLogger daemonLogger,
            int maximumActiveConnections)
        {
            this.daemonLogger = daemonLogger ?? throw new ArgumentNullException(nameof(daemonLogger));
            if (maximumActiveConnections <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumActiveConnections),
                    maximumActiveConnections,
                    "Maximum active connections must be greater than zero.");
            }

            this.maximumActiveConnections = maximumActiveConnections;
        }

        /// <summary> Attempts to admit and start one accepted connection without blocking the listener accept loop. </summary>
        /// <param name="transportHandle"> The disposable transport handle owned by this connection. </param>
        /// <param name="handleConnection"> The connection exchange delegate. </param>
        /// <param name="onConnectionCompleted"> The callback invoked when the connection completes normally. </param>
        /// <param name="cancellationToken"> The listener lifecycle cancellation token. </param>
        /// <returns> <see langword="true" /> when the connection was admitted; otherwise, <see langword="false" /> after closing the rejected transport handle. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when a dependency is <see langword="null" />. </exception>
        public bool TryStart (
            IDisposable transportHandle,
            Func<Task<UnityIpcConnectionHandleResult>> handleConnection,
            Action<UnityIpcConnectionHandleResult> onConnectionCompleted,
            CancellationToken cancellationToken)
        {
            if (transportHandle == null)
            {
                throw new ArgumentNullException(nameof(transportHandle));
            }

            if (handleConnection == null)
            {
                throw new ArgumentNullException(nameof(handleConnection));
            }

            if (onConnectionCompleted == null)
            {
                throw new ArgumentNullException(nameof(onConnectionCompleted));
            }

            ActiveConnection activeConnection = null;
            lock (syncRoot)
            {
                if (!isReleased
                    && !cancellationToken.IsCancellationRequested
                    && activeConnections.Count < maximumActiveConnections)
                {
                    activeConnection = new ActiveConnection(
                        transportHandle,
                        DisposeTransportHandle);
                    activeConnections.Add(activeConnection);
                }
            }

            if (activeConnection == null)
            {
                DisposeTransportHandle(transportHandle);
                return false;
            }

            var startSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _ = RunConnectionAsync(
                startSignal.Task,
                activeConnection,
                handleConnection,
                onConnectionCompleted,
                cancellationToken);
            startSignal.TrySetResult(true);
            return true;
        }

        /// <summary> Starts asynchronous cleanup for active connection handles without blocking the caller. </summary>
        public void Release ()
        {
            ActiveConnection[] connections;
            lock (syncRoot)
            {
                isReleased = true;
                connections = activeConnections.ToArray();
            }

            foreach (var connection in connections)
            {
                _ = connection.BeginTransportHandleRelease();
            }
        }

        /// <summary> Waits for active connection tasks to observe release or cancellation. </summary>
        /// <param name="drainTimeout"> The maximum time allowed for active connections to finish after their transport handles are released. </param>
        /// <returns> A task that completes when the currently tracked connection tasks have finished. </returns>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="drainTimeout" /> is not positive. </exception>
        /// <exception cref="TimeoutException"> Thrown when at least one connection remains active after <paramref name="drainTimeout" />. </exception>
        public async Task WaitForCompletionAsync (TimeSpan drainTimeout)
        {
            if (drainTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(drainTimeout),
                    drainTimeout,
                    "Connection drain timeout must be greater than zero.");
            }

            Task[] tasks;
            lock (syncRoot)
            {
                tasks = new Task[activeConnections.Count];
                for (var index = 0; index < activeConnections.Count; index++)
                {
                    tasks[index] = activeConnections[index].Completion;
                }
            }

            if (tasks.Length == 0)
            {
                return;
            }

            var completionTask = Task.WhenAll(tasks);
            using var timeoutCancellationTokenSource = new CancellationTokenSource();
            var timeoutTask = Task.Delay(drainTimeout, timeoutCancellationTokenSource.Token);
            var completedTask = await Task.WhenAny(completionTask, timeoutTask);
            if (!ReferenceEquals(completedTask, completionTask))
            {
                ObserveFault(completionTask);
                throw new TimeoutException(
                    $"IPC transport connection drain did not complete within {drainTimeout.TotalMilliseconds:0} milliseconds.");
            }

            timeoutCancellationTokenSource.Cancel();
            await completionTask;
        }

        private async Task RunConnectionAsync (
            Task startSignal,
            ActiveConnection activeConnection,
            Func<Task<UnityIpcConnectionHandleResult>> handleConnection,
            Action<UnityIpcConnectionHandleResult> onConnectionCompleted,
            CancellationToken cancellationToken)
        {
            try
            {
                await startSignal;
                cancellationToken.ThrowIfCancellationRequested();
                var result = await handleConnection();
                onConnectionCompleted(result);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || startSignal.IsCanceled)
            {
            }
            catch (Exception exception) when (IsConnectionLocalFailure(exception))
            {
                // NOTE: Clients can time out and close sockets while Unity is processing another main-thread request.
                // Keep those failures connection-local so one abandoned request cannot stop the daemon listener.
            }
            catch (Exception exception)
            {
                daemonLogger.Exception(
                    DaemonLogCategories.Transport,
                    "IPC connection handling failed unexpectedly.",
                    exception);
            }
            finally
            {
                await activeConnection.BeginTransportHandleRelease();
                activeConnection.Complete();
                lock (syncRoot)
                {
                    activeConnections.Remove(activeConnection);
                }
            }
        }

        private static bool IsConnectionLocalFailure (Exception exception)
        {
            return exception is IOException
                or InvalidDataException
                or SocketException
                or ObjectDisposedException
                or InvalidOperationException
                or TimeoutException;
        }

        private static void ObserveFault (Task task)
        {
            _ = task.ContinueWith(
                static completedTask => _ = completedTask.Exception,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }

        private void DisposeTransportHandle (IDisposable transportHandle)
        {
            try
            {
                transportHandle.Dispose();
            }
            catch (Exception exception) when (exception is ObjectDisposedException or IOException or SocketException or InvalidOperationException)
            {
            }
            catch (Exception exception)
            {
                daemonLogger.Exception(
                    DaemonLogCategories.Transport,
                    "IPC transport handle cleanup failed unexpectedly.",
                    exception);
            }
        }

        private sealed class ActiveConnection
        {
            private readonly object releaseSyncRoot = new object();

            private readonly IDisposable transportHandle;

            private readonly Action<IDisposable> releaseTransportHandle;

            private readonly TaskCompletionSource<bool> completionSource =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private Task transportHandleReleaseTask;

            public ActiveConnection (
                IDisposable transportHandle,
                Action<IDisposable> releaseTransportHandle)
            {
                this.transportHandle = transportHandle ?? throw new ArgumentNullException(nameof(transportHandle));
                this.releaseTransportHandle = releaseTransportHandle ?? throw new ArgumentNullException(nameof(releaseTransportHandle));
            }

            public Task Completion => completionSource.Task;

            public Task BeginTransportHandleRelease ()
            {
                lock (releaseSyncRoot)
                {
                    if (transportHandleReleaseTask == null)
                    {
                        transportHandleReleaseTask = Task.Run(() => releaseTransportHandle(transportHandle));
                    }

                    return transportHandleReleaseTask;
                }
            }

            public void Complete ()
            {
                completionSource.TrySetResult(true);
            }
        }
    }
}
