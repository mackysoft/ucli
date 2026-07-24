using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Project;
using MackySoft.Ucli.Unity.ScreenshotCapture.Staging;
using NUnit.Framework;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class ScreenshotStagingImageWriterTests
    {
        private static readonly Guid FirstCaptureId =
            Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static readonly Guid SecondCaptureId =
            Guid.Parse("22222222-2222-2222-2222-222222222222");

        private static readonly Guid SymlinkCaptureId =
            Guid.Parse("33333333-3333-3333-3333-333333333333");

        [Test]
        [Category("Size.Small")]
        public async Task WriteAtomicAsync_WithPreparedCapturePath_PublishesExactBytes ()
        {
            using var scope = new TemporaryScreenshotDirectory();
            var writer = scope.CreateWriter();
            var path = scope.PrepareCapturePath(FirstCaptureId);
            var bytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

            var sizeBytes = await writer.WriteAtomicAsync(FirstCaptureId, bytes, CancellationToken.None);

            Assert.That(sizeBytes, Is.EqualTo(bytes.LongLength));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
            var directoryPath = Path.GetDirectoryName(path);
            Assert.That(directoryPath, Is.Not.Null.And.Not.Empty);
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                Assert.Fail("Prepared screenshot path has no parent directory.");
                return;
            }

            Assert.That(
                Directory.EnumerateFiles(directoryPath).Select(Path.GetFileName),
                Is.EqualTo(new[] { Path.GetFileName(path) }));
        }

        [Test]
        [Category("Size.Small")]
        public void WriteAtomicAsync_WithEmptyCaptureId_RejectsWrite ()
        {
            using var scope = new TemporaryScreenshotDirectory();
            var writer = scope.CreateWriter();

            Assert.ThrowsAsync<ArgumentException>(() =>
                writer.WriteAtomicAsync(Guid.Empty, new byte[] { 1, 2, 3, 4 }, CancellationToken.None));
        }

        [Test]
        [Category("Size.Small")]
        public void WriteAtomicAsync_WithExistingTarget_DoesNotReplaceIt ()
        {
            using var scope = new TemporaryScreenshotDirectory();
            var writer = scope.CreateWriter();
            var path = scope.PrepareCapturePath(SecondCaptureId);
            var original = new byte[] { 9, 8, 7, 6 };
            File.WriteAllBytes(path, original);

            Assert.ThrowsAsync<IOException>(() =>
                writer.WriteAtomicAsync(SecondCaptureId, new byte[] { 1, 2, 3, 4 }, CancellationToken.None));
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
                scope.ProjectAbsolutePath,
                TemporaryScreenshotDirectory.ProjectFingerprint);
            Directory.CreateDirectory(screenshotWorkDirectory.Value);
            var outsideDirectory = Path.Combine(scope.RootPath, "symlink-target");
            Directory.CreateDirectory(outsideDirectory);
            var captureDirectory = UcliStoragePathResolver.ResolveScreenshotCaptureStagingDirectory(
                scope.ProjectAbsolutePath,
                TemporaryScreenshotDirectory.ProjectFingerprint,
                SymlinkCaptureId);
            using (var process = Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/ln",
                Arguments = $"-s \"{outsideDirectory}\" \"{captureDirectory.Value}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            }))
            {
                Assert.That(process, Is.Not.Null);
                if (process == null)
                {
                    Assert.Fail("Could not launch the symbolic-link fixture process.");
                    return;
                }

                process.WaitForExit();
                Assert.That(process.ExitCode, Is.Zero);
            }

            Assert.ThrowsAsync<IOException>(() =>
                writer.WriteAtomicAsync(SymlinkCaptureId, new byte[] { 1, 2, 3, 4 }, CancellationToken.None));
            Assert.That(File.Exists(Path.Combine(outsideDirectory, "capture.rgba")), Is.False);
        }

        private sealed class TemporaryScreenshotDirectory : IDisposable
        {
            internal static readonly ProjectFingerprint ProjectFingerprint =
                ProjectFingerprintTestFactory.Create("screenshot-staging-image-writer");

            public TemporaryScreenshotDirectory ()
            {
                RootPath = Path.Combine(
                    Path.GetTempPath(),
                    $"ucli-screenshot-writer-{Guid.NewGuid():N}");
                ProjectPath = Path.Combine(RootPath, "UnityProject");
                ProjectAbsolutePath = AbsolutePath.Parse(ProjectPath);
                Directory.CreateDirectory(ProjectPath);
            }

            public string RootPath { get; }

            public string ProjectPath { get; }

            public AbsolutePath ProjectAbsolutePath { get; }

            public ScreenshotStagingImageWriter CreateWriter ()
            {
                return new ScreenshotStagingImageWriter(new UnityHostProjectIdentity(
                    ProjectAbsolutePath,
                    ProjectFingerprint,
                    "2023.2.22f1"));
            }

            public string PrepareCapturePath (Guid captureId)
            {
                var captureDirectory = UcliStoragePathResolver.ResolveScreenshotCaptureStagingDirectory(
                    ProjectAbsolutePath,
                    ProjectFingerprint,
                    captureId);
                Directory.CreateDirectory(captureDirectory.Value);
                return UcliStoragePathResolver.ResolveScreenshotCaptureRawStagingPath(
                    ProjectAbsolutePath,
                    ProjectFingerprint,
                    captureId).Value;
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
