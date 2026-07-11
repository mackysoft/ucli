using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MackySoft.Ucli.Unity.ScreenshotCapture
{
    /// <summary> Captures supported GameView and SceneView presentation surfaces through explicit GPU normalization. </summary>
    internal sealed class UnityEditorScreenshotCaptureBackend : IUnityScreenshotCaptureBackend
    {
        private const string NormalizeShaderName = "Hidden/uCLI/ScreenshotNormalize";

        private const int ResolutionRepaintAttemptCount = 16;

        private const int ResolutionRestoreTimeoutMilliseconds = 2000;

        private static readonly GraphicsFormat StagingGraphicsFormat = GraphicsFormat.R8G8B8A8_SRGB;

        private readonly UnityEditorScreenshotReflectionAdapter reflectionAdapter;

        private readonly IUnityScreenshotResolutionOrphanCleaner resolutionOrphanCleaner;

        private readonly IUnityEditorUpdateAwaiter editorUpdateAwaiter;

        /// <summary> Initializes a new Unity Editor screenshot backend. </summary>
        public UnityEditorScreenshotCaptureBackend (IUnityEditorUpdateAwaiter editorUpdateAwaiter)
            : this(
                new UnityEditorScreenshotReflectionAdapter(),
                editorUpdateAwaiter,
                new UnityScreenshotResolutionOrphanCleaner())
        {
            TryCleanupOrphanResolutionBestEffort();
        }

        /// <summary> Initializes a backend with explicit dependencies for tests. </summary>
        internal UnityEditorScreenshotCaptureBackend (
            UnityEditorScreenshotReflectionAdapter reflectionAdapter,
            IUnityEditorUpdateAwaiter editorUpdateAwaiter,
            IUnityScreenshotResolutionOrphanCleaner resolutionOrphanCleaner)
        {
            this.reflectionAdapter = reflectionAdapter ?? throw new ArgumentNullException(nameof(reflectionAdapter));
            this.editorUpdateAwaiter = editorUpdateAwaiter ?? throw new ArgumentNullException(nameof(editorUpdateAwaiter));
            this.resolutionOrphanCleaner = resolutionOrphanCleaner
                ?? throw new ArgumentNullException(nameof(resolutionOrphanCleaner));
        }

        /// <inheritdoc />
        public Task<UnityScreenshotBackendResult> CaptureAsync (
            IpcScreenshotCaptureRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.RequestedWidth.HasValue != request.RequestedHeight.HasValue
                || request.RequestedWidth.HasValue
                && (request.RequestedWidth.Value <= 0 || request.RequestedHeight!.Value <= 0))
            {
                return Task.FromResult(UnityScreenshotBackendResult.Failure(
                    UcliCoreErrorCodes.InvalidArgument,
                    "Screenshot requested dimensions must be positive and specified together."));
            }

            return request.Target switch
            {
                IpcScreenshotTargetNames.Game => CaptureGameAsync(request, cancellationToken),
                IpcScreenshotTargetNames.Scene => CaptureSceneAsync(request, cancellationToken),
                _ => Task.FromResult(UnityScreenshotBackendResult.Failure(
                    UcliCoreErrorCodes.InvalidArgument,
                    $"Unsupported screenshot target: {request.Target}.")),
            };
        }

        private async Task<UnityScreenshotBackendResult> CaptureGameAsync (
            IpcScreenshotCaptureRequest request,
            CancellationToken cancellationToken)
        {
            if (request.RequestedWidth.HasValue)
            {
                var requestedWidth = request.RequestedWidth.Value;
                var requestedHeight = request.RequestedHeight!.Value;
                if (!AreDimensionsSupported(requestedWidth, requestedHeight))
                {
                    return RequestedSizeUnsupported(requestedWidth, requestedHeight);
                }
            }

            if (!reflectionAdapter.TryGetGameViewSource(out var initialSource, out var sourceError))
            {
                return Unsupported(sourceError);
            }

            if (!TryCaptureStablePresentationState(
                out var presentationState,
                out var presentationError))
            {
                return Unsupported(presentationError);
            }

            UnityEditorScreenshotReflectionAdapter.GameViewResolutionLease resolutionLease = null;
            UnityScreenshotBackendResult captureResult = null;
            OperationCanceledException cancellationException = null;
            try
            {
                var source = initialSource;
                if (request.RequestedWidth.HasValue)
                {
                    var requestedWidth = request.RequestedWidth.Value;
                    var requestedHeight = request.RequestedHeight!.Value;
                    if (TryPrepareRequestedResolutionTransaction(
                        request,
                        out captureResult)
                        && !reflectionAdapter.TryBeginGameViewResolution(
                        initialSource.View,
                        requestedWidth,
                        requestedHeight,
                        out resolutionLease,
                        out var resolutionError))
                    {
                        captureResult = UnityScreenshotBackendResult.Failure(
                            ScreenshotErrorCodes.ScreenshotRequestedSizeUnsupported,
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
                        if (!TryValidatePresentationState(
                            presentationState,
                            out presentationError))
                        {
                            captureResult = Unsupported(presentationError);
                            break;
                        }

                        if (!reflectionAdapter.TryGetGameViewSource(out var candidate, out _))
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
                            == UnityScreenshotRequestedResolutionFreshnessTracker.Observation.ReadyForImmediateRepaint)
                        {
                            var preRepaintTexture = candidate.RenderTexture;
                            cancellationToken.ThrowIfCancellationRequested();
                            if (!reflectionAdapter.TryRepaintGameViewImmediately(
                                resolutionLease.GameView,
                                out var repaintError))
                            {
                                captureResult = Unsupported(repaintError);
                                break;
                            }

                            cancellationToken.ThrowIfCancellationRequested();
                            if (!TryValidatePresentationState(
                                presentationState,
                                out presentationError))
                            {
                                captureResult = Unsupported(presentationError);
                                break;
                            }

                            if (!reflectionAdapter.TryGetGameViewSource(
                                out candidate,
                                out _))
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
                                && ReferenceEquals(candidate.RenderTexture, preRepaintTexture))
                            {
                                source = candidate;
                                resolved = true;
                                break;
                            }
                        }
                    }

                    if (captureResult == null && !resolved)
                    {
                        captureResult = RequestedSizeUnsupported(requestedWidth, requestedHeight);
                    }
                }

                if (captureResult == null && !AreDimensionsSupported(source.Width, source.Height))
                {
                    captureResult = Unsupported(
                        "GameView presentation dimensions exceed the screenshot staging limit.");
                }

                var colorSpace = ResolveColorSpace();
                if (captureResult == null
                    && !TryValidatePresentationState(
                        presentationState,
                        out presentationError))
                {
                    captureResult = Unsupported(presentationError);
                }

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
                    captureResult = TryNormalizeAndReadback(
                        source.RenderTexture,
                        source.Width,
                        source.Height,
                        source.SourceUvTransform,
                        colorSpace);
                }

                if (captureResult.IsSuccess)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!reflectionAdapter.TryValidateGameViewSource(source, out sourceError))
                    {
                        captureResult = Unsupported(sourceError);
                    }

                    if (captureResult.IsSuccess
                        && !TryValidatePresentationState(
                            presentationState,
                            out presentationError))
                    {
                        captureResult = Unsupported(presentationError);
                    }

                    if (captureResult.IsSuccess
                        && !string.Equals(colorSpace, ResolveColorSpace(), StringComparison.Ordinal))
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
                var restoreError = await RestoreGameViewResolutionAsync(
                    resolutionLease,
                    initialSource);
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

        /// <summary> Gates orphan cleanup to the requested-resolution GameView transaction only. </summary>
        internal bool TryPrepareRequestedResolutionTransaction (
            IpcScreenshotCaptureRequest request,
            out UnityScreenshotBackendResult failureResult)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            failureResult = null;
            if (!string.Equals(request.Target, IpcScreenshotTargetNames.Game, StringComparison.Ordinal)
                || !request.RequestedWidth.HasValue)
            {
                return true;
            }

            if (resolutionOrphanCleaner.TryCleanup(out var cleanupError))
            {
                return true;
            }

            failureResult = Unsupported(
                $"Temporary GameView resolution state is not safe to modify. {cleanupError}");
            return false;
        }

        private void TryCleanupOrphanResolutionBestEffort ()
        {
            if (!resolutionOrphanCleaner.TryCleanup(out var cleanupError))
            {
                Debug.LogWarning(
                    $"uCLI deferred temporary GameView resolution cleanup. {cleanupError}");
            }
        }

        private async Task<string> RestoreGameViewResolutionAsync (
            UnityEditorScreenshotReflectionAdapter.GameViewResolutionLease resolutionLease,
            UnityEditorScreenshotReflectionAdapter.GameViewSource initialSource)
        {
            using var cleanupCancellationTokenSource =
                new CancellationTokenSource(ResolutionRestoreTimeoutMilliseconds);
            var cancellationToken = cleanupCancellationTokenSource.Token;
            var lastError = "GameView presentation dimensions did not return to their original values.";
            var freshnessTracker = new UnityScreenshotRequestedResolutionFreshnessTracker(
                initialSource.Width,
                initialSource.Height);
            var completedEditorUpdateGeneration = 0u;
            try
            {
                for (var attempt = 0; attempt < ResolutionRepaintAttemptCount; attempt++)
                {
                    if (!resolutionLease.TryRestore(out var restoreError))
                    {
                        lastError = restoreError;
                        if (!resolutionLease.CanRetryRestore)
                        {
                            break;
                        }

                        await editorUpdateAwaiter.WaitForNextUpdateAsync(cancellationToken);
                        completedEditorUpdateGeneration = unchecked(completedEditorUpdateGeneration + 1u);
                        continue;
                    }

                    resolutionLease.GameView.Repaint();
                    await editorUpdateAwaiter.WaitForNextUpdateAsync(cancellationToken);
                    completedEditorUpdateGeneration = unchecked(completedEditorUpdateGeneration + 1u);
                    if (!resolutionLease.TryValidateRestoredState(out restoreError))
                    {
                        lastError = restoreError;
                        continue;
                    }

                    if (!reflectionAdapter.TryGetGameViewSource(out var currentSource, out var sourceError))
                    {
                        lastError = sourceError;
                        continue;
                    }

                    var freshness = freshnessTracker.Observe(
                        currentSource.RenderTexture,
                        currentSource.Width,
                        currentSource.Height,
                        currentSource.RenderTexture.width,
                        currentSource.RenderTexture.height,
                        completedEditorUpdateGeneration);
                    if (freshness
                        == UnityScreenshotRequestedResolutionFreshnessTracker.Observation.WaitingForRequestedDimensions)
                    {
                        continue;
                    }

                    if (!IsSameRestoredGameViewPresentation(currentSource, initialSource))
                    {
                        lastError = "GameView presentation mapping did not return to its original state.";
                        continue;
                    }

                    if (freshness
                        == UnityScreenshotRequestedResolutionFreshnessTracker.Observation.ReadyForImmediateRepaint)
                    {
                        var preRepaintTexture = currentSource.RenderTexture;
                        if (!reflectionAdapter.TryRepaintGameViewImmediately(
                            resolutionLease.GameView,
                            out var repaintError))
                        {
                            lastError = repaintError;
                            break;
                        }

                        if (!resolutionLease.TryValidateRestoredState(out restoreError))
                        {
                            lastError = restoreError;
                            continue;
                        }

                        if (!reflectionAdapter.TryGetGameViewSource(
                            out currentSource,
                            out sourceError))
                        {
                            lastError = sourceError;
                            continue;
                        }

                        freshness = freshnessTracker.Observe(
                            currentSource.RenderTexture,
                            currentSource.Width,
                            currentSource.Height,
                            currentSource.RenderTexture.width,
                            currentSource.RenderTexture.height,
                            completedEditorUpdateGeneration);
                        if (freshness
                                == UnityScreenshotRequestedResolutionFreshnessTracker.Observation.ReadyForImmediateRepaint
                            && ReferenceEquals(currentSource.RenderTexture, preRepaintTexture)
                            && IsSameRestoredGameViewPresentation(currentSource, initialSource))
                        {
                            return null;
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                lastError = "Timed out while waiting for the original GameView presentation to be repainted.";
            }
            catch (Exception exception)
            {
                lastError = $"GameView resolution restoration verification failed. {exception.Message}";
            }

            return $"GameView resolution state could not be restored. {lastError}";
        }

        private static bool IsSameRestoredGameViewPresentation (
            UnityEditorScreenshotReflectionAdapter.GameViewSource current,
            UnityEditorScreenshotReflectionAdapter.GameViewSource initial)
        {
            return ReferenceEquals(current.View, initial.View)
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

        private Task<UnityScreenshotBackendResult> CaptureSceneAsync (
            IpcScreenshotCaptureRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request.RequestedWidth.HasValue || request.RequestedHeight.HasValue)
            {
                return Task.FromResult(UnityScreenshotBackendResult.Failure(
                    UcliCoreErrorCodes.InvalidArgument,
                    "Requested screenshot dimensions are supported only for the game target."));
            }

            if (!reflectionAdapter.TryGetSceneViewSource(out var source, out var sourceError))
            {
                return Task.FromResult(Unsupported(sourceError));
            }

            if (!TryCaptureStablePresentationState(
                out var presentationState,
                out var presentationError))
            {
                return Task.FromResult(Unsupported(presentationError));
            }

            if (!AreDimensionsSupported(source.FramebufferWidth, source.FramebufferHeight)
                || !AreDimensionsSupported(source.ContentWidth, source.ContentHeight))
            {
                return Task.FromResult(Unsupported(
                    "SceneView presentation dimensions exceed the screenshot staging limit."));
            }

            RenderTexture framebuffer = null;
            var previousActive = RenderTexture.active;
            var previousSrgbWrite = GL.sRGBWrite;
            try
            {
                var colorSpace = ResolveColorSpace();
                if (!UnityScreenshotSourceFormatPolicy.TryResolveSceneFramebufferFormat(
                    out var framebufferGraphicsFormat,
                    out var framebufferFormatError))
                {
                    return Task.FromResult(Unsupported(framebufferFormatError));
                }

                if (!TryCreateRenderTexture(
                    source.FramebufferWidth,
                    source.FramebufferHeight,
                    "uCLI SceneView framebuffer",
                    framebufferGraphicsFormat,
                    out framebuffer,
                    out var renderTextureError))
                {
                    return Task.FromResult(Unsupported(renderTextureError));
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (!reflectionAdapter.TryCaptureSceneViewFramebuffer(
                    source,
                    framebuffer,
                    out var captureError))
                {
                    return Task.FromResult(Unsupported(captureError));
                }

                var normalizeResult = TryNormalizeAndReadback(
                    framebuffer,
                    source.ContentWidth,
                    source.ContentHeight,
                    source.SourceUvTransform,
                    colorSpace);
                if (!normalizeResult.IsSuccess)
                {
                    return Task.FromResult(normalizeResult);
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (!reflectionAdapter.TryValidateSceneViewSource(source, out sourceError))
                {
                    return Task.FromResult(Unsupported(sourceError));
                }

                if (!TryValidatePresentationState(
                    presentationState,
                    out presentationError))
                {
                    return Task.FromResult(Unsupported(presentationError));
                }

                if (!string.Equals(colorSpace, ResolveColorSpace(), StringComparison.Ordinal))
                {
                    return Task.FromResult(Unsupported(
                        "Unity project color space changed while SceneView pixels were captured."));
                }

                return Task.FromResult(normalizeResult);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return Task.FromResult(UnityScreenshotBackendResult.Failure(
                    UcliCoreErrorCodes.InternalError,
                    $"SceneView screenshot capture failed. {exception.Message}"));
            }
            finally
            {
                RenderTexture.active = previousActive;
                GL.sRGBWrite = previousSrgbWrite;
                DestroyImmediate(framebuffer);
            }
        }

        private static UnityScreenshotBackendResult TryNormalizeAndReadback (
            Texture source,
            int width,
            int height,
            Vector4 sourceUvTransform,
            string colorSpace)
        {
            if (source == null)
            {
                return Unsupported("Screenshot source texture is unavailable.");
            }

            if (!SystemInfo.IsFormatSupported(source.graphicsFormat, GraphicsFormatUsage.Sample))
            {
                return Unsupported($"Screenshot source format is not sampleable: {source.graphicsFormat}.");
            }

            if (!TryValidateStagingFormat(out var formatError))
            {
                return Unsupported(formatError);
            }

            var shader = Shader.Find(NormalizeShaderName);
            if (shader == null || !shader.isSupported)
            {
                return Unsupported($"Screenshot normalization shader is unavailable: {NormalizeShaderName}.");
            }

            Material material = null;
            RenderTexture staging = null;
            Texture2D cpuTexture = null;
            try
            {
                material = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };
                material.SetTexture("_SourceTex", source);
                material.SetVector("_SourceUvTransform", sourceUvTransform);

                if (!TryCreateRenderTexture(
                    width,
                    height,
                    "uCLI screenshot staging",
                    StagingGraphicsFormat,
                    out staging,
                    out var stagingError))
                {
                    return Unsupported(stagingError);
                }

                if (!TryDraw(material, shaderPass: 0, staging, colorSpace, out var drawError))
                {
                    return Unsupported(drawError);
                }

                if (!TryReadRaw(staging, out cpuTexture, out var rawBytes, out var readError))
                {
                    return Unsupported(readError);
                }

                if (!TryResolveRawRowOrder(material, colorSpace, out var rawIsTopDown, out var calibrationError))
                {
                    return Unsupported(calibrationError);
                }

                if (!rawIsTopDown)
                {
                    ReverseRowsInPlace(rawBytes, width, height);
                }

                return UnityScreenshotBackendResult.Success(
                    new UnityScreenshotBackendResult.CapturedFrame(
                        width,
                        height,
                        colorSpace,
                        rawBytes));
            }
            finally
            {
                DestroyImmediate(cpuTexture);
                DestroyImmediate(staging);
                DestroyImmediate(material);
            }
        }

        private static bool TryResolveRawRowOrder (
            Material material,
            string colorSpace,
            out bool rawIsTopDown,
            out string errorMessage)
        {
            rawIsTopDown = false;
            RenderTexture calibration = null;
            Texture2D cpuTexture = null;
            try
            {
                if (!TryCreateRenderTexture(
                    width: 2,
                    height: 2,
                    "uCLI screenshot calibration",
                    StagingGraphicsFormat,
                    out calibration,
                    out errorMessage)
                    || !TryDraw(material, shaderPass: 1, calibration, colorSpace, out errorMessage)
                    || !TryReadRaw(calibration, out cpuTexture, out var rawBytes, out errorMessage))
                {
                    return false;
                }

                var bottomUp = new byte[]
                {
                    255, 0, 0, 255,
                    0, 255, 0, 255,
                    0, 0, 255, 255,
                    255, 255, 0, 255,
                };
                var topDown = new byte[]
                {
                    0, 0, 255, 255,
                    255, 255, 0, 255,
                    255, 0, 0, 255,
                    0, 255, 0, 255,
                };
                if (BytesEqual(rawBytes, topDown))
                {
                    rawIsTopDown = true;
                    errorMessage = null;
                    return true;
                }

                if (BytesEqual(rawBytes, bottomUp))
                {
                    rawIsTopDown = false;
                    errorMessage = null;
                    return true;
                }

                errorMessage = "Screenshot readback calibration did not produce a known RGBA channel and row order.";
                return false;
            }
            finally
            {
                DestroyImmediate(cpuTexture);
                DestroyImmediate(calibration);
            }
        }

        private static bool TryDraw (
            Material material,
            int shaderPass,
            RenderTexture destination,
            string colorSpace,
            out string errorMessage)
        {
            var previousActive = RenderTexture.active;
            var previousSrgbWrite = GL.sRGBWrite;
            using var commandBuffer = new CommandBuffer
            {
                name = "uCLI screenshot normalization",
            };
            try
            {
                GL.sRGBWrite = string.Equals(
                    colorSpace,
                    IpcScreenshotColorSpaceNames.Linear,
                    StringComparison.Ordinal);
                commandBuffer.SetRenderTarget(destination);
                commandBuffer.SetViewport(new Rect(0f, 0f, destination.width, destination.height));
                commandBuffer.ClearRenderTarget(clearDepth: false, clearColor: true, Color.clear);
                commandBuffer.DrawProcedural(
                    Matrix4x4.identity,
                    material,
                    shaderPass,
                    MeshTopology.Triangles,
                    vertexCount: 3);
                Graphics.ExecuteCommandBuffer(commandBuffer);
                errorMessage = null;
                return true;
            }
            catch (Exception exception)
            {
                errorMessage = $"Screenshot GPU normalization failed. {exception.Message}";
                return false;
            }
            finally
            {
                RenderTexture.active = previousActive;
                GL.sRGBWrite = previousSrgbWrite;
            }
        }

        private static bool TryReadRaw (
            RenderTexture source,
            out Texture2D cpuTexture,
            out byte[] rawBytes,
            out string errorMessage)
        {
            cpuTexture = null;
            rawBytes = null;
            var previousActive = RenderTexture.active;
            try
            {
                cpuTexture = new Texture2D(
                    source.width,
                    source.height,
                    StagingGraphicsFormat,
                    TextureCreationFlags.None)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };
                RenderTexture.active = source;
                cpuTexture.ReadPixels(
                    new Rect(0f, 0f, source.width, source.height),
                    destX: 0,
                    destY: 0,
                    recalculateMipMaps: false);
                var rawData = cpuTexture.GetRawTextureData<byte>();
                var expectedLength = checked(source.width * source.height * 4);
                if (rawData.Length != expectedLength)
                {
                    errorMessage = $"Screenshot readback byte count is invalid: {rawData.Length} != {expectedLength}.";
                    return false;
                }

                rawBytes = new byte[expectedLength];
                rawData.CopyTo(rawBytes);
                errorMessage = null;
                return true;
            }
            catch (Exception exception)
            {
                errorMessage = $"Screenshot synchronous pixel readback failed. {exception.Message}";
                return false;
            }
            finally
            {
                RenderTexture.active = previousActive;
            }
        }

        private static bool TryCreateRenderTexture (
            int width,
            int height,
            string name,
            GraphicsFormat graphicsFormat,
            out RenderTexture renderTexture,
            out string errorMessage)
        {
            renderTexture = null;
            try
            {
                var descriptor = new RenderTextureDescriptor(
                    width,
                    height,
                    graphicsFormat,
                    depthBufferBits: 0)
                {
                    dimension = TextureDimension.Tex2D,
                    msaaSamples = 1,
                    volumeDepth = 1,
                    useMipMap = false,
                    autoGenerateMips = false,
                    enableRandomWrite = false,
                    useDynamicScale = false,
                    bindMS = false,
                };
                renderTexture = new RenderTexture(descriptor)
                {
                    name = name,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                if (!renderTexture.Create()
                    || !renderTexture.IsCreated()
                    || renderTexture.width != width
                    || renderTexture.height != height
                    || renderTexture.graphicsFormat != graphicsFormat
                    || renderTexture.antiAliasing != 1)
                {
                    DestroyImmediate(renderTexture);
                    renderTexture = null;
                    errorMessage =
                        $"Unity could not create the exact screenshot RenderTexture format: {graphicsFormat}.";
                    return false;
                }

                errorMessage = null;
                return true;
            }
            catch (Exception exception)
            {
                DestroyImmediate(renderTexture);
                renderTexture = null;
                errorMessage = $"Screenshot staging RenderTexture creation failed. {exception.Message}";
                return false;
            }
        }

        private static bool TryValidateStagingFormat (out string errorMessage)
        {
            if (!SystemInfo.IsFormatSupported(StagingGraphicsFormat, GraphicsFormatUsage.Sample)
                || !SystemInfo.IsFormatSupported(StagingGraphicsFormat, GraphicsFormatUsage.Render)
                || !SystemInfo.IsFormatSupported(StagingGraphicsFormat, GraphicsFormatUsage.ReadPixels))
            {
                errorMessage = $"Screenshot staging format does not support sample, render, and readback usage: {StagingGraphicsFormat}.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        private static bool AreDimensionsSupported (int width, int height)
        {
            if (width <= 0
                || height <= 0
                || width > IpcScreenshotCaptureLimits.MaximumDimension
                || height > IpcScreenshotCaptureLimits.MaximumDimension
                || width > SystemInfo.maxTextureSize
                || height > SystemInfo.maxTextureSize)
            {
                return false;
            }

            try
            {
                return checked((long)width * height * 4L)
                    <= IpcScreenshotCaptureLimits.MaximumRawImageBytes;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        private static bool TryCaptureStablePresentationState (
            out UnityScreenshotPresentationStateFence.PresentationState state,
            out string errorMessage)
        {
            return UnityScreenshotPresentationStateFence.TryCaptureCurrent(
                    out state,
                    out errorMessage)
                && UnityScreenshotPresentationStateFence.TryValidateObservation(
                    state,
                    out errorMessage);
        }

        private static bool TryValidatePresentationState (
            UnityScreenshotPresentationStateFence.PresentationState expected,
            out string errorMessage)
        {
            if (!UnityScreenshotPresentationStateFence.TryCaptureCurrent(
                out var current,
                out errorMessage))
            {
                return false;
            }

            return UnityScreenshotPresentationStateFence.TryValidateStable(
                expected,
                current,
                out errorMessage);
        }

        private static string ResolveColorSpace ()
        {
            return QualitySettings.activeColorSpace == ColorSpace.Linear
                ? IpcScreenshotColorSpaceNames.Linear
                : IpcScreenshotColorSpaceNames.Gamma;
        }

        private static UnityScreenshotBackendResult RequestedSizeUnsupported (
            int width,
            int height)
        {
            return UnityScreenshotBackendResult.Failure(
                ScreenshotErrorCodes.ScreenshotRequestedSizeUnsupported,
                $"GameView could not establish the exact requested screenshot resolution: {width}x{height}.");
        }

        private static UnityScreenshotBackendResult Unsupported (string message)
        {
            return UnityScreenshotBackendResult.Failure(
                ScreenshotErrorCodes.ScreenshotCaptureUnsupported,
                message);
        }

        private static void ReverseRowsInPlace (
            byte[] bytes,
            int width,
            int height)
        {
            var rowStride = checked(width * 4);
            var temporaryRow = new byte[rowStride];
            for (var row = 0; row < height / 2; row++)
            {
                var topOffset = row * rowStride;
                var bottomOffset = (height - row - 1) * rowStride;
                Buffer.BlockCopy(
                    bytes,
                    topOffset,
                    temporaryRow,
                    0,
                    rowStride);
                Buffer.BlockCopy(bytes, bottomOffset, bytes, topOffset, rowStride);
                Buffer.BlockCopy(temporaryRow, 0, bytes, bottomOffset, rowStride);
            }
        }

        private static bool BytesEqual (byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            for (var i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static void DestroyImmediate (Object value)
        {
            if (value != null)
            {
                Object.DestroyImmediate(value);
            }
        }
    }
}
