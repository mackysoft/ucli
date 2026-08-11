using System;
using System.IO;
using System.Linq;
using MackySoft.FileSystem;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Recording.Recorder
{
    /// <summary> Maps the uCLI fixed recording profile onto one Recorder controller and one Movie recorder. </summary>
    internal static class UnityRecorderSettingsFactory
    {
        public static UnityRecorderSettings Create (GameViewRecordingStartRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var outputPath = request.StagingOutputPath
                ?? throw new ArgumentException(
                    "The provider-private output path is required.",
                    nameof(request));
            RecorderControllerSettings controllerSettings = null;
            MovieRecorderSettings movieSettings = null;
            try
            {
                controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
                controllerSettings.name = "uCLI GameView Recording";
                controllerSettings.hideFlags = HideFlags.HideAndDontSave;
                controllerSettings.ExitPlayMode = false;
                controllerSettings.CapFrameRate = true;
                controllerSettings.FrameRatePlayback = FrameRatePlayback.Constant;
                controllerSettings.FrameRate = request.FrameRate;
                controllerSettings.SetRecordModeToManual();

                movieSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
                movieSettings.name = "uCLI GameView H.264 Recording";
                movieSettings.hideFlags = HideFlags.HideAndDontSave;
                movieSettings.Enabled = true;
                movieSettings.EncoderSettings = new CoreEncoderSettings
                {
                    Codec = CoreEncoderSettings.OutputCodec.MP4,
                    EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
                };
                movieSettings.CaptureAlpha = false;
                movieSettings.CaptureAudio = false;
                movieSettings.ImageInputSettings = new GameViewInputSettings
                {
                    OutputWidth = request.Dimensions.Width,
                    OutputHeight = request.Dimensions.Height,
                };
                movieSettings.OutputFile = Path.ChangeExtension(outputPath.Value, extension: null);

                if (!AbsolutePath.TryParse(
                    movieSettings.FileNameGenerator.BuildAbsolutePath(null),
                    out var resolvedOutputPath,
                    out _)
                    || !outputPath.IsSameAs(resolvedOutputPath))
                {
                    throw new ArgumentException(
                        "Unity Recorder resolved the provider-private output to a different path.",
                        nameof(request));
                }

                controllerSettings.AddRecorderSettings(movieSettings);
                if (controllerSettings.RecorderSettings.Count() != 1)
                {
                    throw new InvalidOperationException(
                        "The uCLI recording controller must own exactly one Movie recorder.");
                }

                return new UnityRecorderSettings(controllerSettings, movieSettings, outputPath);
            }
            catch
            {
                Destroy(movieSettings);
                Destroy(controllerSettings);
                throw;
            }
        }

        private static void Destroy (UnityEngine.Object value)
        {
            if (value != null)
            {
                UnityEngine.Object.DestroyImmediate(value);
            }
        }
    }

    /// <summary> Owns the transient ScriptableObjects used by one Recorder controller. </summary>
    internal sealed class UnityRecorderSettings : IDisposable
    {
        private bool isDisposed;

        public UnityRecorderSettings (
            RecorderControllerSettings controllerSettings,
            MovieRecorderSettings movieSettings,
            AbsolutePath outputPath)
        {
            ControllerSettings = controllerSettings
                ?? throw new ArgumentNullException(nameof(controllerSettings));
            MovieSettings = movieSettings ?? throw new ArgumentNullException(nameof(movieSettings));
            OutputPath = outputPath ?? throw new ArgumentNullException(nameof(outputPath));
        }

        public RecorderControllerSettings ControllerSettings { get; }

        public MovieRecorderSettings MovieSettings { get; }

        public AbsolutePath OutputPath { get; }

        public bool TryValidateEffectiveProfile (
            GameViewRecordingStartRequest request,
            out string errorMessage)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (ControllerSettings == null || MovieSettings == null)
            {
                errorMessage = "Unity Recorder settings were destroyed before the recording ended.";
                return false;
            }
            if (ControllerSettings.ExitPlayMode
                || !ControllerSettings.CapFrameRate
                || ControllerSettings.FrameRatePlayback != FrameRatePlayback.Constant
                || !Mathf.Approximately(ControllerSettings.FrameRate, request.FrameRate))
            {
                errorMessage = "Unity Recorder controller settings no longer match the fixed recording profile.";
                return false;
            }

            var recorders = ControllerSettings.RecorderSettings.ToArray();
            if (recorders.Length != 1 || recorders[0] != MovieSettings || !MovieSettings.Enabled)
            {
                errorMessage = "Unity Recorder no longer owns exactly one enabled Movie recorder.";
                return false;
            }
            if (MovieSettings.CaptureAudio
                || MovieSettings.CaptureAlpha
                || MovieSettings.EncoderSettings is not CoreEncoderSettings encoder
                || encoder.Codec != CoreEncoderSettings.OutputCodec.MP4
                || encoder.EncodingQuality != CoreEncoderSettings.VideoEncodingQuality.High)
            {
                errorMessage = "Unity Movie Recorder settings no longer match the fixed MP4/H.264 profile.";
                return false;
            }
            if (MovieSettings.ImageInputSettings is not GameViewInputSettings input
                || input.OutputWidth != request.Dimensions.Width
                || input.OutputHeight != request.Dimensions.Height)
            {
                errorMessage = "Unity Recorder GameView input settings no longer match the requested resolution.";
                return false;
            }
            if (!AbsolutePath.TryParse(
                    MovieSettings.FileNameGenerator.BuildAbsolutePath(null),
                    out var resolvedOutputPath,
                    out _)
                || !OutputPath.IsSameAs(resolvedOutputPath)
                || !request.StagingOutputPath.IsSameAs(resolvedOutputPath))
            {
                errorMessage = "Unity Recorder output no longer resolves to the request-owned staging path.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        public void Dispose ()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            Exception failure = null;
            try
            {
                if (MovieSettings != null)
                {
                    UnityEngine.Object.DestroyImmediate(MovieSettings);
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            try
            {
                if (ControllerSettings != null)
                {
                    UnityEngine.Object.DestroyImmediate(ControllerSettings);
                }
            }
            catch (Exception exception)
            {
                failure ??= exception;
            }

            if (failure != null)
            {
                throw failure;
            }
        }
    }
}
