using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Build;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class BuildRunUnityIpcMethodHandlerTests
    {
        private const string ProjectFingerprint = "project-fingerprint";
        private const string RunId = "build-run-1";

        [Test]
        [Category("Size.Small")]
        public void TryValidateRequest_WithExpectedArtifactPaths_ReturnsTrue ()
        {
            using (var scope = TemporaryDirectoryScope.Create())
            {
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var request = CreateRequest(scope.ProjectPath, identity);

                var result = BuildRunUnityIpcMethodHandler.TryValidateRequest(request, identity, out var errorMessage);

                Assert.That(result, Is.True, errorMessage);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void TryValidateRequest_WithRelativeArtifactPath_ReturnsFalse ()
        {
            using (var scope = TemporaryDirectoryScope.Create())
            {
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var storageRoot = UcliStoragePathResolver.ResolveStorageRoot(scope.ProjectPath);
                var runDirectory = UcliStoragePathResolver.ResolveBuildRunArtifactsDirectory(
                    storageRoot,
                    ProjectFingerprint,
                    RunId);
                var relativeBuildReportPath = Path.GetRelativePath(
                    Directory.GetCurrentDirectory(),
                    Path.Combine(runDirectory, UcliStoragePathNames.BuildReportFileName));
                var request = CreateRequest(scope.ProjectPath, identity) with
                {
                    BuildReportPath = relativeBuildReportPath,
                };

                var result = BuildRunUnityIpcMethodHandler.TryValidateRequest(request, identity, out var errorMessage);

                Assert.That(result, Is.False);
                Assert.That(errorMessage, Does.Contain("expected uCLI build artifact layout"));
            }
        }

        [Test]
        [Category("Size.Small")]
        public void TryValidateRequest_WithArtifactPathOutsideExpectedLayout_ReturnsFalse ()
        {
            using (var scope = TemporaryDirectoryScope.Create())
            {
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var request = CreateRequest(scope.ProjectPath, identity) with
                {
                    BuildReportPath = Path.Combine(scope.RootPath, "outside", UcliStoragePathNames.BuildReportFileName),
                };

                var result = BuildRunUnityIpcMethodHandler.TryValidateRequest(request, identity, out var errorMessage);

                Assert.That(result, Is.False);
                Assert.That(errorMessage, Does.Contain("expected uCLI build artifact layout"));
            }
        }

        [Test]
        [Category("Size.Small")]
        public async Task HandleAsync_WithArtifactPathOutsideExpectedLayout_ReturnsInvalidArgumentWithoutSideEffects ()
        {
            using (var scope = TemporaryDirectoryScope.Create())
            {
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var readinessGate = new CountingReadinessGate();
                var buildPipelineRunner = new CountingBuildPipelineRunner();
                var logRangeExporter = new CountingEditorLogRangeExporter();
                var timeoutScopeFactory = new CountingTimeoutScopeFactory();
                var handler = new BuildRunUnityIpcMethodHandler(
                    new UnityBuildPreconditionProbe(
                        readinessGate,
                        identity,
                        new StubServerVersionProvider("1.2.3"),
                        new CountingBuildTargetSupportProbe()),
                    buildPipelineRunner,
                    logRangeExporter,
                    identity,
                    timeoutScopeFactory);
                var payload = CreateRequest(scope.ProjectPath, identity) with
                {
                    BuildReportPath = Path.Combine(scope.RootPath, "outside", UcliStoragePathNames.BuildReportFileName),
                };
                var ipcRequest = CreateIpcRequest(payload);

                var response = await handler.HandleAsync(ipcRequest, CancellationToken.None);

                Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
                Assert.That(response.Errors.Count, Is.EqualTo(1));
                Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
                Assert.That(readinessGate.EnsureExecutionReadyCallCount, Is.EqualTo(0));
                Assert.That(readinessGate.CaptureSnapshotCallCount, Is.EqualTo(0));
                Assert.That(buildPipelineRunner.CallCount, Is.EqualTo(0));
                Assert.That(logRangeExporter.CallCount, Is.EqualTo(0));
                Assert.That(timeoutScopeFactory.CallCount, Is.EqualTo(0));
            }
        }

        private static IpcProjectIdentity CreateProjectIdentity (string projectPath)
        {
            return new IpcProjectIdentity(
                ProjectPath: projectPath,
                ProjectFingerprint: ProjectFingerprint,
                UnityVersion: "6000.1.4f1");
        }

        private static IpcBuildRunRequest CreateRequest (
            string projectPath,
            IpcProjectIdentity identity)
        {
            var storageRoot = UcliStoragePathResolver.ResolveStorageRoot(projectPath);
            var runDirectory = UcliStoragePathResolver.ResolveBuildRunArtifactsDirectory(
                storageRoot,
                identity.ProjectFingerprint,
                RunId);
            return new IpcBuildRunRequest(
                RunId: RunId,
                TargetStableName: "standaloneLinux64",
                UnityBuildTarget: "StandaloneLinux64",
                SceneSource: "explicit",
                ScenePaths: new[] { "Assets/Scenes/Main.unity" },
                Development: true,
                OutputPath: Path.Combine(runDirectory, UcliStoragePathNames.BuildOutputDirectoryName),
                BuildReportPath: Path.Combine(runDirectory, UcliStoragePathNames.BuildReportFileName),
                BuildLogPath: Path.Combine(runDirectory, UcliStoragePathNames.BuildLogFileName));
        }

        private static IpcRequest CreateIpcRequest (IpcBuildRunRequest payload)
        {
            return new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "request-build-run",
                SessionToken: "session-token",
                Method: IpcMethodNames.BuildRun,
                Payload: IpcPayloadCodec.SerializeToElement(payload),
                responseMode: IpcResponseMode.Single);
        }

        private static UnityEditorLifecycleSnapshot CreateLifecycleSnapshot ()
        {
            return new UnityEditorLifecycleSnapshot(
                EditorMode: DaemonEditorMode.Batchmode,
                LifecycleState: IpcEditorLifecycleStateCodec.Ready,
                BlockingReason: null,
                CompileState: IpcCompileStateCodec.Ready,
                CompileGeneration: "compile-1",
                DomainReloadGeneration: "domain-1",
                CanAcceptExecutionRequests: true,
                ObservedAtUtc: DateTimeOffset.Parse("2026-06-12T00:00:00+00:00"),
                PlayMode: new IpcPlayModeSnapshot(
                    State: "stopped",
                    Transition: "none",
                    IsPlaying: false,
                    IsPlayingOrWillChangePlaymode: false,
                    Generation: "play-1"),
                AssetRefreshGeneration: "asset-1");
        }

        private sealed class CountingReadinessGate : IUnityEditorReadinessGate
        {
            public int CaptureSnapshotCallCount { get; private set; }

            public int EnsureExecutionReadyCallCount { get; private set; }

            public UnityEditorLifecycleSnapshot CaptureSnapshot ()
            {
                CaptureSnapshotCallCount++;
                return CreateLifecycleSnapshot();
            }

            public Task<UnityEditorExecutionReadinessResult> EnsureExecutionReadyAsync (
                bool failFast,
                CancellationToken cancellationToken = default,
                bool allowPlayMode = false)
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnsureExecutionReadyCallCount++;
                return Task.FromResult(UnityEditorExecutionReadinessResult.Ready(CreateLifecycleSnapshot()));
            }
        }

        private sealed class CountingBuildTargetSupportProbe : IUnityBuildTargetSupportProbe
        {
            public int CallCount { get; private set; }

            public UnityBuildTargetSupportProbeResult Probe (string unityBuildTargetLiteral)
            {
                CallCount++;
                return UnityBuildTargetSupportProbeResult.Resolved(
                    BuildTarget.StandaloneLinux64,
                    BuildTargetGroup.Standalone,
                    isSupported: true);
            }
        }

        private sealed class CountingBuildPipelineRunner : IUnityBuildPipelineRunner
        {
            public int CallCount { get; private set; }

            public BuildReport Run (BuildPlayerOptions options)
            {
                CallCount++;
                throw new InvalidOperationException("BuildPipeline must not run for an invalid build.run request.");
            }
        }

        private sealed class CountingEditorLogRangeExporter : IEditorLogRangeExporter
        {
            public int CallCount { get; private set; }

            public Task ExportRangeAsync (
                string sourcePath,
                string destinationPath,
                long startOffset,
                long endOffset,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallCount++;
                throw new InvalidOperationException("Log export must not run for an invalid build.run request.");
            }
        }

        private sealed class CountingTimeoutScopeFactory : IIpcRequestTimeoutScopeFactory
        {
            public int CallCount { get; private set; }

            public IIpcRequestTimeoutScope CreateLinked (
                int? timeoutMilliseconds,
                CancellationToken cancellationToken)
            {
                CallCount++;
                return new PassthroughTimeoutScope(cancellationToken);
            }
        }

        private sealed class PassthroughTimeoutScope : IIpcRequestTimeoutScope
        {
            public PassthroughTimeoutScope (CancellationToken token)
            {
                Token = token;
            }

            public CancellationToken Token { get; }

            public bool IsTimeoutCancellationRequested => false;

            public void Dispose ()
            {
            }
        }

        private sealed class StubServerVersionProvider : IServerVersionProvider
        {
            private readonly string version;

            public StubServerVersionProvider (string version)
            {
                this.version = version;
            }

            public string GetVersion ()
            {
                return version;
            }
        }

        private sealed class TemporaryDirectoryScope : IDisposable
        {
            private TemporaryDirectoryScope (string rootPath)
            {
                RootPath = rootPath;
                ProjectPath = Path.Combine(rootPath, "UnityProject");
                Directory.CreateDirectory(ProjectPath);
            }

            public string RootPath { get; }

            public string ProjectPath { get; }

            public static TemporaryDirectoryScope Create ()
            {
                var rootPath = Path.Combine(Path.GetTempPath(), "ucli-build-run-handler-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(rootPath);
                return new TemporaryDirectoryScope(rootPath);
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
