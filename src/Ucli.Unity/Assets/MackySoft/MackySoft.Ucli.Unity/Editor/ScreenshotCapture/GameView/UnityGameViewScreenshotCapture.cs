using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using MackySoft.Ucli.Unity.ScreenshotCapture.Capture;
using MackySoft.Ucli.Unity.ScreenshotCapture.GameView.Resolution;
using MackySoft.Ucli.Unity.ScreenshotCapture.Pixels;
using UnityEngine;

namespace MackySoft.Ucli.Unity.ScreenshotCapture.GameView
{
    /// <summary> Captures GameView presentation pixels, including reversible requested-resolution transactions. </summary>
    internal sealed class UnityGameViewScreenshotCapture
    {
        private const int ResolutionRepaintAttemptCount = 16;

        private const int ResolutionRestoreTimeoutMilliseconds = 2000;

        private readonly UnityGameViewPresentationAdapter presentationAdapter;

        private readonly UnityGameViewResolutionAdapter resolutionAdapter;

        private readonly IUnityEditorUpdateAwaiter editorUpdateAwaiter;

        public UnityGameViewScreenshotCapture (
            UnityGameViewPresentationAdapter presentationAdapter,
            UnityGameViewResolutionAdapter resolutionAdapter,
            IUnityEditorUpdateAwaiter editorUpdateAwaiter)
        {
            this.presentationAdapter = presentationAdapter
                ?? throw new ArgumentNullException(nameof(presentationAdapter));
            this.resolutionAdapter = resolutionAdapter
                ?? throw new ArgumentNullException(nameof(resolutionAdapter));
            this.editorUpdateAwaiter = editorUpdateAwaiter
                ?? throw new ArgumentNullException(nameof(editorUpdateAwaiter));
        }

        public async Task<UnityScreenshotBackendResult> CaptureAsync (
            IpcScreenshotCaptureRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.RequestedWidth.HasValue)
            {
                var requestedWidth = request.RequestedWidth.Value;
                var requestedHeight = request.RequestedHeight.Value;
                if (!UnityScreenshotPixelNormalizer.AreDimensionsSupported(
                    requestedWidth,
                    requestedHeight))
                {
                    return RequestedSizeUnsupported(requestedWidth, requestedHeight);
                }
            }

            if (!presentationAdapter.TryGetSource(out var initialSource, out var sourceError))
            {
                return Unsupported(sourceError);
            }

            if (!TryValidateNoPendingRecovery(
                initialSource.View,
                resolutionAdapter,
                out var recoveryError))
            {
                return Unsupported(recoveryError);
            }

