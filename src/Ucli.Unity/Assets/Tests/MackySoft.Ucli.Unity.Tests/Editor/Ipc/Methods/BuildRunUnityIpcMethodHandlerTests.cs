using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Build;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Project;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class BuildRunUnityIpcMethodHandlerTests
    {
        private static readonly Guid RunId = Guid.Parse("00000000-0000-0000-0000-000000000603");
        private static readonly Sha256Digest ProfileDigest = Sha256Digest.Parse(new string('a', 64));
        private const string ExecuteMethodTypeName = "MackySoft.Ucli.Unity.Tests.BuildRunUnityIpcMethodHandlerTests";

        private static readonly ProjectFingerprint ProjectFingerprint =
            ProjectFingerprintTestFactory.Create("project-fingerprint");

        private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

        private static UcliBuildRunnerContext? executeMethodContext;
        private static IUnityLogStream? executeMethodLogStream;
        private static Action? executeMethodCallback;

        [Test]
        [Category("Size.Small")]
        public void TryValidateRequest_WithExpectedArtifactPaths_ReturnsTrue ()
        {
            using (var scope = TemporaryDirectoryScope.Create())
            {
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var request = CreateRequest(scope.ProjectPath, identity);

                var result = BuildRunUnityIpcMethodHandler.TryValidateRequest(
                    BuildRunExecutionRequest.Create(request),
                    identity,
                    out var errorMessage);

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
                var request = CreateRequest(scope.ProjectPath, identity, projectMutationMode: mode);

                var result = BuildRunUnityIpcMethodHandler.TryValidateRequest(
                    BuildRunExecutionRequest.Create(request),
                    identity,
                    out var errorMessage);

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

                var result = BuildRunUnityIpcMethodHandler.TryValidateRequest(
                    BuildRunExecutionRequest.Create(request),
                    identity,
                    out var errorMessage);

                Assert.That(result, Is.True, errorMessage);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void TryValidateRequest_WithOutputLayoutOutsideExpectedLayout_ReturnsFalse ()
        {
            using (var scope = TemporaryDirectoryScope.Create())
            {
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var request = CreateRequest(
                    scope.ProjectPath,
                    identity,
                    outputLayout: new IpcBuildOutputLayout(
                        Shape: IpcBuildOutputLayoutShape.File,
                        LocationPathName: Path.Combine(scope.RootPath, "outside", "Player")));

                var result = BuildRunUnityIpcMethodHandler.TryValidateRequest(
                    BuildRunExecutionRequest.Create(request),
                    identity,
                    out var errorMessage);

                Assert.That(result, Is.False);
                Assert.That(errorMessage, Does.Contain("outputLayout"));
            }
        }

        [TestCase("shapeMismatch")]
        [TestCase("unsupportedTarget")]
        [Category("Size.Small")]
        public void TryValidateRequest_WithInvalidOutputLayout_ReturnsFalse (string scenario)
        {
            using (var scope = TemporaryDirectoryScope.Create())
            {
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var validRequest = CreateRequest(scope.ProjectPath, identity);
                var validOutputLayout = RequireOutputLayout(validRequest);
                var request = CreateRequest(
                    scope.ProjectPath,
                    identity,
                    buildTarget: scenario == "unsupportedTarget"
                        ? BuildTargetStableName.Switch
                        : BuildTargetStableName.StandaloneLinux64,
                    outputLayout: scenario == "shapeMismatch"
                        ? new IpcBuildOutputLayout(IpcBuildOutputLayoutShape.Directory, validOutputLayout.LocationPathName)
                        : validOutputLayout);

                var result = BuildRunUnityIpcMethodHandler.TryValidateRequest(
                    BuildRunExecutionRequest.Create(request),
                    identity,
                    out var errorMessage);

                Assert.That(result, Is.False);
                Assert.That(errorMessage, Does.Contain("outputLayout"));
            }
        }

        [TestCase("output")]
        [TestCase("report")]
        [TestCase("log")]
        [Category("Size.Small")]
        public void CreateExecutionRequest_WithRelativeArtifactPath_ThrowsPathValidationException (string artifact)
        {
            using (var scope = TemporaryDirectoryScope.Create())
            {
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var relativeArtifactPath = Path.Combine("relative", artifact);
                var request = CreateRequest(
                    scope.ProjectPath,
                    identity,
                    outputPath: artifact == "output" ? relativeArtifactPath : null,
                    buildReportPath: artifact == "report" ? relativeArtifactPath : null,
                    buildLogPath: artifact == "log" ? relativeArtifactPath : null);

                var exception = Assert.Throws<PathValidationException>(
                    () => BuildRunExecutionRequest.Create(request));

                Assert.That(
                    exception!.Failure.Kind,
                    Is.EqualTo(PathValidationFailureKind.ExpectedAbsolutePath));
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
                var outsidePath = Path.Combine(scope.RootPath, "outside", artifact);
                var request = CreateRequest(
                    scope.ProjectPath,
                    identity,
                    outputPath: artifact == "output" ? outsidePath : null,
                    buildReportPath: artifact == "report" ? outsidePath : null,
                    buildLogPath: artifact == "log" ? outsidePath : null);

                var result = BuildRunUnityIpcMethodHandler.TryValidateRequest(
                    BuildRunExecutionRequest.Create(request),
                    identity,
                    out var errorMessage);

                Assert.That(result, Is.False);
                Assert.That(errorMessage, Does.Contain("expected uCLI build artifact layout"));
            }
        }

        [TestCase("output", "outside")]
        [TestCase("report", "outside")]
        [TestCase("log", "outside")]
        [TestCase("output", "relative")]
        [TestCase("report", "relative")]
        [TestCase("log", "relative")]
        [Category("Size.Small")]
        public async Task HandleAsync_WithInvalidArtifactPath_ReturnsInvalidArgumentWithoutSideEffects (
            string artifact,
            string invalidPathKind)
        {
            using (var scope = TemporaryDirectoryScope.Create())
            {
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var readinessGate = new CountingReadinessGate();
                var buildPipelineRunner = new CountingBuildPipelineRunner();
                var logRangeExporter = new CountingEditorLogRangeExporter();
                var handler = new BuildRunUnityIpcMethodHandler(
                    new UnityBuildPreconditionProbe(
                        readinessGate,
                        identity.IpcIdentity,
                        new StubServerVersionProvider("1.2.3"),
                        new CountingBuildTargetSupportProbe()),
                    new UnsupportedUnityBuildProfileInputResolver(),
                    new UnityProjectMutationAuditProbe(),
                    buildPipelineRunner,
                    new UnsupportedUnityBuildProfileBuildRunner(),
                    CreateExecuteMethodRunner(),
                    logRangeExporter,
                    identity,
                    new UnityLogRedactionScopeProvider(),
                    new UnityLogRingBuffer(),
                    new ImmediateUnityMutationLaneControl(),
                    new ImmediateUnityEditorUpdateAwaiter());
                var invalidPath = invalidPathKind == "relative"
                    ? Path.Combine("relative", artifact)
                    : Path.Combine(scope.RootPath, "outside", artifact);
                var payload = CreateRequest(
                    scope.ProjectPath,
                    identity,
                    outputPath: artifact == "output" ? invalidPath : null,
                    buildReportPath: artifact == "report" ? invalidPath : null,
                    buildLogPath: artifact == "log" ? invalidPath : null);
                var ipcRequest = CreateIpcRequest(payload);

                var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(handler, ipcRequest, CancellationToken.None);

                Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
                Assert.That(response.Errors.Count, Is.EqualTo(1));
                Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
                Assert.That(readinessGate.EnsureExecutionReadyCallCount, Is.EqualTo(0));
                Assert.That(readinessGate.CaptureObservationCallCount, Is.EqualTo(0));
                Assert.That(buildPipelineRunner.CallCount, Is.EqualTo(0));
                Assert.That(logRangeExporter.CallCount, Is.EqualTo(0));
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
                        identity.IpcIdentity,
                        new StubServerVersionProvider("1.2.3"),
                        new CountingBuildTargetSupportProbe()),
                    new UnsupportedUnityBuildProfileInputResolver(),
                    new UnityProjectMutationAuditProbe(),
                    buildPipelineRunner,
                    new UnsupportedUnityBuildProfileBuildRunner(),
                    CreateExecuteMethodRunner(),
                    logRangeExporter,
                    identity,
                    new UnityLogRedactionScopeProvider(),
                    new UnityLogRingBuffer(),
                    new ImmediateUnityMutationLaneControl(),
                    new ImmediateUnityEditorUpdateAwaiter());
                var payload = CreateRequest(scope.ProjectPath, identity);
                var outputLayout = RequireOutputLayout(payload);
                var outputDirectoryPath = Path.GetDirectoryName(outputLayout.LocationPathName);
                if (string.IsNullOrEmpty(outputDirectoryPath))
                {
                    throw new AssertionException("Expected the build output layout to have a parent directory.");
                }

                Directory.CreateDirectory(outputDirectoryPath);
                File.WriteAllText(outputLayout.LocationPathName, "existing player");

                var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(handler, CreateIpcRequest(payload), CancellationToken.None);

                Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
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
                        identity.IpcIdentity,
                        new StubServerVersionProvider("1.2.3"),
                        new CountingBuildTargetSupportProbe()),
                    buildProfileInputResolver,
                    new UnityProjectMutationAuditProbe(),
                    buildPipelineRunner,
                    buildProfileBuildRunner,
                    CreateExecuteMethodRunner(),
                    logRangeExporter,
                    identity,
                    new UnityLogRedactionScopeProvider(),
                    new UnityLogRingBuffer(),
                    new ImmediateUnityMutationLaneControl(),
                    new ImmediateUnityEditorUpdateAwaiter());

                var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(handler, CreateIpcRequest(requestPayload), CancellationToken.None);

                Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
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
                var requestPayload = CreateUnityBuildProfileRequest(scope.ProjectPath, identity);
                using var executionDeadlineCancellationTokenSource = new CancellationTokenSource();
                using var executionCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                    executionDeadlineCancellationTokenSource.Token);
                using var requestCancellation = new IpcRequestCancellation(
                    executionCancellationTokenSource.Token,
                    executionDeadlineCancellationTokenSource.Token,
                    CancellationToken.None);
                var buildProfileInputResolver = new TimeoutAfterBuildProfileInputResolver(
                    executionDeadlineCancellationTokenSource.Cancel);
                var buildPipelineRunner = new CountingBuildPipelineRunner();
                var logRangeExporter = new CountingEditorLogRangeExporter();
                var handler = new BuildRunUnityIpcMethodHandler(
                    new UnityBuildPreconditionProbe(
                        new CountingReadinessGate(),
                        identity.IpcIdentity,
                        new StubServerVersionProvider("1.2.3"),
                        new CountingBuildTargetSupportProbe()),
                    buildProfileInputResolver,
                    new UnityProjectMutationAuditProbe(),
                    buildPipelineRunner,
                    new UnsupportedUnityBuildProfileBuildRunner(),
                    CreateExecuteMethodRunner(),
                    logRangeExporter,
                    identity,
                    new UnityLogRedactionScopeProvider(),
                    new UnityLogRingBuffer(),
                    new ImmediateUnityMutationLaneControl(),
                    new ImmediateUnityEditorUpdateAwaiter());

                var response = await handler.HandleAsync(
                    ValidatedUnityIpcRequestTestFactory.Create(CreateIpcRequest(requestPayload)),
                    requestCancellation);

                Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
                Assert.That(response.Errors.Count, Is.EqualTo(1));
                Assert.That(response.Errors[0].Code, Is.EqualTo(IpcTransportErrorCodes.IpcTimeout));
                Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcBuildRunErrorPayload payload, out _), Is.True);
                Assert.That(payload.UnityBuildProfile, Is.Not.Null);
                Assert.That(payload.UnityBuildProfile!.Path.Value, Is.EqualTo("Assets/BuildProfiles/Linux.asset"));
                Assert.That(payload.UnityBuildProfile.ApplyAudit, Is.Not.Null);
                Assert.That(payload.LifecycleBefore, Is.Not.Null);
                Assert.That(
                    payload.LifecycleBefore!.State.Generations.CompileGeneration,
                    Is.EqualTo(payload.UnityBuildProfile.ApplyAudit!.LifecycleAfter.State.Generations.CompileGeneration));
                Assert.That(payload.DirtyState, Is.Not.Null);
                Assert.That(payload.DirtyState!.Coverage, Is.EqualTo(IpcBuildDirtyStateCoverage.Full));
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
                    reportResult,
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
                        identity.IpcIdentity,
                        new StubServerVersionProvider("1.2.3"),
                        new CountingBuildTargetSupportProbe()),
                    new UnsupportedUnityBuildProfileInputResolver(),
                    new UnityProjectMutationAuditProbe(),
                    buildPipelineRunner,
                    new UnsupportedUnityBuildProfileBuildRunner(),
                    CreateExecuteMethodRunner(),
                    logRangeExporter,
                    identity,
                    new UnityLogRedactionScopeProvider(),
                    new UnityLogRingBuffer(),
                    new ImmediateUnityMutationLaneControl(),
                    new ImmediateUnityEditorUpdateAwaiter());

                var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(handler, CreateIpcRequest(requestPayload), CancellationToken.None);

                Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
                Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcBuildRunResponse payload, out _), Is.True);
                var report = payload.Report;
                if (report is null)
                {
                    throw new AssertionException("Expected a normalized BuildReport artifact in the terminal response.");
                }

                Assert.That(payload.RunId, Is.EqualTo(RunId));
                Assert.That(report.Result, Is.EqualTo(reportResult));
                Assert.That(report.ErrorCount, Is.EqualTo(reportErrorCount));
                Assert.That(report.WarningCount, Is.EqualTo(reportWarningCount));
                Assert.That(payload.Logs.CompletionReason, Is.EqualTo(completionReason));
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
        public async Task HandleAsync_WhenExecuteMethodSucceeds_CompletesMutationAfterLifecycleCaptureAndEditorUpdate ()
        {
            using (var scope = TemporaryDirectoryScope.Create())
            using (var editorScope = new EditorTestScope().SuppressExistingPersistentDirtyObjects())
            {
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var requestPayload = CreateExecuteMethodRequest(
                    scope.ProjectPath,
                    identity,
                    ExecuteMethodTypeName + ".HandlerExecuteMethodSuccess");
                Directory.CreateDirectory(requestPayload.OutputPath);
                var readinessGate = new CountingReadinessGate();
                var mutationLaneControl = new ImmediateUnityMutationLaneControl();
                var editorUpdateAwaiter = new ControllableUnityEditorUpdateAwaiter();
                var handler = CreateCheckpointTestHandler(
                    identity,
                    readinessGate,
                    new CountingBuildPipelineRunner(),
                    mutationLaneControl,
                    editorUpdateAwaiter);

                var responseTask = UnityIpcMethodHandlerTestInvoker
                    .HandleAsync(handler, CreateIpcRequest(requestPayload), CancellationToken.None)
                    .AsTask();
                await TestAwaiter.WaitAsync(
                    editorUpdateAwaiter.WaitStarted,
                    "successful execute-method post-update checkpoint",
                    SignalWaitTimeout);

                Assert.That(readinessGate.CaptureObservationCallCount, Is.EqualTo(1));
                Assert.That(mutationLaneControl.CompleteCount, Is.EqualTo(0));
                Assert.That(responseTask.IsCompleted, Is.False);

                editorUpdateAwaiter.Release();
                var response = await TestAwaiter.WaitAsync(
                    responseTask,
                    "successful execute-method response after post-update checkpoint",
                    SignalWaitTimeout);

                Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
                Assert.That(mutationLaneControl.CompleteCount, Is.EqualTo(1));
            }
        }

        [Test]
        [Category("Size.Small")]
        public async Task HandleAsync_WhenExecuteMethodThrows_CompletesMutationAfterLifecycleCaptureAndEditorUpdate ()
        {
            using (var scope = TemporaryDirectoryScope.Create())
            using (var editorScope = new EditorTestScope().SuppressExistingPersistentDirtyObjects())
            {
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var requestPayload = CreateExecuteMethodRequest(
                    scope.ProjectPath,
                    identity,
                    ExecuteMethodTypeName + ".HandlerExecuteMethodThrows");
                Directory.CreateDirectory(requestPayload.OutputPath);
                var readinessGate = new CountingReadinessGate();
                var mutationLaneControl = new ImmediateUnityMutationLaneControl();
                var editorUpdateAwaiter = new ControllableUnityEditorUpdateAwaiter();
                var handler = CreateCheckpointTestHandler(
                    identity,
                    readinessGate,
                    new CountingBuildPipelineRunner(),
                    mutationLaneControl,
                    editorUpdateAwaiter);

                var responseTask = UnityIpcMethodHandlerTestInvoker
                    .HandleAsync(handler, CreateIpcRequest(requestPayload), CancellationToken.None)
                    .AsTask();
                await TestAwaiter.WaitAsync(
                    editorUpdateAwaiter.WaitStarted,
                    "failed execute-method post-update checkpoint",
                    SignalWaitTimeout);

                Assert.That(readinessGate.CaptureObservationCallCount, Is.EqualTo(1));
                Assert.That(mutationLaneControl.CompleteCount, Is.EqualTo(0));
                Assert.That(responseTask.IsCompleted, Is.False);

                editorUpdateAwaiter.Release();
                var response = await TestAwaiter.WaitAsync(
                    responseTask,
                    "failed execute-method response after post-update checkpoint",
                    SignalWaitTimeout);

                Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
                Assert.That(mutationLaneControl.CompleteCount, Is.EqualTo(1));
            }
        }

        [Test]
        [Category("Size.Small")]
        public async Task HandleAsync_WhenExecuteMethodIsCanceled_WaitsForUncancelableEditorUpdateBeforeCompletingMutation ()
        {
            using (var scope = TemporaryDirectoryScope.Create())
            using (var editorScope = new EditorTestScope().SuppressExistingPersistentDirtyObjects())
            using (var cancellationTokenSource = new CancellationTokenSource())
            {
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var requestPayload = CreateExecuteMethodRequest(
                    scope.ProjectPath,
                    identity,
                    ExecuteMethodTypeName + ".HandlerExecuteMethodInvokesCallback");
                Directory.CreateDirectory(requestPayload.OutputPath);
                var readinessGate = new CountingReadinessGate();
                var mutationLaneControl = new ImmediateUnityMutationLaneControl();
                var editorUpdateAwaiter = new ControllableUnityEditorUpdateAwaiter();
                var handler = CreateCheckpointTestHandler(
                    identity,
                    readinessGate,
                    new CountingBuildPipelineRunner(),
                    mutationLaneControl,
                    editorUpdateAwaiter);
                executeMethodCallback = cancellationTokenSource.Cancel;

                try
                {
                    var responseTask = UnityIpcMethodHandlerTestInvoker
                        .HandleAsync(handler, CreateIpcRequest(requestPayload), cancellationTokenSource.Token)
                        .AsTask();
                    await TestAwaiter.WaitAsync(
                        editorUpdateAwaiter.WaitStarted,
                        "canceled execute-method post-update checkpoint",
                        SignalWaitTimeout);

                    Assert.That(readinessGate.CaptureObservationCallCount, Is.EqualTo(1));
                    Assert.That(editorUpdateAwaiter.ObservedCancellationToken, Is.EqualTo(CancellationToken.None));
                    Assert.That(mutationLaneControl.CompleteCount, Is.EqualTo(0));
                    Assert.That(responseTask.IsCompleted, Is.False);

                    editorUpdateAwaiter.Release();
                    await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
                    {
                        await responseTask;
                    }, "canceled execute-method response after post-update checkpoint", SignalWaitTimeout);

                    Assert.That(mutationLaneControl.CompleteCount, Is.EqualTo(1));
                }
                finally
                {
                    executeMethodCallback = null;
                    editorUpdateAwaiter.Release();
                }
            }
        }

        [Test]
        [Category("Size.Small")]
        public async Task HandleAsync_WhenBuildPipelineSucceeds_CompletesMutationAfterLifecycleCaptureAndEditorUpdate ()
        {
            using (var scope = TemporaryDirectoryScope.Create())
            using (var editorScope = new EditorTestScope().SuppressExistingPersistentDirtyObjects())
            {
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var requestPayload = CreateRequest(scope.ProjectPath, identity);
                var outputLayout = RequireOutputLayout(requestPayload);
                Directory.CreateDirectory(requestPayload.OutputPath);
                var buildPipelineRunner = new CountingBuildPipelineRunner(
                    CreateReportArtifact(
                        IpcBuildReportResult.Succeeded,
                        outputLayout.LocationPathName,
                        errorCount: 0,
                        warningCount: 0));
                var readinessGate = new CountingReadinessGate();
                var mutationLaneControl = new ImmediateUnityMutationLaneControl();
                var editorUpdateAwaiter = new ControllableUnityEditorUpdateAwaiter();
                var handler = CreateCheckpointTestHandler(
                    identity,
                    readinessGate,
                    buildPipelineRunner,
                    mutationLaneControl,
                    editorUpdateAwaiter);

                var responseTask = UnityIpcMethodHandlerTestInvoker
                    .HandleAsync(handler, CreateIpcRequest(requestPayload), CancellationToken.None)
                    .AsTask();
                try
                {
                    await TestAwaiter.WaitAsync(
                        editorUpdateAwaiter.WaitStarted,
                        "successful BuildPipeline post-update checkpoint",
                        SignalWaitTimeout);

                    Assert.That(readinessGate.CaptureObservationCallCount, Is.EqualTo(1));
                    Assert.That(mutationLaneControl.CompleteCount, Is.EqualTo(0));
                    Assert.That(responseTask.IsCompleted, Is.False);

                    editorUpdateAwaiter.Release();
                    var response = await TestAwaiter.WaitAsync(
                        responseTask,
                        "successful BuildPipeline response after post-update checkpoint",
                        SignalWaitTimeout);

                    Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
                    Assert.That(buildPipelineRunner.CallCount, Is.EqualTo(1));
                    Assert.That(mutationLaneControl.CompleteCount, Is.EqualTo(1));
                }
                finally
                {
                    editorUpdateAwaiter.Release();
                }
            }
        }

        [Test]
        [Category("Size.Small")]
        public async Task HandleAsync_WhenBuildPipelineThrows_CompletesMutationAfterLifecycleCaptureAndEditorUpdate ()
        {
            using (var scope = TemporaryDirectoryScope.Create())
            using (var editorScope = new EditorTestScope().SuppressExistingPersistentDirtyObjects())
            {
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var requestPayload = CreateRequest(scope.ProjectPath, identity);
                Directory.CreateDirectory(requestPayload.OutputPath);
                var buildPipelineRunner = new CountingBuildPipelineRunner();
                var readinessGate = new CountingReadinessGate();
                var mutationLaneControl = new ImmediateUnityMutationLaneControl();
                var editorUpdateAwaiter = new ControllableUnityEditorUpdateAwaiter();
                var handler = CreateCheckpointTestHandler(
                    identity,
                    readinessGate,
                    buildPipelineRunner,
                    mutationLaneControl,
                    editorUpdateAwaiter);

                var responseTask = UnityIpcMethodHandlerTestInvoker
                    .HandleAsync(handler, CreateIpcRequest(requestPayload), CancellationToken.None)
                    .AsTask();
                try
                {
                    await TestAwaiter.WaitAsync(
                        editorUpdateAwaiter.WaitStarted,
                        "failed BuildPipeline post-update checkpoint",
                        SignalWaitTimeout);

                    Assert.That(readinessGate.CaptureObservationCallCount, Is.EqualTo(1));
                    Assert.That(mutationLaneControl.CompleteCount, Is.EqualTo(0));
                    Assert.That(responseTask.IsCompleted, Is.False);

                    editorUpdateAwaiter.Release();
                    var response = await TestAwaiter.WaitAsync(
                        responseTask,
                        "failed BuildPipeline response after post-update checkpoint",
                        SignalWaitTimeout);

                    Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
                    Assert.That(buildPipelineRunner.CallCount, Is.EqualTo(1));
                    Assert.That(mutationLaneControl.CompleteCount, Is.EqualTo(1));
                }
                finally
                {
                    editorUpdateAwaiter.Release();
                }
            }
        }

        [Test]
        [Category("Size.Small")]
        public async Task HandleAsync_WhenBuildPipelineIsCanceled_WaitsForUncancelableEditorUpdateBeforeCompletingMutation ()
        {
            using (var scope = TemporaryDirectoryScope.Create())
            using (var editorScope = new EditorTestScope().SuppressExistingPersistentDirtyObjects())
            using (var cancellationTokenSource = new CancellationTokenSource())
            {
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var requestPayload = CreateRequest(scope.ProjectPath, identity);
                var outputLayout = RequireOutputLayout(requestPayload);
                Directory.CreateDirectory(requestPayload.OutputPath);
                var buildPipelineRunner = new CountingBuildPipelineRunner(
                    CreateReportArtifact(
                        IpcBuildReportResult.Succeeded,
                        outputLayout.LocationPathName,
                        errorCount: 0,
                        warningCount: 0),
                    _ => cancellationTokenSource.Cancel());
                var readinessGate = new CountingReadinessGate();
                var mutationLaneControl = new ImmediateUnityMutationLaneControl();
                var editorUpdateAwaiter = new ControllableUnityEditorUpdateAwaiter();
                var handler = CreateCheckpointTestHandler(
                    identity,
                    readinessGate,
                    buildPipelineRunner,
                    mutationLaneControl,
                    editorUpdateAwaiter);

                var responseTask = UnityIpcMethodHandlerTestInvoker
                    .HandleAsync(handler, CreateIpcRequest(requestPayload), cancellationTokenSource.Token)
                    .AsTask();
                try
                {
                    await TestAwaiter.WaitAsync(
                        editorUpdateAwaiter.WaitStarted,
                        "canceled BuildPipeline post-update checkpoint",
                        SignalWaitTimeout);

                    Assert.That(readinessGate.CaptureObservationCallCount, Is.EqualTo(1));
                    Assert.That(editorUpdateAwaiter.ObservedCancellationToken, Is.EqualTo(CancellationToken.None));
                    Assert.That(mutationLaneControl.CompleteCount, Is.EqualTo(0));
                    Assert.That(responseTask.IsCompleted, Is.False);

                    editorUpdateAwaiter.Release();
                    await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
                    {
                        await responseTask;
                    }, "canceled BuildPipeline response after post-update checkpoint", SignalWaitTimeout);

                    Assert.That(buildPipelineRunner.CallCount, Is.EqualTo(1));
                    Assert.That(mutationLaneControl.CompleteCount, Is.EqualTo(1));
                }
                finally
                {
                    editorUpdateAwaiter.Release();
                }
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
                    IpcBuildReportResult.Succeeded,
                    outputLayout.LocationPathName,
                    errorCount: 0,
                    warningCount: 1);
                var buildPipelineRunner = new CountingBuildPipelineRunner(
                    reportArtifact,
                    _ => unityLogStream.Write(
                        IpcUnityLogSource.Runtime,
                        IpcLogLevel.Info,
                        "build pipeline progress log"));
                var handler = new BuildRunUnityIpcMethodHandler(
                    new UnityBuildPreconditionProbe(
                        new CountingReadinessGate(),
                        identity.IpcIdentity,
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
                    new UnityLogRedactionScopeProvider(),
                    unityLogStream,
                    new ImmediateUnityMutationLaneControl(),
                    new ImmediateUnityEditorUpdateAwaiter());
                var request = CreateStreamingIpcRequest(requestPayload);
                var streamWriter = new CollectingIpcStreamFrameWriter(request.RequestId);

                var response = await UnityIpcMethodHandlerTestInvoker.HandleStreamingAsync(handler,
                    request,
                    streamWriter,
                    CancellationToken.None);

                Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
                Assert.That(streamWriter.ProgressFrames, Has.Count.EqualTo(5));
                Assert.That(streamWriter.ProgressFrames[0].Event, Is.EqualTo(BuildRunProgressEventNames.ReadinessCompleted));
                Assert.That(streamWriter.ProgressFrames[1].Event, Is.EqualTo(BuildRunProgressEventNames.RunnerResolved));
                Assert.That(streamWriter.ProgressFrames[2].Event, Is.EqualTo(BuildRunProgressEventNames.RunnerStarted));
                Assert.That(streamWriter.ProgressFrames[3].Event, Is.EqualTo(BuildRunProgressEventNames.LogEntry));
                Assert.That(streamWriter.ProgressFrames[4].Event, Is.EqualTo(BuildRunProgressEventNames.RunnerCompleted));

                Assert.That(IpcPayloadCodec.TryDeserialize(streamWriter.ProgressFrames[4].Payload, out BuildProgressEntry runnerCompleted, out _), Is.True);
                Assert.That(runnerCompleted.RunId, Is.EqualTo(RunId));
                Assert.That(runnerCompleted.Phase, Is.EqualTo(BuildRunProgressPhase.RunnerResult));
                Assert.That(runnerCompleted.RunnerStatus, Is.EqualTo(IpcBuildReportResult.Succeeded));

                Assert.That(IpcPayloadCodec.TryDeserialize(streamWriter.ProgressFrames[3].Payload, out BuildLogEntry logEntry, out _), Is.True);
                Assert.That(logEntry.Message, Is.EqualTo("build pipeline progress log"));
                Assert.That(logEntry.Source, Is.EqualTo(BuildLogEntrySource.UnityLog));
                Assert.That(logEntry.Cursor, Is.Not.Null);
                Assert.That(logEntry.Cursor!.Sequence, Is.EqualTo(1));

                Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcBuildRunResponse payload, out _), Is.True);
                Assert.That(payload.Logs.Window.CursorStart, Is.Not.Null);
                Assert.That(payload.Logs.Window.CursorEnd, Is.Not.Null);
            }
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator HandleStreamingAsync_WhenExecutionDeadlineElapsesWithPendingProgress_StopsWaitingAndReturnsIpcTimeout () => UniTask.ToCoroutine(async () =>
        {
            using (var scope = TemporaryDirectoryScope.Create())
            using (var editorScope = new EditorTestScope().SuppressExistingPersistentDirtyObjects())
            {
                var identity = CreateProjectIdentity(scope.ProjectPath);
                var requestPayload = CreateRequest(scope.ProjectPath, identity);
                var outputLayout = RequireOutputLayout(requestPayload);
                Directory.CreateDirectory(requestPayload.OutputPath);
                using var executionDeadlineCancellationTokenSource = new CancellationTokenSource();
                using var executionCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                    executionDeadlineCancellationTokenSource.Token);
                using var requestCancellation = new IpcRequestCancellation(
                    executionCancellationTokenSource.Token,
                    executionDeadlineCancellationTokenSource.Token,
                    CancellationToken.None);
                var reportArtifact = CreateReportArtifact(
                    IpcBuildReportResult.Succeeded,
                    outputLayout.LocationPathName,
                    errorCount: 0,
                    warningCount: 0);
                var buildPipelineRunner = new CountingBuildPipelineRunner(
                    reportArtifact,
                    _ => executionDeadlineCancellationTokenSource.Cancel());
                var handler = new BuildRunUnityIpcMethodHandler(
                    new UnityBuildPreconditionProbe(
                        new CountingReadinessGate(),
                        identity.IpcIdentity,
                        new StubServerVersionProvider("1.2.3"),
                        new CountingBuildTargetSupportProbe()),
                    new UnsupportedUnityBuildProfileInputResolver(),
                    new UnityProjectMutationAuditProbe(),
                    buildPipelineRunner,
                    new UnsupportedUnityBuildProfileBuildRunner(),
                    CreateExecuteMethodRunner(),
                    new CountingEditorLogRangeExporter(),
                    identity,
                    new UnityLogRedactionScopeProvider(),
                    new UnityLogRingBuffer(),
                    new ImmediateUnityMutationLaneControl(),
                    new ImmediateUnityEditorUpdateAwaiter());
                var streamWriter = new BlockingIpcStreamFrameWriter();

                var responseTask = handler.HandleStreamingAsync(
                        ValidatedUnityIpcRequestTestFactory.Create(CreateStreamingIpcRequest(requestPayload)),
                        streamWriter,
                        requestCancellation)
                    .AsTask();

                try
                {
                    var response = await TestAwaiter.WaitAsync(
                        responseTask,
                        "streaming build-run timeout response",
                        SignalWaitTimeout);

                    Assert.That(streamWriter.FirstWriteObserved.IsCompleted, Is.True);
                    Assert.That(streamWriter.LastWriteCancellationToken.IsCancellationRequested, Is.True);
                    Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
                    Assert.That(response.Errors.Count, Is.EqualTo(1));
                    Assert.That(response.Errors[0].Code, Is.EqualTo(IpcTransportErrorCodes.IpcTimeout));
                }
                finally
                {
                    streamWriter.ReleaseWrites();
                }
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator BuildRunProgressSink_WhenCompletionStarts_FlushesAcceptedFrameAndIgnoresLaterPublish () => UniTask.ToCoroutine(async () =>
        {
            var streamWriter = new BlockingIpcStreamFrameWriter();
            var progressSink = new UnityIpcBuildRunProgressSink(
                streamWriter,
                RunId,
                CancellationToken.None);
            progressSink.Publish(
                BuildRunProgressEventNames.ReadinessCompleted,
                new UcliEmptyArgs());
            await TestAwaiter.WaitAsync(
                streamWriter.FirstWriteObserved,
                "accepted build-run progress write",
                SignalWaitTimeout);

            var completionTask = progressSink.CompleteAndFlushAsync(CancellationToken.None);
            progressSink.Publish(
                BuildRunProgressEventNames.RunnerStarted,
                new UcliEmptyArgs());

            Assert.That(completionTask.IsCompleted, Is.False);

            streamWriter.ReleaseWrites();
            await TestAwaiter.WaitAsync(
                completionTask,
                "build-run progress completion",
                SignalWaitTimeout);

            Assert.DoesNotThrow(() => progressSink.Publish(string.Empty, null!));
            Assert.That(streamWriter.ProgressWriteAttemptCount, Is.EqualTo(1));
        });

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
                    IpcBuildReportResult.Succeeded,
                    outputLayout.LocationPathName,
                    errorCount: 0,
                    warningCount: 0);
                var buildPipelineRunner = new CountingBuildPipelineRunner(
                    reportArtifact,
                    _ => unityLogStream.Write(
                        IpcUnityLogSource.Runtime,
                        IpcLogLevel.Info,
                        oversizedMessage));
                var handler = new BuildRunUnityIpcMethodHandler(
                    new UnityBuildPreconditionProbe(
                        new CountingReadinessGate(),
                        identity.IpcIdentity,
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
                    new UnityLogRedactionScopeProvider(),
                    unityLogStream,
                    new ImmediateUnityMutationLaneControl(),
                    new ImmediateUnityEditorUpdateAwaiter());
                var request = CreateStreamingIpcRequest(requestPayload);
                var streamWriter = new CollectingIpcStreamFrameWriter(request.RequestId);

                var response = await UnityIpcMethodHandlerTestInvoker.HandleStreamingAsync(handler,
                    request,
                    streamWriter,
                    CancellationToken.None);

                Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
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
                        identity.IpcIdentity,
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
                    new UnityLogRedactionScopeProvider(),
                    unityLogStream,
                    new ImmediateUnityMutationLaneControl(),
                    new ImmediateUnityEditorUpdateAwaiter());
                var request = CreateStreamingIpcRequest(requestPayload);
                var streamWriter = new CollectingIpcStreamFrameWriter(request.RequestId);

                IpcResponse response;
                try
                {
                    response = await UnityIpcMethodHandlerTestInvoker.HandleStreamingAsync(handler,
                        request,
                        streamWriter,
                        CancellationToken.None);
                }
                finally
                {
                    executeMethodLogStream = null;
                }

                Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
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
                Assert.That(runnerResolved.Phase, Is.EqualTo(BuildRunProgressPhase.RunnerResolution));
                Assert.That(runnerResolved.RunnerKind, Is.EqualTo(BuildRunnerKind.ExecuteMethod));
                Assert.That(runnerResolved.RunnerStatus, Is.Null);

                Assert.That(IpcPayloadCodec.TryDeserialize(streamWriter.ProgressFrames[2].Payload, out BuildProgressEntry runnerStarted, out _), Is.True);
                Assert.That(runnerStarted.Phase, Is.EqualTo(BuildRunProgressPhase.RunnerInvocation));
                Assert.That(runnerStarted.RunnerKind, Is.EqualTo(BuildRunnerKind.ExecuteMethod));
                Assert.That(runnerStarted.RunnerStatus, Is.Null);

                Assert.That(IpcPayloadCodec.TryDeserialize(streamWriter.ProgressFrames[3].Payload, out BuildLogEntry logEntry, out _), Is.True);
                Assert.That(logEntry.Message, Is.EqualTo("executeMethod progress log"));
                Assert.That(logEntry.Source, Is.EqualTo(BuildLogEntrySource.UnityLog));

                Assert.That(IpcPayloadCodec.TryDeserialize(streamWriter.ProgressFrames[4].Payload, out BuildProgressEntry runnerCompleted, out _), Is.True);
                Assert.That(runnerCompleted.Phase, Is.EqualTo(BuildRunProgressPhase.RunnerResult));
                Assert.That(runnerCompleted.RunnerKind, Is.EqualTo(BuildRunnerKind.ExecuteMethod));
                Assert.That(runnerCompleted.RunnerStatus, Is.EqualTo(IpcBuildReportResult.Succeeded));

                Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcBuildRunResponse payload, out _), Is.True);
                Assert.That(payload.RunnerResult, Is.Not.Null);
                Assert.That(payload.RunnerResult!.Source, Is.EqualTo(IpcBuildRunnerResultSource.UcliBuildRunnerResult));
                Assert.That(payload.RunnerResult.Status, Is.EqualTo(IpcBuildReportResult.Succeeded));
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
                        identity.IpcIdentity,
                        new StubServerVersionProvider("1.2.3"),
                        new CountingBuildTargetSupportProbe()),
                    new UnsupportedUnityBuildProfileInputResolver(),
                    new UnityProjectMutationAuditProbe(),
                    buildPipelineRunner,
                    new UnsupportedUnityBuildProfileBuildRunner(),
                    CreateExecuteMethodRunner(),
                    logRangeExporter,
                    identity,
                    new UnityLogRedactionScopeProvider(),
                    new UnityLogRingBuffer(),
                    new ImmediateUnityMutationLaneControl(),
                    new ImmediateUnityEditorUpdateAwaiter());

                var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(handler, CreateIpcRequest(requestPayload), CancellationToken.None);

                Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
                Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcBuildRunResponse payload, out _), Is.True);
                Assert.That(buildPipelineRunner.CallCount, Is.EqualTo(0));
                Assert.That(logRangeExporter.CallCount, Is.EqualTo(1));
                Assert.That(executeMethodContext, Is.Not.Null);
                Assert.That(executeMethodContext!.Environment.Variables["UCLI_MODE"], Is.EqualTo("release"));
                Assert.That(executeMethodContext.Environment.Secrets["UCLI_SECRET"], Is.EqualTo("secret-value"));
                Assert.That(executeMethodContext.Environment.Secrets["UCLI_SECRET_LONG"], Is.EqualTo("secret-value-tail"));
                Assert.That(UcliBuildRunnerContext.Current, Is.Null);
                Assert.That(payload.RunnerResult, Is.Not.Null);
                Assert.That(payload.RunnerResult!.Source, Is.EqualTo(IpcBuildRunnerResultSource.UcliBuildRunnerResult));
                Assert.That(payload.RunnerResult.Status, Is.EqualTo(IpcBuildReportResult.Succeeded));
                Assert.That(payload.RunnerResult.Outputs, Has.Count.EqualTo(1));
                Assert.That(payload.RunnerResult.Outputs[0].Value, Is.EqualTo("player.txt"));
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
                           new UnityCompileMessageDedupeCache(new ManualMonotonicClock()),
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
                            identity.IpcIdentity,
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
                        redactionScopeProvider,
                        unityLogStream,
                        new ImmediateUnityMutationLaneControl(),
                        new ImmediateUnityEditorUpdateAwaiter());

                    var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(handler, CreateIpcRequest(requestPayload), CancellationToken.None);

                    Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
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
                var requestPayload = CreateRequest(
                    scope.ProjectPath,
                    identity,
                    projectMutationMode: BuildProfileProjectMutationMode.Audit);
                Directory.CreateDirectory(requestPayload.OutputPath);
                const string MutatedPath = "Assets/GeneratedByRunner.txt";
                var mutatedFullPath = Path.Combine(scope.ProjectPath, MutatedPath);
                var reportArtifact = CreateReportArtifact(
                    IpcBuildReportResult.Succeeded,
                    Path.Combine(requestPayload.OutputPath, "build"),
                    errorCount: 0,
                    warningCount: 0);
                var buildPipelineRunner = new CountingBuildPipelineRunner(
                    reportArtifact,
                    _ => File.WriteAllText(mutatedFullPath, "generated by runner"));
                var handler = new BuildRunUnityIpcMethodHandler(
                    new UnityBuildPreconditionProbe(
                        new CountingReadinessGate(),
                        identity.IpcIdentity,
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
                    new UnityLogRedactionScopeProvider(),
                    new UnityLogRingBuffer(),
                    new ImmediateUnityMutationLaneControl(),
                    new ImmediateUnityEditorUpdateAwaiter());

                var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(handler, CreateIpcRequest(requestPayload), CancellationToken.None);

                Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
                Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcBuildRunResponse payload, out _), Is.True);
                Assert.That(buildPipelineRunner.CallCount, Is.EqualTo(1));
                Assert.That(payload.ProjectMutation.Mode, Is.EqualTo(BuildProfileProjectMutationMode.Audit));
                Assert.That(payload.ProjectMutation.Coverage, Is.EqualTo(IpcBuildProjectMutationAuditCoverage.Full));
                Assert.That(payload.ProjectMutation.Mutated, Is.True);
                Assert.That(payload.ProjectMutation.BeforeDigest, Is.Not.EqualTo(payload.ProjectMutation.AfterDigest));
                Assert.That(payload.ProjectMutation.Items, Has.Count.EqualTo(1));
                var item = payload.ProjectMutation.Items[0];
                Assert.That(item.Path, Is.EqualTo(new ProjectMutationAuditPath(MutatedPath)));
                Assert.That(item.ChangeKind, Is.EqualTo(IpcBuildProjectMutationChangeKind.Added));
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
                var requestPayload = CreateRequest(
                    scope.ProjectPath,
                    identity,
                    scenePaths: new[] { new SceneAssetPath(scenePath) });
                var buildPipelineRunner = new CountingBuildPipelineRunner();
                var logRangeExporter = new CountingEditorLogRangeExporter();
                var handler = new BuildRunUnityIpcMethodHandler(
                    new UnityBuildPreconditionProbe(
                        new CountingReadinessGate(),
                        identity.IpcIdentity,
                        new StubServerVersionProvider("1.2.3"),
                        new CountingBuildTargetSupportProbe()),
                    new UnsupportedUnityBuildProfileInputResolver(),
                    new UnityProjectMutationAuditProbe(),
                    buildPipelineRunner,
                    new UnsupportedUnityBuildProfileBuildRunner(),
                    CreateExecuteMethodRunner(),
                    logRangeExporter,
                    identity,
                    new UnityLogRedactionScopeProvider(),
                    new UnityLogRingBuffer(),
                    new ImmediateUnityMutationLaneControl(),
                    new ImmediateUnityEditorUpdateAwaiter());

                var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(handler, CreateIpcRequest(requestPayload), CancellationToken.None);

                Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
                Assert.That(response.Errors.Count, Is.EqualTo(1));
                Assert.That(response.Errors[0].Code, Is.EqualTo(BuildErrorCodes.BuildDirtyStatePresent));
                Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcBuildRunErrorPayload payload, out _), Is.True);
                Assert.That(payload.DirtyState, Is.Not.Null);
                Assert.That(payload.DirtyState!.Dirty, Is.True);
                Assert.That(payload.DirtyState.Items, Has.Count.EqualTo(1));
                Assert.That(
                    payload.DirtyState.Items[0].Path,
                    Is.EqualTo(new ProjectMutationAuditPath(scenePath)));
                Assert.That(buildPipelineRunner.CallCount, Is.EqualTo(0));
                Assert.That(logRangeExporter.CallCount, Is.EqualTo(0));
                Assert.That(File.Exists(requestPayload.BuildReportPath), Is.False);
                Assert.That(File.Exists(requestPayload.BuildLogPath), Is.False);
            }
        }

        private static UnityHostProjectIdentity CreateProjectIdentity (string projectPath)
        {
            return new UnityHostProjectIdentity(
                AbsolutePath.Parse(projectPath),
                ProjectFingerprint,
                "6000.1.4f1");
        }

        private static BuildRunUnityIpcMethodHandler CreateCheckpointTestHandler (
            UnityHostProjectIdentity projectIdentity,
            IUnityEditorReadinessGate readinessGate,
            IUnityBuildPipelineRunner buildPipelineRunner,
            IUnityMutationLaneControl mutationLaneControl,
            IUnityEditorUpdateAwaiter editorUpdateAwaiter)
        {
            return new BuildRunUnityIpcMethodHandler(
                new UnityBuildPreconditionProbe(
                    readinessGate,
                    projectIdentity.IpcIdentity,
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
                projectIdentity,
                new UnityLogRedactionScopeProvider(),
                new UnityLogRingBuffer(),
                mutationLaneControl,
                editorUpdateAwaiter);
        }

        private static IpcBuildOutputLayout RequireOutputLayout (IpcBuildRunRequest request)
        {
            return request.OutputLayout
                ?? throw new AssertionException("Expected the build request to contain a resolved output layout.");
        }

        private static IpcBuildRunRequest CreateRequest (
            string projectPath,
            UnityHostProjectIdentity identity,
            BuildProfileProjectMutationMode projectMutationMode = BuildProfileProjectMutationMode.Forbid,
            IReadOnlyList<SceneAssetPath>? scenePaths = null,
            BuildTargetStableName buildTarget = BuildTargetStableName.StandaloneLinux64,
            IpcBuildOutputLayout? outputLayout = null,
            string? outputPath = null,
            string? buildReportPath = null,
            string? buildLogPath = null)
        {
            var paths = ResolveRequestArtifactPaths(projectPath);
            var defaultOutputLayout = ResolveOutputLayout(
                AbsolutePath.Parse(paths.OutputPath),
                BuildTargetStableName.StandaloneLinux64,
                androidAppBundle: false);

            return new IpcBuildRunRequest(
                RunId: RunId,
                InputKind: BuildProfileInputsKind.Explicit,
                BuildTarget: buildTarget,
                SceneSource: BuildProfileSceneSource.Explicit,
                ScenePaths: scenePaths ?? new[] { new SceneAssetPath("Assets/Scenes/SampleScene.unity") },
                Development: true,
                OutputPath: outputPath ?? paths.OutputPath,
                OutputLayout: outputLayout ?? defaultOutputLayout,
                BuildReportPath: buildReportPath ?? paths.BuildReportPath,
                BuildLogPath: buildLogPath ?? paths.BuildLogPath,
                AllowedEditorModes: new[] { DaemonEditorMode.Batchmode },
                ProjectMutationMode: projectMutationMode,
                RunnerKind: BuildRunnerKind.BuildPipeline,
                ProfileDigest: ProfileDigest,
                UnityBuildProfile: null,
                ProfilePath: null,
                RunnerMethod: null,
                RunnerArguments: new Dictionary<string, string>(StringComparer.Ordinal),
                RunnerEnvironmentVariables: Array.Empty<string>(),
                RunnerEnvironmentSecrets: Array.Empty<string>(),
                RunnerEnvironmentVariableValues: new Dictionary<string, string>(StringComparer.Ordinal),
                RunnerEnvironmentSecretValues: new Dictionary<string, string>(StringComparer.Ordinal));
        }

        private static IpcBuildOutputLayout ResolveOutputLayout (
            AbsolutePath outputPath,
            BuildTargetStableName buildTarget,
            bool androidAppBundle)
        {
            return ResolveGuardedOutputLayout(outputPath, buildTarget, androidAppBundle).ToContract();
        }

        private static ResolvedBuildPipelineOutputLayout ResolveGuardedOutputLayout (
            AbsolutePath outputPath,
            BuildTargetStableName buildTarget,
            bool androidAppBundle)
        {
            if (!BuildPipelineOutputLayoutPolicy.TryResolve(
                    buildTarget,
                    androidAppBundle,
                    out var definition))
            {
                throw new InvalidOperationException("Test build target must resolve a BuildPipeline output layout.");
            }

            var location = ContainedPath.Create(
                outputPath,
                BuildRunnerOutputPathAdapter.ToRootRelativePath(definition.RunnerOutputPath));
            return new ResolvedBuildPipelineOutputLayout(definition.Shape, location.Target);
        }

        private static IpcBuildRunRequest CreateExecuteMethodRequest (
            string projectPath,
            UnityHostProjectIdentity identity,
            string method)
        {
            var paths = ResolveRequestArtifactPaths(projectPath);
            return new IpcBuildRunRequest(
                RunId: RunId,
                InputKind: BuildProfileInputsKind.Explicit,
                BuildTarget: BuildTargetStableName.StandaloneLinux64,
                SceneSource: BuildProfileSceneSource.Explicit,
                ScenePaths: new[] { new SceneAssetPath("Assets/Scenes/SampleScene.unity") },
                Development: true,
                OutputPath: paths.OutputPath,
                OutputLayout: null,
                BuildReportPath: paths.BuildReportPath,
                BuildLogPath: paths.BuildLogPath,
                AllowedEditorModes: new[] { DaemonEditorMode.Batchmode },
                ProjectMutationMode: BuildProfileProjectMutationMode.Forbid,
                RunnerKind: BuildRunnerKind.ExecuteMethod,
                ProfileDigest: ProfileDigest,
                UnityBuildProfile: null,
                ProfilePath: Path.Combine(projectPath, "build.ucli.json"),
                RunnerMethod: method,
                RunnerArguments: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["argument"] = "value",
                },
                RunnerEnvironmentVariables: new[] { "UCLI_MODE" },
                RunnerEnvironmentSecrets: new[] { "UCLI_SECRET", "UCLI_SECRET_LONG" },
                RunnerEnvironmentVariableValues: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["UCLI_MODE"] = "release",
                },
                RunnerEnvironmentSecretValues: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["UCLI_SECRET"] = "secret-value",
                    ["UCLI_SECRET_LONG"] = "secret-value-tail",
                });
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

        public static UcliBuildRunnerResult HandlerExecuteMethodThrows (UcliBuildRunnerContext context)
        {
            throw new InvalidOperationException("Execute-method runner failed.");
        }

        public static UcliBuildRunnerResult HandlerExecuteMethodInvokesCallback (UcliBuildRunnerContext context)
        {
            executeMethodCallback?.Invoke();
            return HandlerExecuteMethodSuccess(context);
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
                IpcUnityLogSource.Runtime,
                IpcLogLevel.Info,
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
            UnityHostProjectIdentity identity)
        {
            var paths = ResolveRequestArtifactPaths(projectPath);
            return new IpcBuildRunRequest(
                RunId: RunId,
                InputKind: BuildProfileInputsKind.UnityBuildProfile,
                BuildTarget: null,
                SceneSource: null,
                ScenePaths: Array.Empty<SceneAssetPath>(),
                Development: false,
                OutputPath: paths.OutputPath,
                OutputLayout: null,
                BuildReportPath: paths.BuildReportPath,
                BuildLogPath: paths.BuildLogPath,
                AllowedEditorModes: new[] { DaemonEditorMode.Batchmode },
                ProjectMutationMode: BuildProfileProjectMutationMode.Forbid,
                RunnerKind: BuildRunnerKind.BuildPipeline,
                ProfileDigest: ProfileDigest,
                UnityBuildProfile: new IpcUnityBuildProfileInput(
                    Path: new UnityBuildProfileAssetPath("Assets/BuildProfiles/Linux.asset"),
                    Digest: null,
                    ApplyAudit: null),
                ProfilePath: null,
                RunnerMethod: null,
                RunnerArguments: new Dictionary<string, string>(StringComparer.Ordinal),
                RunnerEnvironmentVariables: Array.Empty<string>(),
                RunnerEnvironmentSecrets: Array.Empty<string>(),
                RunnerEnvironmentVariableValues: new Dictionary<string, string>(StringComparer.Ordinal),
                RunnerEnvironmentSecretValues: new Dictionary<string, string>(StringComparer.Ordinal));
        }

        private static (string OutputPath, string BuildReportPath, string BuildLogPath) ResolveRequestArtifactPaths (
            string projectPath)
        {
            var storageRoot = UcliStoragePathResolver.ResolveStorageRoot(AbsolutePath.Parse(projectPath));
            var artifactsDirectory = UcliStoragePathResolver.ResolveBuildRunArtifactsDirectory(
                storageRoot,
                RunId);
            return (
                UcliStoragePathResolver.ResolveBuildRunOutputDirectory(storageRoot, RunId).Value,
                Path.Combine(artifactsDirectory.Value, UcliStoragePathNames.BuildReportFileName),
                Path.Combine(artifactsDirectory.Value, UcliStoragePathNames.BuildLogFileName));
        }

        private static IpcUnityBuildProfileInput CreateAppliedUnityBuildProfileInput (string path)
        {
            var lifecycle = Create();
            return new IpcUnityBuildProfileInput(
                Path: new UnityBuildProfileAssetPath(path),
                Digest: Sha256Digest.Parse(new string('f', 64)),
                ApplyAudit: new IpcUnityBuildProfileApplyAudit(
                    Applied: true,
                    LifecycleBefore: lifecycle,
                    LifecycleAfter: lifecycle,
                    DirtyStateAfter: new IpcBuildDirtyState(
                        Dirty: false,
                        Coverage: IpcBuildDirtyStateCoverage.Full,
                        Items: Array.Empty<IpcBuildDirtyStateItem>())));
        }

        private static IpcRequestEnvelope CreateIpcRequest (
            IpcBuildRunRequest payload,
            int requestDeadlineRemainingMilliseconds = 30_000)
        {
            return new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: "session-token",
                method: ContractLiteralCodec.ToValue(UnityIpcMethod.BuildRun),
                payload: IpcPayloadCodec.SerializeToElement(payload),
                responseMode: "single",
                requestDeadlineUtc: DateTimeOffset.UtcNow
                    + TimeSpan.FromMilliseconds(requestDeadlineRemainingMilliseconds),
                requestDeadlineRemainingMilliseconds: requestDeadlineRemainingMilliseconds);
        }

        private static IpcRequestEnvelope CreateStreamingIpcRequest (
            IpcBuildRunRequest payload,
            int requestDeadlineRemainingMilliseconds = 30_000)
        {
            return new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: "session-token",
                method: ContractLiteralCodec.ToValue(UnityIpcMethod.BuildRun),
                payload: IpcPayloadCodec.SerializeToElement(payload),
                responseMode: "stream",
                requestDeadlineUtc: DateTimeOffset.UtcNow
                    + TimeSpan.FromMilliseconds(requestDeadlineRemainingMilliseconds),
                requestDeadlineRemainingMilliseconds: requestDeadlineRemainingMilliseconds);
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

        private sealed class ControllableUnityEditorUpdateAwaiter : IUnityEditorUpdateAwaiter
        {
            private readonly TaskCompletionSource<bool> startedSource = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> releaseSource = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            public Task WaitStarted => startedSource.Task;

            public CancellationToken ObservedCancellationToken { get; private set; }

            public Task WaitForNextUpdateAsync (CancellationToken cancellationToken)
            {
                ObservedCancellationToken = cancellationToken;
                startedSource.TrySetResult(true);
                return releaseSource.Task;
            }

            public void Release ()
            {
                releaseSource.TrySetResult(true);
            }
        }

        private static IpcBuildReportArtifact CreateReportArtifact (
            IpcBuildReportResult result,
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

            public UnityBuildTargetSupportProbeResult Probe (BuildTargetStableName buildTarget)
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
                BuildRunExecutionRequest.UnityBuildProfile request,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallCount++;
                var outputLayout = ResolveGuardedOutputLayout(
                    request.OutputPath,
                    BuildTargetStableName.StandaloneLinux64,
                    androidAppBundle: false);

                var preconditionInput = new UnityBuildPreconditionInput(
                    InputKind: BuildProfileInputsKind.UnityBuildProfile,
                    BuildTarget: BuildTargetStableName.StandaloneLinux64,
                    SceneSource: BuildProfileSceneSource.UnityBuildProfile,
                    ScenePaths: new[] { new SceneAssetPath("Assets/Scenes/SampleScene.unity") },
                    Development: false,
                    AllowedEditorModes: request.AllowedEditorModes);
                var unityBuildProfile = CreateAppliedUnityBuildProfileInput(request.Profile.Path.Value);

                return Task.FromResult(UnityBuildProfileInputResolutionResult.Success(
                    preconditionInput,
                    outputLayout,
                    unityBuildProfile));
            }
        }

        private sealed class TimeoutAfterBuildProfileInputResolver : IUnityBuildProfileInputResolver
        {
            private readonly Action cancelExecutionDeadline;

            public TimeoutAfterBuildProfileInputResolver (Action cancelExecutionDeadline)
            {
                this.cancelExecutionDeadline = cancelExecutionDeadline;
            }

            public int CallCount { get; private set; }

            public Task<UnityBuildProfileInputResolutionResult> ResolveAsync (
                BuildRunExecutionRequest.UnityBuildProfile request,
                CancellationToken cancellationToken)
            {
                CallCount++;
                var outputLayout = ResolveGuardedOutputLayout(
                    request.OutputPath,
                    BuildTargetStableName.StandaloneLinux64,
                    androidAppBundle: false);

                var preconditionInput = new UnityBuildPreconditionInput(
                    InputKind: BuildProfileInputsKind.UnityBuildProfile,
                    BuildTarget: BuildTargetStableName.StandaloneLinux64,
                    SceneSource: BuildProfileSceneSource.UnityBuildProfile,
                    ScenePaths: new[] { new SceneAssetPath("Assets/Scenes/SampleScene.unity") },
                    Development: false,
                    AllowedEditorModes: request.AllowedEditorModes);
                var unityBuildProfile = CreateAppliedUnityBuildProfileInput(request.Profile.Path.Value);
                cancelExecutionDeadline();
                return Task.FromResult(UnityBuildProfileInputResolutionResult.Success(
                    preconditionInput,
                    outputLayout,
                    unityBuildProfile));
            }
        }

        private sealed class FailingUnityBuildProfileBuildRunner : IUnityBuildProfileBuildRunner
        {
            public int CallCount { get; private set; }

            public IpcBuildReportArtifact? Run (
                IpcUnityBuildProfileInput unityBuildProfile,
                UnityBuildResolvedInput resolvedInput,
                ResolvedBuildPipelineOutputLayout outputLayout)
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
                AbsolutePath sourcePath,
                AbsolutePath destinationPath,
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

                var directoryPath = Path.GetDirectoryName(destinationPath.Value);
                if (!string.IsNullOrWhiteSpace(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                File.WriteAllText(destinationPath.Value, Redact(contents, redactionValues));
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
            private readonly Guid requestId;

            public CollectingIpcStreamFrameWriter (Guid requestId)
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
                    IpcStreamFrameKind.Progress,
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

        private sealed class BlockingIpcStreamFrameWriter : IIpcStreamFrameWriter
        {
            private readonly TaskCompletionSource<bool> writeReleaseSource =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> firstWriteObservedSource =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task FirstWriteObserved => firstWriteObservedSource.Task;

            public CancellationToken LastWriteCancellationToken { get; private set; }

            public int ProgressWriteAttemptCount { get; private set; }

            public async ValueTask WriteProgressAsync<TPayload> (
                string eventName,
                TPayload payload,
                CancellationToken cancellationToken = default)
                where TPayload : notnull
            {
                cancellationToken.ThrowIfCancellationRequested();
                LastWriteCancellationToken = cancellationToken;
                ProgressWriteAttemptCount++;
                firstWriteObservedSource.TrySetResult(true);
                await writeReleaseSource.Task.ConfigureAwait(false);
            }

            public ValueTask WriteTerminalAsync (
                IpcResponse response,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return default;
            }

            public void ReleaseWrites ()
            {
                writeReleaseSource.TrySetResult(true);
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
                for (var i = 0; i < ProjectMutationAuditPath.RootDirectoryNames.Count; i++)
                {
                    Directory.CreateDirectory(Path.Combine(ProjectPath, ProjectMutationAuditPath.RootDirectoryNames[i]));
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
