using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts;
using GameViewRecorderCompatibilityMetadata = MackySoft.Ucli.Contracts.Recording.GameViewRecorderCompatibilityMetadata;
using ContractCaptureProfile = MackySoft.Ucli.Contracts.Recording.GameViewRecordingCaptureProfile;
using ContractCleanupDisposition = MackySoft.Ucli.Contracts.Recording.GameViewRecordingCleanupDisposition;
using ContractCleanupRecord = MackySoft.Ucli.Contracts.Recording.GameViewRecordingCleanupRecord;
using ContractCodec = MackySoft.Ucli.Contracts.Recording.GameViewRecordingCodec;
using ContractContainer = MackySoft.Ucli.Contracts.Recording.GameViewRecordingContainer;
using ContractLimits = MackySoft.Ucli.Contracts.Recording.GameViewRecordingLimits;
using ContractDimensions = MackySoft.Ucli.Contracts.Presentation.PixelDimensions;
using ContractProjectColorSpace = MackySoft.Ucli.Contracts.Projects.UnityProjectColorSpace;
using ContractResourceKind = MackySoft.Ucli.Contracts.Recording.GameViewRecordingResourceKind;
using ContractResourceRelease = MackySoft.Ucli.Contracts.Recording.GameViewRecordingResourceRelease;
using ContractResourceReleaseDisposition = MackySoft.Ucli.Contracts.Recording.GameViewRecordingResourceReleaseDisposition;
using ContractRuntimeIdentity = MackySoft.Ucli.Contracts.Recording.GameViewRecordingRuntimeIdentity;
using ContractStateRestoration = MackySoft.Ucli.Contracts.Recording.GameViewRecordingStateRestoration;
using ContractStateRestorationDisposition = MackySoft.Ucli.Contracts.Recording.GameViewRecordingStateRestorationDisposition;
using ContractStateRestorationKind = MackySoft.Ucli.Contracts.Recording.GameViewRecordingStateRestorationKind;
using ContractTargetObservation = MackySoft.Ucli.Contracts.Recording.GameViewRecordingTargetObservation;
using ContractTimingObservation = MackySoft.Ucli.Contracts.Recording.GameViewRecordingTimingObservation;
using ContractTimingMode = MackySoft.Ucli.Contracts.Recording.GameViewRecordingTimingMode;
using MackySoft.Ucli.Unity.Runtime;
using MackySoft.Ucli.Unity.ScreenshotCapture.GameView;
using MackySoft.Ucli.Unity.ScreenshotCapture.GameView.Resolution;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace MackySoft.Ucli.Unity.Recording.Recorder
{
    /// <summary> Owns one uCLI recording session backed by one Unity Recorder controller. </summary>
    internal sealed class UnityRecorderGameViewRecordingAdapter : IGameViewRecordingAdapter
    {
        private static readonly GameViewRecordingAdapterMetadata AdapterMetadata =
            new GameViewRecordingAdapterMetadata(
                GameViewRecorderCompatibilityMetadata.AdapterId,
                GameViewRecorderCompatibilityMetadata.AdapterVersion,
                GameViewRecorderCompatibilityMetadata.PackageId,
                GameViewRecorderCompatibilityMetadata.RecorderPackageVersionRange,
                "[6000.3.11f1,6000.3.12)",
                GameViewRecordingEditorPlatform.Windows,
                new ContractCaptureProfile(
                    ContractContainer.Mp4,
                    ContractCodec.H264,
                    audio: false,
                    alpha: false,
                    encodingProfile: "coreEncoder",
                    encodingQuality: "high",
                    timingMode: ContractTimingMode.ConstantFrameRateCapture),
                new ContractLimits(
                    minimumWidth: 10,
                    maximumWidth: 4096,
                    minimumHeight: 10,
                    maximumHeight: 4096,
                    dimensionMultiple: 2,
                    minimumFrameRate: 1,
                    maximumFrameRate: 120,
                    defaultMaxDurationSeconds: 120,
                    maximumMaxDurationSeconds: 600));

        private RecordingSession activeSession;

        private GameViewRecordingSnapshot lastSnapshot;

        private bool isFinalizing;

        public UnityRecorderGameViewRecordingAdapter ()
        {
            lastSnapshot = GameViewRecordingSessionSnapshotStore.TryLoad();
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.quitting += OnEditorQuitting;
        }

        public GameViewRecordingAdapterMetadata Metadata => AdapterMetadata;

        public event Action<GameViewRecordingSnapshot> StateChanged;

        public GameViewRecordingRuntimeAdmission GetRuntimeAdmission ()
        {
            if (!TryRequireMainThread(out var mainThreadFailure))
            {
                return new GameViewRecordingRuntimeRejectedAdmission(
                    mainThreadFailure.Failure,
                    mainThreadFailure.Message);
            }

            return TryValidateRuntimeAdmission(
                out var failure,
                out var errorMessage,
                out _)
                ? new GameViewRecordingRuntimeReadyAdmission()
                : new GameViewRecordingRuntimeRejectedAdmission(
                    failure,
                    errorMessage);
        }

        public GameViewRecordingOperationResult Start (GameViewRecordingStartRequest request)
        {
            if (!TryRequireMainThread(out var mainThreadFailure))
            {
                return mainThreadFailure;
            }

            if (request == null)
            {
                return GameViewRecordingOperationResult.Failed(
                    GameViewRecordingFailure.InvalidRequest,
                    "A normalized GameView recording request is required.");
            }

            if (activeSession != null)
            {
                if (activeSession.Request.RecordingId == request.RecordingId)
                {
                    if (activeSession.Request.RequestDigest != request.RequestDigest)
                    {
                        return GameViewRecordingOperationResult.Failed(
                            GameViewRecordingFailure.IdConflict,
                            "The recording identifier is already bound to another normalized request.");
                    }

                    return GameViewRecordingOperationResult.Observed(activeSession.Snapshot);
                }

                return GameViewRecordingOperationResult.Failed(
                    GameViewRecordingFailure.Conflict,
                    "Another uCLI GameView recording owns the Editor recording lease.");
            }

            if (lastSnapshot?.RecordingId == request.RecordingId)
            {
                return lastSnapshot.RequestDigest == request.RequestDigest
                    ? GameViewRecordingOperationResult.Observed(lastSnapshot)
                    : GameViewRecordingOperationResult.Failed(
                        GameViewRecordingFailure.IdConflict,
                        "The recording identifier is already bound to another normalized request.");
            }

            if (!TryValidateRequest(request, out var failure, out var errorMessage))
            {
                return GameViewRecordingOperationResult.Failed(failure, errorMessage);
            }

            if (!TryValidateRuntimeAdmission(
                out failure,
                out errorMessage,
                out var originalSource))
            {
                return GameViewRecordingOperationResult.Failed(failure, errorMessage);
            }

            var presentationAdapter = new UnityGameViewPresentationAdapter();
            var presentationRecovery = new GameViewResolutionPresentationRecovery(
                originalSource,
                presentationAdapter);
            var resolutionAdapter = new UnityGameViewResolutionAdapter(
                new UnityScreenshotResolutionOrphanCleaner());
            if (!resolutionAdapter.TryBegin(
                originalSource.View,
                request.Dimensions.Width,
                request.Dimensions.Height,
                presentationRecovery.TryReserve,
                out var resolutionLease,
                out _,
                out errorMessage))
            {
                presentationRecovery.ReleaseOwnership();
                return GameViewRecordingOperationResult.Failed(
                    GameViewRecordingFailure.RequestedSizeUnsupported,
                    errorMessage);
            }

            UnityRecorderSettings settings = null;
            RecordingSession candidate = null;
            var recorderAccepted = false;
            try
            {
                settings = UnityRecorderSettingsFactory.Create(request);
                var controller = new RecorderController(settings.ControllerSettings);
                candidate = new RecordingSession(
                    request,
                    controller,
                    settings,
                    resolutionLease,
                    presentationRecovery,
                    presentationAdapter,
                    originalSource,
                    Application.runInBackground,
                    Time.captureFramerate,
                    Time.captureDeltaTime,
                    Time.timeScale);

                candidate.ObserveRuntimeIntegrity(requireTarget: false);
                if (!candidate.RuntimeIntegrityAllowsRecording)
                {
                    throw new InvalidOperationException(candidate.RuntimeIntegrityMessage);
                }

                // Revalidate immediately before the Recorder receives the path: the directory can change after admission.
                if (!TryValidateOutputPath(request.StagingOutputPath, out var outputError))
                {
                    throw new InvalidOperationException(outputError);
                }

                controller.PrepareRecording();
                if (!controller.StartRecording())
                {
                    var release = ReleaseSession(candidate, verifyOutput: false);
                    return GameViewRecordingOperationResult.Failed(
                        GameViewRecordingFailure.RecorderStartFailed,
                        AppendReleaseErrors(
                            "Unity Recorder did not accept the GameView recording.",
                            release));
                }

                recorderAccepted = true;
                activeSession = candidate;
                candidate.MarkRecordingStarted();
                candidate.ObserveRuntimeIntegrity(requireTarget: false);
                candidate.Snapshot = CreateAcceptedSnapshot(
                    candidate,
                    "GameView recording is active.");
                Notify(candidate.Snapshot);
                if (!candidate.RuntimeIntegrityAllowsRecording)
                {
                    return CompleteActiveSession(
                        GameViewRecordingStopReason.InternalFailure,
                        interrupted: false);
                }

                return GameViewRecordingOperationResult.Observed(candidate.Snapshot);
            }
            catch (Exception exception)
            {
                if (recorderAccepted)
                {
                    if (candidate.Snapshot == null)
                    {
                        candidate.MarkRecordingStarted();
                        candidate.Snapshot = CreateAcceptedSnapshot(
                            candidate,
                            "GameView recording was accepted before initialization failed.");
                    }

                    candidate.AcceptanceFailureMessage =
                        $"Recording initialization failed after Unity Recorder accepted the session. {FormatException(exception)}";
                    return CompleteActiveSession(
                        GameViewRecordingStopReason.InternalFailure,
                        interrupted: false);
                }

                Exception settingsCleanupException = null;
                if (candidate == null)
                {
                    try
                    {
                        settings?.Dispose();
                    }
                    catch (Exception cleanupException)
                    {
                        settingsCleanupException = cleanupException;
                    }
                }

                var release = candidate != null
                    ? ReleaseSession(candidate, verifyOutput: false)
                    : RestoreResolutionAfterFailedStart(resolutionLease, presentationRecovery);
                if (settingsCleanupException != null)
                {
                    var settingsCleanupError = FormatException(settingsCleanupException);
                    release = release with
                    {
                        CleanupError = release.CleanupError == null
                            ? settingsCleanupError
                            : $"{release.CleanupError} {settingsCleanupError}",
                    };
                }

                return GameViewRecordingOperationResult.Failed(
                    GameViewRecordingFailure.RecorderStartFailed,
                    AppendReleaseErrors(
                        $"Unity Recorder could not start. {FormatException(exception)}",
                        release));
            }
        }

        public GameViewRecordingOperationResult GetStatus (Guid? recordingId)
        {
            if (recordingId.HasValue)
            {
                if (activeSession?.Request.RecordingId == recordingId.Value)
                {
                    return GameViewRecordingOperationResult.Observed(activeSession.Snapshot);
                }

                if (lastSnapshot?.RecordingId == recordingId.Value)
                {
                    return GameViewRecordingOperationResult.Observed(lastSnapshot);
                }

                return GameViewRecordingOperationResult.Failed(
                    GameViewRecordingFailure.NotFound,
                    $"GameView recording '{recordingId.Value:D}' is not known to this Editor domain.");
            }

            return activeSession == null
                ? GameViewRecordingOperationResult.Failed(
                    GameViewRecordingFailure.NotFound,
                    "No active uCLI GameView recording exists in this Editor domain.")
                : GameViewRecordingOperationResult.Observed(activeSession.Snapshot);
        }

        public GameViewRecordingOperationResult Stop (Guid recordingId)
        {
            if (!TryRequireMainThread(out var mainThreadFailure))
            {
                return mainThreadFailure;
            }

            if (recordingId == Guid.Empty)
            {
                return GameViewRecordingOperationResult.Failed(
                    GameViewRecordingFailure.InvalidRequest,
                    "recordingId must be a non-zero UUID.");
            }

            if (activeSession?.Request.RecordingId == recordingId)
            {
                return CompleteActiveSession(
                    GameViewRecordingStopReason.Manual,
                    interrupted: false);
            }

            if (lastSnapshot?.RecordingId == recordingId)
            {
                return GameViewRecordingOperationResult.Observed(lastSnapshot);
            }

            return GameViewRecordingOperationResult.Failed(
                GameViewRecordingFailure.NotFound,
                $"GameView recording '{recordingId:D}' is not known to this Editor domain.");
        }

        private static bool TryValidateRequest (
            GameViewRecordingStartRequest request,
            out GameViewRecordingFailure failure,
            out string errorMessage)
        {
            if (request.RecordingId == Guid.Empty)
            {
                return FailValidation(
                    GameViewRecordingFailure.InvalidRequest,
                    "recordingId must be a non-zero UUID.",
                    out failure,
                    out errorMessage);
            }

            if (request.RequestDigest == null)
            {
                return FailValidation(
                    GameViewRecordingFailure.InvalidRequest,
                    "The normalized recording request digest is required.",
                    out failure,
                    out errorMessage);
            }

            if (request.Dimensions.Width < 10
                || request.Dimensions.Height < 10
                || (request.Dimensions.Width & 1) != 0
                || (request.Dimensions.Height & 1) != 0
                || request.Dimensions.Width > AdapterMetadata.Limits.MaximumWidth
                || request.Dimensions.Height > AdapterMetadata.Limits.MaximumHeight)
            {
                return FailValidation(
                    GameViewRecordingFailure.RequestedSizeUnsupported,
                    "GameView recording dimensions must be even and within the adapter limits.",
                    out failure,
                    out errorMessage);
            }

            if (request.FrameRate <= 0
                || request.FrameRate > AdapterMetadata.Limits.MaximumFrameRate
                || request.MaximumDuration <= TimeSpan.Zero
                || request.MaximumDuration.TotalSeconds
                    > AdapterMetadata.Limits.MaximumMaxDurationSeconds)
            {
                return FailValidation(
                    GameViewRecordingFailure.InvalidRequest,
                    "Frame rate or maximum duration is outside the adapter limits.",
                    out failure,
                    out errorMessage);
            }

            if (!TryValidateOutputPath(request.StagingOutputPath, out errorMessage))
            {
                failure = GameViewRecordingFailure.InvalidRequest;
                return false;
            }

            failure = GameViewRecordingFailure.None;
            errorMessage = null;
            return true;
        }

        private static GameViewRecordingSnapshot CreateAcceptedSnapshot (
            RecordingSession session,
            string message)
        {
            return new GameViewRecordingSnapshot(
                session.Request.RecordingId,
                session.Request.RequestDigest,
                (int)session.Request.MaximumDuration.TotalSeconds,
                GameViewRecordingState.Recording,
                GameViewRecordingStopReason.None,
                GameViewRecordingFailure.None,
                Runtime: session.Runtime,
                Cleanup: null,
                Target: session.Target,
                Timing: null,
                StartBinding: session.Request.StartBinding,
                StartedAtUtc: session.StartedAtUtc,
                StopRequestedAtUtc: null,
                CompletedAtUtc: null,
                UpdatedAtUtc: session.StartedAtUtc,
                Message: message);
        }

        private static bool TryValidateRuntimeAdmission (
            out GameViewRecordingFailure failure,
            out string errorMessage,
            out GameViewPresentationSource source)
        {
            source = null;
            if (!TryGetCurrentPlatform(out var platform)
                || (AdapterMetadata.SupportedPlatforms & platform) == 0)
            {
                return FailValidation(
                    GameViewRecordingFailure.UnsupportedPlatform,
                    "The fixed H.264 recording profile is unavailable on this Editor platform.",
                    out failure,
                    out errorMessage);
            }

            if (!string.Equals(Application.unityVersion, "6000.3.11f1", StringComparison.Ordinal))
            {
                return FailValidation(
                    GameViewRecordingFailure.UnsupportedPlatform,
                    $"Unity {Application.unityVersion} is outside the verified adapter range {AdapterMetadata.UnityVersionRange}.",
                    out failure,
                    out errorMessage);
            }

            var encoderSettings = new CoreEncoderSettings
            {
                Codec = CoreEncoderSettings.OutputCodec.MP4,
            };
            if (!encoderSettings.SupportsCurrentPlatform())
            {
                return FailValidation(
                    GameViewRecordingFailure.EncoderUnsupported,
                    "Unity Media Encoder does not report H.264 MP4 support on this Editor platform.",
                    out failure,
                    out errorMessage);
            }

            if (Application.isBatchMode)
            {
                return FailValidation(
                    GameViewRecordingFailure.RequiresGuiSession,
                    "GameView recording requires a GUI Editor session.",
                    out failure,
                    out errorMessage);
            }

            if (!EditorApplication.isPlaying)
            {
                var isTransitioning = EditorApplication.isPlayingOrWillChangePlaymode;
                return FailValidation(
                    isTransitioning
                        ? GameViewRecordingFailure.PlayModeTransitioning
                        : GameViewRecordingFailure.RequiresPlayMode,
                    isTransitioning
                        ? "GameView recording cannot start while Play Mode is transitioning."
                        : "GameView recording requires active Play Mode.",
                    out failure,
                    out errorMessage);
            }

            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return FailValidation(
                    GameViewRecordingFailure.PlayModeTransitioning,
                    "GameView recording cannot start while Play Mode is exiting.",
                    out failure,
                    out errorMessage);
            }

            if (EditorApplication.isPaused)
            {
                return FailValidation(
                    GameViewRecordingFailure.EditorPaused,
                    "GameView recording cannot start while the Editor is paused.",
                    out failure,
                    out errorMessage);
            }

            if (Time.captureFramerate != 0 || !Mathf.Approximately(Time.captureDeltaTime, 0f))
            {
                return FailValidation(
                    GameViewRecordingFailure.Conflict,
                    "Another fixed-frame-rate capture already owns Unity's capture timing state.",
                    out failure,
                    out errorMessage);
            }

            var presentationAdapter = new UnityGameViewPresentationAdapter();
            if (!presentationAdapter.TryGetSource(out source, out errorMessage))
            {
                failure = GameViewRecordingFailure.GameViewUnavailable;
                return false;
            }

            failure = GameViewRecordingFailure.None;
            errorMessage = null;
            return true;
        }

        private static bool TryValidateOutputPath (
            AbsolutePath outputPath,
            out string errorMessage)
        {
            if (outputPath == null)
            {
                errorMessage = "The provider-private staging output path is required.";
                return false;
            }

            if (!string.Equals(
                Path.GetExtension(outputPath.Value),
                ".mp4",
                StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "The provider-private staging output must use the .mp4 extension.";
                return false;
            }

            if (!outputPath.TryGetParent(out var directory)
                || !Directory.Exists(directory.Value))
            {
                errorMessage = "The provider-private staging directory does not exist.";
                return false;
            }

            try
            {
                GameViewRecordingStagingOutputBoundary.EnsureSecureDirectory(directory);
            }
            catch (Exception exception)
            {
                errorMessage = $"The provider-private staging directory is not safe. {FormatException(exception)}";
                return false;
            }

            if (File.Exists(outputPath.Value) || Directory.Exists(outputPath.Value))
            {
                errorMessage = "The provider-private staging output already exists.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        private static bool TryGetCurrentPlatform (out GameViewRecordingEditorPlatform platform)
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                    platform = GameViewRecordingEditorPlatform.Windows;
                    return true;
                case RuntimePlatform.OSXEditor:
                    platform = GameViewRecordingEditorPlatform.MacOS;
                    return true;
                default:
                    platform = GameViewRecordingEditorPlatform.None;
                    return false;
            }
        }

        private static bool FailValidation (
            GameViewRecordingFailure value,
            string message,
            out GameViewRecordingFailure failure,
            out string errorMessage)
        {
            failure = value;
            errorMessage = message;
            return false;
        }

        private GameViewRecordingOperationResult CompleteActiveSession (
            GameViewRecordingStopReason stopReason,
            bool interrupted)
        {
            if (activeSession == null)
            {
                return GameViewRecordingOperationResult.Failed(
                    GameViewRecordingFailure.NotFound,
                    "No active uCLI GameView recording exists in this Editor domain.");
            }

            var session = activeSession;
            if (session.ReleaseOutcome != null)
            {
                if (!interrupted)
                {
                    return GameViewRecordingOperationResult.Observed(session.Snapshot);
                }

                session.Interrupted = true;
                session.StopReason = stopReason;
                session.MarkPostStopObservationUnavailable();
                session.PresentationRecovery.ReleaseOwnership();
                return FinalizeActiveSession(session, changed: true);
            }

            if (isFinalizing)
            {
                return GameViewRecordingOperationResult.Observed(session.Snapshot);
            }

            isFinalizing = true;
            var stopRequestedAtUtc = DateTimeOffset.UtcNow;
            session.ObserveRuntimeIntegrity(requireTarget: true);
            session.Interrupted = interrupted;
            session.StopReason = stopReason;
            session.MonotonicStopRequestedTimestamp = Stopwatch.GetTimestamp();
            session.Snapshot = session.Snapshot with
            {
                State = GameViewRecordingState.Finalizing,
                StopReason = stopReason,
                StopRequestedAtUtc = stopRequestedAtUtc,
                UpdatedAtUtc = stopRequestedAtUtc,
                Message = "GameView recording is finalizing.",
            };
            Notify(session.Snapshot);

            try
            {
                session.ReleaseOutcome = ReleaseSession(session, verifyOutput: true);
                session.BeginPostStopObservation();
                if (interrupted)
                {
                    session.MarkPostStopObservationUnavailable();
                    if (session.PresentationRecovery.IsPending)
                    {
                        session.PresentationRecovery.ReleaseOwnership();
                    }
                }

                return session.PostStopObservationPending
                    || session.PresentationRecovery.IsPending
                    ? GameViewRecordingOperationResult.Observed(session.Snapshot)
                    : FinalizeActiveSession(session, changed: true);
            }
            finally
            {
                isFinalizing = false;
            }
        }

        private GameViewRecordingOperationResult FinalizeActiveSession (
            RecordingSession session,
            bool changed)
        {
            if (!ReferenceEquals(activeSession, session))
            {
                return GameViewRecordingOperationResult.Failed(
                    GameViewRecordingFailure.InternalFailure,
                    "The finalizing recording no longer owns the Editor recording exclusion.");
            }

            var completedAtUtc = DateTimeOffset.UtcNow;
            var completionTimestamp = Stopwatch.GetTimestamp();
            activeSession = null;
            session.ExclusionReleased = true;

            var cleanup = CreateCleanupRecord(session, completedAtUtc);
            var timing = session.CreateTimingObservation(completionTimestamp);
            var target = session.Target;

            GameViewRecordingState state;
            GameViewRecordingFailure failure;
            string message;
            if (session.HasUnconfirmedRuntimeIntegrity
                || target == null
                || HasUnconfirmedRuntimeCleanup(cleanup))
            {
                state = GameViewRecordingState.Indeterminate;
                failure = session.HasUnconfirmedRuntimeIntegrity || target == null
                    ? GameViewRecordingFailure.InternalFailure
                    : GameViewRecordingFailure.CleanupFailed;
                message = session.HasUnconfirmedRuntimeIntegrity
                    ? "GameView recording runtime integrity could not be confirmed."
                    : target == null
                        ? "The recording target was not observed at the requested GameView resolution."
                        : "GameView recording cleanup could not be fully confirmed.";
            }
            else if (session.HasFailedRuntimeIntegrity)
            {
                state = GameViewRecordingState.Failed;
                failure = GameViewRecordingFailure.InternalFailure;
                message = "GameView recording runtime integrity failed.";
            }
            else if (session.Interrupted)
            {
                state = GameViewRecordingState.Interrupted;
                failure = GameViewRecordingFailure.Interrupted;
                message = "GameView recording was interrupted by the Editor lifecycle.";
            }
            else if (session.AcceptanceFailureMessage != null)
            {
                state = GameViewRecordingState.Failed;
                failure = GameViewRecordingFailure.InternalFailure;
                message = "GameView recording initialization failed after Recorder accepted the session.";
            }
            else if (session.ReleaseOutcome.FinalizationError != null)
            {
                state = GameViewRecordingState.Failed;
                failure = GameViewRecordingFailure.FinalizationFailed;
                message = "GameView recording finalization failed.";
            }
            else if (HasFailedRuntimeCleanup(cleanup))
            {
                state = GameViewRecordingState.Failed;
                failure = GameViewRecordingFailure.CleanupFailed;
                message = "GameView recording cleanup failed.";
            }
            else
            {
                state = GameViewRecordingState.Completed;
                failure = GameViewRecordingFailure.None;
                message = "GameView recording stopped and its runtime-owned Editor state was restored.";
            }

            message = AppendReleaseErrors(message, session.ReleaseOutcome);
            if (session.PresentationRecovery.TerminalErrorMessage != null)
            {
                message += $" Presentation: {session.PresentationRecovery.TerminalErrorMessage}";
            }
            if (session.AcceptanceFailureMessage != null)
            {
                message += $" Start: {session.AcceptanceFailureMessage}";
            }
            if (session.RuntimeIntegrityMessage != null)
            {
                message += $" Integrity: {session.RuntimeIntegrityMessage}";
            }

            var terminal = new GameViewRecordingSnapshot(
                session.Request.RecordingId,
                session.Request.RequestDigest,
                (int)session.Request.MaximumDuration.TotalSeconds,
                state,
                session.StopReason,
                failure,
                Runtime: session.Runtime,
                Cleanup: cleanup,
                Target: target,
                Timing: timing,
                StartBinding: session.Request.StartBinding,
                StartedAtUtc: session.StartedAtUtc,
                StopRequestedAtUtc: session.Snapshot.StopRequestedAtUtc,
                CompletedAtUtc: completedAtUtc,
                UpdatedAtUtc: completedAtUtc,
                Message: message);

            session.Snapshot = terminal;
            GameViewRecordingSessionSnapshotStore.Save(terminal);
            lastSnapshot = terminal;
            Notify(terminal);
            return GameViewRecordingOperationResult.Observed(terminal);
        }

        private static ReleaseOutcome ReleaseSession (
            RecordingSession session,
            bool verifyOutput)
        {
            var finalizationErrors = new List<string>();
            var cleanupErrors = new List<string>();
            var controllerStopped = false;
            var settingsDisposed = false;

            try
            {
                session.Controller.StopRecording();
                controllerStopped = true;
            }
            catch (Exception exception)
            {
                finalizationErrors.Add(FormatException(exception));
            }

            if (verifyOutput)
            {
                try
                {
                    var output = new FileInfo(session.Settings.OutputPath.Value);
                    if (!output.Exists || output.Length <= 0)
                    {
                        finalizationErrors.Add(
                            "Unity Recorder did not produce a non-empty MP4 staging file.");
                    }
                }
                catch (Exception exception)
                {
                    finalizationErrors.Add(FormatException(exception));
                }
            }

            try
            {
                session.Settings.Dispose();
                settingsDisposed = true;
            }
            catch (Exception exception)
            {
                cleanupErrors.Add(FormatException(exception));
            }

            var timeState = TryRestoreTimeState(session, cleanupErrors);
            var resolution = TryRestoreResolution(session);

            return new ReleaseOutcome(
                JoinErrors(finalizationErrors),
                JoinErrors(cleanupErrors),
                controllerStopped,
                settingsDisposed,
                timeState,
                resolution);
        }

        private static TimeStateReleaseOutcome TryRestoreTimeState (
            RecordingSession session,
            ICollection<string> cleanupErrors)
        {
            var before = FormatTimeState(
                session.OriginalRunInBackground,
                session.OriginalCaptureFramerate,
                session.OriginalCaptureDeltaTime,
                session.OriginalTimeScale);
            var errors = new List<string>();
            if (session.CanRestoreRunInBackground())
            {
                session.RunInBackgroundRestoreAttempted = true;
                RestoreTimeStateValue(
                    "Run In Background",
                    () => Application.runInBackground,
                    value => Application.runInBackground = value,
                    session.OriginalRunInBackground,
                    static (left, right) => left == right,
                    errors);
            }
            if (session.CanRestoreCaptureFramerate())
            {
                session.CaptureFramerateRestoreAttempted = true;
                RestoreTimeStateValue(
                    "captureFramerate",
                    () => Time.captureFramerate,
                    value => Time.captureFramerate = value,
                    session.OriginalCaptureFramerate,
                    static (left, right) => left == right,
                    errors);
            }
            if (session.CanRestoreCaptureDeltaTime())
            {
                session.CaptureDeltaTimeRestoreAttempted = true;
                RestoreTimeStateValue(
                    "captureDeltaTime",
                    () => Time.captureDeltaTime,
                    value => Time.captureDeltaTime = value,
                    session.OriginalCaptureDeltaTime,
                    static (left, right) => Mathf.Approximately(left, right),
                    errors);
            }
            if (session.CanRestoreTimeScale())
            {
                session.TimeScaleRestoreAttempted = true;
                RestoreTimeStateValue(
                    "timeScale",
                    () => Time.timeScale,
                    value => Time.timeScale = value,
                    session.OriginalTimeScale,
                    static (left, right) => Mathf.Approximately(left, right),
                    errors);
            }

            foreach (var error in errors)
            {
                cleanupErrors.Add(error);
            }

            var after = FormatTimeState(
                Application.runInBackground,
                Time.captureFramerate,
                Time.captureDeltaTime,
                Time.timeScale);
            return new TimeStateReleaseOutcome(
                before,
                after,
                Changed: session.HasTimeStateRestoreAttempt || errors.Count > 0,
                RestoreAttempted: session.HasTimeStateRestoreAttempt,
                errors.Count == 0,
                Confirmed: false,
                JoinErrors(errors));
        }

        private static void RestoreTimeStateValue<T> (
            string name,
            Func<T> read,
            Action<T> write,
            T expected,
            Func<T, T, bool> equals,
            ICollection<string> errors)
        {
            try
            {
                if (!equals(read(), expected))
                {
                    write(expected);
                }

                if (!equals(read(), expected))
                {
                    errors.Add($"{name} could not be restored.");
                }
            }
            catch (Exception exception)
            {
                errors.Add($"{name}: {FormatException(exception)}");
            }
        }

        private static void CompletePostStopObservation (RecordingSession session)
        {
            if (!session.PostStopObservationPending || session.ReleaseOutcome == null)
            {
                return;
            }

            var release = session.ReleaseOutcome;
            var timeState = release.TimeState;
            try
            {
                var postStopErrors = new List<string>();
                if (session.RunInBackgroundRestoreAttempted
                    && Application.runInBackground != session.OriginalRunInBackground)
                {
                    postStopErrors.Add(
                        "Run In Background changed before the post-stop observation.");
                }
                if ((session.CaptureFramerateRestoreAttempted
                        && Time.captureFramerate != session.OriginalCaptureFramerate)
                    || (session.CaptureDeltaTimeRestoreAttempted
                        && !Mathf.Approximately(
                            Time.captureDeltaTime,
                            session.OriginalCaptureDeltaTime)))
                {
                    postStopErrors.Add(
                        "Unity capture timing changed before the post-stop observation.");
                }
                if (session.TimeScaleRestoreAttempted
                    && !Mathf.Approximately(Time.timeScale, session.OriginalTimeScale))
                {
                    postStopErrors.Add(
                        "Unity time scale changed before the post-stop observation.");
                }

                var changed = timeState.Changed;
                var timeStateErrors = new[]
                {
                    timeState.Error,
                    JoinErrors(postStopErrors),
                }.Where(value => value != null).ToArray();
                var updatedTimeState = timeState with
                {
                    AfterValue = FormatTimeState(
                        Application.runInBackground,
                        Time.captureFramerate,
                        Time.captureDeltaTime,
                        Time.timeScale),
                    Changed = changed,
                    RestoreAttempted = timeState.RestoreAttempted,
                    Succeeded = timeState.Succeeded && postStopErrors.Count == 0,
                    Confirmed = true,
                    Error = JoinErrors(timeStateErrors),
                };
                session.ReleaseOutcome = release with
                {
                    CleanupError = JoinErrors(new[]
                    {
                        release.CleanupError,
                        JoinErrors(postStopErrors),
                    }.Where(value => value != null).ToArray()),
                    TimeState = updatedTimeState,
                };
                session.MarkPostStopObservationCompleted();
            }
            catch (Exception exception)
            {
                var observationError = FormatException(exception);
                session.ReleaseOutcome = release with
                {
                    CleanupError = JoinErrors(new[]
                    {
                        release.CleanupError,
                        observationError,
                    }.Where(value => value != null).ToArray()),
                    TimeState = timeState with
                    {
                        Confirmed = false,
                        Error = observationError,
                    },
                };
                session.MarkPostStopObservationCompleted();
            }
        }

        private static ResolutionReleaseOutcome TryRestoreResolution (
            IResolutionSession session)
        {
            string lastError = null;
            var outcome = GameViewResolutionLease.RestoreOutcome.Retryable;
            for (var attempt = 0; attempt < 3 && session.ResolutionLease.CanRetryRestore; attempt++)
            {
                outcome = session.ResolutionLease.TryRestore(
                    session.PresentationRecovery.TryReserve,
                    out lastError);
                if (outcome != GameViewResolutionLease.RestoreOutcome.Retryable)
                {
                    break;
                }
            }

            switch (outcome)
            {
                case GameViewResolutionLease.RestoreOutcome.RestoredOriginal:
                    if (!session.ResolutionLease.TryValidateRestoredState(out lastError))
                    {
                        session.PresentationRecovery.ReleaseOwnership();
                        return new ResolutionReleaseOutcome(
                            ResolutionReleaseDisposition.Failed,
                            lastError);
                    }

                    session.PresentationRecovery.TryRequestRepaint(out _);
                    if (!session.PresentationRecovery.TrySchedule(out lastError))
                    {
                        session.PresentationRecovery.ReleaseOwnership();
                        return new ResolutionReleaseOutcome(
                            ResolutionReleaseDisposition.Failed,
                            lastError);
                    }

                    return new ResolutionReleaseOutcome(
                        ResolutionReleaseDisposition.RestoredAwaitingPresentation,
                        Error: null);
                case GameViewResolutionLease.RestoreOutcome.UserSelectionPreserved:
                    session.PresentationRecovery.ReleaseOwnership();
                    return new ResolutionReleaseOutcome(
                        ResolutionReleaseDisposition.UserSelectionPreserved,
                        "The user selected another GameView resolution before cleanup.");
                case GameViewResolutionLease.RestoreOutcome.OwnershipHandedOff:
                    session.PresentationRecovery.ReleaseOwnership();
                    return new ResolutionReleaseOutcome(
                        ResolutionReleaseDisposition.Unconfirmed,
                        lastError ?? "GameView resolution cleanup was handed to orphan recovery.");
                default:
                    session.ResolutionLease.ScheduleDeferredRecovery(
                        session.PresentationRecovery.TrySchedule);
                    return new ResolutionReleaseOutcome(
                        ResolutionReleaseDisposition.Unconfirmed,
                        lastError ?? "GameView resolution cleanup remains pending.");
            }
        }

        private static ReleaseOutcome RestoreResolutionAfterFailedStart (
            GameViewResolutionLease lease,
            GameViewResolutionPresentationRecovery recovery)
        {
            var shell = new ResolutionOnlySession(lease, recovery);
            var resolution = TryRestoreResolution(shell);
            return new ReleaseOutcome(
                FinalizationError: null,
                CleanupError: null,
                ControllerStopped: false,
                SettingsDisposed: false,
                TimeState: new TimeStateReleaseOutcome(
                    BeforeValue: null,
                    AfterValue: null,
                    Changed: false,
                    RestoreAttempted: false,
                    Succeeded: false,
                    Confirmed: false,
                    Error: null),
                Resolution: resolution);
        }

        private static ContractCleanupRecord CreateCleanupRecord (
            RecordingSession session,
            DateTimeOffset completedAtUtc)
        {
            var restorations = CreateStateRestorations(session);
            var releases = CreateResourceReleases(session);
            var disposition = ResolveCleanupDisposition(restorations, releases);
            return new ContractCleanupRecord(
                ContractCleanupRecord.CurrentSchemaVersion,
                session.Request.RecordingId,
                session.Request.RequestDigest,
                restorations,
                releases,
                disposition,
                completedAtUtc);
        }

        private static IReadOnlyList<ContractStateRestoration> CreateStateRestorations (
            RecordingSession session)
        {
            var cleanupCode = GameViewRecordingErrorCodes.CleanupFailed;
            var originalId = session.OriginalSessionId;
            var beforeView = $"playModeView:{originalId}";
            var beforeGameView = $"gameView:{originalId}";
            var beforeDisplay = session.OriginalSource.TargetDisplay.ToString(CultureInfo.InvariantCulture);
            var currentObserved = session.PresentationAdapter.TryGetSource(
                out var currentSource,
                out _);
            var currentId = currentObserved && currentSource.View != null
                ? UnityObjectSessionId.Create(currentSource.View).ToString()
                : null;
            var sameView = currentObserved && currentSource.View == session.OriginalSource.View;

            var restorations = new List<ContractStateRestoration>(capacity: 6)
            {
                CreateUnchangedOrUnconfirmedState(
                    ContractStateRestorationKind.PlayModeView,
                    beforeView,
                    currentId == null ? null : $"playModeView:{currentId}",
                    sameView,
                    cleanupCode),
                CreateUnchangedOrUnconfirmedState(
                    ContractStateRestorationKind.GameView,
                    beforeGameView,
                    currentId == null ? null : $"gameView:{currentId}",
                    sameView,
                    cleanupCode),
                CreateUnchangedOrUnconfirmedState(
                    ContractStateRestorationKind.Display,
                    beforeDisplay,
                    currentObserved
                        ? currentSource.TargetDisplay.ToString(CultureInfo.InvariantCulture)
                        : null,
                    currentObserved
                        && currentSource.TargetDisplay == session.OriginalSource.TargetDisplay,
                    cleanupCode),
            };

            var resolutionBefore = FormatResolution(
                session.OriginalSource.Width,
                session.OriginalSource.Height);
            switch (session.ReleaseOutcome.Resolution.Disposition)
            {
                case ResolutionReleaseDisposition.RestoredAwaitingPresentation:
                    restorations.Add(new ContractStateRestoration(
                        ContractStateRestorationKind.ResolutionSelection,
                        resolutionBefore,
                        resolutionBefore,
                        changed: true,
                        restoreAttempted: true,
                        ContractStateRestorationDisposition.Restored,
                        reasonCode: null));
                    break;
                case ResolutionReleaseDisposition.Failed:
                    restorations.Add(new ContractStateRestoration(
                        ContractStateRestorationKind.ResolutionSelection,
                        resolutionBefore,
                        currentObserved
                            ? FormatResolution(currentSource.Width, currentSource.Height)
                            : null,
                        changed: true,
                        restoreAttempted: true,
                        ContractStateRestorationDisposition.Failed,
                        cleanupCode));
                    break;
                default:
                    restorations.Add(new ContractStateRestoration(
                        ContractStateRestorationKind.ResolutionSelection,
                        resolutionBefore,
                        currentObserved
                            ? FormatResolution(currentSource.Width, currentSource.Height)
                            : null,
                        changed: true,
                        restoreAttempted: true,
                        ContractStateRestorationDisposition.Unconfirmed,
                        cleanupCode));
                    break;
            }

            var presentationRestored = session.ReleaseOutcome.Resolution.Disposition
                    == ResolutionReleaseDisposition.RestoredAwaitingPresentation
                && session.PresentationRecovery.TerminalObservation
                    == GameViewResolutionPresentationRecovery.Observation.RestoredResolutionPresented;
            var presentationFailed = session.ReleaseOutcome.Resolution.Disposition
                == ResolutionReleaseDisposition.Failed;
            restorations.Add(session.PresentationChanged
                ? new ContractStateRestoration(
                    ContractStateRestorationKind.Presentation,
                    resolutionBefore,
                    currentObserved
                        ? FormatResolution(currentSource.Width, currentSource.Height)
                        : null,
                    changed: true,
                    restoreAttempted: true,
                    presentationRestored
                        ? ContractStateRestorationDisposition.Restored
                        : presentationFailed
                            ? ContractStateRestorationDisposition.Failed
                            : ContractStateRestorationDisposition.Unconfirmed,
                    presentationRestored ? null : cleanupCode)
                : new ContractStateRestoration(
                    ContractStateRestorationKind.Presentation,
                    resolutionBefore,
                    resolutionBefore,
                    changed: false,
                    restoreAttempted: false,
                    ContractStateRestorationDisposition.Unchanged,
                    reasonCode: null));

            var timeState = session.ReleaseOutcome.TimeState;
            restorations.Add(new ContractStateRestoration(
                ContractStateRestorationKind.TimeState,
                timeState.BeforeValue,
                timeState.AfterValue,
                timeState.Changed,
                timeState.RestoreAttempted,
                !timeState.Confirmed
                    ? ContractStateRestorationDisposition.Unconfirmed
                    : !timeState.Changed
                        ? ContractStateRestorationDisposition.Unchanged
                        : timeState.Succeeded
                            ? ContractStateRestorationDisposition.Restored
                            : ContractStateRestorationDisposition.Failed,
                timeState.Confirmed && (!timeState.Changed || timeState.Succeeded)
                    ? null
                    : cleanupCode));
            return restorations;
        }

        private static IReadOnlyList<ContractResourceRelease> CreateResourceReleases (
            RecordingSession session)
        {
            var release = session.ReleaseOutcome;
            var captureDisposition = !release.ControllerStopped || !release.SettingsDisposed
                ? ContractResourceReleaseDisposition.Failed
                : session.PostStopObservationCompleted
                    ? ContractResourceReleaseDisposition.Released
                    : ContractResourceReleaseDisposition.Unconfirmed;
            return new[]
            {
                new ContractResourceRelease(
                    ContractResourceKind.CaptureSession,
                    acquired: true,
                    releaseAttempted: true,
                    captureDisposition,
                    captureDisposition == ContractResourceReleaseDisposition.Released
                        ? null
                        : release.ControllerStopped
                            ? GameViewRecordingErrorCodes.CleanupFailed
                            : GameViewRecordingErrorCodes.FinalizationFailed),
                new ContractResourceRelease(
                    ContractResourceKind.TemporaryOutput,
                    acquired: true,
                    releaseAttempted: false,
                    ContractResourceReleaseDisposition.Unconfirmed,
                    reasonCode: null),
                new ContractResourceRelease(
                    ContractResourceKind.LifecycleSubscriptions,
                    acquired: false,
                    releaseAttempted: false,
                    ContractResourceReleaseDisposition.NotAcquired,
                    reasonCode: null),
                new ContractResourceRelease(
                    ContractResourceKind.RuntimeRegistration,
                    acquired: false,
                    releaseAttempted: false,
                    ContractResourceReleaseDisposition.NotAcquired,
                    reasonCode: null),
                new ContractResourceRelease(
                    ContractResourceKind.RecordingExclusion,
                    acquired: true,
                    releaseAttempted: true,
                    session.ExclusionReleased
                        ? ContractResourceReleaseDisposition.Released
                        : ContractResourceReleaseDisposition.Failed,
                    session.ExclusionReleased
                        ? null
                        : GameViewRecordingErrorCodes.CleanupFailed),
            };
        }

        private static ContractStateRestoration CreateUnchangedOrUnconfirmedState (
            ContractStateRestorationKind kind,
            string beforeValue,
            string afterValue,
            bool confirmed,
            UcliCode reasonCode)
        {
            return new ContractStateRestoration(
                kind,
                beforeValue,
                afterValue,
                changed: false,
                restoreAttempted: false,
                confirmed
                    ? ContractStateRestorationDisposition.Unchanged
                    : ContractStateRestorationDisposition.Unconfirmed,
                confirmed ? null : reasonCode);
        }

        private static ContractCleanupDisposition ResolveCleanupDisposition (
            IReadOnlyList<ContractStateRestoration> restorations,
            IReadOnlyList<ContractResourceRelease> releases)
        {
            foreach (var restoration in restorations)
            {
                if (restoration.Disposition == ContractStateRestorationDisposition.Unconfirmed)
                {
                    return ContractCleanupDisposition.Unconfirmed;
                }
            }
            foreach (var release in releases)
            {
                if (release.Disposition == ContractResourceReleaseDisposition.Unconfirmed)
                {
                    return ContractCleanupDisposition.Unconfirmed;
                }
            }
            foreach (var restoration in restorations)
            {
                if (restoration.Disposition == ContractStateRestorationDisposition.Failed)
                {
                    return ContractCleanupDisposition.Failed;
                }
            }
            foreach (var release in releases)
            {
                if (release.Disposition == ContractResourceReleaseDisposition.Failed)
                {
                    return ContractCleanupDisposition.Failed;
                }
            }
            return ContractCleanupDisposition.Complete;
        }

        private static bool HasUnconfirmedRuntimeCleanup (ContractCleanupRecord cleanup)
        {
            foreach (var restoration in cleanup.StateRestorations)
            {
                if (restoration.Disposition == ContractStateRestorationDisposition.Unconfirmed)
                {
                    return true;
                }
            }

            foreach (var release in cleanup.ResourceReleases)
            {
                if (release.Kind != ContractResourceKind.TemporaryOutput
                    && release.Disposition == ContractResourceReleaseDisposition.Unconfirmed)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasFailedRuntimeCleanup (ContractCleanupRecord cleanup)
        {
            foreach (var restoration in cleanup.StateRestorations)
            {
                if (restoration.Disposition == ContractStateRestorationDisposition.Failed)
                {
                    return true;
                }
            }

            foreach (var release in cleanup.ResourceReleases)
            {
                if (release.Disposition == ContractResourceReleaseDisposition.Failed)
                {
                    return true;
                }
            }

            return false;
        }

        private static string FormatResolution (int width, int height)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}x{1}", width, height);
        }

        private static string FormatTimeState (
            bool runInBackground,
            int captureFramerate,
            float captureDeltaTime,
            float timeScale)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "runInBackground={0};captureFramerate={1};captureDeltaTime={2:R};timeScale={3:R}",
                runInBackground,
                captureFramerate,
                captureDeltaTime,
                timeScale);
        }

        private void OnEditorUpdate ()
        {
            if (activeSession == null || isFinalizing)
            {
                return;
            }

            try
            {
                if (activeSession.ReleaseOutcome != null)
                {
                    CompletePostStopObservation(activeSession);
                    if (!activeSession.PostStopObservationPending
                        && !activeSession.PresentationRecovery.IsPending)
                    {
                        if (activeSession.ReleaseOutcome.Resolution.Disposition
                                == ResolutionReleaseDisposition.Unconfirmed
                            && activeSession.ResolutionLease.TryValidateRestoredState(out _)
                            && activeSession.PresentationRecovery.TerminalObservation
                                == GameViewResolutionPresentationRecovery.Observation.RestoredResolutionPresented)
                        {
                            activeSession.ReleaseOutcome = activeSession.ReleaseOutcome with
                            {
                                Resolution = new ResolutionReleaseOutcome(
                                    ResolutionReleaseDisposition.RestoredAwaitingPresentation,
                                    Error: null),
                            };
                        }

                        FinalizeActiveSession(activeSession, changed: true);
                    }
                    return;
                }

                var targetBeforeObservation = activeSession.Target;
                activeSession.ObserveRuntimeIntegrity(requireTarget: false);
                if (targetBeforeObservation == null && activeSession.Target != null)
                {
                    activeSession.Snapshot = activeSession.Snapshot with
                    {
                        Target = activeSession.Target,
                        UpdatedAtUtc = DateTimeOffset.UtcNow,
                    };
                    Notify(activeSession.Snapshot);
                }

                if (!activeSession.RuntimeIntegrityAllowsRecording)
                {
                    CompleteActiveSession(
                        GameViewRecordingStopReason.InternalFailure,
                        interrupted: false);
                    return;
                }

                if (!activeSession.Controller.IsRecording())
                {
                    CompleteActiveSession(
                        GameViewRecordingStopReason.RecorderFailure,
                        interrupted: false);
                    return;
                }

                if (EditorApplication.timeSinceStartup - activeSession.StartedAt
                    >= activeSession.Request.MaximumDuration.TotalSeconds)
                {
                    CompleteActiveSession(
                        GameViewRecordingStopReason.MaxDurationReached,
                        interrupted: false);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                CompleteActiveSession(
                    GameViewRecordingStopReason.InternalFailure,
                    interrupted: false);
            }
        }

        private void OnPlayModeStateChanged (PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode && activeSession != null)
            {
                CompleteActiveSession(
                    GameViewRecordingStopReason.PlayModeExited,
                    interrupted: true);
            }
        }

        private void OnBeforeAssemblyReload ()
        {
            if (activeSession != null)
            {
                CompleteActiveSession(
                    GameViewRecordingStopReason.DomainReload,
                    interrupted: true);
            }
        }

        private void OnEditorQuitting ()
        {
            if (activeSession != null)
            {
                CompleteActiveSession(
                    GameViewRecordingStopReason.UnityExited,
                    interrupted: true);
            }
        }

        private void Notify (GameViewRecordingSnapshot snapshot)
        {
            if (snapshot == null || StateChanged == null)
            {
                return;
            }

            foreach (Action<GameViewRecordingSnapshot> subscriber in StateChanged.GetInvocationList())
            {
                try
                {
                    subscriber(snapshot);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private static bool TryRequireMainThread (
            out GameViewRecordingRejectedOperation failure)
        {
            try
            {
                UnityMainThreadGuard.CaptureSynchronizationContext("GameView recording");
                failure = null;
                return true;
            }
            catch (Exception exception)
            {
                failure = new GameViewRecordingRejectedOperation(
                    GameViewRecordingFailure.InternalFailure,
                    exception.Message);
                return false;
            }
        }

        private static string AppendReleaseErrors (
            string message,
            ReleaseOutcome outcome)
        {
            if (outcome.FinalizationError != null)
            {
                message += $" Finalization: {outcome.FinalizationError}";
            }

            if (outcome.CleanupError != null)
            {
                message += $" Cleanup: {outcome.CleanupError}";
            }

            if (outcome.Resolution.Error != null)
            {
                message += $" Resolution: {outcome.Resolution.Error}";
            }

            return message;
        }

        private static string JoinErrors (IReadOnlyCollection<string> errors)
        {
            return errors.Count == 0 ? null : string.Join(" ", errors);
        }

        private static string FormatException (Exception exception)
        {
            return $"{exception.GetType().Name}: {exception.Message}";
        }

        private sealed class RecordingSession : IResolutionSession
        {
            public RecordingSession (
                GameViewRecordingStartRequest request,
                RecorderController controller,
                UnityRecorderSettings settings,
                GameViewResolutionLease resolutionLease,
                GameViewResolutionPresentationRecovery presentationRecovery,
                UnityGameViewPresentationAdapter presentationAdapter,
                GameViewPresentationSource originalSource,
                bool originalRunInBackground,
                int originalCaptureFramerate,
                float originalCaptureDeltaTime,
                float originalTimeScale)
            {
                Request = request;
                Controller = controller;
                Settings = settings;
                ResolutionLease = resolutionLease;
                PresentationRecovery = presentationRecovery;
                PresentationAdapter = presentationAdapter
                    ?? throw new ArgumentNullException(nameof(presentationAdapter));
                OriginalSource = originalSource
                    ?? throw new ArgumentNullException(nameof(originalSource));
                OriginalSessionId = UnityObjectSessionId.Create(originalSource.View).ToString();
                PresentationChanged = originalSource.Width != request.Dimensions.Width
                    || originalSource.Height != request.Dimensions.Height;
                OriginalRunInBackground = originalRunInBackground;
                OriginalCaptureFramerate = originalCaptureFramerate;
                OriginalCaptureDeltaTime = originalCaptureDeltaTime;
                OriginalTimeScale = originalTimeScale;
                Runtime = GameViewRecordingRuntimeIdentityFactory.Create(
                    UnityEditorSessionStateStore.GetOrCreateEditorInstanceId());
            }

            public GameViewRecordingStartRequest Request { get; }

            public RecorderController Controller { get; }

            public UnityRecorderSettings Settings { get; }

            public GameViewResolutionLease ResolutionLease { get; }

            public GameViewResolutionPresentationRecovery PresentationRecovery { get; }

            public UnityGameViewPresentationAdapter PresentationAdapter { get; }

            public GameViewPresentationSource OriginalSource { get; }

            public string OriginalSessionId { get; }

            public bool PresentationChanged { get; }

            public bool OriginalRunInBackground { get; }

            public int OriginalCaptureFramerate { get; }

            public float OriginalCaptureDeltaTime { get; }

            public float OriginalTimeScale { get; }

            public double StartedAt { get; private set; }

            public DateTimeOffset StartedAtUtc { get; private set; }

            public long MonotonicStartedTimestamp { get; private set; }

            public long? MonotonicStopRequestedTimestamp { get; set; }

            public double? GameTimeStartedSeconds { get; private set; }

            public double? TimeScaleStarted { get; private set; }

            public int? FrameCountStarted { get; private set; }

            public ContractRuntimeIdentity Runtime { get; }

            public ContractTargetObservation Target { get; private set; }

            public GameViewRecordingStopReason StopReason { get; set; }

            public bool Interrupted { get; set; }

            public bool ExclusionReleased { get; set; }

            public bool RecorderStartupPauseObserved { get; private set; }

            public bool RecorderChangedRunInBackground { get; private set; }

            public bool RecorderChangedCaptureFramerate { get; private set; }

            public bool RecorderChangedCaptureDeltaTime { get; private set; }

            public bool RunInBackgroundRestoreAttempted { get; set; }

            public bool CaptureFramerateRestoreAttempted { get; set; }

            public bool CaptureDeltaTimeRestoreAttempted { get; set; }

            public bool TimeScaleRestoreAttempted { get; set; }

            public bool HasTimeStateRestoreAttempt => RunInBackgroundRestoreAttempted
                || CaptureFramerateRestoreAttempted
                || CaptureDeltaTimeRestoreAttempted
                || TimeScaleRestoreAttempted;

            private bool RecorderRunInBackgroundValue { get; set; }

            private int RecorderCaptureFramerateValue { get; set; }

            private float RecorderCaptureDeltaTimeValue { get; set; }

            public bool PostStopObservationPending { get; private set; }

            public bool PostStopObservationCompleted { get; private set; }

            public bool HasFailedRuntimeIntegrity =>
                SettingsIntegrity == RuntimeIntegrityDisposition.Failed
                || TargetIntegrity == RuntimeIntegrityDisposition.Failed;

            public bool HasUnconfirmedRuntimeIntegrity =>
                SettingsIntegrity == RuntimeIntegrityDisposition.Unconfirmed
                || TargetIntegrity == RuntimeIntegrityDisposition.Unconfirmed;

            public bool RuntimeIntegrityAllowsRecording =>
                !HasFailedRuntimeIntegrity && !HasUnconfirmedRuntimeIntegrity;

            public string RuntimeIntegrityMessage
            {
                get
                {
                    if (SettingsIntegrityMessage == null)
                    {
                        return TargetIntegrityMessage;
                    }
                    if (TargetIntegrityMessage == null)
                    {
                        return SettingsIntegrityMessage;
                    }
                    return $"{SettingsIntegrityMessage} {TargetIntegrityMessage}";
                }
            }

            public string AcceptanceFailureMessage { get; set; }

            public ReleaseOutcome ReleaseOutcome { get; set; }

            public GameViewRecordingSnapshot Snapshot { get; set; }

            public void MarkRecordingStarted ()
            {
                if (StartedAtUtc != default)
                {
                    return;
                }

                StartedAt = EditorApplication.timeSinceStartup;
                StartedAtUtc = DateTimeOffset.UtcNow;
                MonotonicStartedTimestamp = Stopwatch.GetTimestamp();
                try
                {
                    GameTimeStartedSeconds = Time.timeAsDouble;
                    TimeScaleStarted = Time.timeScale;
                    FrameCountStarted = Time.frameCount;
                    ObserveRecorderOwnedTimeState();
                }
                catch
                {
                    GameTimeStartedSeconds = null;
                    TimeScaleStarted = null;
                    FrameCountStarted = null;
                }
            }

            public bool CanRestoreRunInBackground () => RecorderChangedRunInBackground
                && Application.runInBackground == RecorderRunInBackgroundValue;

            public bool CanRestoreCaptureFramerate () => RecorderChangedCaptureFramerate
                && Time.captureFramerate == RecorderCaptureFramerateValue;

            public bool CanRestoreCaptureDeltaTime () => RecorderChangedCaptureDeltaTime
                && Mathf.Approximately(Time.captureDeltaTime, RecorderCaptureDeltaTimeValue);

            public bool CanRestoreTimeScale () => RecorderStartupPauseObserved
                && Mathf.Approximately(Time.timeScale, 0f);

            private void ObserveRecorderOwnedTimeState ()
            {
                RecorderChangedRunInBackground = Application.runInBackground != OriginalRunInBackground;
                RecorderRunInBackgroundValue = Application.runInBackground;
                RecorderChangedCaptureFramerate = Time.captureFramerate != OriginalCaptureFramerate;
                RecorderCaptureFramerateValue = Time.captureFramerate;
                RecorderChangedCaptureDeltaTime = !Mathf.Approximately(
                    Time.captureDeltaTime,
                    OriginalCaptureDeltaTime);
                RecorderCaptureDeltaTimeValue = Time.captureDeltaTime;
                RecorderStartupPauseObserved = !Mathf.Approximately(OriginalTimeScale, 0f)
                    && Mathf.Approximately(Time.timeScale, 0f);
            }

            public void ObserveRuntimeIntegrity (bool requireTarget)
            {
                ObserveSettingsIntegrity();
                ObserveTargetIntegrity(requireTarget);
            }

            public void BeginPostStopObservation ()
            {
                PostStopObservationPending = true;
                PostStopObservationCompleted = false;
            }

            public void MarkPostStopObservationCompleted ()
            {
                PostStopObservationPending = false;
                PostStopObservationCompleted = true;
            }

            public void MarkPostStopObservationUnavailable ()
            {
                PostStopObservationPending = false;
                PostStopObservationCompleted = false;
            }

            private RuntimeIntegrityDisposition SettingsIntegrity { get; set; }

            private string SettingsIntegrityMessage { get; set; }

            private RuntimeIntegrityDisposition TargetIntegrity { get; set; }

            private string TargetIntegrityMessage { get; set; }

            private void ObserveSettingsIntegrity ()
            {
                if (SettingsIntegrity is RuntimeIntegrityDisposition.Failed
                    or RuntimeIntegrityDisposition.Unconfirmed)
                {
                    return;
                }

                try
                {
                    if (Settings.TryValidateEffectiveProfile(Request, out var errorMessage))
                    {
                        SettingsIntegrity = RuntimeIntegrityDisposition.Confirmed;
                        return;
                    }

                    SettingsIntegrity = RuntimeIntegrityDisposition.Failed;
                    SettingsIntegrityMessage = errorMessage;
                }
                catch (Exception exception)
                {
                    SettingsIntegrity = RuntimeIntegrityDisposition.Unconfirmed;
                    SettingsIntegrityMessage =
                        $"Unity Recorder settings could not be observed. {FormatException(exception)}";
                }
            }

            private void ObserveTargetIntegrity (bool requireTarget)
            {
                if (TargetIntegrity is RuntimeIntegrityDisposition.Failed
                    or RuntimeIntegrityDisposition.Unconfirmed)
                {
                    return;
                }

                if (!PresentationAdapter.TryGetSource(out var source, out var sourceError))
                {
                    if (Target != null || requireTarget)
                    {
                        TargetIntegrity = RuntimeIntegrityDisposition.Unconfirmed;
                        TargetIntegrityMessage = sourceError
                            ?? "The GameView recording target could not be observed.";
                    }
                    return;
                }

                if (source.View != OriginalSource.View)
                {
                    TargetIntegrity = RuntimeIntegrityDisposition.Failed;
                    TargetIntegrityMessage =
                        "The main Play Mode GameView changed during recording.";
                    return;
                }
                if (source.TargetDisplay != OriginalSource.TargetDisplay)
                {
                    TargetIntegrity = RuntimeIntegrityDisposition.Failed;
                    TargetIntegrityMessage =
                        "The GameView target Display changed during recording.";
                    return;
                }
                if (source.Width != Request.Dimensions.Width
                    || source.Height != Request.Dimensions.Height)
                {
                    if (Target != null || requireTarget)
                    {
                        TargetIntegrity = RuntimeIntegrityDisposition.Failed;
                        TargetIntegrityMessage =
                            "The GameView presentation no longer matches the requested recording resolution.";
                    }
                    return;
                }

                if (!TryGetOrientation(source.SourceUvTransform, out var orientation))
                {
                    TargetIntegrity = RuntimeIntegrityDisposition.Failed;
                    TargetIntegrityMessage =
                        "The GameView presentation uses an unsupported orientation transform.";
                    return;
                }

                var sessionId = UnityObjectSessionId.Create(source.View).ToString();
                var observedTarget = new ContractTargetObservation(
                    $"playModeView:{sessionId}",
                    $"gameView:{sessionId}",
                    source.TargetDisplay,
                    Request.Dimensions,
                    new ContractDimensions(source.Width, source.Height),
                    orientation,
                    QualitySettings.activeColorSpace == ColorSpace.Linear
                        ? ContractProjectColorSpace.Linear
                        : ContractProjectColorSpace.Gamma);
                if (Target == null)
                {
                    Target = observedTarget;
                    TargetIntegrity = RuntimeIntegrityDisposition.Confirmed;
                    return;
                }

                if (Target.PlayModeViewId != observedTarget.PlayModeViewId
                    || Target.GameViewId != observedTarget.GameViewId
                    || Target.Display != observedTarget.Display
                    || Target.RequestedDimensions != observedTarget.RequestedDimensions
                    || Target.Dimensions != observedTarget.Dimensions
                    || Target.Orientation != observedTarget.Orientation
                    || Target.ProjectColorSpace != observedTarget.ProjectColorSpace)
                {
                    TargetIntegrity = RuntimeIntegrityDisposition.Failed;
                    TargetIntegrityMessage =
                        "The observed GameView recording target changed during recording.";
                    return;
                }

                TargetIntegrity = RuntimeIntegrityDisposition.Confirmed;
            }

            public ContractTimingObservation CreateTimingObservation (
                long monotonicCompletedTimestamp)
            {
                double? completedGameTime = null;
                double? completedTimeScale = null;
                int? completedFrameCount = null;
                try
                {
                    completedGameTime = Time.timeAsDouble;
                    completedTimeScale = Time.timeScale;
                    completedFrameCount = Time.frameCount;
                }
                catch
                {
                    // A lifecycle interruption can make game-time observations unavailable.
                }

                return new ContractTimingObservation(
                    MonotonicStartedTimestamp,
                    MonotonicStopRequestedTimestamp,
                    monotonicCompletedTimestamp,
                    Stopwatch.Frequency,
                    GameTimeStartedSeconds,
                    completedGameTime,
                    TimeScaleStarted,
                    completedTimeScale,
                    FrameCountStarted,
                    completedFrameCount,
                    mp4DurationSeconds: null,
                    encodedFrameCount: null,
                    effectiveFrameRate: null,
                    droppedFrameCount: null,
                    duplicatedFrameCount: null,
                    delayedFrameCount: null);
            }

            private static bool TryGetOrientation (Vector4 transform, out string orientation)
            {
                if (transform == new Vector4(1f, 1f, 0f, 0f))
                {
                    orientation = "upright";
                    return true;
                }
                if (transform == new Vector4(1f, -1f, 0f, 1f))
                {
                    orientation = "verticallyFlipped";
                    return true;
                }

                orientation = null;
                return false;
            }
        }

        private interface IResolutionSession
        {
            GameViewResolutionLease ResolutionLease { get; }

            GameViewResolutionPresentationRecovery PresentationRecovery { get; }
        }

        private sealed class ResolutionOnlySession : IResolutionSession
        {
            public ResolutionOnlySession (
                GameViewResolutionLease resolutionLease,
                GameViewResolutionPresentationRecovery presentationRecovery)
            {
                ResolutionLease = resolutionLease;
                PresentationRecovery = presentationRecovery;
            }

            public GameViewResolutionLease ResolutionLease { get; }

            public GameViewResolutionPresentationRecovery PresentationRecovery { get; }
        }

        private sealed record ReleaseOutcome (
            string FinalizationError,
            string CleanupError,
            bool ControllerStopped,
            bool SettingsDisposed,
            TimeStateReleaseOutcome TimeState,
            ResolutionReleaseOutcome Resolution);

        private sealed record TimeStateReleaseOutcome (
            string BeforeValue,
            string AfterValue,
            bool Changed,
            bool RestoreAttempted,
            bool Succeeded,
            bool Confirmed,
            string Error);

        private enum RuntimeIntegrityDisposition
        {
            Pending,
            Confirmed,
            Failed,
            Unconfirmed,
        }

        private sealed record ResolutionReleaseOutcome (
            ResolutionReleaseDisposition Disposition,
            string Error);

        private enum ResolutionReleaseDisposition
        {
            RestoredAwaitingPresentation,
            UserSelectionPreserved,
            Failed,
            Unconfirmed,
        }
    }
}
