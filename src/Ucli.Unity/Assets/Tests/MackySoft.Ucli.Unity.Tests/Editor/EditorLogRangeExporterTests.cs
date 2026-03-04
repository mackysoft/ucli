using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class EditorLogRangeExporterTests
    {
        [Test]
        [Category("Size.Small")]
        public async Task ExportRange_WritesOnlySpecifiedByteRange ()
        {
            var sourcePath = Path.Combine(Application.temporaryCachePath, $"editor-log-source-{Guid.NewGuid():N}.log");
            var destinationPath = Path.Combine(Application.temporaryCachePath, $"editor-log-destination-{Guid.NewGuid():N}.log");
            File.WriteAllText(sourcePath, "0123456789");
            var exporter = new EditorLogRangeExporter();

            try
            {
                await exporter.ExportRange(sourcePath, destinationPath, 2, 7, CancellationToken.None);

                Assert.That(File.Exists(destinationPath), Is.True);
                Assert.That(File.ReadAllText(destinationPath), Is.EqualTo("23456"));
            }
            finally
            {
                TryDeleteFile(sourcePath);
                TryDeleteFile(destinationPath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void ExportRange_WhenOffsetIsInvalid_ThrowsArgumentOutOfRangeException ()
        {
            var sourcePath = Path.Combine(Application.temporaryCachePath, $"editor-log-source-{Guid.NewGuid():N}.log");
            var destinationPath = Path.Combine(Application.temporaryCachePath, $"editor-log-destination-{Guid.NewGuid():N}.log");
            File.WriteAllText(sourcePath, "abc");
            var exporter = new EditorLogRangeExporter();

            try
            {
                Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
                {
                    await exporter.ExportRange(sourcePath, destinationPath, 5, 1, CancellationToken.None);
                });
            }
            finally
            {
                TryDeleteFile(sourcePath);
                TryDeleteFile(destinationPath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void ExportRange_WhenSourceFileIsMissing_ThrowsFileNotFoundException ()
        {
            var sourcePath = Path.Combine(Application.temporaryCachePath, $"editor-log-missing-{Guid.NewGuid():N}.log");
            var destinationPath = Path.Combine(Application.temporaryCachePath, $"editor-log-destination-{Guid.NewGuid():N}.log");
            var exporter = new EditorLogRangeExporter();

            Assert.ThrowsAsync<FileNotFoundException>(async () =>
            {
                await exporter.ExportRange(sourcePath, destinationPath, 0, 0, CancellationToken.None);
            });
            TryDeleteFile(destinationPath);
        }

        [Test]
        [Category("Size.Small")]
        public void ExportRange_WhenSourcePathIsDirectory_ThrowsUnauthorizedAccessException ()
        {
            var sourceDirectoryPath = Path.Combine(Application.temporaryCachePath, $"editor-log-source-dir-{Guid.NewGuid():N}");
            var destinationPath = Path.Combine(Application.temporaryCachePath, $"editor-log-destination-{Guid.NewGuid():N}.log");
            Directory.CreateDirectory(sourceDirectoryPath);
            var exporter = new EditorLogRangeExporter();

            try
            {
                Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
                {
                    await exporter.ExportRange(sourceDirectoryPath, destinationPath, 0, 0, CancellationToken.None);
                });
            }
            finally
            {
                TryDeleteDirectory(sourceDirectoryPath);
                TryDeleteFile(destinationPath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void ExportRange_WhenDestinationPathIsDirectory_ThrowsUnauthorizedAccessException ()
        {
            var sourcePath = Path.Combine(Application.temporaryCachePath, $"editor-log-source-{Guid.NewGuid():N}.log");
            var destinationDirectoryPath = Path.Combine(Application.temporaryCachePath, $"editor-log-destination-dir-{Guid.NewGuid():N}");
            Directory.CreateDirectory(destinationDirectoryPath);
            File.WriteAllText(sourcePath, "abc");
            var exporter = new EditorLogRangeExporter();

            try
            {
                Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
                {
                    await exporter.ExportRange(sourcePath, destinationDirectoryPath, 0, 1, CancellationToken.None);
                });
            }
            finally
            {
                TryDeleteFile(sourcePath);
                TryDeleteDirectory(destinationDirectoryPath);
            }
        }

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
