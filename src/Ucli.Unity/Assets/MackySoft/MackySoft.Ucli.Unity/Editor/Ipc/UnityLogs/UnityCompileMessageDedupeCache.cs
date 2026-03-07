using System;
using System.Collections.Generic;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Tracks recent compile messages to suppress duplicate runtime capture. </summary>
    internal sealed class UnityCompileMessageDedupeCache
    {
        private static readonly TimeSpan EntryLifetime = TimeSpan.FromSeconds(5);

        private readonly object syncRoot = new object();

        private readonly Dictionary<string, DateTimeOffset> entries = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);

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
                PruneExpired(DateTimeOffset.UtcNow);
                entries[message] = DateTimeOffset.UtcNow + EntryLifetime;
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
                var now = DateTimeOffset.UtcNow;
                PruneExpired(now);
                if (!entries.TryGetValue(message, out var expiresAt))
                {
                    return false;
                }

                return expiresAt >= now;
            }
        }

        private void PruneExpired (DateTimeOffset now)
        {
            if (entries.Count == 0)
            {
                return;
            }

            var expiredKeys = new List<string>();
            foreach (var entry in entries)
            {
                if (entry.Value < now)
                {
                    expiredKeys.Add(entry.Key);
                }
            }

            foreach (var expiredKey in expiredKeys)
            {
                entries.Remove(expiredKey);
            }
        }
    }
}
