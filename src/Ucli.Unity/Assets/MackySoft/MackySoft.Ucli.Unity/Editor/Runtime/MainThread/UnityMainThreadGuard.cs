using System;
using System.Threading;
using UnityEditor;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Proves that Unity API work is being admitted from the Editor main thread. </summary>
    [InitializeOnLoad]
    internal static class UnityMainThreadGuard
    {
        private static readonly int MainThreadId;

        static UnityMainThreadGuard ()
        {
            MainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary> Captures the current synchronization context after verifying Unity main-thread ownership. </summary>
        /// <param name="operation"> The operation name included in an ownership failure. </param>
        /// <returns> The synchronization context currently installed on Unity's main thread. </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the caller is not on Unity's main thread or no synchronization context is installed there.
        /// </exception>
        public static SynchronizationContext CaptureSynchronizationContext (string operation)
        {
            if (string.IsNullOrWhiteSpace(operation))
            {
                throw new ArgumentException("Operation must not be empty.", nameof(operation));
            }

            if (Thread.CurrentThread.ManagedThreadId != MainThreadId)
            {
                throw new InvalidOperationException(
                    $"{operation} must run on the Unity Editor main thread.");
            }

            return SynchronizationContext.Current
                ?? throw new InvalidOperationException(
                    $"{operation} requires the Unity main-thread synchronization context.");
        }
    }
}
