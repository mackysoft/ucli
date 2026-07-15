using System;
using System.Collections.Generic;

#nullable enable

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Provides common filtering helpers shared by daemon-log and Unity-log query engines. </summary>
    internal static class LogReadFilterUtilities
    {
        /// <summary> Determines whether one event passes shared sequence and time-window filters. </summary>
        public static bool PassesSequenceAndTimeWindow (
            long sequence,
            DateTimeOffset timestamp,
            long? afterSequence,
            DateTimeOffset? since,
            DateTimeOffset? until)
        {
            if (afterSequence.HasValue && sequence < afterSequence.Value)
            {
                return false;
            }

            if (since.HasValue
                && !afterSequence.HasValue
                && timestamp < since.Value)
            {
                return false;
            }

            if (until.HasValue
                && timestamp > until.Value)
            {
                return false;
            }

            return true;
        }

        /// <summary> Determines whether one query matches selected text fields. </summary>
        public static bool MatchesQuery (
            string? primaryText,
            string? secondaryText,
            string query,
            bool searchPrimaryText,
            bool searchSecondaryText)
        {
            var primaryHit = searchPrimaryText
                && primaryText != null
                && primaryText.Length > 0
                && primaryText.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
            var secondaryHit = searchSecondaryText
                && secondaryText != null
                && secondaryText.Length > 0
                && secondaryText.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
            return primaryHit || secondaryHit;
        }

        /// <summary> Applies one tail limit to the filtered event list. </summary>
        public static IReadOnlyList<TEvent> ApplyTail<TEvent> (
            List<TEvent> filteredEvents,
            int? tail)
        {
            if (filteredEvents == null)
            {
                throw new ArgumentNullException(nameof(filteredEvents));
            }

            if (!tail.HasValue || filteredEvents.Count <= tail.Value)
            {
                return filteredEvents;
            }

            var tailEvents = new List<TEvent>(tail.Value);
            var startIndex = filteredEvents.Count - tail.Value;
            for (var i = startIndex; i < filteredEvents.Count; i++)
            {
                tailEvents.Add(filteredEvents[i]);
            }

            return tailEvents;
        }
    }
}
