using System;
using System.Collections.Generic;
using MackySoft.Ucli.Unity.ScreenshotCapture.GameView.Resolution;
using UnityEditor;
using UnityEngine;

namespace MackySoft.Ucli.Unity.ScreenshotCapture.GameView
{
    /// <summary> Retains ownership until restored GameView resolution dimensions produce a fresh presentation texture. </summary>
    internal sealed class GameViewResolutionPresentationRecovery
    {
        private const int DeferredRecoveryUpdateInterval = 30;

        private static readonly Dictionary<int, GameViewResolutionPresentationRecovery> ActiveRecoveries = new();

        private readonly EditorWindow gameView;

        private readonly int gameViewInstanceId;

        private readonly IGameViewPresentationAdapter presentationAdapter;

        private readonly UnityScreenshotRequestedResolutionFreshnessTracker freshnessTracker;

        private uint completedEditorUpdateGeneration;

        private int deferredRecoveryUpdateCountdown;

        private bool isReserved;

        private bool isScheduled;

        private bool isTerminal;

        public GameViewResolutionPresentationRecovery (
            GameViewPresentationSource originalSource,
            IGameViewPresentationAdapter presentationAdapter)
        {
            if (originalSource == null)
            {
                throw new ArgumentNullException(nameof(originalSource));
            }

            if (originalSource.View == null)
            {
                throw new ArgumentException(
                    "Original GameView presentation source must have a live target.",
                    nameof(originalSource));
            }

            gameView = originalSource.View;
            gameViewInstanceId = gameView.GetInstanceID();
            this.presentationAdapter = presentationAdapter
                ?? throw new ArgumentNullException(nameof(presentationAdapter));
            freshnessTracker = new UnityScreenshotRequestedResolutionFreshnessTracker(
                originalSource.Width,
                originalSource.Height);
        }

        public bool IsPending => !isTerminal;

        internal bool IsScheduled => isScheduled;

        /// <summary> Releases ownership when an external state change makes original-resolution recovery inapplicable. </summary>
        public void ReleaseOwnership ()
        {
            Complete();
        }

        /// <summary> Reserves this target before the resolution transaction releases its durable ownership marker. </summary>
        public bool TryReserve (out string errorMessage)
        {
            if (isTerminal)
            {
                errorMessage = "GameView presentation recovery is no longer active.";
                return false;
            }

            if (isReserved)
            {
                errorMessage = null;
                return true;
            }

            if (gameView == null)
            {
                errorMessage = "The target GameView was destroyed before presentation recovery ownership could be reserved.";
                Complete();
                return false;
            }

            if (ActiveRecoveries.TryGetValue(gameViewInstanceId, out var existing)
                && !ReferenceEquals(existing, this))
            {
                errorMessage = "Another recovery already owns the target GameView presentation.";
                return false;
            }

            ActiveRecoveries[gameViewInstanceId] = this;
            isReserved = true;
            errorMessage = null;
            return true;
        }

        public bool TryRequestRepaint (out string errorMessage)
        {
            if (gameView == null)
            {
                errorMessage = "The target GameView was destroyed before presentation recovery completed.";
                Complete();
                return false;
            }

            try
            {
                gameView.Repaint();
                errorMessage = null;
                return true;
            }
            catch (Exception exception)
            {
                errorMessage = $"GameView presentation recovery repaint failed. {exception.Message}";
                return false;
            }
        }

