using System;
using UnityEngine;

namespace MackySoft.Ucli.Unity.ScreenshotCapture.GameView.Resolution
{
    /// <summary> Requires a post-resize render generation before requested-resolution pixels are accepted. </summary>
    internal sealed class UnityScreenshotRequestedResolutionFreshnessTracker
    {
        private readonly int requestedWidth;

        private readonly int requestedHeight;

        private RenderTexture trackedTexture;

        private uint baselineCompletedEditorUpdateGeneration;

        /// <summary> Initializes a tracker for one requested GameView resolution. </summary>
        public UnityScreenshotRequestedResolutionFreshnessTracker (
            int requestedWidth,
            int requestedHeight)
        {
            if (requestedWidth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(requestedWidth));
            }

            if (requestedHeight <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(requestedHeight));
            }

            this.requestedWidth = requestedWidth;
            this.requestedHeight = requestedHeight;
        }

        /// <summary> Observes one source identity, dimensions, and render generation. </summary>
        public Observation Observe (
            RenderTexture texture,
            int width,
            int height,
            int textureWidth,
            int textureHeight,
            uint completedEditorUpdateGeneration)
        {
            if (texture == null)
            {
                throw new ArgumentNullException(nameof(texture));
            }

            if (width != requestedWidth
                || height != requestedHeight
                || textureWidth != requestedWidth
                || textureHeight != requestedHeight)
            {
                trackedTexture = null;
                baselineCompletedEditorUpdateGeneration = 0;
                return Observation.WaitingForRequestedDimensions;
            }

            if (texture != trackedTexture)
            {
                trackedTexture = texture;
                baselineCompletedEditorUpdateGeneration = completedEditorUpdateGeneration;
                return Observation.WaitingForSubsequentEditorUpdate;
            }

            return completedEditorUpdateGeneration == baselineCompletedEditorUpdateGeneration
                ? Observation.WaitingForSubsequentEditorUpdate
                : Observation.ReadyForImmediateRepaint;
        }

        /// <summary> Describes whether dimensions or a post-resize render are still pending. </summary>
        internal enum Observation
        {
            WaitingForRequestedDimensions,
            WaitingForSubsequentEditorUpdate,
            ReadyForImmediateRepaint,
        }
    }
}
