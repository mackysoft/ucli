using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Publishes a conservative lifecycle snapshot before mutation-lane request execution begins. </summary>
    internal sealed class UnityMutationLifecycleSidecarObserver : IDisposable
    {
        private readonly IUnityMutationRequestExecutionStartSource executionStartSource;

        private readonly IUnityEditorAvailabilityObservationSource availabilityObservationSource;

        private readonly UnityLifecycleSidecarWriter lifecycleSidecarWriter;

        private readonly IDaemonLogger daemonLogger;

        private int disposed;

        /// <summary> Starts observing one mutation-lane and sidecar generation. </summary>
        public UnityMutationLifecycleSidecarObserver (
            IUnityMutationRequestExecutionStartSource executionStartSource,
            IUnityEditorAvailabilityObservationSource availabilityObservationSource,
            UnityLifecycleSidecarWriter lifecycleSidecarWriter,
            IDaemonLogger daemonLogger)
        {
            this.executionStartSource = executionStartSource
                ?? throw new ArgumentNullException(nameof(executionStartSource));
            this.availabilityObservationSource = availabilityObservationSource
                ?? throw new ArgumentNullException(nameof(availabilityObservationSource));
            this.lifecycleSidecarWriter = lifecycleSidecarWriter
                ?? throw new ArgumentNullException(nameof(lifecycleSidecarWriter));
            this.daemonLogger = daemonLogger ?? throw new ArgumentNullException(nameof(daemonLogger));
            executionStartSource.RequestExecutionStarting += OnRequestExecutionStartingAsync;
        }

        /// <inheritdoc />
        public void Dispose ()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                executionStartSource.RequestExecutionStarting -= OnRequestExecutionStartingAsync;
            }
        }

        private async Task OnRequestExecutionStartingAsync (CancellationToken cancellationToken)
        {
            if (Volatile.Read(ref disposed) != 0)
            {
                throw new ObjectDisposedException(nameof(UnityMutationLifecycleSidecarObserver));
            }

            try
            {
                var observedAtUtc = DateTimeOffset.UtcNow;
                var snapshot = availabilityObservationSource
                    .CaptureAvailabilityObservation()
                    .WithObservedAtUtc(observedAtUtc);
                if (!lifecycleSidecarWriter.TryEnqueue(
                        snapshot,
                        out var version))
                {
                    throw new InvalidOperationException(
                        "The GUI lifecycle sidecar writer is not accepting mutation snapshots.");
                }

                // The mutation delegate must not begin while a prior durable Ready snapshot can still be read.
                await lifecycleSidecarWriter
                    .FlushAsync(version, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Lifecycle,
                    $"GUI lifecycle sidecar could not publish mutation execution start. {exception.Message}");
                throw;
            }
        }
    }
}
