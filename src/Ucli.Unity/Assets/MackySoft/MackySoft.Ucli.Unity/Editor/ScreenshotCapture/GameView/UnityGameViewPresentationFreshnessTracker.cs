using System;
using UnityEngine;

namespace MackySoft.Ucli.Unity.ScreenshotCapture.GameView
{
    /// <summary> Requires a completed Editor update for one stable GameView presentation texture. </summary>
    internal sealed class UnityGameViewPresentationFreshnessTracker
    {
        private readonly int expectedWidth;

        private readonly int expectedHeight;

        private RenderTexture trackedTexture;

        private uint baselineCompletedEditorUpdateGeneration;

        /// <summary> Initializes a tracker for one expected GameView presentation size. </summary>
        public UnityGameViewPresentationFreshnessTracker (
            int expectedWidth,
            int expectedHeight)
        {
            if (expectedWidth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(expectedWidth));
            }

            if (expectedHeight <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(expectedHeight));
            }

            this.expectedWidth = expectedWidth;
            this.expectedHeight = expectedHeight;
        }

        /// <summary> Observes one source identity, dimensions, and completed Editor update generation. </summary>
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

            if (width != expectedWidth
                || height != expectedHeight
                || textureWidth != expectedWidth
                || textureHeight != expectedHeight)
            {
                trackedTexture = null;
                baselineCompletedEditorUpdateGeneration = 0;
                return Observation.WaitingForExpectedDimensions;
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

        /// <summary> Describes whether dimensions or a completed Editor update are still pending. </summary>
        internal enum Observation
        {
            WaitingForExpectedDimensions,
            WaitingForSubsequentEditorUpdate,
            ReadyForImmediateRepaint,
        }
    }
}
