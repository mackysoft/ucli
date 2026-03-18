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
                await TestAwaiter.WaitAsync(
                    exporter.ExportRange(sourcePath, destinationPath, 2, 7, CancellationToken.None).AsUniTask(),
                    "Editor log range export",
                    AsyncWaitTimeout);

                Assert.That(File.Exists(destinationPath), Is.True);
                Assert.That(File.ReadAllText(destinationPath), Is.EqualTo("23456"));
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
                    await exporter.ExportRange(sourcePath, destinationPath, 5, 1, CancellationToken.None).AsUniTask();
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
                await exporter.ExportRange(sourcePath, destinationPath, 0, 0, CancellationToken.None).AsUniTask();
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
                    await exporter.ExportRange(sourceDirectoryPath, destinationPath, 0, 0, CancellationToken.None).AsUniTask();
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
        public IEnumerator ExportRange_WhenDestinationPathIsDirectory_ThrowsUnauthorizedAccessException () => UniTask.ToCoroutine(async () =>
        {
            var sourcePath = Path.Combine(Application.temporaryCachePath, $"editor-log-source-{Guid.NewGuid():N}.log");
            var destinationDirectoryPath = Path.Combine(Application.temporaryCachePath, $"editor-log-destination-dir-{Guid.NewGuid():N}");
            Directory.CreateDirectory(destinationDirectoryPath);
            File.WriteAllText(sourcePath, "abc");
            var exporter = new EditorLogRangeExporter();

            try
            {
                await AsyncExceptionCapture.CaptureAsync<UnauthorizedAccessException>(async () =>
                {
                    await exporter.ExportRange(sourcePath, destinationDirectoryPath, 0, 1, CancellationToken.None).AsUniTask();
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