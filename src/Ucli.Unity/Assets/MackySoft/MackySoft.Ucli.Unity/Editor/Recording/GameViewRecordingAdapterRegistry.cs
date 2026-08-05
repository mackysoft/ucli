using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Recording
{
    /// <summary> Holds the single optional GameView recording adapter compiled for this Editor domain. </summary>
    internal sealed class GameViewRecordingAdapterRegistry
    {
        private static readonly TimeSpan StopIntentRetentionAfterDispatchDeadline =
            TimeSpan.FromMinutes(1);

        private readonly object syncRoot = new object();

        private IGameViewRecordingAdapter adapter;

        private readonly Dictionary<Guid, GameViewRecordingStopIntent> stopIntents =
            new Dictionary<Guid, GameViewRecordingStopIntent>();

        public static GameViewRecordingAdapterRegistry Shared { get; } = new GameViewRecordingAdapterRegistry();

        public bool TryRegister (
            IGameViewRecordingAdapter candidate,
            out string errorMessage)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            ValidateMetadata(candidate.Metadata);
            lock (syncRoot)
            {
                if (adapter == null)
                {
                    adapter = candidate;
                    errorMessage = null;
                    return true;
                }

                if (ReferenceEquals(adapter, candidate))
                {
                    errorMessage = null;
                    return true;
                }

                errorMessage =
                    $"GameView recording adapter '{adapter.Metadata.AdapterId}' is already registered.";
                return false;
            }
        }

        public bool TryGet (out IGameViewRecordingAdapter registeredAdapter)
        {
            lock (syncRoot)
            {
                registeredAdapter = adapter;
                return registeredAdapter != null;
            }
        }

        /// <summary>Registers a stop that arrived before the corresponding start crossed into adapter ownership.</summary>
        public bool TryRegisterStopIntent (
            Guid recordingId,
            Sha256Digest requestDigest,
            int effectiveMaxDurationSeconds,
            IpcGameViewRecordingStartBinding startBinding,
            DateTimeOffset dispatchDeadlineUtc,
            DateTimeOffset requestedAtUtc,
            out GameViewRecordingStopIntent intent)
        {
            var candidate = new GameViewRecordingStopIntent(
                recordingId,
                requestDigest,
                effectiveMaxDurationSeconds,
                startBinding,
                dispatchDeadlineUtc,
                requestedAtUtc,
                StartObserved: false);
            lock (syncRoot)
            {
                RemoveExpiredStopIntentsNoLock(requestedAtUtc);
                if (stopIntents.TryGetValue(recordingId, out intent))
                {
                    if (!intent.HasIdentity(candidate))
                    {
                        return false;
                    }

                    return true;
                }

                stopIntents.Add(recordingId, candidate);
                intent = candidate;
                return true;
            }
        }

        /// <summary>Finds the stop intent registered for one recording identifier.</summary>
        public bool TryGetStopIntent (
            Guid recordingId,
            out GameViewRecordingStopIntent intent)
        {
            lock (syncRoot)
            {
                RemoveExpiredStopIntentsNoLock(DateTimeOffset.UtcNow);
                return stopIntents.TryGetValue(recordingId, out intent);
            }
        }

        /// <summary>Removes stop intents whose dispatch window can no longer admit their start.</summary>
        public void RemoveExpiredStopIntents (DateTimeOffset observedAtUtc)
        {
            lock (syncRoot)
            {
                RemoveExpiredStopIntentsNoLock(observedAtUtc);
            }
        }

        /// <summary>Marks a matching delayed start as observed, preventing Recorder admission.</summary>
        public bool TryObserveStopBeforeStart (
            Guid recordingId,
            Sha256Digest requestDigest,
            int effectiveMaxDurationSeconds,
            IpcGameViewRecordingStartBinding startBinding,
            out GameViewRecordingStopIntent intent)
        {
            lock (syncRoot)
            {
                RemoveExpiredStopIntentsNoLock(DateTimeOffset.UtcNow);
                if (!stopIntents.TryGetValue(recordingId, out intent)
                    || !intent.HasIdentity(recordingId, requestDigest, effectiveMaxDurationSeconds, startBinding))
                {
                    return false;
                }

                intent = intent with { StartObserved = true };
                stopIntents[recordingId] = intent;
                return true;
            }
        }

        private void RemoveExpiredStopIntentsNoLock (DateTimeOffset observedAtUtc)
        {
            if (stopIntents.Count == 0)
            {
                return;
            }

            List<Guid> expiredRecordingIds = null;
            foreach (var pair in stopIntents)
            {
                if (observedAtUtc >= pair.Value.DispatchDeadlineUtc
                    && observedAtUtc - pair.Value.DispatchDeadlineUtc
                        >= StopIntentRetentionAfterDispatchDeadline)
                {
                    expiredRecordingIds ??= new List<Guid>();
                    expiredRecordingIds.Add(pair.Key);
                }
            }

            if (expiredRecordingIds == null)
            {
                return;
            }

            foreach (var recordingId in expiredRecordingIds)
            {
                stopIntents.Remove(recordingId);
            }
        }

        public bool TryGetPersistedTerminal (
            Guid recordingId,
            out GameViewRecordingSnapshot snapshot)
        {
            return TryGetPersistedTerminal(
                recordingId,
                refineAdapterUnload: false,
                out snapshot);
        }

        public bool TryGetPersistedTerminalAfterAdapterUnload (
            Guid recordingId,
            out GameViewRecordingSnapshot snapshot)
        {
            return TryGetPersistedTerminal(
                recordingId,
                refineAdapterUnload: true,
                out snapshot);
        }

        private static bool TryGetPersistedTerminal (
            Guid recordingId,
            bool refineAdapterUnload,
            out GameViewRecordingSnapshot snapshot)
        {
            snapshot = GameViewRecordingSessionSnapshotStore.TryLoad();
            if (snapshot?.RecordingId == recordingId)
            {
                if (refineAdapterUnload
                    && snapshot.StopReason == GameViewRecordingStopReason.DomainReload)
                {
                    snapshot = snapshot with
                    {
                        StopReason = GameViewRecordingStopReason.AdapterUnloaded,
                        Message = "GameView recording was interrupted because its optional adapter was unloaded.",
                    };
                    GameViewRecordingSessionSnapshotStore.Save(snapshot);
                }
                return true;
            }

            snapshot = null;
            return false;
        }

        private static void ValidateMetadata (GameViewRecordingAdapterMetadata metadata)
        {
            if (metadata == null)
            {
                throw new ArgumentException("Recording adapter metadata is required.", nameof(metadata));
            }

            if (string.IsNullOrWhiteSpace(metadata.AdapterId)
                || string.IsNullOrWhiteSpace(metadata.AdapterVersion)
                || string.IsNullOrWhiteSpace(metadata.RecorderPackageId)
                || string.IsNullOrWhiteSpace(metadata.RecorderPackageVersionRange)
                || string.IsNullOrWhiteSpace(metadata.UnityVersionRange)
                || metadata.SupportedPlatforms == GameViewRecordingEditorPlatform.None
                || metadata.CaptureProfile == null
                || metadata.Limits == null)
            {
                throw new ArgumentException("Recording adapter metadata is incomplete.", nameof(metadata));
            }

        }
    }

    /// <summary>Identifies a stop request that must win over a delayed matching start.</summary>
    internal sealed record GameViewRecordingStopIntent (
        Guid RecordingId,
        Sha256Digest RequestDigest,
        int EffectiveMaxDurationSeconds,
        IpcGameViewRecordingStartBinding StartBinding,
        DateTimeOffset DispatchDeadlineUtc,
        DateTimeOffset RequestedAtUtc,
        bool StartObserved)
    {
        public bool HasIdentity (GameViewRecordingStopIntent other) =>
            other != null && HasIdentity(
                other.RecordingId,
                other.RequestDigest,
                other.EffectiveMaxDurationSeconds,
                other.StartBinding);

        public bool HasIdentity (
            Guid recordingId,
            Sha256Digest requestDigest,
            int effectiveMaxDurationSeconds,
            IpcGameViewRecordingStartBinding startBinding) =>
            RecordingId == recordingId
            && RequestDigest == requestDigest
            && EffectiveMaxDurationSeconds == effectiveMaxDurationSeconds
            && StartBinding == startBinding;
    }
}
