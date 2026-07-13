using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Ipc;
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
        private const string ExecuteMethodTypeName = "MackySoft.Ucli.Unity.Tests.BuildRunUnityIpcMethodHandlerTests";

        private static UcliBuildRunnerContext? executeMethodContext;
        private static IUnityLogStream? executeMethodLogStream;

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
        public void TryValidateRequest_WithUnityBuildProfileInput_ReturnsTrue ()
        {
            using (var scope = TemporaryDirectoryScope.Create())
            {
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var request = CreateUnityBuildProfileRequest(scope.ProjectPath, identity);

                var result = BuildRunUnityIpcMethodHandler.TryValidateRequest(request, identity, out var errorMessage);

                Assert.That(result, Is.True, errorMessage);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void TryValidateRequest_WithUnityBuildProfileDigest_ReturnsFalse ()
        {
            using (var scope = TemporaryDirectoryScope.Create())
            {
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var request = CreateUnityBuildProfileRequest(scope.ProjectPath, identity) with
                {
                    UnityBuildProfile = new IpcUnityBuildProfileInput(
                        "Assets/BuildProfiles/Linux.asset",
                        Digest: new string('f', 64)),
                };

                var result = BuildRunUnityIpcMethodHandler.TryValidateRequest(request, identity, out var errorMessage);

                Assert.That(result, Is.False);
                Assert.That(errorMessage, Does.Contain("may only specify path"));
            }
        }

        [Test]
        [Category("Size.Small")]
        public void TryValidateRequest_WithUnityBuildProfileApplyAudit_ReturnsFalse ()
        {
            using (var scope = TemporaryDirectoryScope.Create())
            {
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var request = CreateUnityBuildProfileRequest(scope.ProjectPath, identity) with
                {
                    UnityBuildProfile = CreateAppliedUnityBuildProfileInput("Assets/BuildProfiles/Linux.asset"),
                };

                var result = BuildRunUnityIpcMethodHandler.TryValidateRequest(request, identity, out var errorMessage);

                Assert.That(result, Is.False);
                Assert.That(errorMessage, Does.Contain("may only specify path"));
            }
        }

        [Test]
        [Category("Size.Small")]
        public void TryValidateRequest_WithUnityBuildProfileAndExplicitInputFields_ReturnsFalse ()
        {
            using (var scope = TemporaryDirectoryScope.Create())
            {
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var request = CreateUnityBuildProfileRequest(scope.ProjectPath, identity) with
                {
                    BuildTarget = "standaloneLinux64",
                };

                var result = BuildRunUnityIpcMethodHandler.TryValidateRequest(request, identity, out var errorMessage);

                Assert.That(result, Is.False);
                Assert.That(errorMessage, Does.Contain("must not be specified for unityBuildProfile"));
            }
        }

        [Test]
        [Category("Size.Small")]
        public void TryValidateRequest_WithExplicitInputAndUnityBuildProfile_ReturnsFalse ()
        {
            using (var scope = TemporaryDirectoryScope.Create())
            {
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var request = CreateRequest(scope.ProjectPath, identity) with
                {
                    UnityBuildProfile = new IpcUnityBuildProfileInput("Assets/BuildProfiles/Linux.asset"),
                };

                var result = BuildRunUnityIpcMethodHandler.TryValidateRequest(request, identity, out var errorMessage);

                Assert.That(result, Is.False);
                Assert.That(errorMessage, Does.Contain("must not be specified for explicit"));
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
                var outputLayout = RequireOutputLayout(request);
                request = scenario switch
                {
                    "missing" => request with
                    {
                        OutputLayout = null,
                    },
                    "shapeMismatch" => request with
                    {
                        OutputLayout = outputLayout with
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
                    new UnsupportedUnityBuildProfileInputResolver(),
                    new UnityProjectMutationAuditProbe(),
                    buildPipelineRunner,
                    new UnsupportedUnityBuildProfileBuildRunner(),
                    CreateExecuteMethodRunner(),
                    logRangeExporter,
                    identity,
                    timeoutScopeFactory,
                    new UnityLogRedactionScopeProvider(),
                    new UnityLogRingBuffer());
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
                Assert.That(readinessGate.CaptureObservationCallCount, Is.EqualTo(0));
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
                    new UnsupportedUnityBuildProfileInputResolver(),
                    new UnityProjectMutationAuditProbe(),
                    buildPipelineRunner,
                    new UnsupportedUnityBuildProfileBuildRunner(),
                    CreateExecuteMethodRunner(),
                    logRangeExporter,
                    identity,
                    timeoutScopeFactory,
                    new UnityLogRedactionScopeProvider(),
                    new UnityLogRingBuffer());
                var payload = CreateRequest(scope.ProjectPath, identity) with
                {
                    UnityBuildTarget = "StandaloneWindows64",
                };

                var response = await handler.HandleAsync(CreateIpcRequest(payload), CancellationToken.None);

                Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
                Assert.That(response.Errors.Count, Is.EqualTo(1));
                Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
                Assert.That(readinessGate.EnsureExecutionReadyCallCount, Is.EqualTo(0));
                Assert.That(readinessGate.CaptureObservationCallCount, Is.EqualTo(0));
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
                    new UnsupportedUnityBuildProfileInputResolver(),
                    new UnityProjectMutationAuditProbe(),
                    buildPipelineRunner,
                    new UnsupportedUnityBuildProfileBuildRunner(),
                    CreateExecuteMethodRunner(),
                    logRangeExporter,
                    identity,
                    timeoutScopeFactory,
                    new UnityLogRedactionScopeProvider(),
                    new UnityLogRingBuffer());
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
                Assert.That(readinessGate.CaptureObservationCallCount, Is.EqualTo(0));
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
                    new UnsupportedUnityBuildProfileInputResolver(),
                    new UnityProjectMutationAuditProbe(),
                    buildPipelineRunner,
                    new UnsupportedUnityBuildProfileBuildRunner(),
                    CreateExecuteMethodRunner(),
                    logRangeExporter,
                    identity,
                    new CountingTimeoutScopeFactory(),
                    new UnityLogRedactionScopeProvider(),
                    new UnityLogRingBuffer());
                var payload = CreateRequest(scope.ProjectPath, identity);
                var outputLayout = RequireOutputLayout(payload);
                var outputDirectoryPath = Path.GetDirectoryName(outputLayout.LocationPathName);
                if (string.IsNullOrEmpty(outputDirectoryPath))
                {
                    throw new AssertionException("Expected the build output layout to have a parent directory.");
                }

                Directory.CreateDirectory(outputDirectoryPath);
                File.WriteAllText(outputLayout.LocationPathName, "existing player");

                var response = await handler.HandleAsync(CreateIpcRequest(payload), CancellationToken.None);

                Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
                Assert.That(response.Errors.Count, Is.EqualTo(1));
                Assert.That(response.Errors[0].Code, Is.EqualTo(BuildErrorCodes.BuildArtifactWriteFailed));
                Assert.That(buildPipelineRunner.CallCount, Is.EqualTo(0));
                Assert.That(logRangeExporter.CallCount, Is.EqualTo(0));
            }
        }

        [Test]
        [Category("Size.Small")]
        public async Task HandleAsync_WithUnityBuildProfileRunnerInputFailure_ReturnsBuildProfileInvalid ()
        {
            using (var scope = TemporaryDirectoryScope.Create())
            using (var editorScope = new EditorTestScope().SuppressExistingPersistentDirtyObjects())
            {
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var requestPayload = CreateUnityBuildProfileRequest(scope.ProjectPath, identity);
                Directory.CreateDirectory(requestPayload.OutputPath);
                var buildProfileInputResolver = new SuccessfulUnityBuildProfileInputResolver();
                var buildProfileBuildRunner = new FailingUnityBuildProfileBuildRunner();
                var buildPipelineRunner = new CountingBuildPipelineRunner();
                var logRangeExporter = new CountingEditorLogRangeExporter();
                var handler = new BuildRunUnityIpcMethodHandler(
                    new UnityBuildPreconditionProbe(
                        new CountingReadinessGate(),
                        identity,
                        new StubServerVersionProvider("1.2.3"),
                        new CountingBuildTargetSupportProbe()),
                    buildProfileInputResolver,
                    new UnityProjectMutationAuditProbe(),
                    buildPipelineRunner,
                    buildProfileBuildRunner,
                    CreateExecuteMethodRunner(),
                    logRangeExporter,
                    identity,
                    new CountingTimeoutScopeFactory(),
                    new UnityLogRedactionScopeProvider(),
                    new UnityLogRingBuffer());

                var response = await handler.HandleAsync(CreateIpcRequest(requestPayload), CancellationToken.None);

                Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
                Assert.That(response.Errors.Count, Is.EqualTo(1));
                Assert.That(response.Errors[0].Code, Is.EqualTo(BuildErrorCodes.BuildUnityBuildProfileInvalid));
                Assert.That(buildProfileInputResolver.CallCount, Is.EqualTo(1));
                Assert.That(buildProfileBuildRunner.CallCount, Is.EqualTo(1));
                Assert.That(buildPipelineRunner.CallCount, Is.EqualTo(0));
                Assert.That(logRangeExporter.CallCount, Is.EqualTo(0));
                Assert.That(File.Exists(requestPayload.BuildReportPath), Is.False);
                Assert.That(File.Exists(requestPayload.BuildLogPath), Is.False);
            }
        }

        [Test]
        [Category("Size.Small")]
        public async Task HandleAsync_WithUnityBuildProfileTimeoutAfterApply_ReturnsApplyEvidence ()
        {
            using (var scope = TemporaryDirectoryScope.Create())
            using (var editorScope = new EditorTestScope().SuppressExistingPersistentDirtyObjects())
            {
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var requestPayload = CreateUnityBuildProfileRequest(scope.ProjectPath, identity) with
                {
                    TimeoutMilliseconds = 1,
                };
                var timeoutScopeFactory = new CancelableTimeoutScopeFactory();
                var buildProfileInputResolver = new TimeoutAfterBuildProfileInputResolver(timeoutScopeFactory);
                var buildPipelineRunner = new CountingBuildPipelineRunner();
                var logRangeExporter = new CountingEditorLogRangeExporter();
                var handler = new BuildRunUnityIpcMethodHandler(
                    new UnityBuildPreconditionProbe(
                        new CountingReadinessGate(),
                        identity,
                        new StubServerVersionProvider("1.2.3"),
                        new CountingBuildTargetSupportProbe()),
                    buildProfileInputResolver,
                    new UnityProjectMutationAuditProbe(),
                    buildPipelineRunner,
                    new UnsupportedUnityBuildProfileBuildRunner(),
                    CreateExecuteMethodRunner(),
                    logRangeExporter,
                    identity,
                    timeoutScopeFactory,
                    new UnityLogRedactionScopeProvider(),
                    new UnityLogRingBuffer());

                var response = await handler.HandleAsync(CreateIpcRequest(requestPayload), CancellationToken.None);

                Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
                Assert.That(response.Errors.Count, Is.EqualTo(1));
                Assert.That(response.Errors[0].Code, Is.EqualTo(IpcTransportErrorCodes.IpcTimeout));
                Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcBuildRunErrorPayload payload, out _), Is.True);
                Assert.That(payload.UnityBuildProfile, Is.Not.Null);
                Assert.That(payload.UnityBuildProfile!.Path, Is.EqualTo("Assets/BuildProfiles/Linux.asset"));
                Assert.That(payload.UnityBuildProfile.ApplyAudit, Is.Not.Null);
                Assert.That(payload.LifecycleBefore, Is.Not.Null);
                Assert.That(
                    payload.LifecycleBefore!.State.Generations.CompileGeneration,
                    Is.EqualTo(payload.UnityBuildProfile.ApplyAudit!.LifecycleAfter.State.Generations.CompileGeneration));
                Assert.That(payload.DirtyState, Is.Not.Null);
                Assert.That(payload.DirtyState!.Coverage, Is.EqualTo(ContractLiteralCodec.ToValue(IpcBuildDirtyStateCoverage.Full)));
                Assert.That(buildProfileInputResolver.CallCount, Is.EqualTo(1));
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
                var outputLayout = RequireOutputLayout(requestPayload);
                Directory.CreateDirectory(requestPayload.OutputPath);
                var reportArtifact = CreateReportArtifact(
                    ContractLiteralCodec.ToValue(reportResult),
                    outputLayout.LocationPathName,
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
                    new UnsupportedUnityBuildProfileInputResolver(),
                    new UnityProjectMutationAuditProbe(),
                    buildPipelineRunner,
                    new UnsupportedUnityBuildProfileBuildRunner(),
                    CreateExecuteMethodRunner(),
                    logRangeExporter,
                    identity,
                    new CountingTimeoutScopeFactory(),
                    new UnityLogRedactionScopeProvider(),
                    new UnityLogRingBuffer());

                var response = await handler.HandleAsync(CreateIpcRequest(requestPayload), CancellationToken.None);

                Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
                Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcBuildRunResponse payload, out _), Is.True);
                var report = payload.Report;
                if (report is null)
                {
                    throw new AssertionException("Expected a normalized BuildReport artifact in the terminal response.");
                }

                Assert.That(payload.RunId, Is.EqualTo(RunId));
                Assert.That(report.Result, Is.EqualTo(ContractLiteralCodec.ToValue(reportResult)));
                Assert.That(report.ErrorCount, Is.EqualTo(reportErrorCount));
                Assert.That(report.WarningCount, Is.EqualTo(reportWarningCount));
                Assert.That(payload.Logs.CompletionReason, Is.EqualTo(ContractLiteralCodec.ToValue(completionReason)));
                Assert.That(payload.Logs.EntryCount, Is.EqualTo(3));
                Assert.That(payload.Logs.ErrorCount, Is.EqualTo(1));
                Assert.That(payload.Logs.WarningCount, Is.EqualTo(1));
                Assert.That(buildPipelineRunner.CallCount, Is.EqualTo(1));
                Assert.That(buildPipelineRunner.LastOptions.locationPathName, Is.EqualTo(outputLayout.LocationPathName));
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
        public async Task HandleStreamingAsync_WithBuildPipelineRunner_WritesProgressFramesAndLogEntriesWithCursor ()
        {
            Assume.That(
                !string.IsNullOrWhiteSpace(Application.consoleLogPath) && File.Exists(Application.consoleLogPath),
                "Unity console log path is not available in this test environment.");

            using (var scope = TemporaryDirectoryScope.Create())
            using (var editorScope = new EditorTestScope().SuppressExistingPersistentDirtyObjects())
            {
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var requestPayload = CreateRequest(scope.ProjectPath, identity);
                var outputLayout = RequireOutputLayout(requestPayload);
                Directory.CreateDirectory(requestPayload.OutputPath);
                var unityLogStream = new UnityLogRingBuffer();
                var reportArtifact = CreateReportArtifact(
                    ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                    outputLayout.LocationPathName,
                    errorCount: 0,
                    warningCount: 1);
                var buildPipelineRunner = new CountingBuildPipelineRunner(
                    reportArtifact,
                    _ => unityLogStream.Write(
                        IpcUnityLogsSourceCodec.Runtime,
                        IpcDaemonLogsLevelCodec.Info,
                        "build pipeline progress log"));
                var handler = new BuildRunUnityIpcMethodHandler(
                    new UnityBuildPreconditionProbe(
                        new CountingReadinessGate(),
                        identity,
                        new StubServerVersionProvider("1.2.3"),
                        new CountingBuildTargetSupportProbe()),
                    new UnsupportedUnityBuildProfileInputResolver(),
                    new UnityProjectMutationAuditProbe(),
                    buildPipelineRunner,
                    new UnsupportedUnityBuildProfileBuildRunner(),
                    CreateExecuteMethodRunner(),
                    new CountingEditorLogRangeExporter(
                        string.Empty,
                        entryCount: 0,
                        errorCount: 0,
                        warningCount: 0),
                    identity,
                    new CountingTimeoutScopeFactory(),
                    new UnityLogRedactionScopeProvider(),
                    unityLogStream);
                var streamWriter = new CollectingIpcStreamFrameWriter("request-build-run-stream");

                var response = await handler.HandleStreamingAsync(
                    CreateStreamingIpcRequest(requestPayload),
                    streamWriter,
                    CancellationToken.None);

                Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
                Assert.That(streamWriter.ProgressFrames, Has.Count.EqualTo(5));
                Assert.That(streamWriter.ProgressFrames[0].Event, Is.EqualTo(BuildRunProgressEventNames.ReadinessCompleted));
                Assert.That(streamWriter.ProgressFrames[1].Event, Is.EqualTo(BuildRunProgressEventNames.RunnerResolved));
                Assert.That(streamWriter.ProgressFrames[2].Event, Is.EqualTo(BuildRunProgressEventNames.RunnerStarted));
                Assert.That(streamWriter.ProgressFrames[3].Event, Is.EqualTo(BuildRunProgressEventNames.LogEntry));
                Assert.That(streamWriter.ProgressFrames[4].Event, Is.EqualTo(BuildRunProgressEventNames.RunnerCompleted));

                Assert.That(IpcPayloadCodec.TryDeserialize(streamWriter.ProgressFrames[4].Payload, out BuildProgressEntry runnerCompleted, out _), Is.True);
                Assert.That(runnerCompleted.RunId, Is.EqualTo(RunId));
                Assert.That(runnerCompleted.Phase, Is.EqualTo("runnerResult"));
                Assert.That(runnerCompleted.RunnerStatus, Is.EqualTo("succeeded"));

                Assert.That(IpcPayloadCodec.TryDeserialize(streamWriter.ProgressFrames[3].Payload, out BuildLogEntry logEntry, out _), Is.True);
                Assert.That(logEntry.Message, Is.EqualTo("build pipeline progress log"));
                Assert.That(logEntry.Source, Is.EqualTo("unityLog"));
                Assert.That(logEntry.Cursor, Is.Not.Null);
                Assert.That(IpcLogCursorCodec.TryParse(logEntry.Cursor!, out _, out var logSequence), Is.True);
                Assert.That(logSequence, Is.EqualTo(1));

                Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcBuildRunResponse payload, out _), Is.True);
                Assert.That(payload.Logs.Window.CursorStart, Is.Not.Null);
                Assert.That(payload.Logs.Window.CursorEnd, Is.Not.Null);
            }
        }

        [Test]
        [Category("Size.Small")]
        public async Task HandleStreamingAsync_WithOversizedUnityLogMessage_TruncatesProgressLogEntry ()
        {
            Assume.That(
                !string.IsNullOrWhiteSpace(Application.consoleLogPath) && File.Exists(Application.consoleLogPath),
                "Unity console log path is not available in this test environment.");

            using (var scope = TemporaryDirectoryScope.Create())
            using (var editorScope = new EditorTestScope().SuppressExistingPersistentDirtyObjects())
            {
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var requestPayload = CreateRequest(scope.ProjectPath, identity);
                var outputLayout = RequireOutputLayout(requestPayload);
                Directory.CreateDirectory(requestPayload.OutputPath);
                var unityLogStream = new UnityLogRingBuffer();
                var oversizedMessage = new string('x', 70 * 1024);
                var reportArtifact = CreateReportArtifact(
                    ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                    outputLayout.LocationPathName,
                    errorCount: 0,
                    warningCount: 0);
                var buildPipelineRunner = new CountingBuildPipelineRunner(
                    reportArtifact,
                    _ => unityLogStream.Write(
                        IpcUnityLogsSourceCodec.Runtime,
                        IpcDaemonLogsLevelCodec.Info,
                        oversizedMessage));
                var handler = new BuildRunUnityIpcMethodHandler(
                    new UnityBuildPreconditionProbe(
                        new CountingReadinessGate(),
                        identity,
                        new StubServerVersionProvider("1.2.3"),
                        new CountingBuildTargetSupportProbe()),
                    new UnsupportedUnityBuildProfileInputResolver(),
                    new UnityProjectMutationAuditProbe(),
                    buildPipelineRunner,
                    new UnsupportedUnityBuildProfileBuildRunner(),
                    CreateExecuteMethodRunner(),
                    new CountingEditorLogRangeExporter(
                        string.Empty,
                        entryCount: 0,
                        errorCount: 0,
                        warningCount: 0),
                    identity,
                    new CountingTimeoutScopeFactory(),
                    new UnityLogRedactionScopeProvider(),
                    unityLogStream);
                var streamWriter = new CollectingIpcStreamFrameWriter("request-build-run-stream");

                var response = await handler.HandleStreamingAsync(
                    CreateStreamingIpcRequest(requestPayload),
                    streamWriter,
                    CancellationToken.None);

                Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
                Assert.That(streamWriter.ProgressFrames, Has.Count.EqualTo(5));
                Assert.That(streamWriter.ProgressFrames[3].Event, Is.EqualTo(BuildRunProgressEventNames.LogEntry));
                Assert.That(streamWriter.ProgressFrames[4].Event, Is.EqualTo(BuildRunProgressEventNames.RunnerCompleted));
                Assert.That(IpcPayloadCodec.TryDeserialize(streamWriter.ProgressFrames[3].Payload, out BuildLogEntry logEntry, out _), Is.True);
                Assert.That(logEntry.Message, Is.Not.EqualTo(oversizedMessage));
                Assert.That(logEntry.Message, Does.EndWith("[truncated for build progress stream]"));
                Assert.That(
                    Encoding.UTF8.GetByteCount(logEntry.Message),
                    Is.LessThanOrEqualTo(BuildLogEntryLimits.MaxMessageUtf8Bytes));
            }
        }

        [Test]
        [Category("Size.Small")]
        public async Task HandleStreamingAsync_WithExecuteMethodRunner_WritesRunnerProgressFrames ()
        {
            using (var scope = TemporaryDirectoryScope.Create())
            using (var editorScope = new EditorTestScope().SuppressExistingPersistentDirtyObjects())
            {
                executeMethodContext = null;
                executeMethodLogStream = null;
                UcliBuildRunnerContext.Current = null;
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var unityLogStream = new UnityLogRingBuffer();
                executeMethodLogStream = unityLogStream;
                var requestPayload = CreateExecuteMethodRequest(
                    scope.ProjectPath,
                    identity,
                    ExecuteMethodTypeName + ".HandlerExecuteMethodWritesProgressLog");
                var buildPipelineRunner = new CountingBuildPipelineRunner();
                var handler = new BuildRunUnityIpcMethodHandler(
                    new UnityBuildPreconditionProbe(
                        new CountingReadinessGate(),
                        identity,
                        new StubServerVersionProvider("1.2.3"),
                        new CountingBuildTargetSupportProbe()),
                    new UnsupportedUnityBuildProfileInputResolver(),
                    new UnityProjectMutationAuditProbe(),
                    buildPipelineRunner,
                    new UnsupportedUnityBuildProfileBuildRunner(),
                    CreateExecuteMethodRunner(),
                    new CountingEditorLogRangeExporter(
                        string.Empty,
                        entryCount: 0,
                        errorCount: 0,
                        warningCount: 0),
                    identity,
                    new CountingTimeoutScopeFactory(),
                    new UnityLogRedactionScopeProvider(),
                    unityLogStream);
                var streamWriter = new CollectingIpcStreamFrameWriter("request-build-run-stream");

                IpcResponse response;
                try
                {
                    response = await handler.HandleStreamingAsync(
                        CreateStreamingIpcRequest(requestPayload),
                        streamWriter,
                        CancellationToken.None);
                }
                finally
                {
                    executeMethodLogStream = null;
                }

                Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
                Assert.That(buildPipelineRunner.CallCount, Is.EqualTo(0));
                Assert.That(executeMethodContext, Is.Not.Null);
                Assert.That(UcliBuildRunnerContext.Current, Is.Null);
                Assert.That(streamWriter.ProgressFrames, Has.Count.EqualTo(5));
                Assert.That(streamWriter.ProgressFrames[0].Event, Is.EqualTo(BuildRunProgressEventNames.ReadinessCompleted));
                Assert.That(streamWriter.ProgressFrames[1].Event, Is.EqualTo(BuildRunProgressEventNames.RunnerResolved));
                Assert.That(streamWriter.ProgressFrames[2].Event, Is.EqualTo(BuildRunProgressEventNames.RunnerStarted));
                Assert.That(streamWriter.ProgressFrames[3].Event, Is.EqualTo(BuildRunProgressEventNames.LogEntry));
                Assert.That(streamWriter.ProgressFrames[4].Event, Is.EqualTo(BuildRunProgressEventNames.RunnerCompleted));

                Assert.That(IpcPayloadCodec.TryDeserialize(streamWriter.ProgressFrames[1].Payload, out BuildProgressEntry runnerResolved, out _), Is.True);
                Assert.That(runnerResolved.Phase, Is.EqualTo("runnerResolution"));
                Assert.That(runnerResolved.RunnerKind, Is.EqualTo("executeMethod"));
                Assert.That(runnerResolved.RunnerStatus, Is.Null);

                Assert.That(IpcPayloadCodec.TryDeserialize(streamWriter.ProgressFrames[2].Payload, out BuildProgressEntry runnerStarted, out _), Is.True);
                Assert.That(runnerStarted.Phase, Is.EqualTo("runnerInvocation"));
                Assert.That(runnerStarted.RunnerKind, Is.EqualTo("executeMethod"));
                Assert.That(runnerStarted.RunnerStatus, Is.Null);

                Assert.That(IpcPayloadCodec.TryDeserialize(streamWriter.ProgressFrames[3].Payload, out BuildLogEntry logEntry, out _), Is.True);
                Assert.That(logEntry.Message, Is.EqualTo("executeMethod progress log"));
                Assert.That(logEntry.Source, Is.EqualTo("unityLog"));

                Assert.That(IpcPayloadCodec.TryDeserialize(streamWriter.ProgressFrames[4].Payload, out BuildProgressEntry runnerCompleted, out _), Is.True);
                Assert.That(runnerCompleted.Phase, Is.EqualTo("runnerResult"));
                Assert.That(runnerCompleted.RunnerKind, Is.EqualTo("executeMethod"));
                Assert.That(runnerCompleted.RunnerStatus, Is.EqualTo("succeeded"));

                Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcBuildRunResponse payload, out _), Is.True);
                Assert.That(payload.RunnerResult, Is.Not.Null);
                Assert.That(payload.RunnerResult!.Source, Is.EqualTo("ucliBuildRunnerResult"));
                Assert.That(payload.RunnerResult.Status, Is.EqualTo("succeeded"));
            }
        }

        [Test]
        [Category("Size.Small")]
        public async Task HandleAsync_WithExecuteMethodRunner_RunsBridgeAndRedactsSecretEnvironmentValuesFromLog ()
        {
            Assume.That(
                !string.IsNullOrWhiteSpace(Application.consoleLogPath) && File.Exists(Application.consoleLogPath),
                "Unity console log path is not available in this test environment.");

            using (var scope = TemporaryDirectoryScope.Create())
            using (var editorScope = new EditorTestScope().SuppressExistingPersistentDirtyObjects())
            {
                executeMethodContext = null;
                UcliBuildRunnerContext.Current = null;
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var requestPayload = CreateExecuteMethodRequest(
                    scope.ProjectPath,
                    identity,
                    ExecuteMethodTypeName + ".HandlerExecuteMethodSuccess");
                var buildPipelineRunner = new CountingBuildPipelineRunner();
                var logRangeExporter = new CountingEditorLogRangeExporter(
                    "runner log contains release and secret-value-tail and secret-value",
                    entryCount: 1,
                    errorCount: 0,
                    warningCount: 0);
                var handler = new BuildRunUnityIpcMethodHandler(
                    new UnityBuildPreconditionProbe(
                        new CountingReadinessGate(),
                        identity,
                        new StubServerVersionProvider("1.2.3"),
                        new CountingBuildTargetSupportProbe()),
                    new UnsupportedUnityBuildProfileInputResolver(),
                    new UnityProjectMutationAuditProbe(),
                    buildPipelineRunner,
                    new UnsupportedUnityBuildProfileBuildRunner(),
                    CreateExecuteMethodRunner(),
                    logRangeExporter,
                    identity,
                    new CountingTimeoutScopeFactory(),
                    new UnityLogRedactionScopeProvider(),
                    new UnityLogRingBuffer());

                var response = await handler.HandleAsync(CreateIpcRequest(requestPayload), CancellationToken.None);

                Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
                Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcBuildRunResponse payload, out _), Is.True);
                Assert.That(buildPipelineRunner.CallCount, Is.EqualTo(0));
                Assert.That(logRangeExporter.CallCount, Is.EqualTo(1));
                Assert.That(executeMethodContext, Is.Not.Null);
                Assert.That(executeMethodContext!.Environment.Variables["UCLI_MODE"], Is.EqualTo("release"));
                Assert.That(executeMethodContext.Environment.Secrets["UCLI_SECRET"], Is.EqualTo("secret-value"));
                Assert.That(executeMethodContext.Environment.Secrets["UCLI_SECRET_LONG"], Is.EqualTo("secret-value-tail"));
                Assert.That(UcliBuildRunnerContext.Current, Is.Null);
                Assert.That(payload.RunnerResult, Is.Not.Null);
                Assert.That(payload.RunnerResult!.Source, Is.EqualTo("ucliBuildRunnerResult"));
                Assert.That(payload.RunnerResult.Status, Is.EqualTo("succeeded"));
                Assert.That(payload.RunnerResult.Outputs, Is.EqualTo(new[] { "player.txt" }));
                Assert.That(payload.RunnerResult.BuildReport, Is.Null);
                Assert.That(payload.Report, Is.Null);
                Assert.That(File.Exists(requestPayload.BuildReportPath), Is.False);
                Assert.That(File.Exists(requestPayload.BuildLogPath), Is.True);
                var persistedLog = File.ReadAllText(requestPayload.BuildLogPath);
                Assert.That(persistedLog, Does.Contain("release"));
                Assert.That(persistedLog, Does.Not.Contain("secret-value"));
                Assert.That(persistedLog, Does.Not.Contain("tail"));
                Assert.That(persistedLog, Does.Contain("[ucli redacted environment value]"));
            }
        }

        [Test]
        [Category("Size.Small")]
        public async Task HandleAsync_WithExecuteMethodRunner_RedactsSecretEnvironmentValuesFromUnityLogStream ()
        {
            Assume.That(
                !string.IsNullOrWhiteSpace(Application.consoleLogPath) && File.Exists(Application.consoleLogPath),
                "Unity console log path is not available in this test environment.");

            using (var scope = TemporaryDirectoryScope.Create())
            using (var editorScope = new EditorTestScope().SuppressExistingPersistentDirtyObjects())
            {
                executeMethodContext = null;
                UcliBuildRunnerContext.Current = null;
                var redactionScopeProvider = new UnityLogRedactionScopeProvider();
                var unityLogStream = new UnityLogRingBuffer();
                using (var unityLogCaptureService = new UnityLogCaptureService(new UnityLogCollector(
                           unityLogStream,
                           new UnityCompileMessageDedupeCache(),
                           redactionScopeProvider)))
                {
                    unityLogCaptureService.Start();
                    var identity = CreateProjectIdentity(scope.ProjectPath);
                    var requestPayload = CreateExecuteMethodRequest(
                        scope.ProjectPath,
                        identity,
                        ExecuteMethodTypeName + ".HandlerExecuteMethodLogsEnvironmentValues");
                    var handler = new BuildRunUnityIpcMethodHandler(
                        new UnityBuildPreconditionProbe(
                            new CountingReadinessGate(),
                            identity,
                            new StubServerVersionProvider("1.2.3"),
                            new CountingBuildTargetSupportProbe()),
                        new UnsupportedUnityBuildProfileInputResolver(),
                        new UnityProjectMutationAuditProbe(),
                        new CountingBuildPipelineRunner(),
                        new UnsupportedUnityBuildProfileBuildRunner(),
                        CreateExecuteMethodRunner(),
                        new CountingEditorLogRangeExporter(
                            string.Empty,
                            entryCount: 0,
                            errorCount: 0,
                            warningCount: 0),
                        identity,
                        new CountingTimeoutScopeFactory(),
                        redactionScopeProvider,
                        unityLogStream);

                    var response = await handler.HandleAsync(CreateIpcRequest(requestPayload), CancellationToken.None);

                    Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
                    Assert.That(executeMethodContext, Is.Not.Null);
                    var snapshot = unityLogStream.Snapshot();
                    Assert.That(snapshot.Events.Count, Is.GreaterThanOrEqualTo(1));
                    var foundRedactedRunnerLog = false;
                    var foundVariableValue = false;
                    for (var i = 0; i < snapshot.Events.Count; i++)
                    {
                        var unityLogEvent = snapshot.Events[i];
                        Assert.That(unityLogEvent.Message, Does.Not.Contain("secret-value"));
                        Assert.That(unityLogEvent.Message, Does.Not.Contain("tail"));
                        if (unityLogEvent.Message.Contains("release"))
                        {
                            foundVariableValue = true;
                        }

                        if (unityLogEvent.Message.Contains(SensitiveValueRedactor.Replacement))
                        {
                            foundRedactedRunnerLog = true;
                        }
                    }

                    Assert.That(foundRedactedRunnerLog, Is.True);
                    Assert.That(foundVariableValue, Is.True);
                }
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
                    new UnsupportedUnityBuildProfileInputResolver(),
                    new UnityProjectMutationAuditProbe(),
                    buildPipelineRunner,
                    new UnsupportedUnityBuildProfileBuildRunner(),
                    CreateExecuteMethodRunner(),
                    new CountingEditorLogRangeExporter(
                        string.Empty,
                        entryCount: 0,
                        errorCount: 0,
                        warningCount: 0),
                    identity,
                    new CountingTimeoutScopeFactory(),
                    new UnityLogRedactionScopeProvider(),
                    new UnityLogRingBuffer());

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
                    new UnsupportedUnityBuildProfileInputResolver(),
                    new UnityProjectMutationAuditProbe(),
                    buildPipelineRunner,
                    new UnsupportedUnityBuildProfileBuildRunner(),
                    CreateExecuteMethodRunner(),
                    logRangeExporter,
                    identity,
                    new CountingTimeoutScopeFactory(),
                    new UnityLogRedactionScopeProvider(),
                    new UnityLogRingBuffer());

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

        private static IpcBuildOutputLayout RequireOutputLayout (IpcBuildRunRequest request)
        {
            return request.OutputLayout
                ?? throw new AssertionException("Expected the build request to contain a resolved output layout.");
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
            if (!IpcBuildOutputLayoutResolver.TryResolve(outputPath, "standaloneLinux64", out var outputLayout)
                || outputLayout is null)
            {
                throw new InvalidOperationException("Test build target must resolve a BuildPipeline output layout.");
            }

            return new IpcBuildRunRequest(
                RunId: RunId,
                InputKind: ContractLiteralCodec.ToValue(BuildProfileInputsKind.Explicit),
                BuildTarget: "standaloneLinux64",
                UnityBuildTarget: "StandaloneLinux64",
                SceneSource: "explicit",
                ScenePaths: new[] { "Assets/Scenes/SampleScene.unity" },
                Development: true,
                OutputPath: outputPath,
                OutputLayout: outputLayout,
                BuildReportPath: Path.Combine(artifactsDirectory, UcliStoragePathNames.BuildReportFileName),
                BuildLogPath: Path.Combine(artifactsDirectory, UcliStoragePathNames.BuildLogFileName),
                AllowedEditorModes: new[] { ContractLiteralCodec.ToValue(DaemonEditorMode.Batchmode) },
                ProjectMutationMode: ContractLiteralCodec.ToValue(BuildProfileProjectMutationMode.Forbid),
                RunnerKind: ContractLiteralCodec.ToValue(IpcBuildRunnerKind.BuildPipeline))
            {
                ProfileDigest = new string('a', 64),
            };
        }

        private static IpcBuildRunRequest CreateExecuteMethodRequest (
            string projectPath,
            IpcProjectIdentity identity,
            string method)
        {
            return CreateRequest(projectPath, identity) with
            {
                OutputLayout = null,
                RunnerKind = ContractLiteralCodec.ToValue(IpcBuildRunnerKind.ExecuteMethod),
                ProfilePath = Path.Combine(projectPath, "build.ucli.json"),
                ProfileDigest = new string('a', 64),
                RunnerMethod = method,
                RunnerArguments = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["argument"] = "value",
                },
                RunnerEnvironmentVariables = new[] { "UCLI_MODE" },
                RunnerEnvironmentSecrets = new[] { "UCLI_SECRET", "UCLI_SECRET_LONG" },
                RunnerEnvironmentVariableValues = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["UCLI_MODE"] = "release",
                },
                RunnerEnvironmentSecretValues = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["UCLI_SECRET"] = "secret-value",
                    ["UCLI_SECRET_LONG"] = "secret-value-tail",
                },
            };
        }

        private static BuildExecuteMethodRunner CreateExecuteMethodRunner ()
        {
            return new BuildExecuteMethodRunner(new BuildExecuteMethodResolver());
        }

        public static UcliBuildRunnerResult HandlerExecuteMethodSuccess (UcliBuildRunnerContext context)
        {
            executeMethodContext = context;
            WriteExecuteMethodOutput(context, "player.txt");
            return UcliBuildRunnerResult.Succeeded(new[] { "player.txt" }, 2500, warningCount: 1);
        }

        public static UcliBuildRunnerResult HandlerExecuteMethodLogsEnvironmentValues (UcliBuildRunnerContext context)
        {
            executeMethodContext = context;
            Debug.Log("runner log contains " + context.Environment.Variables["UCLI_MODE"] + " and " + context.Environment.Secrets["UCLI_SECRET_LONG"]);
            WriteExecuteMethodOutput(context, "player.txt");
            return UcliBuildRunnerResult.Succeeded(new[] { "player.txt" }, 2500, warningCount: 1);
        }

        public static UcliBuildRunnerResult HandlerExecuteMethodWritesProgressLog (UcliBuildRunnerContext context)
        {
            executeMethodContext = context;
            executeMethodLogStream!.Write(
                IpcUnityLogsSourceCodec.Runtime,
                IpcDaemonLogsLevelCodec.Info,
                "executeMethod progress log");
            WriteExecuteMethodOutput(context, "player.txt");
            return UcliBuildRunnerResult.Succeeded(new[] { "player.txt" }, 2500, warningCount: 1);
        }

        private static void WriteExecuteMethodOutput (
            UcliBuildRunnerContext context,
            string relativePath)
        {
            var outputPath = Path.Combine(context.OutputDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(outputPath, "player output");
        }

        private static IpcBuildRunRequest CreateUnityBuildProfileRequest (
            string projectPath,
            IpcProjectIdentity identity)
        {
            var explicitRequest = CreateRequest(projectPath, identity);
            return explicitRequest with
            {
                InputKind = ContractLiteralCodec.ToValue(BuildProfileInputsKind.UnityBuildProfile),
                BuildTarget = null,
                UnityBuildTarget = null,
                SceneSource = null,
                ScenePaths = Array.Empty<string>(),
                Development = false,
                OutputLayout = null,
                UnityBuildProfile = new IpcUnityBuildProfileInput("Assets/BuildProfiles/Linux.asset"),
            };
        }

        private static IpcUnityBuildProfileInput CreateAppliedUnityBuildProfileInput (string path)
        {
            var lifecycle = Create();
            return new IpcUnityBuildProfileInput(
                Path: path,
                Digest: new string('f', 64),
                ApplyAudit: new IpcUnityBuildProfileApplyAudit(
                    Applied: true,
                    LifecycleBefore: lifecycle,
                    LifecycleAfter: lifecycle,
                    DirtyStateAfter: new IpcBuildDirtyState(
                        Checked: true,
                        Dirty: false,
                        Coverage: ContractLiteralCodec.ToValue(IpcBuildDirtyStateCoverage.Full),
                        Items: Array.Empty<IpcBuildDirtyStateItem>())));
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

        private static IpcRequest CreateStreamingIpcRequest (IpcBuildRunRequest payload)
        {
            return new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "request-build-run-stream",
                SessionToken: "session-token",
                Method: IpcMethodNames.BuildRun,
                Payload: IpcPayloadCodec.SerializeToElement(payload),
                responseMode: IpcResponseMode.Stream);
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

        private static UnityEditorObservation CreateObservation ()
        {
            return new UnityEditorObservation(
                state: new UnityEditorStateSnapshot(
                    editorMode: DaemonEditorMode.Batchmode,
                    lifecycleState: IpcEditorLifecycleState.Ready,
                    compileState: IpcCompileState.Ready,
                    generations: new IpcUnityGenerationSnapshot(1, 1, 1, 1),
                    playMode: new IpcPlayModeSnapshot(
                        State: IpcPlayModeState.Stopped,
                        Transition: IpcPlayModeTransition.None,
                        IsPlaying: false,
                        IsPlayingOrWillChangePlaymode: false)),
                observedAtUtc: new DateTimeOffset(2026, 6, 12, 0, 0, 0, TimeSpan.Zero));
        }

        private sealed class CountingReadinessGate : IUnityEditorReadinessGate
        {
            public int CaptureObservationCallCount { get; private set; }

            public int EnsureExecutionReadyCallCount { get; private set; }

            public UnityEditorObservation CaptureObservation ()
            {
                CaptureObservationCallCount++;
                return CreateObservation();
            }

            public Task<UnityEditorExecutionReadinessResult> EnsureExecutionReadyAsync (
                bool failFast,
                CancellationToken cancellationToken = default,
                bool allowPlayMode = false)
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnsureExecutionReadyCallCount++;
                return Task.FromResult(UnityEditorExecutionReadinessResult.Ready(CreateObservation()));
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

        private sealed class SuccessfulUnityBuildProfileInputResolver : IUnityBuildProfileInputResolver
        {
            public int CallCount { get; private set; }

            public Task<UnityBuildProfileInputResolutionResult> ResolveAsync (
                IpcBuildRunRequest request,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallCount++;
                if (!IpcBuildOutputLayoutResolver.TryResolve(request.OutputPath, "standaloneLinux64", out var outputLayout))
                {
                    throw new InvalidOperationException("Test output layout must resolve.");
                }

                var preconditionInput = new UnityBuildPreconditionInput(
                    InputKind: ContractLiteralCodec.ToValue(BuildProfileInputsKind.UnityBuildProfile),
                    BuildTarget: "standaloneLinux64",
                    UnityBuildTarget: "StandaloneLinux64",
                    SceneSource: ContractLiteralCodec.ToValue(BuildProfileSceneSource.UnityBuildProfile),
                    ScenePaths: new[] { "Assets/Scenes/SampleScene.unity" },
                    Development: false,
                    AllowedEditorModes: request.AllowedEditorModes);
                var unityBuildProfile = CreateAppliedUnityBuildProfileInput(request.UnityBuildProfile!.Path);

                return Task.FromResult(UnityBuildProfileInputResolutionResult.Success(
                    preconditionInput,
                    outputLayout!,
                    unityBuildProfile));
            }
        }

        private sealed class TimeoutAfterBuildProfileInputResolver : IUnityBuildProfileInputResolver
        {
            private readonly CancelableTimeoutScopeFactory timeoutScopeFactory;

            public TimeoutAfterBuildProfileInputResolver (CancelableTimeoutScopeFactory timeoutScopeFactory)
            {
                this.timeoutScopeFactory = timeoutScopeFactory;
            }

            public int CallCount { get; private set; }

            public Task<UnityBuildProfileInputResolutionResult> ResolveAsync (
                IpcBuildRunRequest request,
                CancellationToken cancellationToken = default)
            {
                CallCount++;
                if (!IpcBuildOutputLayoutResolver.TryResolve(request.OutputPath, "standaloneLinux64", out var outputLayout))
                {
                    throw new InvalidOperationException("Test output layout must resolve.");
                }

                var preconditionInput = new UnityBuildPreconditionInput(
                    InputKind: ContractLiteralCodec.ToValue(BuildProfileInputsKind.UnityBuildProfile),
                    BuildTarget: "standaloneLinux64",
                    UnityBuildTarget: "StandaloneLinux64",
                    SceneSource: ContractLiteralCodec.ToValue(BuildProfileSceneSource.UnityBuildProfile),
                    ScenePaths: new[] { "Assets/Scenes/SampleScene.unity" },
                    Development: false,
                    AllowedEditorModes: request.AllowedEditorModes);
                var unityBuildProfile = CreateAppliedUnityBuildProfileInput(request.UnityBuildProfile!.Path);
                timeoutScopeFactory.Cancel();
                return Task.FromResult(UnityBuildProfileInputResolutionResult.Success(
                    preconditionInput,
                    outputLayout!,
                    unityBuildProfile));
            }
        }

        private sealed class FailingUnityBuildProfileBuildRunner : IUnityBuildProfileBuildRunner
        {
            public int CallCount { get; private set; }

            public IpcBuildReportArtifact? Run (
                IpcUnityBuildProfileInput unityBuildProfile,
                UnityBuildResolvedInput resolvedInput,
                IpcBuildOutputLayout outputLayout)
            {
                CallCount++;
                throw new UnityBuildProfileInputException("Unity Build Profile asset could not be used.");
            }
        }

        private static IpcUnityEditorObservation Create ()
        {
            return new IpcUnityEditorObservation(
                serverVersion: "1.2.3",
                unityVersion: "6000.1.4f1",
                projectFingerprint: ProjectFingerprint,
                state: new UnityEditorStateSnapshot(
                    editorMode: DaemonEditorMode.Batchmode,
                    lifecycleState: IpcEditorLifecycleState.Ready,
                    compileState: IpcCompileState.Ready,
                    generations: new IpcUnityGenerationSnapshot(11, 12, 13, 14),
                    playMode: new IpcPlayModeSnapshot(
                        State: IpcPlayModeState.Stopped,
                        Transition: IpcPlayModeTransition.None,
                        IsPlaying: false,
                        IsPlayingOrWillChangePlaymode: false)),
                observedAtUtc: new DateTimeOffset(2026, 6, 12, 0, 0, 0, TimeSpan.Zero),
                actionRequired: null,
                primaryDiagnostic: null);
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
                IEnumerable<string>? redactionValues = null,
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

                File.WriteAllText(destinationPath, Redact(contents, redactionValues));
                return Task.FromResult(summary);
            }

            private static string Redact (
                string value,
                IEnumerable<string>? redactionValues)
            {
                if (redactionValues == null)
                {
                    return value;
                }

                var values = new List<string>();
                foreach (var redactionValue in redactionValues)
                {
                    if (!string.IsNullOrEmpty(redactionValue) && !values.Contains(redactionValue))
                    {
                        values.Add(redactionValue);
                    }
                }

                values.Sort(static (left, right) => right.Length.CompareTo(left.Length));
                var redacted = value;
                for (var i = 0; i < values.Count; i++)
                {
                    redacted = redacted.Replace(values[i], "[ucli redacted environment value]");
                }

                return redacted;
            }
        }

        private sealed class CollectingIpcStreamFrameWriter : IIpcStreamFrameWriter
        {
            private readonly string requestId;

            public CollectingIpcStreamFrameWriter (string requestId)
            {
                this.requestId = requestId;
            }

            public List<IpcStreamFrame> ProgressFrames { get; } = new List<IpcStreamFrame>();

            public ValueTask WriteProgressAsync<TPayload> (
                string eventName,
                TPayload payload,
                CancellationToken cancellationToken = default)
                where TPayload : notnull
            {
                cancellationToken.ThrowIfCancellationRequested();
                ProgressFrames.Add(new IpcStreamFrame(
                    IpcProtocol.CurrentVersion,
                    requestId,
                    IpcStreamFrameKinds.Progress,
                    eventName,
                    IpcPayloadCodec.SerializeToElement(payload),
                    null));
                return default;
            }

            public ValueTask WriteTerminalAsync (
                IpcResponse response,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new InvalidOperationException("build.run handler returns the terminal response to the dispatcher.");
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

        private sealed class CancelableTimeoutScopeFactory : IIpcRequestTimeoutScopeFactory
        {
            private CancellationTokenSource? cancellationTokenSource;

            public int CallCount { get; private set; }

            public IIpcRequestTimeoutScope CreateLinked (
                int? timeoutMilliseconds,
                CancellationToken cancellationToken)
            {
                CallCount++;
                cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                return new CancelableTimeoutScope(cancellationTokenSource);
            }

            public void Cancel ()
            {
                cancellationTokenSource?.Cancel();
            }
        }

        private sealed class CancelableTimeoutScope : IIpcRequestTimeoutScope
        {
            private readonly CancellationTokenSource cancellationTokenSource;

            public CancelableTimeoutScope (CancellationTokenSource cancellationTokenSource)
            {
                this.cancellationTokenSource = cancellationTokenSource;
            }

            public CancellationToken Token => cancellationTokenSource.Token;

            public bool IsTimeoutCancellationRequested => cancellationTokenSource.IsCancellationRequested;

            public void Dispose ()
            {
                cancellationTokenSource.Dispose();
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