            GameViewResolutionLease resolutionLease = null;
            GameViewResolutionPresentationRecovery resolutionPresentationRecovery = null;
            UnityScreenshotBackendResult captureResult = null;
            OperationCanceledException cancellationException = null;
            try
            {
                var source = initialSource;
                if (request.RequestedWidth.HasValue)
                {
                    var requestedWidth = request.RequestedWidth.Value;
                    var requestedHeight = request.RequestedHeight.Value;
                    resolutionPresentationRecovery = new GameViewResolutionPresentationRecovery(
                        initialSource,
                        presentationAdapter);
                    if (!resolutionAdapter.TryBegin(
                        initialSource.View,
                        requestedWidth,
                        requestedHeight,
                        (out string ownershipError) =>
                            TryScheduleResolutionPresentationRecovery(
                                resolutionPresentationRecovery,
                                out ownershipError),
                        out resolutionLease,
                        out var resolutionErrorCode,
                        out var resolutionError))
                    {
                        captureResult = UnityScreenshotBackendResult.Failure(
                            resolutionErrorCode ?? UcliCoreErrorCodes.InternalError,
                            resolutionError);
                    }

                    var resolved = false;
                    var freshnessTracker = new UnityScreenshotRequestedResolutionFreshnessTracker(
                        requestedWidth,
                        requestedHeight);
                    var completedEditorUpdateGeneration = 0u;
                    for (var attempt = 0;
                        captureResult == null && attempt < ResolutionRepaintAttemptCount;
                        attempt++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        resolutionLease.GameView.Repaint();
                        await editorUpdateAwaiter.WaitForNextUpdateAsync(cancellationToken);
                        completedEditorUpdateGeneration = unchecked(completedEditorUpdateGeneration + 1u);
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!presentationAdapter.TryGetSource(out var candidate, out _))
                        {
                            continue;
                        }

                        if (candidate.TargetDisplay != initialSource.TargetDisplay)
                        {
                            captureResult = Unsupported(
                                "GameView target display changed while establishing the requested resolution.");
                            break;
                        }

                        var freshness = freshnessTracker.Observe(
                            candidate.RenderTexture,
                            candidate.Width,
                            candidate.Height,
                            candidate.RenderTexture.width,
                            candidate.RenderTexture.height,
                            completedEditorUpdateGeneration);
                        if (freshness
                            != UnityScreenshotRequestedResolutionFreshnessTracker.Observation.ReadyForImmediateRepaint)
                        {
                            continue;
                        }

                        var preRepaintTexture = candidate.RenderTexture;
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!presentationAdapter.TryRepaintImmediately(
                            resolutionLease.GameView,
                            out var repaintError))
                        {
                            captureResult = Unsupported(repaintError);
                            break;
                        }

                        cancellationToken.ThrowIfCancellationRequested();
                        if (!presentationAdapter.TryGetSource(out candidate, out _))
                        {
                            continue;
                        }

                        if (candidate.TargetDisplay != initialSource.TargetDisplay)
                        {
                            captureResult = Unsupported(
                                "GameView target display changed during its requested-resolution repaint.");
                            break;
                        }

                        freshness = freshnessTracker.Observe(
                            candidate.RenderTexture,
                            candidate.Width,
                            candidate.Height,
                            candidate.RenderTexture.width,
                            candidate.RenderTexture.height,
                            completedEditorUpdateGeneration);
                        if (freshness
                                == UnityScreenshotRequestedResolutionFreshnessTracker.Observation.ReadyForImmediateRepaint
                            && candidate.RenderTexture == preRepaintTexture)
                        {
                            source = candidate;
                            resolved = true;
                            break;
                        }
                    }

                    if (captureResult == null && !resolved)
                    {
                        captureResult = RequestedSizeUnsupported(requestedWidth, requestedHeight);
                    }
                }

                if (captureResult == null
                    && !UnityScreenshotPixelNormalizer.AreDimensionsSupported(source.Width, source.Height))
                {
                    captureResult = Unsupported(
                        "GameView presentation dimensions exceed the screenshot staging limit.");
                }

                var colorSpace = UnityScreenshotPixelNormalizer.ResolveColorSpace();
                if (captureResult == null
                    && !UnityScreenshotSourceFormatPolicy.TryValidateGameViewSource(
                        source.RenderTexture,
                        colorSpace,
                        out var formatError))
                {
                    captureResult = Unsupported(formatError);
                }

                if (captureResult == null)
                {
                    var normalizationResult = UnityScreenshotPixelNormalizer.Normalize(
                        source.RenderTexture,
                        source.Width,
                        source.Height,
                        source.SourceUvTransform,
                        colorSpace,
                        cancellationToken);
                    captureResult = ToBackendResult(normalizationResult);
                }

