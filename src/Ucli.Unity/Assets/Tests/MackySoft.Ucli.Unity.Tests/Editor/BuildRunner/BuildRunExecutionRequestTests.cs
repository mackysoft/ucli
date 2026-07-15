using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Build;
using NUnit.Framework;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class BuildRunExecutionRequestTests
    {
        private static readonly Guid RunId = Guid.Parse("00000000-0000-0000-0000-000000000622");
        private static readonly Sha256Digest ProfileDigest = Sha256Digest.Parse(new string('b', 64));

        [Test]
        [Category("Size.Small")]
        public void Create_WithExplicitBuildPipelineRequest_ReturnsNonNullableCaseValues ()
        {
            var outputLayout = new IpcBuildOutputLayout(
                IpcBuildOutputLayoutShape.File,
                "/tmp/ucli/output/player/Player");

            var result = BuildRunExecutionRequest.Create(CreateExplicitRequest(
                BuildRunnerKind.BuildPipeline,
                outputLayout,
                profilePath: null,
                runnerMethod: null));

            Assert.That(result, Is.TypeOf<BuildRunExecutionRequest.ExplicitBuildPipeline>());
            var request = (BuildRunExecutionRequest.ExplicitBuildPipeline)result;
            Assert.That(request.InputKind, Is.EqualTo(BuildProfileInputsKind.Explicit));
            Assert.That(request.RunnerKind, Is.EqualTo(BuildRunnerKind.BuildPipeline));
            Assert.That(request.BuildTarget, Is.EqualTo(BuildTargetStableName.StandaloneLinux64));
            Assert.That(request.SceneSource, Is.EqualTo(BuildProfileSceneSource.Explicit));
            Assert.That(request.OutputLayout, Is.SameAs(outputLayout));
        }

        [Test]
        [Category("Size.Small")]
        public void Create_WithExplicitExecuteMethodRequest_ReturnsNonNullableCaseValues ()
        {
            var result = BuildRunExecutionRequest.Create(CreateExplicitRequest(
                BuildRunnerKind.ExecuteMethod,
                outputLayout: null,
                profilePath: "/tmp/ucli/build.ucli.json",
                runnerMethod: "Build.Entry.Run"));

            Assert.That(result, Is.TypeOf<BuildRunExecutionRequest.ExplicitExecuteMethod>());
            var request = (BuildRunExecutionRequest.ExplicitExecuteMethod)result;
            Assert.That(request.InputKind, Is.EqualTo(BuildProfileInputsKind.Explicit));
            Assert.That(request.RunnerKind, Is.EqualTo(BuildRunnerKind.ExecuteMethod));
            Assert.That(request.BuildTarget, Is.EqualTo(BuildTargetStableName.StandaloneLinux64));
            Assert.That(request.ProfilePath, Is.EqualTo("/tmp/ucli/build.ucli.json"));
            Assert.That(request.RunnerMethod, Is.EqualTo("Build.Entry.Run"));
            Assert.That(request.RunnerEnvironmentSecretValues["UCLI_SECRET"], Is.EqualTo("secret-value"));
        }

        [Test]
        [Category("Size.Small")]
        public void Create_WithUnityBuildProfileRequest_ReturnsNonNullableCaseValues ()
        {
            var profile = new IpcUnityBuildProfileInput(
                new UnityBuildProfileAssetPath("Assets/BuildProfiles/Linux.asset"),
                Digest: null,
                ApplyAudit: null);

            var result = BuildRunExecutionRequest.Create(CreateUnityBuildProfileRequest(profile));

            Assert.That(result, Is.TypeOf<BuildRunExecutionRequest.UnityBuildProfile>());
            var request = (BuildRunExecutionRequest.UnityBuildProfile)result;
            Assert.That(request.InputKind, Is.EqualTo(BuildProfileInputsKind.UnityBuildProfile));
            Assert.That(request.RunnerKind, Is.EqualTo(BuildRunnerKind.BuildPipeline));
            Assert.That(request.Profile, Is.SameAs(profile));
            Assert.That(request.Profile.Path.Value, Is.EqualTo("Assets/BuildProfiles/Linux.asset"));
        }

        private static IpcBuildRunRequest CreateExplicitRequest (
            BuildRunnerKind runnerKind,
            IpcBuildOutputLayout? outputLayout,
            string? profilePath,
            string? runnerMethod)
        {
            return new IpcBuildRunRequest(
                RunId: RunId,
                InputKind: BuildProfileInputsKind.Explicit,
                BuildTarget: BuildTargetStableName.StandaloneLinux64,
                SceneSource: BuildProfileSceneSource.Explicit,
                ScenePaths: new[] { new SceneAssetPath("Assets/Scenes/Main.unity") },
                Development: true,
                OutputPath: "/tmp/ucli/output",
                OutputLayout: outputLayout,
                BuildReportPath: "/tmp/ucli/build-report.json",
                BuildLogPath: "/tmp/ucli/build.log",
                AllowedEditorModes: new[] { DaemonEditorMode.Batchmode },
                ProjectMutationMode: BuildProfileProjectMutationMode.Forbid,
                RunnerKind: runnerKind,
                ProfileDigest: ProfileDigest,
                UnityBuildProfile: null,
                ProfilePath: profilePath,
                RunnerMethod: runnerMethod,
                RunnerArguments: new Dictionary<string, string>(StringComparer.Ordinal),
                RunnerEnvironmentVariables: Array.Empty<string>(),
                RunnerEnvironmentSecrets: runnerKind == BuildRunnerKind.ExecuteMethod
                    ? new[] { "UCLI_SECRET" }
                    : Array.Empty<string>(),
                RunnerEnvironmentVariableValues: new Dictionary<string, string>(StringComparer.Ordinal),
                RunnerEnvironmentSecretValues: runnerKind == BuildRunnerKind.ExecuteMethod
                    ? new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["UCLI_SECRET"] = "secret-value",
                    }
                    : new Dictionary<string, string>(StringComparer.Ordinal));
        }

        private static IpcBuildRunRequest CreateUnityBuildProfileRequest (IpcUnityBuildProfileInput profile)
        {
            return new IpcBuildRunRequest(
                RunId: RunId,
                InputKind: BuildProfileInputsKind.UnityBuildProfile,
                BuildTarget: null,
                SceneSource: null,
                ScenePaths: Array.Empty<SceneAssetPath>(),
                Development: false,
                OutputPath: "/tmp/ucli/output",
                OutputLayout: null,
                BuildReportPath: "/tmp/ucli/build-report.json",
                BuildLogPath: "/tmp/ucli/build.log",
                AllowedEditorModes: new[] { DaemonEditorMode.Batchmode },
                ProjectMutationMode: BuildProfileProjectMutationMode.Forbid,
                RunnerKind: BuildRunnerKind.BuildPipeline,
                ProfileDigest: ProfileDigest,
                UnityBuildProfile: profile,
                ProfilePath: null,
                RunnerMethod: null,
                RunnerArguments: new Dictionary<string, string>(StringComparer.Ordinal),
                RunnerEnvironmentVariables: Array.Empty<string>(),
                RunnerEnvironmentSecrets: Array.Empty<string>(),
                RunnerEnvironmentVariableValues: new Dictionary<string, string>(StringComparer.Ordinal),
                RunnerEnvironmentSecretValues: new Dictionary<string, string>(StringComparer.Ordinal));
        }
    }
}
