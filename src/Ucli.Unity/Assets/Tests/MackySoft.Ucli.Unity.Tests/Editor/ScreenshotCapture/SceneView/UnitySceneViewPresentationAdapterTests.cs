using System;
using System.Reflection;
using MackySoft.Ucli.Unity.ScreenshotCapture.SceneView;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnitySceneViewPresentationAdapterTests
    {
        [Test]
        [Category("Size.Small")]
        public void TryCaptureSceneViewFramebuffer_RestoresGlobalRenderStateWhenGrabPixelsChangesIt ()
        {
            var sceneView = EditorWindow.CreateInstance<SceneView>();
            var previous = CreateRenderTexture("uCLI previous active");
            var destination = CreateRenderTexture("uCLI destination");
            var host = ScriptableObject.CreateInstance<MutatingGrabPixelsHost>();
            var previousSrgbWrite = GL.sRGBWrite;
            try
            {
                RenderTexture.active = previous;
                GL.sRGBWrite = false;
                var source = CreateSceneViewSource(
                    sceneView,
                    host,
                    nameof(MutatingGrabPixelsHost.GrabPixels));

                var result = new UnitySceneViewPresentationAdapter()
                    .TryCaptureFramebuffer(source, destination, out var errorMessage);

                Assert.That(result, Is.True, errorMessage);
                Assert.That(RenderTexture.active, Is.SameAs(previous));
                Assert.That(GL.sRGBWrite, Is.False);
            }
            finally
            {
                RenderTexture.active = null;
                GL.sRGBWrite = previousSrgbWrite;
                UnityEngine.Object.DestroyImmediate(destination);
                UnityEngine.Object.DestroyImmediate(previous);
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(sceneView);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void TryCaptureSceneViewFramebuffer_RestoresGlobalRenderStateWhenGrabPixelsThrows ()
        {
            var sceneView = EditorWindow.CreateInstance<SceneView>();
            var previous = CreateRenderTexture("uCLI previous active");
            var destination = CreateRenderTexture("uCLI destination");
            var host = ScriptableObject.CreateInstance<ThrowingGrabPixelsHost>();
            var previousSrgbWrite = GL.sRGBWrite;
            try
            {
                RenderTexture.active = previous;
                GL.sRGBWrite = false;
                var source = CreateSceneViewSource(
                    sceneView,
                    host,
                    nameof(ThrowingGrabPixelsHost.GrabPixels));

                var result = new UnitySceneViewPresentationAdapter()
                    .TryCaptureFramebuffer(source, destination, out _);

                Assert.That(result, Is.False);
                Assert.That(RenderTexture.active, Is.SameAs(previous));
                Assert.That(GL.sRGBWrite, Is.False);
            }
            finally
            {
                RenderTexture.active = null;
                GL.sRGBWrite = previousSrgbWrite;
                UnityEngine.Object.DestroyImmediate(destination);
                UnityEngine.Object.DestroyImmediate(previous);
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(sceneView);
            }
        }

        private static SceneViewPresentationSource CreateSceneViewSource (
            SceneView sceneView,
            UnityEngine.Object host,
            string methodName)
        {
            var method = host.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public);
            var mapping = new UnitySceneViewPresentationMapping(
                FramebufferWidth: 2,
                FramebufferHeight: 2,
                ContentWidth: 2,
                ContentHeight: 2,
                BackingScale: 1f,
                WindowRect: new Rect(0f, 0f, 2f, 2f),
                ContentRect: new Rect(0f, 0f, 2f, 2f),
                SourceUvTransform: new Vector4(1f, -1f, 0f, 1f));
            return new SceneViewPresentationSource(
                sceneView,
                host,
                new UnitySceneViewCaptureCapability(
                    method,
                    GraphicsFormat.B8G8R8A8_SRGB,
                    mapping));
        }

        private static RenderTexture CreateRenderTexture (string name)
        {
            var renderTexture = new RenderTexture(new RenderTextureDescriptor(
                2,
                2,
                GraphicsFormat.R8G8B8A8_SRGB,
                depthBufferBits: 0))
            {
                name = name,
            };
            Assert.That(renderTexture.Create(), Is.True);
            return renderTexture;
        }

        private sealed class MutatingGrabPixelsHost : ScriptableObject
        {
            public void GrabPixels (RenderTexture destination, Rect _)
            {
                RenderTexture.active = destination;
                GL.sRGBWrite = true;
            }
        }

        private sealed class ThrowingGrabPixelsHost : ScriptableObject
        {
            public void GrabPixels (RenderTexture destination, Rect _)
            {
                RenderTexture.active = destination;
                GL.sRGBWrite = true;
                throw new InvalidOperationException("Expected test failure.");
            }
        }
    }
}