        public Observation ObserveAfterEditorUpdate (
            out GameViewPresentationSource restoredSource,
            out string errorMessage)
        {
            restoredSource = null;
            if (isTerminal)
            {
                errorMessage = "GameView presentation recovery is no longer active.";
                return Observation.TargetUnavailable;
            }

            if (gameView == null)
            {
                errorMessage = "The target GameView was destroyed before presentation recovery completed.";
                Complete();
                return Observation.TargetUnavailable;
            }

            completedEditorUpdateGeneration = unchecked(completedEditorUpdateGeneration + 1u);
            if (!presentationAdapter.TryGetSource(out var currentSource, out errorMessage))
            {
                if (!presentationAdapter.IsCurrentTarget(gameView))
                {
                    errorMessage = "The target GameView is no longer the current presentation target.";
                    Complete();
                    return Observation.TargetUnavailable;
                }

                return Observation.Waiting;
            }

            if (currentSource.View != gameView)
            {
                errorMessage = "The selected GameView changed before presentation recovery completed.";
                Complete();
                return Observation.TargetUnavailable;
            }

            var freshness = ObserveFreshness(currentSource);
            if (freshness
                != UnityScreenshotRequestedResolutionFreshnessTracker.Observation.ReadyForImmediateRepaint)
            {
                errorMessage = "GameView presentation dimensions have not produced a fresh restored frame.";
                return Observation.Waiting;
            }

            var preRepaintTexture = currentSource.RenderTexture;
            if (!presentationAdapter.TryRepaintImmediately(gameView, out errorMessage))
            {
                return Observation.Waiting;
            }

            if (!presentationAdapter.TryGetSource(out currentSource, out errorMessage))
            {
                return Observation.Waiting;
            }

            if (currentSource.View != gameView)
            {
                errorMessage = "The selected GameView changed during presentation recovery repaint.";
                Complete();
                return Observation.TargetUnavailable;
            }

            freshness = ObserveFreshness(currentSource);
            if (freshness
                    != UnityScreenshotRequestedResolutionFreshnessTracker.Observation.ReadyForImmediateRepaint
                || currentSource.RenderTexture != preRepaintTexture)
            {
                errorMessage = "GameView presentation changed during its recovery repaint.";
                return Observation.Waiting;
            }

            restoredSource = currentSource;
            errorMessage = null;
            Complete();
            return Observation.RestoredResolutionPresented;
        }

        public bool TrySchedule (out string errorMessage)
        {
            if (isScheduled)
            {
                errorMessage = null;
                return true;
            }

            if (!TryReserve(out errorMessage))
            {
                return false;
            }

            isScheduled = true;
            EditorApplication.update += RetryDeferredRecovery;
            AssemblyReloadEvents.beforeAssemblyReload += Complete;
            EditorApplication.quitting += Complete;
            errorMessage = null;
            return true;
        }

        internal void RetryDeferredRecovery ()
        {
            if (!isScheduled)
            {
                return;
            }

            if (gameView == null)
            {
                Complete();
                return;
            }

            if (deferredRecoveryUpdateCountdown > 0)
            {
                deferredRecoveryUpdateCountdown--;
                return;
            }

            deferredRecoveryUpdateCountdown = DeferredRecoveryUpdateInterval - 1;
            TryRequestRepaint(out _);
            if (!isTerminal)
            {
                ObserveAfterEditorUpdate(out _, out _);
            }
        }

        public static bool HasPending (EditorWindow target)
        {
            if (target == null)
            {
                return false;
            }

            return ActiveRecoveries.TryGetValue(target.GetInstanceID(), out var recovery)
                && recovery.isReserved
                && recovery.gameView == target;
        }

        internal static void ClearForTests ()
        {
            foreach (var recovery in new List<GameViewResolutionPresentationRecovery>(ActiveRecoveries.Values))
            {
                recovery.Complete();
            }

            ActiveRecoveries.Clear();
        }

        private UnityScreenshotRequestedResolutionFreshnessTracker.Observation ObserveFreshness (
            GameViewPresentationSource source)
        {
            return freshnessTracker.Observe(
                source.RenderTexture,
                source.Width,
                source.Height,
                source.RenderTexture.width,
                source.RenderTexture.height,
                completedEditorUpdateGeneration);
        }

        private void Complete ()
        {
            if (isTerminal)
            {
                return;
            }

            isTerminal = true;
            if (isReserved)
            {
                if (ActiveRecoveries.TryGetValue(gameViewInstanceId, out var active)
                    && ReferenceEquals(active, this))
                {
                    ActiveRecoveries.Remove(gameViewInstanceId);
                }
            }

            EditorApplication.update -= RetryDeferredRecovery;
            AssemblyReloadEvents.beforeAssemblyReload -= Complete;
            EditorApplication.quitting -= Complete;
            isReserved = false;
            isScheduled = false;
        }

        internal enum Observation
        {
            Waiting,
            RestoredResolutionPresented,
            TargetUnavailable,
        }
    }
}
