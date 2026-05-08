using System;
using System.Threading.Tasks;
using UnityEditor;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Signals when a oneshot host has exceeded its absolute exit deadline. </summary>
    internal sealed class OneshotDeadlineWatcher : IDisposable
    {
        private const double PollIntervalSeconds = 0.25d;

        private readonly DateTimeOffset exitDeadlineUtc;

        private readonly Func<DateTimeOffset> utcNowProvider;

        private readonly Func<double> editorTimeProvider;

        private readonly TaskCompletionSource<bool> deadlineCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        private double nextPollTime;

        private volatile bool disposed;

        private volatile bool hasRequestedExit;

        private OneshotDeadlineWatcher (
            DateTimeOffset exitDeadlineUtc,
            Func<DateTimeOffset> utcNowProvider,
            Func<double> editorTimeProvider,
            bool subscribeToEditorUpdate)
        {
            this.exitDeadlineUtc = exitDeadlineUtc;
            this.utcNowProvider = utcNowProvider ?? throw new ArgumentNullException(nameof(utcNowProvider));
            this.editorTimeProvider = editorTimeProvider ?? throw new ArgumentNullException(nameof(editorTimeProvider));
            nextPollTime = editorTimeProvider();

            if (subscribeToEditorUpdate)
            {
                EditorApplication.update += OnEditorUpdate;
            }
        }

        internal OneshotDeadlineWatcher (
            DateTimeOffset exitDeadlineUtc,
            Func<DateTimeOffset> utcNowProvider,
            Func<double> editorTimeProvider)
            : this(exitDeadlineUtc, utcNowProvider, editorTimeProvider, subscribeToEditorUpdate: false)
        {
        }

        /// <summary> Gets whether the deadline has already requested exit. </summary>
        internal bool HasRequestedExit => hasRequestedExit;

        /// <summary> Starts monitoring one absolute UTC deadline. </summary>
        /// <param name="exitDeadlineUtc"> The deadline after which oneshot must exit itself. </param>
        /// <returns> The started watcher. </returns>
        internal static OneshotDeadlineWatcher Start (DateTimeOffset exitDeadlineUtc)
        {
            return new OneshotDeadlineWatcher(
                exitDeadlineUtc,
                static () => DateTimeOffset.UtcNow,
                static () => EditorApplication.timeSinceStartup,
                subscribeToEditorUpdate: true);
        }

        /// <inheritdoc />
        public void Dispose ()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            EditorApplication.update -= OnEditorUpdate;
        }

        /// <summary> Waits until the configured deadline is reached. </summary>
        /// <returns> A task that completes when the deadline is reached. </returns>
        internal Task WaitAsync ()
        {
            return deadlineCompletionSource.Task;
        }

        internal void OnEditorUpdate ()
        {
            if (disposed)
            {
                return;
            }

            var now = editorTimeProvider();
            if (now < nextPollTime)
            {
                return;
            }

            nextPollTime = now + PollIntervalSeconds;
            if (utcNowProvider() < exitDeadlineUtc)
            {
                return;
            }

            hasRequestedExit = true;
            deadlineCompletionSource.TrySetResult(true);
            Dispose();
        }
    }
}
