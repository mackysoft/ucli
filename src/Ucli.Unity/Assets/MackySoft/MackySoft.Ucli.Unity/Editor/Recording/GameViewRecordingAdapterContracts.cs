using System;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Presentation;
using MackySoft.Ucli.Contracts.Recording;
using MackySoft.Ucli.Infrastructure.Storage;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Recording
{
    /// <summary>Applies uCLI's local-storage access boundary to Recorder staging directories.</summary>
    internal static class GameViewRecordingStagingOutputBoundary
    {
        /// <summary>Ensures the existing provider staging directory remains restricted to the current user.</summary>
        public static void EnsureSecureDirectory (AbsolutePath directoryPath)
        {
            FileSystemAccessBoundary.EnsureSecureDirectory(
                directoryPath ?? throw new ArgumentNullException(nameof(directoryPath)));
        }
    }

    /// <summary> Creates the runtime identity shared by binding admission and the optional Recorder adapter. </summary>
    internal static class GameViewRecordingRuntimeIdentityFactory
    {
        internal const string EncoderName = "UnityEditor.Media.MediaEncoder";

        public static GameViewRecordingRuntimeIdentity Create (Guid editorInstanceId)
        {
            return new GameViewRecordingRuntimeIdentity(
                editorInstanceId,
                SystemInfo.operatingSystem,
                EncoderName,
                Application.unityVersion);
        }
    }

    [Flags]
    internal enum GameViewRecordingEditorPlatform
    {
        None = 0,
        Windows = 1 << 0,
        MacOS = 1 << 1,
    }

    internal enum GameViewRecordingState
    {
        Recording,
        Finalizing,
        Completed,
        Failed,
        Interrupted,
        Indeterminate,
    }

    internal enum GameViewRecordingStopReason
    {
        None,
        Manual,
        MaxDurationReached,
        PlayModeExited,
        DomainReload,
        AdapterUnloaded,
        UnityExited,
        RecorderFailure,
        InternalFailure,
    }

    internal enum GameViewRecordingFailure
    {
        None,
        InvalidRequest,
        UnsupportedPlatform,
        RequiresGuiSession,
        RequiresPlayMode,
        PlayModeTransitioning,
        EditorPaused,
        GameViewUnavailable,
        RequestedSizeUnsupported,
        EncoderUnsupported,
        IdConflict,
        Conflict,
        NotFound,
        RecorderStartFailed,
        FinalizationFailed,
        CleanupFailed,
        Interrupted,
        InternalFailure,
    }

    /// <summary> Identifies the package and runtime range implemented by one immutable adapter build. </summary>
    internal sealed record GameViewRecordingAdapterMetadata (
        string AdapterId,
        string AdapterVersion,
        string RecorderPackageId,
        string RecorderPackageVersionRange,
        string UnityVersionRange,
        GameViewRecordingEditorPlatform SupportedPlatforms,
        GameViewRecordingCaptureProfile CaptureProfile,
        GameViewRecordingLimits Limits);

    /// <summary> Carries a normalized request and its provider-private staging destination. </summary>
    internal sealed record GameViewRecordingStartRequest
    {
        public GameViewRecordingStartRequest (
            Guid recordingId,
            Sha256Digest requestDigest,
            PixelDimensions dimensions,
            int frameRate,
            TimeSpan maximumDuration,
            AbsolutePath stagingOutputPath,
            IpcGameViewRecordingStartBinding startBinding)
        {
            RecordingId = recordingId;
            RequestDigest = requestDigest;
            Dimensions = dimensions ?? throw new ArgumentNullException(nameof(dimensions));
            FrameRate = frameRate;
            MaximumDuration = maximumDuration;
            StagingOutputPath = stagingOutputPath;
            StartBinding = startBinding ?? throw new ArgumentNullException(nameof(startBinding));
        }

        public Guid RecordingId { get; }

        public Sha256Digest RequestDigest { get; }

        public PixelDimensions Dimensions { get; }

        public int FrameRate { get; }

        public TimeSpan MaximumDuration { get; }

        public AbsolutePath StagingOutputPath { get; }

        /// <summary>Gets the complete execution identity that admitted this Recorder start.</summary>
        public IpcGameViewRecordingStartBinding StartBinding { get; }
    }

    /// <summary>Represents the closed result of runtime admission for a new recording.</summary>
    internal abstract record GameViewRecordingRuntimeAdmission;

    /// <summary>Represents a runtime that can admit a recording.</summary>
    internal sealed record GameViewRecordingRuntimeReadyAdmission : GameViewRecordingRuntimeAdmission;

    /// <summary>Represents a runtime rejection before a recording is owned.</summary>
    internal sealed record GameViewRecordingRuntimeRejectedAdmission (
        GameViewRecordingFailure Failure,
        string Message) : GameViewRecordingRuntimeAdmission;

    /// <summary> Captures the adapter-owned runtime state for one logical recording. </summary>
    internal sealed record GameViewRecordingSnapshot (
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

    /// <summary>Represents an adapter operation before or after recording ownership was established.</summary>
    internal abstract record GameViewRecordingOperationResult
    {
        /// <summary>Creates an operation result for a recording observation owned by the adapter.</summary>
        public static GameViewRecordingOperationResult Observed (
            GameViewRecordingSnapshot recording)
        {
            return new GameViewRecordingObservedOperation(recording);
        }

        /// <summary>Creates a rejection that occurred before an adapter-owned observation exists.</summary>
        public static GameViewRecordingOperationResult Failed (
            GameViewRecordingFailure failure,
            string message)
        {
            if (failure == GameViewRecordingFailure.None)
            {
                throw new ArgumentOutOfRangeException(nameof(failure));
            }

            return new GameViewRecordingRejectedOperation(failure, message);
        }
    }

    /// <summary>Represents an operation rejected before the adapter owned a recording observation.</summary>
    internal sealed record GameViewRecordingRejectedOperation (
        GameViewRecordingFailure Failure,
        string Message) : GameViewRecordingOperationResult;

    /// <summary>Represents an operation that observed an adapter-owned recording snapshot.</summary>
    internal sealed record GameViewRecordingObservedOperation : GameViewRecordingOperationResult
    {
        public GameViewRecordingObservedOperation (GameViewRecordingSnapshot recording)
        {
            Recording = recording ?? throw new ArgumentNullException(nameof(recording));
        }

        public GameViewRecordingSnapshot Recording { get; }
    }

    /// <summary> Isolates optional Recorder types from the uCLI Editor assembly. </summary>
    internal interface IGameViewRecordingAdapter
    {
        GameViewRecordingAdapterMetadata Metadata { get; }

        event Action<GameViewRecordingSnapshot> StateChanged;

        GameViewRecordingRuntimeAdmission GetRuntimeAdmission ();

        GameViewRecordingOperationResult Start (GameViewRecordingStartRequest request);

        GameViewRecordingOperationResult GetStatus (Guid? recordingId);

        GameViewRecordingOperationResult Stop (Guid recordingId);
    }
}
