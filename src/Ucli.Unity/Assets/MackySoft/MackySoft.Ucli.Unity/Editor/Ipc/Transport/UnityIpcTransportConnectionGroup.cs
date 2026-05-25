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

        private readonly List<Task> activeConnectionTasks = new List<Task>();

        private readonly List<IDisposable> activeTransportHandles = new List<IDisposable>();

        private bool isReleased;

        /// <summary> Initializes a new instance of the <see cref="UnityIpcTransportConnectionGroup" /> class. </summary>
        /// <param name="daemonLogger"> The daemon logger dependency. </param>
        public UnityIpcTransportConnectionGroup (IDaemonLogger daemonLogger)
        {
            this.daemonLogger = daemonLogger ?? NoOpDaemonLogger.Instance;
        }

        /// <summary> Starts one accepted connection without blocking the listener accept loop. </summary>
        /// <param name="transportHandle"> The disposable transport handle owned by this connection. </param>
        /// <param name="handleConnection"> The connection exchange delegate. </param>
        /// <param name="onConnectionCompleted"> The callback invoked when the connection completes normally. </param>
        /// <param name="cancellationToken"> The listener lifecycle cancellation token. </param>
        /// <exception cref="ArgumentNullException"> Thrown when a dependency is <see langword="null" />. </exception>
        public void Start (
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

            var startSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var connectionTask = RunConnectionAsync(
                startSignal.Task,
                transportHandle,
                handleConnection,
                onConnectionCompleted,
                cancellationToken);
            var shouldStart = false;
            lock (syncRoot)
            {
                if (!isReleased && !cancellationToken.IsCancellationRequested)
                {
                    activeConnectionTasks.Add(connectionTask);
                    activeTransportHandles.Add(transportHandle);
                    shouldStart = true;
                }
            }

            if (!shouldStart)
            {
                DisposeTransportHandle(transportHandle);
                startSignal.TrySetCanceled();
                return;
            }

            _ = ObserveCompletionAsync(connectionTask, transportHandle);
            startSignal.TrySetResult(true);
        }

        /// <summary> Releases all active transport handles to unblock pending connection work. </summary>
        public void Release ()
        {
            IDisposable[] handles;
            lock (syncRoot)
            {
                isReleased = true;
                handles = activeTransportHandles.ToArray();
                activeTransportHandles.Clear();
            }

            foreach (var handle in handles)
            {
                DisposeTransportHandle(handle);
            }
        }

        /// <summary> Waits for active connection tasks to observe release or cancellation. </summary>
        /// <returns> A task that completes when the currently tracked connection tasks have finished. </returns>
        public async Task WaitForCompletionAsync ()
        {
            Task[] tasks;
            lock (syncRoot)
            {
                tasks = activeConnectionTasks.ToArray();
            }

            if (tasks.Length == 0)
            {
                return;
            }

            await Task.WhenAll(tasks);
        }

        private async Task RunConnectionAsync (
            Task startSignal,
            IDisposable transportHandle,
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
                DisposeTransportHandle(transportHandle);
            }
        }

        private async Task ObserveCompletionAsync (
            Task connectionTask,
            IDisposable transportHandle)
        {
            try
            {
                await connectionTask;
            }
            finally
            {
                lock (syncRoot)
                {
                    activeConnectionTasks.Remove(connectionTask);
                    activeTransportHandles.Remove(transportHandle);
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

        private static void DisposeTransportHandle (IDisposable transportHandle)
        {
            try
            {
                transportHandle.Dispose();
            }
            catch (Exception exception) when (exception is ObjectDisposedException or IOException or SocketException or InvalidOperationException)
            {
            }
        }
    }
}
