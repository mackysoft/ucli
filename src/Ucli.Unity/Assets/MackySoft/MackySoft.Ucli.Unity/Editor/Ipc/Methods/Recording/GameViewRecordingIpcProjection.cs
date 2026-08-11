using System;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Recording;
using MackySoft.Ucli.Unity.Runtime;
using ContractRecordingState = MackySoft.Ucli.Contracts.Recording.GameViewRecordingState;
using ContractStopReason = MackySoft.Ucli.Contracts.Recording.GameViewRecordingStopReason;
using ContractTargetObservation = MackySoft.Ucli.Contracts.Recording.GameViewRecordingTargetObservation;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Projects adapter observations and binding-scoped recovery onto the recording IPC contract. </summary>
    internal sealed class GameViewRecordingIpcProjection
    {
        private readonly UnityLifecycleExecutionHostContext hostContext;

        private readonly IUnityEditorAvailabilityObservationSource availabilityObservationSource;

        public GameViewRecordingIpcProjection (
            UnityLifecycleExecutionHostContext hostContext,
            IUnityEditorAvailabilityObservationSource availabilityObservationSource)
        {
            this.hostContext = hostContext ?? throw new ArgumentNullException(nameof(hostContext));
            this.availabilityObservationSource = availabilityObservationSource
                ?? throw new ArgumentNullException(nameof(availabilityObservationSource));
        }

        public IpcGameViewRecordingStartBinding CaptureCurrentBinding ()
        {
            return new IpcGameViewRecordingStartBinding(
                hostContext.Process,
                GameViewRecordingRuntimeIdentityFactory.Create(hostContext.EditorInstanceId),
                CaptureGeneration());
        }

        public bool IsCurrentBinding (IpcGameViewRecordingStartBinding binding)
        {
            if (binding == null)
            {
                throw new ArgumentNullException(nameof(binding));
            }

            return binding == CaptureCurrentBinding();
        }

        /// <summary>Checks whether the runtime lifetime admitted at start still exists for an owned session.</summary>
        public bool IsCurrentSessionLifetime (IpcGameViewRecordingStartBinding binding)
        {
            if (binding == null)
            {
                throw new ArgumentNullException(nameof(binding));
            }

            var current = CaptureCurrentBinding();
            return binding.Process == current.Process
                && binding.Runtime == current.Runtime
                && binding.Generation.DomainReloadGeneration == current.Generation.DomainReloadGeneration
                && binding.Generation.PlayModeGeneration == current.Generation.PlayModeGeneration;
        }

        public IpcGameViewRecordingSnapshot Project (
            GameViewRecordingSnapshot snapshot,
            IpcGameViewRecordingStartBinding binding)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }
            if (binding == null)
            {
                throw new ArgumentNullException(nameof(binding));
            }
            if (snapshot.StartBinding != binding)
            {
                throw new InvalidOperationException(
                    "The recording adapter snapshot does not match the admitted start binding.");
            }

            var state = MapState(snapshot.State);
            var failure = snapshot.Failure == GameViewRecordingFailure.None
                ? null
                : new IpcError(
                    MapErrorCode(snapshot.Failure),
                    string.IsNullOrWhiteSpace(snapshot.Message)
                        ? "GameView recording failed."
                        : snapshot.Message,
                    InstancePath: null);
            return IpcGameViewRecordingSnapshot.Create(
                snapshot.RecordingId,
                snapshot.RequestDigest,
                state,
                snapshot.StopReason == GameViewRecordingStopReason.None
                    ? null
                    : MapStopReason(snapshot.StopReason),
                failure,
                snapshot.Runtime,
                snapshot.Cleanup,
                snapshot.Target,
                snapshot.Timing,
                snapshot.EffectiveMaxDurationSeconds,
                snapshot.Timing?.EncodedFrameCount,
                snapshot.StartedAtUtc,
                snapshot.StopRequestedAtUtc,
                snapshot.CompletedAtUtc,
                snapshot.UpdatedAtUtc,
                snapshot.StartBinding.Generation,
                CaptureGeneration());
        }

        public IpcGameViewRecordingStopSnapshot ProjectStop (
            GameViewRecordingSnapshot snapshot,
            IpcGameViewRecordingStartBinding binding) =>
            Project(snapshot, binding) switch
            {
                IpcGameViewRecordingStopSnapshot stopSnapshot => stopSnapshot,
                _ => throw new InvalidOperationException(
                    "A stop request can only return a recovery or terminal recording snapshot."),
            };

        public IpcGameViewRecordingTerminalSnapshot ProjectTerminal (
            GameViewRecordingSnapshot snapshot,
            IpcGameViewRecordingStartBinding binding) =>
            Project(snapshot, binding) switch
            {
                IpcGameViewRecordingTerminalSnapshot terminalSnapshot => terminalSnapshot,
                _ => throw new InvalidOperationException(
                    "A persisted terminal recording must project to a terminal snapshot."),
            };

        public bool IsOwnedByBinding (
            GameViewRecordingSnapshot snapshot,
            IpcGameViewRecordingStartBinding binding)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }
            if (binding == null)
            {
                throw new ArgumentNullException(nameof(binding));
            }

            return snapshot.StartBinding == binding;
        }

        /// <summary>Checks every immutable value that identifies one admitted recording execution.</summary>
        public bool HasExecutionIdentity (
            GameViewRecordingSnapshot snapshot,
            Guid recordingId,
            Sha256Digest requestDigest,
            int effectiveMaxDurationSeconds,
            IpcGameViewRecordingStartBinding binding)
        {
            if (requestDigest == null)
            {
                throw new ArgumentNullException(nameof(requestDigest));
            }

            return snapshot != null
                && snapshot.RecordingId == recordingId
                && snapshot.RequestDigest == requestDigest
                && snapshot.EffectiveMaxDurationSeconds == effectiveMaxDurationSeconds
                && snapshot.StartBinding == binding;
        }

        /// <summary>Extracts the adapter-owned snapshot without exposing operation-state booleans to IPC handlers.</summary>
        public static GameViewRecordingSnapshot RequireObserved (
            GameViewRecordingOperationResult result)
        {
            return result is GameViewRecordingObservedOperation observed
                ? observed.Recording
                : throw new InvalidOperationException(
                    "The recording adapter operation was rejected before it produced an observation.");
        }

        /// <summary>Gets an admission rejection that occurred before adapter ownership.</summary>
        public static bool TryGetRejection (
            GameViewRecordingOperationResult result,
            out GameViewRecordingRejectedOperation rejection)
        {
            rejection = result as GameViewRecordingRejectedOperation;
            return rejection != null;
        }

        public IpcGameViewRecordingSnapshot ProjectMissingForStatus (
            Guid recordingId,
            Sha256Digest requestDigest,
            int effectiveMaxDurationSeconds,
            IpcGameViewRecordingStartBinding binding,
            DateTimeOffset dispatchDeadlineUtc,
            IpcGameViewRecordingSnapshot? known,
            bool adapterRegistered)
        {
            if (binding == null)
            {
                throw new ArgumentNullException(nameof(binding));
            }

            var now = GetObservationTime(known);
            var current = CaptureCurrentBinding();
            var acceptanceUnobserved = known == null
                || known.State == ContractRecordingState.Preparing;
            if (acceptanceUnobserved
                && adapterRegistered
                && current == binding
                && now < dispatchDeadlineUtc)
            {
                return CreatePreparingSnapshot(
                    recordingId,
                    requestDigest,
                    effectiveMaxDurationSeconds,
                    binding,
                    current.Generation,
                    known,
                    now);
            }

            ResolveRecoveryFailure(
                binding,
                current,
                dispatchDeadlineUtc,
                now,
                adapterRegistered,
                out var stopReason,
                out var errorCode,
                out var message);
            return CreateIndeterminateSnapshot(
                recordingId,
                requestDigest,
                effectiveMaxDurationSeconds,
                binding,
                current.Generation,
                known,
                stopReason,
                new IpcError(errorCode, message, InstancePath: null),
                now);
        }

        public IpcGameViewRecordingTerminalSnapshot ProjectMissingForStop (
            Guid recordingId,
            Sha256Digest requestDigest,
            int effectiveMaxDurationSeconds,
            IpcGameViewRecordingStartBinding binding,
            DateTimeOffset dispatchDeadlineUtc,
            IpcGameViewRecordingSnapshot? known,
            bool adapterRegistered)
        {
            if (binding == null)
            {
                throw new ArgumentNullException(nameof(binding));
            }

            var now = GetObservationTime(known);
            var current = CaptureCurrentBinding();
            ResolveRecoveryFailure(
                binding,
                current,
                dispatchDeadlineUtc,
                now,
                adapterRegistered,
                out var stopReason,
                out var errorCode,
                out var message);
            return CreateIndeterminateSnapshot(
                recordingId,
                requestDigest,
                effectiveMaxDurationSeconds,
                binding,
                current.Generation,
                known,
                stopReason,
                new IpcError(errorCode, message, InstancePath: null),
                now);
        }

        /// <summary>Projects the recovery interval after a stop won before adapter ownership began.</summary>
        public IpcGameViewRecordingRecoverySnapshot ProjectStopRequestedBeforeStart (
            GameViewRecordingStopIntent intent,
            IpcGameViewRecordingSnapshot? known)
        {
            if (intent == null)
            {
                throw new ArgumentNullException(nameof(intent));
            }

            var now = GetObservationTime(known);
            return new IpcGameViewRecordingRecoverySnapshot(
                intent.RecordingId,
                intent.RequestDigest,
                ContractRecordingState.Finalizing,
                ContractStopReason.Manual,
                failure: null,
                runtime: intent.StartBinding.Runtime,
                target: GetTarget(known),
                effectiveMaxDurationSeconds: intent.EffectiveMaxDurationSeconds,
                encodedFrameCount: known?.EncodedFrameCount,
                startedAtUtc: null,
                stopRequestedAtUtc: intent.RequestedAtUtc,
                updatedAtUtc: now,
                startGeneration: intent.StartBinding.Generation,
                observedGeneration: CaptureGeneration());
        }

        /// <summary>Projects a terminal stop when Recorder ownership was never observed.</summary>
        public IpcGameViewRecordingIndeterminateSnapshot ProjectStopBeforeStartTerminal (
            GameViewRecordingStopIntent intent)
        {
            if (intent == null)
            {
                throw new ArgumentNullException(nameof(intent));
            }

            var now = DateTimeOffset.UtcNow;
            return new IpcGameViewRecordingIndeterminateSnapshot(
                intent.RecordingId,
                intent.RequestDigest,
                ContractRecordingState.Indeterminate,
                ContractStopReason.Manual,
                failure: new IpcError(
                    GameViewRecordingErrorCodes.Interrupted,
                    "The recording stop was observed before Recorder ownership began.",
                    InstancePath: null),
                runtime: intent.StartBinding.Runtime,
                cleanup: null,
                target: null,
                timing: null,
                effectiveMaxDurationSeconds: intent.EffectiveMaxDurationSeconds,
                encodedFrameCount: null,
                startedAtUtc: null,
                stopRequestedAtUtc: intent.RequestedAtUtc,
                completedAtUtc: now,
                updatedAtUtc: now,
                startGeneration: intent.StartBinding.Generation,
                observedGeneration: CaptureGeneration());
        }

        private static IpcGameViewRecordingActiveSnapshot CreatePreparingSnapshot (
            Guid recordingId,
            Sha256Digest requestDigest,
            int effectiveMaxDurationSeconds,
            IpcGameViewRecordingStartBinding binding,
            UnityEditorGenerationSnapshot observedGeneration,
            IpcGameViewRecordingSnapshot? known,
            DateTimeOffset updatedAtUtc)
        {
            return new IpcGameViewRecordingActiveSnapshot(
                recordingId,
                requestDigest,
                ContractRecordingState.Preparing,
                binding.Runtime,
                GetTarget(known),
                effectiveMaxDurationSeconds,
                known?.EncodedFrameCount,
                GetStartedAtUtc(known),
                updatedAtUtc,
                binding.Generation,
                observedGeneration);
        }

        private static IpcGameViewRecordingIndeterminateSnapshot CreateIndeterminateSnapshot (
            Guid recordingId,
            Sha256Digest requestDigest,
            int effectiveMaxDurationSeconds,
            IpcGameViewRecordingStartBinding binding,
            UnityEditorGenerationSnapshot observedGeneration,
            IpcGameViewRecordingSnapshot known,
            ContractStopReason stopReason,
            IpcError failure,
            DateTimeOffset completedAtUtc)
        {
            return new IpcGameViewRecordingIndeterminateSnapshot(
                recordingId,
                requestDigest,
                ContractRecordingState.Indeterminate,
                stopReason,
                failure,
                binding.Runtime,
                cleanup: null,
                GetTarget(known),
                timing: null,
                effectiveMaxDurationSeconds,
                known?.EncodedFrameCount,
                GetStartedAtUtc(known),
                GetStopRequestedAtUtc(known),
                completedAtUtc,
                completedAtUtc,
                binding.Generation,
                observedGeneration);
        }

        private static DateTimeOffset GetObservationTime (
            IpcGameViewRecordingSnapshot known)
        {
            var now = DateTimeOffset.UtcNow;
            return known != null && known.UpdatedAtUtc > now
                ? known.UpdatedAtUtc
                : now;
        }

        private static ContractTargetObservation? GetTarget (
            IpcGameViewRecordingSnapshot? snapshot)
        {
            return snapshot switch
            {
                null => null,
                IpcGameViewRecordingActiveSnapshot active => active.Target,
                IpcGameViewRecordingRecoverySnapshot recovery => recovery.Target,
                IpcGameViewRecordingCompletedSnapshot completed => completed.Target,
                IpcGameViewRecordingFailedSnapshot failed => failed.Target,
                IpcGameViewRecordingIndeterminateSnapshot indeterminate => indeterminate.Target,
                _ => throw new ArgumentOutOfRangeException(nameof(snapshot)),
            };
        }

        private static DateTimeOffset? GetStartedAtUtc (
            IpcGameViewRecordingSnapshot? snapshot)
        {
            return snapshot switch
            {
                null => null,
                IpcGameViewRecordingActiveSnapshot active => active.StartedAtUtc,
                IpcGameViewRecordingRecoverySnapshot recovery => recovery.StartedAtUtc,
                IpcGameViewRecordingCompletedSnapshot completed => completed.StartedAtUtc,
                IpcGameViewRecordingFailedSnapshot failed => failed.StartedAtUtc,
                IpcGameViewRecordingIndeterminateSnapshot indeterminate => indeterminate.StartedAtUtc,
                _ => throw new ArgumentOutOfRangeException(nameof(snapshot)),
            };
        }

        private static DateTimeOffset? GetStopRequestedAtUtc (
            IpcGameViewRecordingSnapshot? snapshot)
        {
            return snapshot switch
            {
                null or IpcGameViewRecordingActiveSnapshot => null,
                IpcGameViewRecordingRecoverySnapshot recovery => recovery.StopRequestedAtUtc,
                IpcGameViewRecordingCompletedSnapshot completed => completed.StopRequestedAtUtc,
                IpcGameViewRecordingFailedSnapshot failed => failed.StopRequestedAtUtc,
                IpcGameViewRecordingIndeterminateSnapshot indeterminate => indeterminate.StopRequestedAtUtc,
                _ => throw new ArgumentOutOfRangeException(nameof(snapshot)),
            };
        }

        private static void ResolveRecoveryFailure (
            IpcGameViewRecordingStartBinding binding,
            IpcGameViewRecordingStartBinding current,
            DateTimeOffset dispatchDeadlineUtc,
            DateTimeOffset observedAtUtc,
            bool adapterRegistered,
            out ContractStopReason stopReason,
            out UcliCode errorCode,
            out string message)
        {
            if (binding.Process != current.Process || binding.Runtime != current.Runtime)
            {
                stopReason = ContractStopReason.UnityExited;
                errorCode = GameViewRecordingErrorCodes.Interrupted;
                message = "The Unity runtime admitted for this recording is no longer the current runtime.";
                return;
            }

            if (binding.Generation.PlayModeGeneration != current.Generation.PlayModeGeneration
                || binding.Generation.DomainReloadGeneration != current.Generation.DomainReloadGeneration)
            {
                stopReason = binding.Generation.PlayModeGeneration
                        != current.Generation.PlayModeGeneration
                    ? ContractStopReason.PlayModeExited
                    : ContractStopReason.DomainReload;
                errorCode = GameViewRecordingErrorCodes.Interrupted;
                message = "The admitted Unity Editor generation ended without a terminal recording observation.";
                return;
            }

            if (!adapterRegistered)
            {
                stopReason = ContractStopReason.AdapterUnloaded;
                errorCode = GameViewRecordingErrorCodes.Interrupted;
                message = "The recording adapter is no longer loaded in the admitted Unity runtime.";
                return;
            }

            if (observedAtUtc >= dispatchDeadlineUtc)
            {
                stopReason = ContractStopReason.InternalFailure;
                errorCode = GameViewRecordingErrorCodes.DispatchDeadlineExceeded;
                message = "The recording start dispatch deadline elapsed without an adapter-owned observation.";
                return;
            }

            stopReason = ContractStopReason.InternalFailure;
            errorCode = GameViewRecordingErrorCodes.Interrupted;
            message = "The admitted recording adapter no longer owns the recording identifier.";
        }

        private UnityEditorGenerationSnapshot CaptureGeneration ()
        {
            return availabilityObservationSource
                .CaptureAvailabilityObservation()
                .State
                .Generations;
        }

        public static UcliCode MapErrorCode (GameViewRecordingFailure failure)
        {
            return failure switch
            {
                GameViewRecordingFailure.InvalidRequest => UcliCoreErrorCodes.InvalidArgument,
                GameViewRecordingFailure.UnsupportedPlatform => GameViewRecordingErrorCodes.EncoderUnsupported,
                GameViewRecordingFailure.RequiresGuiSession => GameViewRecordingErrorCodes.RequiresGuiSession,
                GameViewRecordingFailure.RequiresPlayMode => GameViewRecordingErrorCodes.RequiresPlayMode,
                GameViewRecordingFailure.PlayModeTransitioning => GameViewRecordingErrorCodes.PlayModeTransitioning,
                GameViewRecordingFailure.EditorPaused => GameViewRecordingErrorCodes.EditorPaused,
                GameViewRecordingFailure.GameViewUnavailable => GameViewRecordingErrorCodes.RequestedSizeUnsupported,
                GameViewRecordingFailure.RequestedSizeUnsupported => GameViewRecordingErrorCodes.RequestedSizeUnsupported,
                GameViewRecordingFailure.EncoderUnsupported => GameViewRecordingErrorCodes.EncoderUnsupported,
                GameViewRecordingFailure.IdConflict => GameViewRecordingErrorCodes.IdConflict,
                GameViewRecordingFailure.Conflict => GameViewRecordingErrorCodes.Conflict,
                GameViewRecordingFailure.NotFound => GameViewRecordingErrorCodes.NotFound,
                GameViewRecordingFailure.RecorderStartFailed => GameViewRecordingErrorCodes.AdapterFaulted,
                GameViewRecordingFailure.FinalizationFailed => GameViewRecordingErrorCodes.FinalizationFailed,
                GameViewRecordingFailure.CleanupFailed => GameViewRecordingErrorCodes.CleanupFailed,
                GameViewRecordingFailure.Interrupted => GameViewRecordingErrorCodes.Interrupted,
                _ => UcliCoreErrorCodes.InternalError,
            };
        }

        private static ContractRecordingState MapState (GameViewRecordingState state)
        {
            return state switch
            {
                GameViewRecordingState.Recording => ContractRecordingState.Recording,
                GameViewRecordingState.Finalizing => ContractRecordingState.Finalizing,
                GameViewRecordingState.Completed => ContractRecordingState.Completed,
                GameViewRecordingState.Failed => ContractRecordingState.Failed,
                GameViewRecordingState.Interrupted => ContractRecordingState.Failed,
                GameViewRecordingState.Indeterminate => ContractRecordingState.Indeterminate,
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Recording state is not supported by IPC."),
            };
        }

        private static ContractStopReason MapStopReason (GameViewRecordingStopReason reason)
        {
            return reason switch
            {
                GameViewRecordingStopReason.Manual => ContractStopReason.Manual,
                GameViewRecordingStopReason.MaxDurationReached => ContractStopReason.MaxDurationReached,
                GameViewRecordingStopReason.PlayModeExited => ContractStopReason.PlayModeExited,
                GameViewRecordingStopReason.DomainReload => ContractStopReason.DomainReload,
                GameViewRecordingStopReason.AdapterUnloaded => ContractStopReason.AdapterUnloaded,
                GameViewRecordingStopReason.UnityExited => ContractStopReason.UnityExited,
                GameViewRecordingStopReason.RecorderFailure => ContractStopReason.EncoderFailure,
                GameViewRecordingStopReason.InternalFailure => ContractStopReason.InternalFailure,
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Recording stop reason is not supported by IPC."),
            };
        }
    }
}
