using System;
using System.Collections.Generic;
using System.IO;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Build;
using NUnit.Framework;
using UnityEditor;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityBuildReportNormalizerTests
    {
        [Test]
        [Category("Size.Small")]
        public void Normalize_WithSucceededSnapshot_ReturnsBuildReportArtifact ()
        {
            var snapshot = new UnityBuildReportNormalizer.BuildReportSnapshot(
                Result: IpcBuildReportResult.Succeeded,
                UnityBuildTarget: "StandaloneLinux64",
                OutputPath: "/tmp/ucli/output/build",
                Duration: TimeSpan.FromMilliseconds(1234.6),
                TotalSizeBytes: 4096,
                ErrorCount: 0,
                WarningCount: 1,
                Steps: new[]
                {
                    new UnityBuildReportNormalizer.BuildReportStepSnapshot(
                        Name: "Build player",
                        Duration: TimeSpan.FromMilliseconds(1234.4),
                        Depth: 0,
                        Messages: new[]
                        {
                            new UnityBuildReportNormalizer.BuildReportMessageSnapshot(
                                Type: "Warning",
                                Content: "Sample warning"),
                        }),
                });

            var artifact = UnityBuildReportNormalizer.Normalize(snapshot);

            Assert.That(artifact.SchemaVersion, Is.EqualTo(1));
            Assert.That(artifact.Result, Is.EqualTo(IpcBuildReportResult.Succeeded));
            Assert.That(artifact.UnityBuildTarget, Is.EqualTo("StandaloneLinux64"));
            Assert.That(artifact.OutputPath, Is.EqualTo("/tmp/ucli/output/build"));
            Assert.That(artifact.DurationMilliseconds, Is.EqualTo(1235));
            Assert.That(artifact.TotalSizeBytes, Is.EqualTo(4096));
            Assert.That(artifact.ErrorCount, Is.Zero);
            Assert.That(artifact.WarningCount, Is.EqualTo(1));
            Assert.That(artifact.Steps.Count, Is.EqualTo(1));
            Assert.That(artifact.Steps[0].Name, Is.EqualTo("Build player"));
            Assert.That(artifact.Steps[0].DurationMilliseconds, Is.EqualTo(1234));
            Assert.That(artifact.Steps[0].Depth, Is.Zero);
            Assert.That(artifact.Steps[0].MessageCount, Is.EqualTo(1));
            Assert.That(artifact.Messages.Count, Is.EqualTo(1));
            Assert.That(artifact.Messages[0].Type, Is.EqualTo("Warning"));
            Assert.That(artifact.Messages[0].Content, Is.EqualTo("Sample warning"));
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WithFailedSnapshot_PreservesFailedResultAndErrorCount ()
        {
            var snapshot = new UnityBuildReportNormalizer.BuildReportSnapshot(
                Result: IpcBuildReportResult.Failed,
                UnityBuildTarget: "StandaloneLinux64",
                OutputPath: "/tmp/ucli/output/build",
                Duration: TimeSpan.FromMilliseconds(10),
                TotalSizeBytes: 0,
                ErrorCount: 1,
                WarningCount: 0,
                Steps: Array.Empty<UnityBuildReportNormalizer.BuildReportStepSnapshot>());

            var artifact = UnityBuildReportNormalizer.Normalize(snapshot);

            Assert.That(artifact.Result, Is.EqualTo(IpcBuildReportResult.Failed));
            Assert.That(artifact.ErrorCount, Is.EqualTo(1));
            Assert.That(artifact.Steps, Is.Empty);
            Assert.That(artifact.Messages, Is.Empty);
        }

        [TestCase(IpcBuildReportResult.Succeeded, IpcBuildLogCompletionReason.Completed)]
        [TestCase(IpcBuildReportResult.Failed, IpcBuildLogCompletionReason.Failed)]
        [TestCase(IpcBuildReportResult.Canceled, IpcBuildLogCompletionReason.Canceled)]
        [Category("Size.Small")]
        public void ToCompletionReason_MapsBuildReportResultToLogCompletionReason (
            IpcBuildReportResult result,
            IpcBuildLogCompletionReason expectedCompletionReason)
        {
            var completionReason = UnityBuildReportNormalizer.ToCompletionReason(result);

            Assert.That(completionReason, Is.EqualTo(expectedCompletionReason));
        }

        [Test]
        [Category("Size.Small")]
        public void ToCompletionReason_WithUnknownBuildReportResult_ReturnsFailed ()
        {
            var completionReason = UnityBuildReportNormalizer.ToCompletionReason(IpcBuildReportResult.Unknown);

            Assert.That(completionReason, Is.EqualTo(IpcBuildLogCompletionReason.Failed));
        }

        [Test]
        [Category("Size.Small")]
        public void Create_WithStandaloneLinuxTarget_CreatesBuildPlayerOptions ()
        {
            var outputPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ucli", "output"));
            var locationPathName = Path.Combine(outputPath, "player", "Player");
            var wireRequest = new IpcBuildRunRequest(
                RunId: Guid.Parse("00000000-0000-0000-0000-000000000604"),
                InputKind: BuildProfileInputsKind.Explicit,
                BuildTarget: BuildTargetStableName.StandaloneLinux64,
                SceneSource: BuildProfileSceneSource.Explicit,
                ScenePaths: new[] { new SceneAssetPath("Assets/Scenes/Main.unity") },
                Development: true,
                OutputPath: outputPath,
                OutputLayout: new IpcBuildOutputLayout(
                    Shape: IpcBuildOutputLayoutShape.File,
                    LocationPathName: locationPathName),
                BuildReportPath: "/tmp/ucli/build-report.json",
                BuildLogPath: "/tmp/ucli/build.log",
                AllowedEditorModes: new[] { DaemonEditorMode.Batchmode },
                ProjectMutationMode: BuildProfileProjectMutationMode.Forbid,
                RunnerKind: BuildRunnerKind.BuildPipeline,
                ProfileDigest: Sha256Digest.Parse(new string('a', 64)),
                UnityBuildProfile: null,
                ProfilePath: null,
                RunnerMethod: null,
                RunnerArguments: new Dictionary<string, string>(StringComparer.Ordinal),
                RunnerEnvironmentVariables: Array.Empty<string>(),
                RunnerEnvironmentSecrets: Array.Empty<string>(),
                RunnerEnvironmentVariableValues: new Dictionary<string, string>(StringComparer.Ordinal),
                RunnerEnvironmentSecretValues: new Dictionary<string, string>(StringComparer.Ordinal));
            var resolvedInput = new UnityBuildResolvedInput(
                UnityBuildTarget: BuildTarget.StandaloneLinux64,
                UnityBuildTargetGroup: BuildTargetGroup.Standalone,
                ScenePaths: new[] { new SceneAssetPath("Assets/Scenes/Main.unity") },
                Options: BuildOptions.Development);

            var request = (BuildRunExecutionRequest.ExplicitBuildPipeline)BuildRunExecutionRequest.Create(wireRequest);
            var options = UnityBuildPlayerOptionsFactory.Create(request, resolvedInput);

            Assert.That(options.scenes, Is.EqualTo(new[] { "Assets/Scenes/Main.unity" }));
            Assert.That(options.target, Is.EqualTo(BuildTarget.StandaloneLinux64));
            Assert.That(options.targetGroup, Is.EqualTo(BuildTargetGroup.Standalone));
            Assert.That(options.options, Is.EqualTo(BuildOptions.Development));
            Assert.That(options.locationPathName, Is.EqualTo(locationPathName));
        }
    }
}
