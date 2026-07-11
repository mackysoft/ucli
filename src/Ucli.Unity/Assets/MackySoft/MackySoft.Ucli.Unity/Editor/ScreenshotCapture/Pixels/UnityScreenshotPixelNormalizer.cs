using System;
using System.Buffers;
using System.Threading;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MackySoft.Ucli.Unity.ScreenshotCapture.Pixels
{
    /// <summary> Normalizes one presentation texture into deterministic top-down RGBA8 sRGB bytes. </summary>
    internal static class UnityScreenshotPixelNormalizer
    {
        private const string NormalizeShaderName = "Hidden/uCLI/ScreenshotNormalize";

        private static readonly GraphicsFormat StagingGraphicsFormat = GraphicsFormat.R8G8B8A8_SRGB;

        public static UnityScreenshotNormalizationResult Normalize (
            Texture source,
            int width,
            int height,
            Vector4 sourceUvTransform,
            string colorSpace,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
                cancellationToken.ThrowIfCancellationRequested();

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

                cancellationToken.ThrowIfCancellationRequested();
                if (!TryDraw(
                    material,
                    shaderPass: 0,
                    staging,
                    colorSpace,
                    cancellationToken,
                    out var drawError))
                {
                    return Unsupported(drawError);
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (!TryReadRaw(
                    staging,
                    cancellationToken,
                    out cpuTexture,
                    out var rawBytes,
                    out var readError))
                {
                    return Unsupported(readError);
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (!TryResolveRawRowOrder(
                    material,
                    colorSpace,
                    cancellationToken,
                    out var rawIsTopDown,
                    out var calibrationError))
                {
                    return Unsupported(calibrationError);
                }

                if (!rawIsTopDown)
                {
                    ReverseRowsInPlace(rawBytes.AsSpan(), width, height, cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                return UnityScreenshotNormalizationResult.Success(
                    new UnityScreenshotNormalizationResult.NormalizedFrame(
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

        public static bool TryCreateRenderTexture (
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

        public static bool AreDimensionsSupported (int width, int height)
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

        public static string ResolveColorSpace ()
        {
            return ContractLiteralCodec.ToValue(QualitySettings.activeColorSpace == ColorSpace.Linear
                ? IpcScreenshotColorSpace.Linear
                : IpcScreenshotColorSpace.Gamma);
        }

        private static bool TryResolveRawRowOrder (
            Material material,
            string colorSpace,
            CancellationToken cancellationToken,
            out bool rawIsTopDown,
            out string errorMessage)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
                    || !TryDraw(
                        material,
                        shaderPass: 1,
                        calibration,
                        colorSpace,
                        cancellationToken,
                        out errorMessage)
                    || !TryReadRaw(
                        calibration,
                        cancellationToken,
                        out cpuTexture,
                        out var rawBytes,
                        out errorMessage))
                {
                    return false;
                }

                cancellationToken.ThrowIfCancellationRequested();
                ReadOnlySpan<byte> bottomUp = stackalloc byte[]
                {
                    255, 0, 0, 255,
                    0, 255, 0, 255,
                    0, 0, 255, 255,
                    255, 255, 0, 255,
                };
                ReadOnlySpan<byte> topDown = stackalloc byte[]
                {
                    0, 0, 255, 255,
                    255, 255, 0, 255,
                    255, 0, 0, 255,
                    0, 255, 0, 255,
                };
                if (rawBytes.AsSpan().SequenceEqual(topDown))
                {
                    rawIsTopDown = true;
                    errorMessage = null;
                    return true;
                }

                if (rawBytes.AsSpan().SequenceEqual(bottomUp))
                {
                    rawIsTopDown = false;
                    errorMessage = null;
                    return true;
                }

                errorMessage =
                    "Screenshot readback calibration did not produce a known RGBA channel and row order.";
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
            CancellationToken cancellationToken,
            out string errorMessage)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var previousActive = RenderTexture.active;
            var previousSrgbWrite = GL.sRGBWrite;
            using var commandBuffer = new CommandBuffer
            {
                name = "uCLI screenshot normalization",
            };
            try
            {
                GL.sRGBWrite = ContractLiteralCodec.Matches(
                    colorSpace,
                    IpcScreenshotColorSpace.Linear);
                commandBuffer.SetRenderTarget(destination);
                commandBuffer.SetViewport(new Rect(0f, 0f, destination.width, destination.height));
                commandBuffer.ClearRenderTarget(clearDepth: false, clearColor: true, Color.clear);
                commandBuffer.DrawProcedural(
                    Matrix4x4.identity,
                    material,
                    shaderPass,
                    MeshTopology.Triangles,
                    vertexCount: 3);
                cancellationToken.ThrowIfCancellationRequested();
                Graphics.ExecuteCommandBuffer(commandBuffer);
                cancellationToken.ThrowIfCancellationRequested();
                errorMessage = null;
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
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
            CancellationToken cancellationToken,
            out Texture2D cpuTexture,
            out byte[] rawBytes,
            out string errorMessage)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
                cancellationToken.ThrowIfCancellationRequested();
                cpuTexture.ReadPixels(
                    new Rect(0f, 0f, source.width, source.height),
                    destX: 0,
                    destY: 0,
                    recalculateMipMaps: false);
                cancellationToken.ThrowIfCancellationRequested();
                var rawData = cpuTexture.GetRawTextureData<byte>();
                var expectedLength = checked(source.width * source.height * 4);
                if (rawData.Length != expectedLength)
                {
                    errorMessage =
                        $"Screenshot readback byte count is invalid: {rawData.Length} != {expectedLength}.";
                    return false;
                }

                rawBytes = new byte[expectedLength];
                cancellationToken.ThrowIfCancellationRequested();
                rawData.CopyTo(rawBytes);
                cancellationToken.ThrowIfCancellationRequested();
                errorMessage = null;
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
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

        private static bool TryValidateStagingFormat (out string errorMessage)
        {
            if (!SystemInfo.IsFormatSupported(StagingGraphicsFormat, GraphicsFormatUsage.Sample)
                || !SystemInfo.IsFormatSupported(StagingGraphicsFormat, GraphicsFormatUsage.Render)
                || !SystemInfo.IsFormatSupported(StagingGraphicsFormat, GraphicsFormatUsage.ReadPixels))
            {
                errorMessage =
                    $"Screenshot staging format does not support sample, render, and readback usage: {StagingGraphicsFormat}.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        private static UnityScreenshotNormalizationResult Unsupported (string message)
        {
            return UnityScreenshotNormalizationResult.Failure(message);
        }

        private static void ReverseRowsInPlace (
            Span<byte> bytes,
            int width,
            int height,
            CancellationToken cancellationToken)
        {
            var rowStride = checked(width * 4);
            var rentedRow = ArrayPool<byte>.Shared.Rent(rowStride);
            try
            {
                var temporaryRow = rentedRow.AsSpan(0, rowStride);
                for (var row = 0; row < height / 2; row++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var topRow = bytes.Slice(row * rowStride, rowStride);
                    var bottomRow = bytes.Slice((height - row - 1) * rowStride, rowStride);
                    topRow.CopyTo(temporaryRow);
                    bottomRow.CopyTo(topRow);
                    temporaryRow.CopyTo(bottomRow);
                }

                cancellationToken.ThrowIfCancellationRequested();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rentedRow, clearArray: true);
            }
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
