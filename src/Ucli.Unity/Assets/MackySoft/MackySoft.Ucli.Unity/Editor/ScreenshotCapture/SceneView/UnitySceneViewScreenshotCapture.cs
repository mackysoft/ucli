using System;
using System.Threading;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.ScreenshotCapture.Capture;
using MackySoft.Ucli.Unity.ScreenshotCapture.Pixels;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MackySoft.Ucli.Unity.ScreenshotCapture.SceneView
{
    /// <summary> Captures the selected SceneView's target-included presentation surface. </summary>
    internal sealed class UnitySceneViewScreenshotCapture
    {
        private readonly UnitySceneViewPresentationAdapter presentationAdapter;

        public UnitySceneViewScreenshotCapture (UnitySceneViewPresentationAdapter presentationAdapter)
        {
            this.presentationAdapter = presentationAdapter
                ?? throw new ArgumentNullException(nameof(presentationAdapter));
        }

        public UnityScreenshotBackendResult Capture (
            IpcScreenshotCaptureRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.RequestedWidth.HasValue || request.RequestedHeight.HasValue)
            {
                return UnityScreenshotBackendResult.Failure(
                    UcliCoreErrorCodes.InvalidArgument,
                    "Requested screenshot dimensions are supported only for the game target.");
            }

            if (!presentationAdapter.TryGetSource(out var source, out var sourceError))
            {
                return Unsupported(sourceError);
            }

            var mapping = source.Capability.Mapping;
            if (!UnityScreenshotPixelNormalizer.AreDimensionsSupported(
                    mapping.FramebufferWidth,
                    mapping.FramebufferHeight)
                || !UnityScreenshotPixelNormalizer.AreDimensionsSupported(
                    mapping.ContentWidth,
                    mapping.ContentHeight))
            {
                return Unsupported(
                    "SceneView presentation dimensions exceed the screenshot staging limit.");
            }

            RenderTexture framebuffer = null;
            var previousActive = RenderTexture.active;
            var previousSrgbWrite = GL.sRGBWrite;
            try
            {
                var colorSpace = UnityScreenshotPixelNormalizer.ResolveColorSpace();
                if (!UnityScreenshotPixelNormalizer.TryCreateRenderTexture(
                    mapping.FramebufferWidth,
                    mapping.FramebufferHeight,
                    "uCLI SceneView framebuffer",
                    source.Capability.FramebufferGraphicsFormat,
                    out framebuffer,
                    out var renderTextureError))
                {
                    return Unsupported(renderTextureError);
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (!presentationAdapter.TryCaptureFramebuffer(
                    source,
                    framebuffer,
                    out var captureError))
                {
                    return Unsupported(captureError);
                }

                var normalizeResult = UnityScreenshotPixelNormalizer.Normalize(
                    framebuffer,
                    mapping.ContentWidth,
                    mapping.ContentHeight,
                    mapping.SourceUvTransform,
                    colorSpace,
                    cancellationToken);
                if (!normalizeResult.IsSuccess)
                {
                    return Unsupported(normalizeResult.ErrorMessage);
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (!presentationAdapter.TryValidateSource(source, out sourceError))
                {
                    return Unsupported(sourceError);
                }

                if (colorSpace != UnityScreenshotPixelNormalizer.ResolveColorSpace())
                {
                    return Unsupported(
                        "Unity project color space changed while SceneView pixels were captured.");
                }

                return UnityScreenshotBackendResult.Success(normalizeResult.Frame);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return UnityScreenshotBackendResult.Failure(
                    UcliCoreErrorCodes.InternalError,
                    $"SceneView screenshot capture failed. {exception.Message}");
            }
            finally
            {
                RenderTexture.active = previousActive;
                GL.sRGBWrite = previousSrgbWrite;
                if (framebuffer != null)
                {
                    Object.DestroyImmediate(framebuffer);
                }
            }
        }

        private static UnityScreenshotBackendResult Unsupported (string message)
        {
            return UnityScreenshotBackendResult.Failure(
                ScreenshotErrorCodes.ScreenshotCaptureUnsupported,
                message);
        }
    }
}