                if (captureResult.IsSuccess)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!presentationAdapter.TryValidateSource(source, out sourceError))
                    {
                        captureResult = Unsupported(sourceError);
                    }

                    if (captureResult.IsSuccess
                        && colorSpace != UnityScreenshotPixelNormalizer.ResolveColorSpace())
                    {
                        captureResult = Unsupported(
                            "Unity project color space changed while GameView pixels were captured.");
                    }
                }
            }
            catch (OperationCanceledException exception)
            {
                cancellationException = exception;
            }
            catch (Exception exception)
            {
                captureResult = UnityScreenshotBackendResult.Failure(
                    UcliCoreErrorCodes.InternalError,
                    $"GameView screenshot capture failed. {exception.Message}");
            }

            if (resolutionLease != null)
            {
                var restoreError = await RestoreResolutionAsync(
                    resolutionLease,
                    initialSource,
                    resolutionPresentationRecovery);
                if (restoreError != null)
                {
                    return Unsupported(restoreError);
                }
            }

            if (cancellationException != null)
            {
                throw cancellationException;
            }

            return captureResult ?? UnityScreenshotBackendResult.Failure(
                UcliCoreErrorCodes.InternalError,
                "GameView screenshot capture ended without a result.");
        }

        private async Task<string> RestoreResolutionAsync (
            GameViewResolutionLease resolutionLease,
            GameViewPresentationSource initialSource,
            GameViewResolutionPresentationRecovery presentationRecovery)
        {
            using var cleanupCancellationTokenSource =
                new CancellationTokenSource(ResolutionRestoreTimeoutMilliseconds);
            var cancellationToken = cleanupCancellationTokenSource.Token;
            var lastError = "GameView presentation dimensions did not return to their original values.";
            var lastRestoreOutcome = GameViewResolutionLease.RestoreOutcome.Retryable;
            try
            {
                for (var attempt = 0; attempt < ResolutionRepaintAttemptCount; attempt++)
                {
                    lastRestoreOutcome = resolutionLease.TryRestore(
                        (out string ownershipError) => presentationRecovery.TryReserve(
                            out ownershipError),
                        out var restoreError);
                    if (lastRestoreOutcome == GameViewResolutionLease.RestoreOutcome.Retryable)
                    {
                        lastError = restoreError;
                        if (!resolutionLease.CanRetryRestore)
                        {
                            break;
                        }

                        await editorUpdateAwaiter.WaitForNextUpdateAsync(cancellationToken);
                        continue;
                    }

                    if (lastRestoreOutcome
                        != GameViewResolutionLease.RestoreOutcome.RestoredOriginal)
                    {
                        lastError = restoreError;
                        presentationRecovery.ReleaseOwnership();
                        presentationRecovery = null;
                        break;
                    }

                    if (!presentationRecovery.TryRequestRepaint(out var repaintRequestError))
                    {
                        lastError = repaintRequestError;
                        if (!presentationRecovery.IsPending)
                        {
                            presentationRecovery = null;
                            break;
                        }
                    }

                    await editorUpdateAwaiter.WaitForNextUpdateAsync(cancellationToken);
                    if (!resolutionLease.TryValidateRestoredState(out restoreError))
                    {
                        lastError = restoreError;
                        if (!resolutionLease.IsOriginalPresentationRecoveryApplicable())
                        {
                            presentationRecovery.ReleaseOwnership();
                            presentationRecovery = null;
                        }

                        break;
                    }

                    var recoveryObservation = presentationRecovery.ObserveAfterEditorUpdate(
                        out var restoredSource,
                        out var recoveryError);
                    if (recoveryObservation == GameViewResolutionPresentationRecovery.Observation.Waiting)
                    {
                        lastError = recoveryError;
                        continue;
                    }

                    if (recoveryObservation == GameViewResolutionPresentationRecovery.Observation.TargetUnavailable)
                    {
                        lastError = recoveryError;
                        presentationRecovery = null;
                        break;
                    }

                    if (!IsSameRestoredPresentation(restoredSource, initialSource))
                    {
                        lastError = "GameView presentation mapping did not return to its original state.";
                        break;
                    }

                    return null;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                lastError =
                    "Timed out while waiting for the original GameView presentation to be repainted.";
            }
            catch (Exception exception)
            {
                lastError =
                    $"GameView resolution restoration verification failed. {exception.Message}";
            }

            if (resolutionLease.CanRetryRestore)
            {
                var retainedPresentationRecovery = presentationRecovery;
                resolutionLease.ScheduleDeferredRecovery(
                    (out string ownershipError) =>
                        TryScheduleResolutionPresentationRecovery(
                            retainedPresentationRecovery,
                            out ownershipError));
            }
            else if (lastRestoreOutcome == GameViewResolutionLease.RestoreOutcome.RestoredOriginal
                && presentationRecovery?.IsPending == true
                && !presentationRecovery.TrySchedule(out var scheduleError))
            {
                lastError = $"{lastError} Deferred presentation recovery could not start. {scheduleError}";
            }

            return $"GameView resolution state could not be restored. {lastError}";
        }

        private static bool TryScheduleResolutionPresentationRecovery (
            GameViewResolutionPresentationRecovery recovery,
            out string errorMessage)
        {
            if (recovery == null)
            {
                errorMessage =
                    "The restored GameView resolution has no presentation recovery owner.";
                return false;
            }

            return recovery.TrySchedule(out errorMessage);
        }

        internal static bool TryValidateNoPendingRecovery (
            UnityEditor.EditorWindow gameView,
            UnityGameViewResolutionAdapter resolutionAdapter,
            out string errorMessage)
        {
            if (resolutionAdapter == null)
            {
                throw new ArgumentNullException(nameof(resolutionAdapter));
            }

            if (GameViewResolutionPresentationRecovery.HasPending(gameView))
            {
                errorMessage =
                    "A previous requested-resolution GameView presentation is still being restored.";
                return false;
            }

            if (GameViewResolutionLease.HasActiveOwnership)
            {
                errorMessage =
                    "A previous temporary GameView resolution transaction is still being restored.";
                return false;
            }

            if (!UnityScreenshotResolutionLeaseRegistry.TryRead(
                out var pendingResolutionTransactions,
                out var resolutionRegistryError))
            {
                errorMessage =
                    $"Temporary GameView resolution ownership could not be verified. {resolutionRegistryError}";
                return false;
            }

            if (pendingResolutionTransactions.Count != 0)
            {
                if (!resolutionAdapter.TryCleanupPendingOwnership(out var cleanupError))
                {
                    errorMessage =
                        $"A previous temporary GameView resolution transaction could not be restored. {cleanupError}";
                    return false;
                }

                if (!UnityScreenshotResolutionLeaseRegistry.TryRead(
                    out pendingResolutionTransactions,
                    out resolutionRegistryError))
                {
                    errorMessage =
                        $"Temporary GameView resolution ownership could not be verified after cleanup. {resolutionRegistryError}";
                    return false;
                }

                if (pendingResolutionTransactions.Count != 0)
                {
                    errorMessage =
                        "A previous temporary GameView resolution transaction is still being restored.";
                    return false;
                }
            }

            errorMessage = null;
            return true;
        }

        private static bool IsSameRestoredPresentation (
            GameViewPresentationSource current,
            GameViewPresentationSource initial)
        {
            return current.View == initial.View
                && current.Width == initial.Width
                && current.Height == initial.Height
                && current.RenderTexture.width == initial.Width
                && current.RenderTexture.height == initial.Height
                && Mathf.Approximately(current.BackingScale, initial.BackingScale)
                && current.TargetDisplay == initial.TargetDisplay
                && current.TargetInView == initial.TargetInView
                && current.DeviceFlippedTargetInView == initial.DeviceFlippedTargetInView
                && current.SourceUvTransform == initial.SourceUvTransform;
        }

        private static UnityScreenshotBackendResult RequestedSizeUnsupported (
            int width,
            int height)
        {
            return UnityScreenshotBackendResult.Failure(
                ScreenshotErrorCodes.ScreenshotRequestedSizeUnsupported,
                $"GameView could not establish the exact requested screenshot resolution: {width}x{height}.");
        }

        private static UnityScreenshotBackendResult ToBackendResult (
            UnityScreenshotNormalizationResult normalizationResult)
        {
            if (!normalizationResult.IsSuccess)
            {
                return Unsupported(normalizationResult.ErrorMessage);
            }

            return UnityScreenshotBackendResult.Success(normalizationResult.Frame);
        }

        private static UnityScreenshotBackendResult Unsupported (string message)
        {
            return UnityScreenshotBackendResult.Failure(
                ScreenshotErrorCodes.ScreenshotCaptureUnsupported,
                message);
        }
    }
}
