using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Assurance.Build;
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
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

#nullable enable

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
        [TestCase(BuildProfileProjectMutationMode.Forbid)]
        [TestCase(BuildProfileProjectMutationMode.Audit)]
        [TestCase(BuildProfileProjectMutationMode.AllowWithAudit)]
        public void TryValidateRequest_WithKnownProjectMutationMode_ReturnsTrue (BuildProfileProjectMutationMode mode)
        {
            using (var scope = TemporaryDirectoryScope.Create())
            {
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var request = CreateRequest(scope.ProjectPath, identity) with
                {
                    ProjectMutationMode = ContractLiteralCodec.ToValue(mode),
                };

                var result = BuildRunUnityIpcMethodHandler.TryValidateRequest(request, identity, out var errorMessage);

                Assert.That(result, Is.True, errorMessage);
            }
        }

        [Test]
        [Category("Size.Small")]
        [TestCase("FORBID")]
        [TestCase("legacy")]
        public void TryValidateRequest_WithInvalidProjectMutationMode_ReturnsFalse (string projectMutationMode)
        {
            using (var scope = TemporaryDirectoryScope.Create())
            {
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var request = CreateRequest(scope.ProjectPath, identity) with
                {
                    ProjectMutationMode = projectMutationMode,
                };

                var result = BuildRunUnityIpcMethodHandler.TryValidateRequest(request, identity, out var errorMessage);

                Assert.That(result, Is.False);
                Assert.That(errorMessage, Does.Contain("projectMutationMode is invalid"));
            }
        }

        [Test]
        [Category("Size.Small")]
        public void TryValidateRequest_WithOutputLayoutOutsideExpectedLayout_ReturnsFalse ()
        {
            using (var scope = TemporaryDirectoryScope.Create())
            {
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var request = CreateRequest(scope.ProjectPath, identity) with
                {
                    OutputLayout = new IpcBuildOutputLayout(
                        Shape: ContractLiteralCodec.ToValue(IpcBuildOutputLayoutShape.File),
                        LocationPathName: Path.Combine(scope.RootPath, "outside", "Player")),
                };

                var result = BuildRunUnityIpcMethodHandler.TryValidateRequest(request, identity, out var errorMessage);

                Assert.That(result, Is.False);
                Assert.That(errorMessage, Does.Contain("outputLayout"));
            }
        }

        [TestCase("missing")]
        [TestCase("shapeMismatch")]
        [TestCase("unsupportedTarget")]
        [Category("Size.Small")]
        public void TryValidateRequest_WithInvalidOutputLayout_ReturnsFalse (string scenario)
        {
            using (var scope = TemporaryDirectoryScope.Create())
            {
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var request = CreateRequest(scope.ProjectPath, identity);
                request = scenario switch
                {
                    "missing" => request with
                    {
                        OutputLayout = null!,
                    },
                    "shapeMismatch" => request with
                    {
                        OutputLayout = request.OutputLayout with
                        {
                            Shape = ContractLiteralCodec.ToValue(IpcBuildOutputLayoutShape.Directory),
                        },
                    },
                    "unsupportedTarget" => request with
                    {
                        BuildTarget = "switch",
                        UnityBuildTarget = "Switch",
                    },
                    _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unsupported output layout validation scenario."),
                };

                var result = BuildRunUnityIpcMethodHandler.TryValidateRequest(request, identity, out var errorMessage);

                Assert.That(result, Is.False);
                Assert.That(errorMessage, Does.Contain("outputLayout"));
            }
        }

        [Test]
        [Category("Size.Small")]
        public void TryValidateRequest_WithUnknownBuildTarget_ReturnsFalse ()
        {
            using (var scope = TemporaryDirectoryScope.Create())
            {
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var request = CreateRequest(scope.ProjectPath, identity) with
                {
                    BuildTarget = "unknownTarget",
                    UnityBuildTarget = "UnknownTarget",
                };

                var result = BuildRunUnityIpcMethodHandler.TryValidateRequest(request, identity, out var errorMessage);

                Assert.That(result, Is.False);
                Assert.That(errorMessage, Does.Contain("buildTarget"));
            }
        }

        [Test]
        [Category("Size.Small")]
        public void TryValidateRequest_WithMismatchedUnityBuildTarget_ReturnsFalse ()
        {
            using (var scope = TemporaryDirectoryScope.Create())
            {
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var request = CreateRequest(scope.ProjectPath, identity) with
                {
                    UnityBuildTarget = "StandaloneWindows64",
                };

                var result = BuildRunUnityIpcMethodHandler.TryValidateRequest(request, identity, out var errorMessage);

                Assert.That(result, Is.False);
                Assert.That(errorMessage, Does.Contain("UnityBuildTarget"));
            }
        }

        [TestCase("output")]
        [TestCase("report")]
        [TestCase("log")]
        [Category("Size.Small")]
        public void TryValidateRequest_WithRelativeArtifactPath_ReturnsFalse (string artifact)
        {
            using (var scope = TemporaryDirectoryScope.Create())
            {
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var request = CreateRequest(scope.ProjectPath, identity);
                var relativeArtifactPath = Path.Combine("relative", artifact);
                request = WithArtifactPath(request, artifact, relativeArtifactPath);

                var result = BuildRunUnityIpcMethodHandler.TryValidateRequest(request, identity, out var errorMessage);

                Assert.That(result, Is.False);
                Assert.That(errorMessage, Does.Contain("expected uCLI build artifact layout"));
            }
        }

        [TestCase("output")]
        [TestCase("report")]
        [TestCase("log")]
        [Category("Size.Small")]
        public void TryValidateRequest_WithArtifactPathOutsideExpectedLayout_ReturnsFalse (string artifact)
        {
            using (var scope = TemporaryDirectoryScope.Create())
            {
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var request = WithArtifactPath(
                    CreateRequest(scope.ProjectPath, identity),
                    artifact,
                    Path.Combine(scope.RootPath, "outside", artifact));

                var result = BuildRunUnityIpcMethodHandler.TryValidateRequest(request, identity, out var errorMessage);

                Assert.That(result, Is.False);
                Assert.That(errorMessage, Does.Contain("expected uCLI build artifact layout"));
            }
        }

        [TestCase("output")]
        [TestCase("report")]
        [TestCase("log")]
        [Category("Size.Small")]
        public async Task HandleAsync_WithArtifactPathOutsideExpectedLayout_ReturnsInvalidArgumentWithoutSideEffects (string artifact)
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
                    new UnityProjectMutationAuditProbe(),
                    buildPipelineRunner,
                    logRangeExporter,
                    identity,
                    timeoutScopeFactory);
                var payload = WithArtifactPath(
                    CreateRequest(scope.ProjectPath, identity),
                    artifact,
                    Path.Combine(scope.RootPath, "outside", artifact));
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

        [Test]
        [Category("Size.Small")]
        public async Task HandleAsync_WithMismatchedUnityBuildTarget_ReturnsInvalidArgumentWithoutSideEffects ()
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
                    new UnityProjectMutationAuditProbe(),
                    buildPipelineRunner,
                    logRangeExporter,
                    identity,
                    timeoutScopeFactory);
                var payload = CreateRequest(scope.ProjectPath, identity) with
                {
                    UnityBuildTarget = "StandaloneWindows64",
                };

                var response = await handler.HandleAsync(CreateIpcRequest(payload), CancellationToken.None);

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

        [Test]
        [Category("Size.Small")]
        public async Task HandleAsync_WithUnknownBuildTarget_ReturnsInvalidArgumentWithoutSideEffects ()
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
                    new UnityProjectMutationAuditProbe(),
                    buildPipelineRunner,
                    logRangeExporter,
                    identity,
                    timeoutScopeFactory);
                var payload = CreateRequest(scope.ProjectPath, identity) with
                {
                    BuildTarget = "unknownTarget",
                    UnityBuildTarget = "UnknownTarget",
                };

                var response = await handler.HandleAsync(CreateIpcRequest(payload), CancellationToken.None);

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

        [Test]
        [Category("Size.Small")]
        public async Task HandleAsync_WithExistingOutputLayoutTarget_ReturnsBuildArtifactWriteFailedWithoutRunningBuild ()
        {
            using (var scope = TemporaryDirectoryScope.Create())
            using (var editorScope = new EditorTestScope().SuppressExistingPersistentDirtyObjects())
            {
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var readinessGate = new CountingReadinessGate();
                var buildPipelineRunner = new CountingBuildPipelineRunner();
                var logRangeExporter = new CountingEditorLogRangeExporter();
                var handler = new BuildRunUnityIpcMethodHandler(
                    new UnityBuildPreconditionProbe(
                        readinessGate,
                        identity,
                        new StubServerVersionProvider("1.2.3"),
                        new CountingBuildTargetSupportProbe()),
                    new UnityProjectMutationAuditProbe(),
                    buildPipelineRunner,
                    logRangeExporter,
                    identity,
                    new CountingTimeoutScopeFactory());
                var payload = CreateRequest(scope.ProjectPath, identity);
                Directory.CreateDirectory(Path.GetDirectoryName(payload.OutputLayout.LocationPathName)!);
                File.WriteAllText(payload.OutputLayout.LocationPathName, "existing player");

                var response = await handler.HandleAsync(CreateIpcRequest(payload), CancellationToken.None);

                Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
                Assert.That(response.Errors.Count, Is.EqualTo(1));
                Assert.That(response.Errors[0].Code, Is.EqualTo(BuildErrorCodes.BuildArtifactWriteFailed));
                Assert.That(buildPipelineRunner.CallCount, Is.EqualTo(0));
                Assert.That(logRangeExporter.CallCount, Is.EqualTo(0));
            }
        }

        [TestCase(IpcBuildReportResult.Succeeded, IpcBuildLogCompletionReason.Completed, 0, 9)]
        [TestCase(IpcBuildReportResult.Failed, IpcBuildLogCompletionReason.Failed, 1, 2)]
        [TestCase(IpcBuildReportResult.Canceled, IpcBuildLogCompletionReason.Canceled, 0, 0)]
        [Category("Size.Small")]
        public async Task HandleAsync_WithTerminalBuildRun_WritesArtifactsAndReturnsLogSummary (
            IpcBuildReportResult reportResult,
            IpcBuildLogCompletionReason completionReason,
            int reportErrorCount,
            int reportWarningCount)
        {
            Assume.That(
                !string.IsNullOrWhiteSpace(Application.consoleLogPath) && File.Exists(Application.consoleLogPath),
                "Unity console log path is not available in this test environment.");

            using (var scope = TemporaryDirectoryScope.Create())
            using (var editorScope = new EditorTestScope().SuppressExistingPersistentDirtyObjects())
            {
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var requestPayload = CreateRequest(scope.ProjectPath, identity);
                Directory.CreateDirectory(requestPayload.OutputPath);
                var reportArtifact = CreateReportArtifact(
                    ContractLiteralCodec.ToValue(reportResult),
                    requestPayload.OutputLayout.LocationPathName,
                    reportErrorCount,
                    reportWarningCount);
                var buildPipelineRunner = new CountingBuildPipelineRunner(reportArtifact);
                var logRangeExporter = new CountingEditorLogRangeExporter(
                    "Assets/Test.cs(1,1): warning CS0168" + Environment.NewLine
                    + "Assets/Test.cs(2,1): error CS1001" + Environment.NewLine
                    + "0 errors, 0 warnings" + Environment.NewLine,
                    entryCount: 3,
                    errorCount: 1,
                    warningCount: 1);
                var handler = new BuildRunUnityIpcMethodHandler(
                    new UnityBuildPreconditionProbe(
                        new CountingReadinessGate(),
                        identity,
                        new StubServerVersionProvider("1.2.3"),
                        new CountingBuildTargetSupportProbe()),
                    new UnityProjectMutationAuditProbe(),
                    buildPipelineRunner,
                    logRangeExporter,
                    identity,
                    new CountingTimeoutScopeFactory());

                var response = await handler.HandleAsync(CreateIpcRequest(requestPayload), CancellationToken.None);

                Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
                Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcBuildRunResponse payload, out _), Is.True);
                Assert.That(payload.RunId, Is.EqualTo(RunId));
                Assert.That(payload.Report.Result, Is.EqualTo(ContractLiteralCodec.ToValue(reportResult)));
                Assert.That(payload.Report.ErrorCount, Is.EqualTo(reportErrorCount));
                Assert.That(payload.Report.WarningCount, Is.EqualTo(reportWarningCount));
                Assert.That(payload.Logs.CompletionReason, Is.EqualTo(ContractLiteralCodec.ToValue(completionReason)));
                Assert.That(payload.Logs.EntryCount, Is.EqualTo(3));
                Assert.That(payload.Logs.ErrorCount, Is.EqualTo(1));
                Assert.That(payload.Logs.WarningCount, Is.EqualTo(1));
                Assert.That(buildPipelineRunner.CallCount, Is.EqualTo(1));
                Assert.That(buildPipelineRunner.LastOptions.locationPathName, Is.EqualTo(requestPayload.OutputLayout.LocationPathName));
                Assert.That(logRangeExporter.CallCount, Is.EqualTo(1));
                Assert.That(File.Exists(requestPayload.BuildReportPath), Is.True);
                Assert.That(File.Exists(requestPayload.BuildLogPath), Is.True);

                var persistedReport = JsonSerializer.Deserialize<IpcBuildReportArtifact>(
                    File.ReadAllText(requestPayload.BuildReportPath),
                    IpcJsonSerializerOptions.Default);
                Assert.That(persistedReport, Is.Not.Null);
                Assert.That(persistedReport!.Result, Is.EqualTo(reportArtifact.Result));
                Assert.That(persistedReport.ErrorCount, Is.EqualTo(reportErrorCount));
                Assert.That(persistedReport.WarningCount, Is.EqualTo(reportWarningCount));
                Assert.That(File.ReadAllText(requestPayload.BuildLogPath), Does.Contain("warning CS0168"));
            }
        }

        [Test]
        [Category("Size.Small")]
        public async Task HandleAsync_WithRunnerProjectMutation_ReturnsMutationAudit ()
        {
            using (var scope = TemporaryDirectoryScope.Create())
            using (var editorScope = new EditorTestScope().SuppressExistingPersistentDirtyObjects())
            {
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var requestPayload = CreateRequest(scope.ProjectPath, identity) with
                {
                    ProjectMutationMode = ContractLiteralCodec.ToValue(BuildProfileProjectMutationMode.Audit),
                };
                Directory.CreateDirectory(requestPayload.OutputPath);
                const string MutatedPath = "Assets/GeneratedByRunner.txt";
                var mutatedFullPath = Path.Combine(scope.ProjectPath, MutatedPath);
                var reportArtifact = CreateReportArtifact(
                    ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                    Path.Combine(requestPayload.OutputPath, "build"),
                    errorCount: 0,
                    warningCount: 0);
                var buildPipelineRunner = new CountingBuildPipelineRunner(
                    reportArtifact,
                    _ => File.WriteAllText(mutatedFullPath, "generated by runner"));
                var handler = new BuildRunUnityIpcMethodHandler(
                    new UnityBuildPreconditionProbe(
                        new CountingReadinessGate(),
                        identity,
                        new StubServerVersionProvider("1.2.3"),
                        new CountingBuildTargetSupportProbe()),
                    new UnityProjectMutationAuditProbe(),
                    buildPipelineRunner,
                    new CountingEditorLogRangeExporter(
                        string.Empty,
                        entryCount: 0,
                        errorCount: 0,
                        warningCount: 0),
                    identity,
                    new CountingTimeoutScopeFactory());

                var response = await handler.HandleAsync(CreateIpcRequest(requestPayload), CancellationToken.None);

                Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
                Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcBuildRunResponse payload, out _), Is.True);
                Assert.That(buildPipelineRunner.CallCount, Is.EqualTo(1));
                Assert.That(payload.ProjectMutation.Mode, Is.EqualTo(ContractLiteralCodec.ToValue(BuildProfileProjectMutationMode.Audit)));
                Assert.That(payload.ProjectMutation.Coverage, Is.EqualTo(ContractLiteralCodec.ToValue(IpcBuildProjectMutationAuditCoverage.Full)));
                Assert.That(payload.ProjectMutation.Mutated, Is.True);
                Assert.That(payload.ProjectMutation.BeforeDigest, Is.Not.EqualTo(payload.ProjectMutation.AfterDigest));
                Assert.That(payload.ProjectMutation.Items, Has.Count.EqualTo(1));
                var item = payload.ProjectMutation.Items[0];
                Assert.That(item.Path, Is.EqualTo(MutatedPath));
                Assert.That(item.ChangeKind, Is.EqualTo(ContractLiteralCodec.ToValue(IpcBuildProjectMutationChangeKind.Added)));
                Assert.That(item.BeforeSha256, Is.Null);
                Assert.That(item.AfterSha256, Is.Not.Null);
            }
        }

        [Test]
        [Category("Size.Small")]
        public async Task HandleAsync_WithDirtyScenePrecondition_ReturnsDirtyStateWithoutRunningBuild ()
        {
            using (var scope = TemporaryDirectoryScope.Create())
            using (var editorScope = new EditorTestScope().SuppressExistingPersistentDirtyObjects())
            {
                var (scenePath, scene) = CreateSavedScene(editorScope, "BuildRunHandlerDirty", NewSceneMode.Single);
                MarkSceneDirty(scene, "DirtyBuildRunRoot");
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var requestPayload = CreateRequest(scope.ProjectPath, identity) with
                {
                    ScenePaths = new[] { scenePath },
                };
                var buildPipelineRunner = new CountingBuildPipelineRunner();
                var logRangeExporter = new CountingEditorLogRangeExporter();
                var handler = new BuildRunUnityIpcMethodHandler(
                    new UnityBuildPreconditionProbe(
                        new CountingReadinessGate(),
                        identity,
                        new StubServerVersionProvider("1.2.3"),
                        new CountingBuildTargetSupportProbe()),
                    new UnityProjectMutationAuditProbe(),
                    buildPipelineRunner,
                    logRangeExporter,
                    identity,
                    new CountingTimeoutScopeFactory());

                var response = await handler.HandleAsync(CreateIpcRequest(requestPayload), CancellationToken.None);

                Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
                Assert.That(response.Errors.Count, Is.EqualTo(1));
                Assert.That(response.Errors[0].Code, Is.EqualTo(BuildErrorCodes.BuildDirtyStatePresent));
                Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcBuildRunErrorPayload payload, out _), Is.True);
                Assert.That(payload.DirtyState, Is.Not.Null);
                Assert.That(payload.DirtyState!.Dirty, Is.True);
                Assert.That(payload.DirtyState.Items, Has.Count.EqualTo(1));
                Assert.That(payload.DirtyState.Items[0].Path, Is.EqualTo(scenePath));
                Assert.That(buildPipelineRunner.CallCount, Is.EqualTo(0));
                Assert.That(logRangeExporter.CallCount, Is.EqualTo(0));
                Assert.That(File.Exists(requestPayload.BuildReportPath), Is.False);
                Assert.That(File.Exists(requestPayload.BuildLogPath), Is.False);
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
            var artifactsDirectory = UcliStoragePathResolver.ResolveBuildRunArtifactsDirectory(
                storageRoot,
                identity.ProjectFingerprint,
                RunId);
            var outputPath = UcliStoragePathResolver.ResolveBuildRunOutputDirectory(
                storageRoot,
                identity.ProjectFingerprint,
                RunId);
            if (!IpcBuildOutputLayoutResolver.TryResolve(outputPath, "standaloneLinux64", out var outputLayout))
            {
                throw new InvalidOperationException("Test build target must resolve a BuildPipeline output layout.");
            }

            return new IpcBuildRunRequest(
                RunId: RunId,
                BuildTarget: "standaloneLinux64",
                UnityBuildTarget: "StandaloneLinux64",
                SceneSource: "explicit",
                ScenePaths: new[] { "Assets/Scenes/SampleScene.unity" },
                Development: true,
                OutputPath: outputPath,
                OutputLayout: outputLayout!,
                BuildReportPath: Path.Combine(artifactsDirectory, UcliStoragePathNames.BuildReportFileName),
                BuildLogPath: Path.Combine(artifactsDirectory, UcliStoragePathNames.BuildLogFileName),
                AllowedEditorModes: new[] { ContractLiteralCodec.ToValue(DaemonEditorMode.Batchmode) },
                ProjectMutationMode: ContractLiteralCodec.ToValue(BuildProfileProjectMutationMode.Forbid));
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

        private static string GetArtifactPath (
            IpcBuildRunRequest request,
            string artifact)
        {
            return artifact switch
            {
                "output" => request.OutputPath,
                "report" => request.BuildReportPath,
                "log" => request.BuildLogPath,
                _ => throw new ArgumentOutOfRangeException(nameof(artifact), artifact, "Unsupported build artifact selector."),
            };
        }

        private static IpcBuildRunRequest WithArtifactPath (
            IpcBuildRunRequest request,
            string artifact,
            string path)
        {
            return artifact switch
            {
                "output" => request with { OutputPath = path },
                "report" => request with { BuildReportPath = path },
                "log" => request with { BuildLogPath = path },
                _ => throw new ArgumentOutOfRangeException(nameof(artifact), artifact, "Unsupported build artifact selector."),
            };
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

        private static IpcBuildReportArtifact CreateReportArtifact (
            string result,
            string outputPath,
            int errorCount,
            int warningCount)
        {
            return new IpcBuildReportArtifact(
                SchemaVersion: 1,
                Result: result,
                UnityBuildTarget: "StandaloneLinux64",
                OutputPath: outputPath,
                DurationMilliseconds: 1234,
                TotalSizeBytes: 4096,
                ErrorCount: errorCount,
                WarningCount: warningCount,
                Steps: new[]
                {
                    new IpcBuildReportStep(
                        Name: "Build player",
                        DurationMilliseconds: 1234,
                        Depth: 0,
                        MessageCount: 1),
                },
                Messages: new[]
                {
                    new IpcBuildReportMessage(
                        Type: warningCount == 0 ? "Info" : "Warning",
                        Content: "Build report message"),
                });
        }

        private static (string Path, Scene Scene) CreateSavedScene (
            EditorTestScope scope,
            string prefix,
            NewSceneMode mode)
        {
            var scenePath = scope.CreateScenePath(prefix);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
            var root = new GameObject(prefix + "Root");
            SceneManager.MoveGameObjectToScene(root, scene);
            Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
            return (scenePath, scene);
        }

        private static void MarkSceneDirty (
            Scene scene,
            string objectName)
        {
            var gameObject = new GameObject(objectName);
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            EditorSceneManager.MarkSceneDirty(scene);
            Assert.That(scene.isDirty, Is.True);
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
            private readonly Action<BuildPlayerOptions>? onRun;

            private readonly IpcBuildReportArtifact? report;

            public CountingBuildPipelineRunner ()
            {
            }

            public CountingBuildPipelineRunner (
                IpcBuildReportArtifact report,
                Action<BuildPlayerOptions>? onRun = null)
            {
                this.report = report;
                this.onRun = onRun;
            }

            public int CallCount { get; private set; }

            public BuildPlayerOptions LastOptions { get; private set; }

            public IpcBuildReportArtifact? Run (BuildPlayerOptions options)
            {
                CallCount++;
                LastOptions = options;
                onRun?.Invoke(options);
                if (report == null)
                {
                    throw new InvalidOperationException("BuildPipeline must not run for an invalid build.run request.");
                }

                return report;
            }
        }

        private sealed class CountingEditorLogRangeExporter : IEditorLogRangeExporter
        {
            private readonly string? contents;

            private readonly EditorLogRangeExportResult summary;

            public CountingEditorLogRangeExporter ()
            {
            }

            public CountingEditorLogRangeExporter (
                string contents,
                int entryCount,
                int errorCount,
                int warningCount)
            {
                this.contents = contents;
                summary = new EditorLogRangeExportResult(entryCount, errorCount, warningCount);
            }

            public int CallCount { get; private set; }

            public Task<EditorLogRangeExportResult> ExportRangeAsync (
                string sourcePath,
                string destinationPath,
                long startOffset,
                long endOffset,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallCount++;
                if (contents == null)
                {
                    throw new InvalidOperationException("Log export must not run for an invalid build.run request.");
                }

                var directoryPath = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrWhiteSpace(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                File.WriteAllText(destinationPath, contents);
                return Task.FromResult(summary);
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
                for (var i = 0; i < UnityProjectMutationAuditScope.RootRelativePaths.Count; i++)
                {
                    Directory.CreateDirectory(Path.Combine(ProjectPath, UnityProjectMutationAuditScope.RootRelativePaths[i]));
                }
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
