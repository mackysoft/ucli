using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.ScreenshotCapture;
using NUnit.Framework;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class ScreenshotStagingImageWriterTests
    {
        [Test]
        [Category("Size.Small")]
        public async Task WriteAtomicAsync_WithPreparedCapturePath_PublishesExactBytes ()
        {
            using var scope = new TemporaryScreenshotDirectory();
            var writer = scope.CreateWriter();
            var path = scope.PrepareCapturePath("capture-1");
            var bytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

            var sizeBytes = await writer.WriteAtomicAsync(path, bytes, CancellationToken.None);

            Assert.That(sizeBytes, Is.EqualTo(bytes.LongLength));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
            Assert.That(
                Directory.EnumerateFiles(Path.GetDirectoryName(path)!).Select(Path.GetFileName),
                Is.EqualTo(new[] { Path.GetFileName(path) }));
        }

        [Test]
        [Category("Size.Small")]
        public void WriteAtomicAsync_WithPathOutsideFingerprintWork_RejectsWrite ()
        {
            using var scope = new TemporaryScreenshotDirectory();
            var writer = scope.CreateWriter();
            var outsideDirectory = Path.Combine(scope.RootPath, "outside");
            Directory.CreateDirectory(outsideDirectory);
            var path = Path.Combine(outsideDirectory, "capture.rgba");

            Assert.ThrowsAsync<IOException>(async () =>
                await writer.WriteAtomicAsync(path, new byte[] { 1, 2, 3, 4 }, CancellationToken.None));
            Assert.That(File.Exists(path), Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public void WriteAtomicAsync_WithExistingTarget_DoesNotReplaceIt ()
        {
            using var scope = new TemporaryScreenshotDirectory();
            var writer = scope.CreateWriter();
            var path = scope.PrepareCapturePath("capture-2");
            var original = new byte[] { 9, 8, 7, 6 };
            File.WriteAllBytes(path, original);

            Assert.ThrowsAsync<IOException>(async () =>
                await writer.WriteAtomicAsync(path, new byte[] { 1, 2, 3, 4 }, CancellationToken.None));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
        }

        [Test]
        [Category("Size.Small")]
        public void WriteAtomicAsync_WithSymlinkCaptureDirectory_RejectsWrite ()
        {
            if (Application.platform != RuntimePlatform.OSXEditor)
            {
                Assert.Ignore("Symbolic-link staging boundary test is implemented for macOS Editor.");
            }

            using var scope = new TemporaryScreenshotDirectory();
            var writer = scope.CreateWriter();
            var screenshotWorkDirectory = UcliStoragePathResolver.ResolveScreenshotWorkDirectory(
                scope.ProjectPath,
                TemporaryScreenshotDirectory.ProjectFingerprint);
            Directory.CreateDirectory(screenshotWorkDirectory);
            var outsideDirectory = Path.Combine(scope.RootPath, "symlink-target");
            Directory.CreateDirectory(outsideDirectory);
            var captureDirectory = UcliStoragePathResolver.ResolveScreenshotCaptureStagingDirectory(
                scope.ProjectPath,
                TemporaryScreenshotDirectory.ProjectFingerprint,
                "capture-link");
            using (var process = Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/ln",
                Arguments = $"-s \"{outsideDirectory}\" \"{captureDirectory}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            }))
            {
                Assert.That(process, Is.Not.Null);
                process!.WaitForExit();
                Assert.That(process.ExitCode, Is.Zero);
            }

            var path = Path.Combine(captureDirectory, "capture.rgba");
            Assert.ThrowsAsync<IOException>(async () =>
                await writer.WriteAtomicAsync(path, new byte[] { 1, 2, 3, 4 }, CancellationToken.None));
            Assert.That(File.Exists(Path.Combine(outsideDirectory, "capture.rgba")), Is.False);
        }

        private sealed class TemporaryScreenshotDirectory : IDisposable
        {
            internal const string ProjectFingerprint = "pf_screenshot_test";

            public TemporaryScreenshotDirectory ()
            {
                RootPath = Path.Combine(
                    Path.GetTempPath(),
                    $"ucli-screenshot-writer-{Guid.NewGuid():N}");
                ProjectPath = Path.Combine(RootPath, "UnityProject");
                Directory.CreateDirectory(ProjectPath);
            }

            public string RootPath { get; }

            public string ProjectPath { get; }

            public ScreenshotStagingImageWriter CreateWriter ()
            {
                return new ScreenshotStagingImageWriter(new IpcProjectIdentity(
                    ProjectPath,
                    ProjectFingerprint,
                    "2023.2.22f1"));
            }

            public string PrepareCapturePath (string captureId)
            {
                var captureDirectory = UcliStoragePathResolver.ResolveScreenshotCaptureStagingDirectory(
                    ProjectPath,
                    ProjectFingerprint,
                    captureId);
                Directory.CreateDirectory(captureDirectory);
                return UcliStoragePathResolver.ResolveScreenshotCaptureRawStagingPath(
                    ProjectPath,
                    ProjectFingerprint,
                    captureId);
            }

            public void Dispose ()
            {
                if (Directory.Exists(RootPath))
                {
                    Directory.Delete(RootPath, recursive: true);
                }
            }
        }
    }
}
