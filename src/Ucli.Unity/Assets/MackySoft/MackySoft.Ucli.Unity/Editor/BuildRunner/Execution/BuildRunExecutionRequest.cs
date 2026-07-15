using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Represents one valid Unity-side execution shape for a validated build-run wire request. </summary>
    internal abstract class BuildRunExecutionRequest
    {
        private BuildRunExecutionRequest (IpcBuildRunRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            RunId = request.RunId;
            OutputPath = request.OutputPath;
            BuildReportPath = request.BuildReportPath;
            BuildLogPath = request.BuildLogPath;
            AllowedEditorModes = request.AllowedEditorModes;
            ProjectMutationMode = request.ProjectMutationMode;
            ProfileDigest = request.ProfileDigest;
        }

        public Guid RunId { get; }

        public abstract BuildProfileInputsKind InputKind { get; }

        public abstract BuildRunnerKind RunnerKind { get; }

        public string OutputPath { get; }

        public string BuildReportPath { get; }

        public string BuildLogPath { get; }

        public IReadOnlyList<DaemonEditorMode> AllowedEditorModes { get; }

        public BuildProfileProjectMutationMode ProjectMutationMode { get; }

        public Sha256Digest ProfileDigest { get; }

        /// <summary> Converts one constructor-validated wire request into its only valid execution case. </summary>
        public static BuildRunExecutionRequest Create (IpcBuildRunRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.InputKind == BuildProfileInputsKind.UnityBuildProfile)
            {
                return new UnityBuildProfile(
                    request,
                    request.UnityBuildProfile!);
            }

            if (request.RunnerKind == BuildRunnerKind.BuildPipeline)
            {
                return new ExplicitBuildPipeline(
                    request,
                    request.BuildTarget!.Value,
                    request.SceneSource!.Value,
                    request.ScenePaths,
                    request.Development,
                    request.OutputLayout!);
            }

            return new ExplicitExecuteMethod(
                request,
                request.BuildTarget!.Value,
                request.SceneSource!.Value,
                request.ScenePaths,
                request.Development,
                request.ProfilePath!,
                request.RunnerMethod!,
                request.RunnerArguments,
                request.RunnerEnvironmentVariableValues,
                request.RunnerEnvironmentSecretValues);
        }

        /// <summary> Represents explicit inputs executed through Unity BuildPipeline. </summary>
        internal sealed class ExplicitBuildPipeline : BuildRunExecutionRequest
        {
            internal ExplicitBuildPipeline (
                IpcBuildRunRequest request,
                BuildTargetStableName buildTarget,
                BuildProfileSceneSource sceneSource,
                IReadOnlyList<SceneAssetPath> scenePaths,
                bool development,
                IpcBuildOutputLayout outputLayout)
                : base(request)
            {
                BuildTarget = buildTarget;
                SceneSource = sceneSource;
                ScenePaths = scenePaths;
                Development = development;
                OutputLayout = outputLayout;
            }

            public override BuildProfileInputsKind InputKind => BuildProfileInputsKind.Explicit;

            public override BuildRunnerKind RunnerKind => BuildRunnerKind.BuildPipeline;

            public BuildTargetStableName BuildTarget { get; }

            public BuildProfileSceneSource SceneSource { get; }

            public IReadOnlyList<SceneAssetPath> ScenePaths { get; }

            public bool Development { get; }

            public IpcBuildOutputLayout OutputLayout { get; }
        }

        /// <summary> Represents explicit inputs executed through the uCLI executeMethod bridge. </summary>
        internal sealed class ExplicitExecuteMethod : BuildRunExecutionRequest
        {
            internal ExplicitExecuteMethod (
                IpcBuildRunRequest request,
                BuildTargetStableName buildTarget,
                BuildProfileSceneSource sceneSource,
                IReadOnlyList<SceneAssetPath> scenePaths,
                bool development,
                string profilePath,
                string runnerMethod,
                IReadOnlyDictionary<string, string> runnerArguments,
                IReadOnlyDictionary<string, string> runnerEnvironmentVariableValues,
                IReadOnlyDictionary<string, string> runnerEnvironmentSecretValues)
                : base(request)
            {
                BuildTarget = buildTarget;
                SceneSource = sceneSource;
                ScenePaths = scenePaths;
                Development = development;
                ProfilePath = profilePath;
                RunnerMethod = runnerMethod;
                RunnerArguments = runnerArguments;
                RunnerEnvironmentVariableValues = runnerEnvironmentVariableValues;
                RunnerEnvironmentSecretValues = runnerEnvironmentSecretValues;
            }

            public override BuildProfileInputsKind InputKind => BuildProfileInputsKind.Explicit;

            public override BuildRunnerKind RunnerKind => BuildRunnerKind.ExecuteMethod;

            public BuildTargetStableName BuildTarget { get; }

            public BuildProfileSceneSource SceneSource { get; }

            public IReadOnlyList<SceneAssetPath> ScenePaths { get; }

            public bool Development { get; }

            public string ProfilePath { get; }

            public string RunnerMethod { get; }

            public IReadOnlyDictionary<string, string> RunnerArguments { get; }

            public IReadOnlyDictionary<string, string> RunnerEnvironmentVariableValues { get; }

            public IReadOnlyDictionary<string, string> RunnerEnvironmentSecretValues { get; }
        }

        /// <summary> Represents a Unity Build Profile executed through Unity BuildPipeline. </summary>
        internal sealed class UnityBuildProfile : BuildRunExecutionRequest
        {
            internal UnityBuildProfile (
                IpcBuildRunRequest request,
                IpcUnityBuildProfileInput profile)
                : base(request)
            {
                Profile = profile;
            }

            public override BuildProfileInputsKind InputKind => BuildProfileInputsKind.UnityBuildProfile;

            public override BuildRunnerKind RunnerKind => BuildRunnerKind.BuildPipeline;

            public IpcUnityBuildProfileInput Profile { get; }
        }
    }
}
