using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using NUnit.Framework;
using UnityEditor;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UcliBuildReportSourceWriterTests
    {
        [Test]
        [Category("Size.Small")]
        public void WriteSourceJson_WithRelativePath_WritesUnderOutputDir ()
        {
            var rootPath = CreateTempRoot();
            try
            {
                var outputDirectory = Path.Combine(rootPath, "output");
                Directory.CreateDirectory(outputDirectory);
                var context = CreateContext(outputDirectory);

                var declaration = UcliBuildReportSourceWriter.WriteSourceJson(
                    context,
                    CreateBuildReportArtifact(outputDirectory),
                    "reports/build-report.json");

                var sourcePath = Path.Combine(outputDirectory, "reports", "build-report.json");
                Assert.That(declaration.Path, Is.EqualTo("reports/build-report.json"));
                Assert.That(File.Exists(sourcePath), Is.True);
                using (var document = JsonDocument.Parse(File.ReadAllText(sourcePath)))
                {
                    Assert.That(document.RootElement.GetProperty("result").GetString(), Is.EqualTo("succeeded"));
                }
            }
            finally
            {
                DeleteDirectory(rootPath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void WriteSourceJson_WithEscapingPath_DoesNotWriteOutsideOutputDir ()
        {
            var rootPath = CreateTempRoot();
            try
            {
                var outputDirectory = Path.Combine(rootPath, "output");
                Directory.CreateDirectory(outputDirectory);
                var context = CreateContext(outputDirectory);

                Assert.Throws<ArgumentException>(() => UcliBuildReportSourceWriter.WriteSourceJson(
                    context,
                    CreateBuildReportArtifact(outputDirectory),
                    "../build-report.json"));
                Assert.That(File.Exists(Path.Combine(rootPath, "build-report.json")), Is.False);
            }
            finally
            {
                DeleteDirectory(rootPath);
            }
        }

        private static UcliBuildRunnerContext CreateContext (string outputDirectory)
        {
            return new UcliBuildRunnerContext(
                runId: Guid.Parse("00000000-0000-0000-0000-000000000605"),
                projectPath: "/workspace/UnityProject",
                projectFingerprint: ProjectFingerprintTestFactory.Create("project-fingerprint"),
                outputDir: outputDirectory,
                profilePath: "/workspace/build.ucli.json",
                profileDigest: Sha256Digest.Parse(new string('a', 64)),
                target: new UcliResolvedBuildTarget(BuildTargetStableName.StandaloneLinux64, BuildTarget.StandaloneLinux64),
                scenes: new[] { "Assets/Scenes/Main.unity" },
                options: new UcliBuildOptions(development: true),
                arguments: new Dictionary<string, string>(StringComparer.Ordinal),
                environmentVariables: new Dictionary<string, string>(StringComparer.Ordinal),
                environmentSecrets: new Dictionary<string, string>(StringComparer.Ordinal));
        }

        private static IpcBuildReportArtifact CreateBuildReportArtifact (string outputDirectory)
        {
            return new IpcBuildReportArtifact(
                SchemaVersion: 1,
                Result: IpcBuildReportResult.Succeeded,
                UnityBuildTarget: "StandaloneLinux64",
                OutputPath: Path.Combine(outputDirectory, "player"),
                DurationMilliseconds: 1,
                TotalSizeBytes: 1,
                ErrorCount: 0,
                WarningCount: 0,
                Steps: Array.Empty<IpcBuildReportStep>(),
                Messages: Array.Empty<IpcBuildReportMessage>());
        }

        private static string CreateTempRoot ()
        {
            return Path.Combine(
                Path.GetTempPath(),
                "ucli-build-report-source-writer-tests",
                Guid.NewGuid().ToString("N"));
        }

        private static void DeleteDirectory (string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }
}
