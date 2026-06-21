using System;
using System.Collections;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class EditorLogRangeExporterTests
    {
        private const int ExportBufferSize = 81920;

        private static readonly TimeSpan AsyncWaitTimeout = TimeSpan.FromSeconds(5);

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExportRange_WritesOnlySpecifiedByteRange () => UniTask.ToCoroutine(async () =>
        {
            var sourcePath = Path.Combine(Application.temporaryCachePath, $"editor-log-source-{Guid.NewGuid():N}.log");
            var destinationPath = Path.Combine(Application.temporaryCachePath, $"editor-log-destination-{Guid.NewGuid():N}.log");
            File.WriteAllText(sourcePath, "0123456789");
            var exporter = new EditorLogRangeExporter();

            try
            {
                var summary = await TestAwaiter.WaitAsync(
                    exporter.ExportRangeAsync(sourcePath, destinationPath, 2, 7, cancellationToken: CancellationToken.None).AsUniTask(),
                    "Editor log range export",
                    AsyncWaitTimeout);

                Assert.That(File.Exists(destinationPath), Is.True);
                Assert.That(File.ReadAllText(destinationPath), Is.EqualTo("23456"));
                Assert.That(summary.EntryCount, Is.EqualTo(1));
                Assert.That(summary.ErrorCount, Is.EqualTo(0));
                Assert.That(summary.WarningCount, Is.EqualTo(0));
            }
            finally
            {
                TryDeleteFile(sourcePath);
                TryDeleteFile(destinationPath);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExportRange_ReturnsSeveritySummaryWithoutCountingBuildTotalsAsErrors () => UniTask.ToCoroutine(async () =>
        {
            var sourcePath = Path.Combine(Application.temporaryCachePath, $"editor-log-source-{Guid.NewGuid():N}.log");
            var destinationPath = Path.Combine(Application.temporaryCachePath, $"editor-log-destination-{Guid.NewGuid():N}.log");
            File.WriteAllText(
                sourcePath,
                "Assets/Test.cs(1,1): warning CS0168" + Environment.NewLine
                + "Assets/Test.cs(2,1): error CS1001" + Environment.NewLine
                + "0 errors, 0 warnings" + Environment.NewLine
                + "Build completed" + Environment.NewLine);
            var exporter = new EditorLogRangeExporter();

            try
            {
                var summary = await TestAwaiter.WaitAsync(
                    exporter.ExportRangeAsync(sourcePath, destinationPath, 0, new FileInfo(sourcePath).Length, cancellationToken: CancellationToken.None).AsUniTask(),
                    "Editor log severity summary export",
                    AsyncWaitTimeout);

                Assert.That(File.Exists(destinationPath), Is.True);
                Assert.That(summary.EntryCount, Is.EqualTo(4));
                Assert.That(summary.ErrorCount, Is.EqualTo(1));
                Assert.That(summary.WarningCount, Is.EqualTo(1));
            }
            finally
            {
                TryDeleteFile(sourcePath);
                TryDeleteFile(destinationPath);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExportRange_ReturnsSeveritySummaryForNormalizedBuildLogRules () => UniTask.ToCoroutine(async () =>
        {
            var sourcePath = Path.Combine(Application.temporaryCachePath, $"editor-log-source-{Guid.NewGuid():N}.log");
            var destinationPath = Path.Combine(Application.temporaryCachePath, $"editor-log-destination-{Guid.NewGuid():N}.log");
            File.WriteAllText(
                sourcePath,
                "info: no severity" + Environment.NewLine
                + "\u001b[40m\u001b[1m\u001b[33mwarn\u001b[39m\u001b[22m\u001b[49m: duplicate hint path" + Environment.NewLine
                + "warn direct logger short" + Environment.NewLine
                + "warning: direct logger long" + Environment.NewLine
                + "[warn] bracket short" + Environment.NewLine
                + "[warning] bracket long" + Environment.NewLine
                + "Assets/Test.cs(1,1): warning CS0168" + Environment.NewLine
                + "error: direct logger" + Environment.NewLine
                + "[error] bracket" + Environment.NewLine
                + "Assets/Test.cs(2,1): error CS1001" + Environment.NewLine
                + "warning: prefix then compiler: error CS1001" + Environment.NewLine
                + "0 errors, 0 warnings" + Environment.NewLine);
            var exporter = new EditorLogRangeExporter();

            try
            {
                var summary = await TestAwaiter.WaitAsync(
                    exporter.ExportRangeAsync(sourcePath, destinationPath, 0, new FileInfo(sourcePath).Length, cancellationToken: CancellationToken.None).AsUniTask(),
                    "Editor log normalized severity summary export",
                    AsyncWaitTimeout);

                Assert.That(File.Exists(destinationPath), Is.True);
                Assert.That(summary.EntryCount, Is.EqualTo(12));
                Assert.That(summary.ErrorCount, Is.EqualTo(4));
                Assert.That(summary.WarningCount, Is.EqualTo(6));
            }
            finally
            {
                TryDeleteFile(sourcePath);
                TryDeleteFile(destinationPath);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExportRange_ReturnsSeveritySummaryAcrossReadBufferBoundaries () => UniTask.ToCoroutine(async () =>
        {
            var sourcePath = Path.Combine(Application.temporaryCachePath, $"editor-log-source-{Guid.NewGuid():N}.log");
            var destinationPath = Path.Combine(Application.temporaryCachePath, $"editor-log-destination-{Guid.NewGuid():N}.log");
            File.WriteAllText(sourcePath, new string(' ', ExportBufferSize - 2) + "warn: split prefix" + Environment.NewLine);
            var exporter = new EditorLogRangeExporter();

            try
            {
                var summary = await TestAwaiter.WaitAsync(
                    exporter.ExportRangeAsync(sourcePath, destinationPath, 0, new FileInfo(sourcePath).Length, cancellationToken: CancellationToken.None).AsUniTask(),
                    "Editor log split prefix severity export",
                    AsyncWaitTimeout);

                Assert.That(summary.EntryCount, Is.EqualTo(1));
                Assert.That(summary.ErrorCount, Is.EqualTo(0));
                Assert.That(summary.WarningCount, Is.EqualTo(1));

                File.WriteAllText(sourcePath, new string(' ', ExportBufferSize - 2) + "\u001b[33mwarning: split ansi" + Environment.NewLine);
                summary = await TestAwaiter.WaitAsync(
                    exporter.ExportRangeAsync(sourcePath, destinationPath, 0, new FileInfo(sourcePath).Length, cancellationToken: CancellationToken.None).AsUniTask(),
                    "Editor log split ANSI severity export",
                    AsyncWaitTimeout);

                Assert.That(summary.EntryCount, Is.EqualTo(1));
                Assert.That(summary.ErrorCount, Is.EqualTo(0));
                Assert.That(summary.WarningCount, Is.EqualTo(1));
            }
            finally
            {
                TryDeleteFile(sourcePath);
                TryDeleteFile(destinationPath);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExportRange_WithOverlappingRedactionValues_WritesOnlyRedactedLog () => UniTask.ToCoroutine(async () =>
        {
            var sourcePath = Path.Combine(Application.temporaryCachePath, $"editor-log-source-{Guid.NewGuid():N}.log");
            var destinationPath = Path.Combine(Application.temporaryCachePath, $"editor-log-destination-{Guid.NewGuid():N}.log");
            File.WriteAllText(
                sourcePath,
                "warning: token-secret and token" + Environment.NewLine
                + "abcdef abc" + Environment.NewLine);
            var exporter = new EditorLogRangeExporter();

            try
            {
                var summary = await TestAwaiter.WaitAsync(
                    exporter.ExportRangeAsync(
                        sourcePath,
                        destinationPath,
                        0,
                        new FileInfo(sourcePath).Length,
                        new[] { "token", "token-secret", "abc", "abcdef" },
                        cancellationToken: CancellationToken.None).AsUniTask(),
                    "Editor log redaction export",
                    AsyncWaitTimeout);

                var redactedLog = File.ReadAllText(destinationPath);
                Assert.That(redactedLog, Does.Not.Contain("token"));
                Assert.That(redactedLog, Does.Not.Contain("secret"));
                Assert.That(redactedLog, Does.Not.Contain("abc"));
                Assert.That(redactedLog, Does.Not.Contain("def"));
                Assert.That(redactedLog, Does.Contain("[ucli redacted environment value]"));
                Assert.That(summary.EntryCount, Is.EqualTo(2));
                Assert.That(summary.WarningCount, Is.EqualTo(1));
            }
            finally
            {
                TryDeleteFile(sourcePath);
                TryDeleteFile(destinationPath);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExportRange_WhenOffsetIsInvalid_ThrowsArgumentOutOfRangeException () => UniTask.ToCoroutine(async () =>
        {
            var sourcePath = Path.Combine(Application.temporaryCachePath, $"editor-log-source-{Guid.NewGuid():N}.log");
            var destinationPath = Path.Combine(Application.temporaryCachePath, $"editor-log-destination-{Guid.NewGuid():N}.log");
            File.WriteAllText(sourcePath, "abc");
            var exporter = new EditorLogRangeExporter();

            try
            {
                await AsyncExceptionCapture.CaptureAsync<ArgumentOutOfRangeException>(async () =>
                {
                    await exporter.ExportRangeAsync(sourcePath, destinationPath, 5, 1, cancellationToken: CancellationToken.None).AsUniTask();
                }, "Invalid editor log offset", AsyncWaitTimeout);
            }
            finally
            {
                TryDeleteFile(sourcePath);
                TryDeleteFile(destinationPath);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExportRange_WhenSourceFileIsMissing_ThrowsFileNotFoundException () => UniTask.ToCoroutine(async () =>
        {
            var sourcePath = Path.Combine(Application.temporaryCachePath, $"editor-log-missing-{Guid.NewGuid():N}.log");
            var destinationPath = Path.Combine(Application.temporaryCachePath, $"editor-log-destination-{Guid.NewGuid():N}.log");
            var exporter = new EditorLogRangeExporter();

            await AsyncExceptionCapture.CaptureAsync<FileNotFoundException>(async () =>
            {
                await exporter.ExportRangeAsync(sourcePath, destinationPath, 0, 0, cancellationToken: CancellationToken.None).AsUniTask();
            }, "Missing editor log source", AsyncWaitTimeout);
            TryDeleteFile(destinationPath);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExportRange_WhenSourcePathIsDirectory_ThrowsUnauthorizedAccessException () => UniTask.ToCoroutine(async () =>
        {
            var sourceDirectoryPath = Path.Combine(Application.temporaryCachePath, $"editor-log-source-dir-{Guid.NewGuid():N}");
            var destinationPath = Path.Combine(Application.temporaryCachePath, $"editor-log-destination-{Guid.NewGuid():N}.log");
            Directory.CreateDirectory(sourceDirectoryPath);
            var exporter = new EditorLogRangeExporter();

            try
            {
                await AsyncExceptionCapture.CaptureAsync<UnauthorizedAccessException>(async () =>
                {
                    await exporter.ExportRangeAsync(sourceDirectoryPath, destinationPath, 0, 0, cancellationToken: CancellationToken.None).AsUniTask();
                }, "Directory source editor log export", AsyncWaitTimeout);
            }
            finally
            {
                TryDeleteDirectory(sourceDirectoryPath);
                TryDeleteFile(destinationPath);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExportRange_WhenDestinationPathIsDirectory_ThrowsIOException () => UniTask.ToCoroutine(async () =>
        {
            var sourcePath = Path.Combine(Application.temporaryCachePath, $"editor-log-source-{Guid.NewGuid():N}.log");
            var destinationDirectoryPath = Path.Combine(Application.temporaryCachePath, $"editor-log-destination-dir-{Guid.NewGuid():N}");
            Directory.CreateDirectory(destinationDirectoryPath);
            File.WriteAllText(sourcePath, "abc");
            var exporter = new EditorLogRangeExporter();

            try
            {
                await AsyncExceptionCapture.CaptureAsync<IOException>(async () =>
                {
                    await exporter.ExportRangeAsync(sourcePath, destinationDirectoryPath, 0, 1, cancellationToken: CancellationToken.None).AsUniTask();
                }, "Directory destination editor log export", AsyncWaitTimeout);
            }
            finally
            {
                TryDeleteFile(sourcePath);
                TryDeleteDirectory(destinationDirectoryPath);
            }
        });

        private static void TryDeleteFile (string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static void TryDeleteDirectory (string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }
}
