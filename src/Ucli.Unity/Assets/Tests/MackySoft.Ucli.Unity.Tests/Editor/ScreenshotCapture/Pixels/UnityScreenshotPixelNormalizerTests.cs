using System;
using System.Threading;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.ScreenshotCapture.Pixels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityScreenshotPixelNormalizerTests
    {
        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenAlreadyCancelled_StopsBeforeChangingGlobalRenderState ()
        {
            var source = CreateRenderTexture();
            var previousSrgbWrite = GL.sRGBWrite;
            try
            {
                RenderTexture.active = source;
                GL.sRGBWrite = false;
                using var cancellationTokenSource = new CancellationTokenSource();
                cancellationTokenSource.Cancel();

                Assert.Catch<OperationCanceledException>(() =>
                    UnityScreenshotPixelNormalizer.Normalize(
                        source,
                        width: 2,
                        height: 2,
                        new Vector4(1f, 1f, 0f, 0f),
                        IpcScreenshotColorSpace.Linear,
                        cancellationTokenSource.Token));

                Assert.That(RenderTexture.active, Is.SameAs(source));
                Assert.That(GL.sRGBWrite, Is.False);
            }
            finally
            {
                RenderTexture.active = null;
                GL.sRGBWrite = previousSrgbWrite;
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        private static RenderTexture CreateRenderTexture ()
        {
            var renderTexture = new RenderTexture(new RenderTextureDescriptor(
                2,
                2,
                GraphicsFormat.R8G8B8A8_SRGB,
                depthBufferBits: 0));
            Assert.That(renderTexture.Create(), Is.True);
            return renderTexture;
        }
    }
}
