using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MackySoft.Ucli.Unity.ScreenshotCapture.Capture
{
    /// <summary> Captures and compares Editor presentation state that can invalidate screenshot evidence. </summary>
    internal static class UnityScreenshotPresentationStateFence
    {
        private const BindingFlags StaticMembers =
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary> Captures the current presentation state through capability-probed members. </summary>
        public static bool TryCaptureCurrent (
            out PresentationState state,
            out string errorMessage)
        {
            state = null;
            try
            {
                var pipelineSwitchCompletedProperty = typeof(RenderPipelineManager).GetProperty(
                    "pipelineSwitchCompleted",
                    StaticMembers);
                if (pipelineSwitchCompletedProperty?.GetValue(obj: null)
                    is not bool pipelineSwitchCompleted)
                {
                    errorMessage =
                        "Unity render-pipeline transition state is unavailable for screenshot validation.";
                    return false;
                }

                var renderPipelineAsset = GraphicsSettings.currentRenderPipeline;
                state = new PresentationState(
                    EditorApplication.isPaused,
                    ShaderUtil.anythingCompiling,
                    pipelineSwitchCompleted,
                    QualitySettings.GetQualityLevel(),
                    renderPipelineAsset != null ? renderPipelineAsset.GetInstanceID() : (int?)null,
                    RenderPipelineManager.currentPipeline);
                errorMessage = null;
                return true;
            }
            catch (Exception exception)
            {
                errorMessage =
                    $"Unity presentation state could not be captured. {exception.Message}";
                return false;
            }
        }

        /// <summary> Captures one presentation observation and rejects transitional state. </summary>
        public static bool TryCaptureStable (
            out PresentationState state,
            out string errorMessage)
        {
            return TryCaptureCurrent(out state, out errorMessage)
                && TryValidateObservation(state, out errorMessage);
        }

        /// <summary> Captures the current presentation state and compares it with an earlier stable observation. </summary>
        public static bool TryValidateCurrentStable (
            PresentationState expected,
            out string errorMessage)
        {
            if (!TryCaptureCurrent(out var current, out errorMessage))
            {
                return false;
            }

            return TryValidateStable(expected, current, out errorMessage);
        }

        /// <summary> Rejects an observation made during shader or render-pipeline transition. </summary>
        public static bool TryValidateObservation (
            PresentationState state,
            out string errorMessage)
        {
            if (state == null)
            {
                errorMessage = "Unity presentation state observation is unavailable.";
                return false;
            }

            if (state.ShaderCompilationInProgress)
            {
                errorMessage = "Unity shader compilation is active; the rendered presentation is not stable.";
                return false;
            }

            if (!state.RenderPipelineSwitchCompleted)
            {
                errorMessage = "Unity render-pipeline switching is active; the rendered presentation is not stable.";
                return false;
            }

            if (state.QualityLevel < 0)
            {
                errorMessage = "Unity quality level is unavailable for screenshot validation.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        /// <summary> Confirms that two stable observations describe the same presentation state. </summary>
        public static bool TryValidateStable (
            PresentationState before,
            PresentationState after,
            out string errorMessage)
        {
            if (!TryValidateObservation(before, out errorMessage)
                || !TryValidateObservation(after, out errorMessage))
            {
                return false;
            }

            if (before.IsPaused != after.IsPaused)
            {
                errorMessage = "Unity pause state changed during screenshot capture.";
                return false;
            }

            if (before.QualityLevel != after.QualityLevel)
            {
                errorMessage = "Unity quality level changed during screenshot capture.";
                return false;
            }

            if (before.RenderPipelineAssetInstanceId != after.RenderPipelineAssetInstanceId
                || !ReferenceEquals(before.CurrentRenderPipeline, after.CurrentRenderPipeline))
            {
                errorMessage = "Unity active render pipeline changed during screenshot capture.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        /// <summary> Describes the minimum state needed to reject stale or transitional rendered frames. </summary>
        internal sealed record PresentationState (
            bool IsPaused,
            bool ShaderCompilationInProgress,
            bool RenderPipelineSwitchCompleted,
            int QualityLevel,
            int? RenderPipelineAssetInstanceId,
            RenderPipeline CurrentRenderPipeline);
    }
}
