using System;
using MackySoft.Ucli.Infrastructure.Execution;
using UnityEditor;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Monitors the originating CLI process and terminates Unity oneshot when the parent disappears. </summary>
    internal sealed class OneshotParentProcessWatcher : IDisposable
    {
        private const double PollIntervalSeconds = 0.25d;

        private readonly int parentProcessId;

        private double nextPollTime;

        private volatile bool disposed;

        private volatile bool hasRequestedExit;

        private OneshotParentProcessWatcher (int parentProcessId)
        {
            if (parentProcessId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(parentProcessId), parentProcessId, "Parent process id must be greater than zero.");
            }

            this.parentProcessId = parentProcessId;
            nextPollTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnEditorUpdate;
        }

        /// <summary> Gets a value indicating whether the watcher has already requested process exit. </summary>
        internal bool HasRequestedExit => hasRequestedExit;

        /// <summary> Starts monitoring the originating CLI process. </summary>
        /// <param name="parentProcessId"> The originating CLI process identifier. </param>
        /// <returns> The started watcher instance. </returns>
        internal static OneshotParentProcessWatcher Start (int parentProcessId)
        {
            return new OneshotParentProcessWatcher(parentProcessId);
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

        private void OnEditorUpdate ()
        {
            if (disposed)
            {
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            if (now < nextPollTime)
            {
                return;
            }

            nextPollTime = now + PollIntervalSeconds;
            if (ProcessLivenessProbe.IsAlive(parentProcessId))
            {
                return;
            }

            hasRequestedExit = true;
            Dispose();
            EditorApplication.Exit(1);
        }
    }
}