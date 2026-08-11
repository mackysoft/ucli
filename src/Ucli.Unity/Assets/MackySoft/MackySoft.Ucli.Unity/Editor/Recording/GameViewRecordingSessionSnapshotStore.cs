using System;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Recording;
using UnityEditor;

namespace MackySoft.Ucli.Unity.Recording
{
    /// <summary> Persists the last terminal recording observation across an Editor domain reload. </summary>
    internal static class GameViewRecordingSessionSnapshotStore
    {
        private const string SessionStateKey =
            "MackySoft.Ucli.Unity.Recording.LastTerminalSnapshot.v1";

        public static void Save (GameViewRecordingSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }
            if (snapshot.StartBinding == null)
            {
                throw new ArgumentException("A persisted recording snapshot requires its admitted start binding.", nameof(snapshot));
            }
            if (snapshot.State is not (GameViewRecordingState.Completed
                or GameViewRecordingState.Failed
                or GameViewRecordingState.Interrupted
                or GameViewRecordingState.Indeterminate))
            {
                throw new ArgumentException("Only a terminal recording snapshot can be persisted.", nameof(snapshot));
            }

            var persisted = new PersistedSnapshot(
                snapshot.RecordingId,
                snapshot.RequestDigest,
                snapshot.EffectiveMaxDurationSeconds,
                snapshot.State,
                snapshot.StopReason,
                snapshot.Failure,
                snapshot.Runtime,
                snapshot.Cleanup,
                snapshot.Target,
                snapshot.Timing,
                snapshot.StartedAtUtc,
                snapshot.StopRequestedAtUtc,
                snapshot.CompletedAtUtc,
                snapshot.UpdatedAtUtc,
                snapshot.Message,
                snapshot.StartBinding);
            SessionState.SetString(
                SessionStateKey,
                JsonSerializer.Serialize(persisted, IpcJsonSerializerOptions.Default));
        }

        public static GameViewRecordingSnapshot TryLoad ()
        {
            var json = SessionState.GetString(SessionStateKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                var persisted = JsonSerializer.Deserialize<PersistedSnapshot>(
                    json,
                    IpcJsonSerializerOptions.Default);
                if (persisted == null
                    || persisted.State is not (GameViewRecordingState.Completed
                        or GameViewRecordingState.Failed
                        or GameViewRecordingState.Interrupted
                        or GameViewRecordingState.Indeterminate)
                    || persisted.StartBinding == null)
                {
                    SessionState.EraseString(SessionStateKey);
                    return null;
                }

                return new GameViewRecordingSnapshot(
                    persisted.RecordingId,
                    persisted.RequestDigest,
                    persisted.EffectiveMaxDurationSeconds,
                    persisted.State,
                    persisted.StopReason,
                    persisted.Failure,
                    persisted.Runtime,
                    persisted.Cleanup,
                    persisted.Target,
                    persisted.Timing,
                    persisted.StartedAtUtc,
                    persisted.StopRequestedAtUtc,
                    persisted.CompletedAtUtc,
                    persisted.UpdatedAtUtc,
                    persisted.Message,
                    persisted.StartBinding);
            }
            catch
            {
                SessionState.EraseString(SessionStateKey);
                return null;
            }
        }

        private sealed record PersistedSnapshot (
            Guid RecordingId,
            Sha256Digest RequestDigest,
            int EffectiveMaxDurationSeconds,
            GameViewRecordingState State,
            GameViewRecordingStopReason StopReason,
            GameViewRecordingFailure Failure,
            GameViewRecordingRuntimeIdentity Runtime,
            GameViewRecordingCleanupRecord Cleanup,
            GameViewRecordingTargetObservation Target,
            GameViewRecordingTimingObservation Timing,
            DateTimeOffset StartedAtUtc,
            DateTimeOffset? StopRequestedAtUtc,
            DateTimeOffset? CompletedAtUtc,
            DateTimeOffset UpdatedAtUtc,
            string Message,
            IpcGameViewRecordingStartBinding StartBinding);
    }
}
