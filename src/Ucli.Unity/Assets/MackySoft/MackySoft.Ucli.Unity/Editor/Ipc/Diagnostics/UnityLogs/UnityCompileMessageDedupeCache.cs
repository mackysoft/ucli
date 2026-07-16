using System;
using System.Collections.Generic;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Tracks recent compile messages to suppress duplicate runtime capture. </summary>
    internal sealed class UnityCompileMessageDedupeCache
    {
        private static readonly TimeSpan EntryLifetime = TimeSpan.FromSeconds(5);

        private readonly object syncRoot = new object();

        private readonly Dictionary<string, TimeSpan> entries = new Dictionary<string, TimeSpan>(StringComparer.Ordinal);

        private readonly IMonotonicClock monotonicClock;

        /// <summary> Initializes a compile-message cache with one monotonic process-time source. </summary>
        /// <param name="monotonicClock"> The monotonic process-time source. </param>
        public UnityCompileMessageDedupeCache (IMonotonicClock monotonicClock)
        {
            this.monotonicClock = monotonicClock ?? throw new ArgumentNullException(nameof(monotonicClock));
        }

        /// <summary> Registers one compile message as recently emitted. </summary>
        /// <param name="message"> The normalized compile message. </param>
        public void Register (string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            lock (syncRoot)
            {
                var monotonicNow = monotonicClock.Elapsed;
                PruneExpired(monotonicNow);
                entries[message] = monotonicNow;
            }
        }

        /// <summary> Determines whether one runtime message matches a recent compile message. </summary>
        /// <param name="message"> The runtime message. </param>
        /// <returns> <see langword="true" /> when message should be deduplicated; otherwise <see langword="false" />. </returns>
        public bool ContainsRecent (string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            lock (syncRoot)
            {
                var monotonicNow = monotonicClock.Elapsed;
                PruneExpired(monotonicNow);
                if (!entries.ContainsKey(message))
                {
                    return false;
                }

                return true;
            }
        }

        private void PruneExpired (TimeSpan monotonicNow)
        {
            if (entries.Count == 0)
            {
                return;
            }

            List<string> expiredKeys = null;
            foreach (var entry in entries)
            {
                if (monotonicNow - entry.Value >= EntryLifetime)
                {
                    if (expiredKeys == null)
                    {
                        expiredKeys = new List<string>();
                    }

                    expiredKeys.Add(entry.Key);
                }
            }

            if (expiredKeys == null)
            {
                return;
            }

            foreach (var expiredKey in expiredKeys)
            {
                entries.Remove(expiredKey);
            }
        }
    }
}
