using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Build;
using MackySoft.Ucli.Unity.Runtime;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles <c>build.run</c> IPC method requests. </summary>
    internal sealed class BuildRunUnityIpcMethodHandler : IStreamingUnityIpcMethodHandler
    {
        private const string ProgressLogEntryTruncatedSuffix = "... [truncated for build progress stream]";

        private static readonly UTF8Encoding Utf8NoBomEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private readonly UnityBuildPreconditionProbe preconditionProbe;

        private readonly IUnityBuildProfileInputResolver buildProfileInputResolver;

        private readonly UnityProjectMutationAuditProbe projectMutationAuditProbe;

        private readonly IUnityBuildPipelineRunner buildPipelineRunner;

        private readonly IUnityBuildProfileBuildRunner buildProfileBuildRunner;

        private readonly BuildExecuteMethodRunner executeMethodRunner;

        private readonly IEditorLogRangeExporter editorLogRangeExporter;

        private readonly IpcProjectIdentity projectIdentity;

        private readonly UnityLogRedactionScopeProvider unityLogRedactionScopeProvider;

        private readonly IUnityLogStream unityLogStream;

        private readonly IUnityMutationLaneControl mutationLaneControl;

        private readonly IUnityEditorUpdateAwaiter editorUpdateAwaiter;

        /// <summary> Initializes a new instance of the <see cref="BuildRunUnityIpcMethodHandler" /> class. </summary>
        public BuildRunUnityIpcMethodHandler (
            UnityBuildPreconditionProbe preconditionProbe,
            IUnityBuildProfileInputResolver buildProfileInputResolver,
            UnityProjectMutationAuditProbe projectMutationAuditProbe,
            IUnityBuildPipelineRunner buildPipelineRunner,
            IUnityBuildProfileBuildRunner buildProfileBuildRunner,
            BuildExecuteMethodRunner executeMethodRunner,
            IEditorLogRangeExporter editorLogRangeExporter,
            IpcProjectIdentity projectIdentity,
            UnityLogRedactionScopeProvider unityLogRedactionScopeProvider,
            IUnityLogStream unityLogStream,
            IUnityMutationLaneControl mutationLaneControl,
            IUnityEditorUpdateAwaiter editorUpdateAwaiter)
        {
            this.preconditionProbe = preconditionProbe ?? throw new ArgumentNullException(nameof(preconditionProbe));
            this.buildProfileInputResolver = buildProfileInputResolver ?? throw new ArgumentNullException(nameof(buildProfileInputResolver));
            this.projectMutationAuditProbe = projectMutationAuditProbe ?? throw new ArgumentNullException(nameof(projectMutationAuditProbe));
            this.buildPipelineRunner = buildPipelineRunner ?? throw new ArgumentNullException(nameof(buildPipelineRunner));
            this.buildProfileBuildRunner = buildProfileBuildRunner ?? throw new ArgumentNullException(nameof(buildProfileBuildRunner));
            this.executeMethodRunner = executeMethodRunner ?? throw new ArgumentNullException(nameof(executeMethodRunner));
            this.editorLogRangeExporter = editorLogRangeExporter ?? throw new ArgumentNullException(nameof(editorLogRangeExporter));
            this.projectIdentity = projectIdentity ?? throw new ArgumentNullException(nameof(projectIdentity));
            this.unityLogRedactionScopeProvider = unityLogRedactionScopeProvider ?? throw new ArgumentNullException(nameof(unityLogRedactionScopeProvider));
            this.unityLogStream = unityLogStream ?? throw new ArgumentNullException(nameof(unityLogStream));
            this.mutationLaneControl = mutationLaneControl ?? throw new ArgumentNullException(nameof(mutationLaneControl));
            this.editorUpdateAwaiter = editorUpdateAwaiter ?? throw new ArgumentNullException(nameof(editorUpdateAwaiter));
        }

        /// <inheritdoc />
        public UnityIpcMethod Method => UnityIpcMethod.BuildRun;

        /// <inheritdoc />
        public async ValueTask<IpcResponse> HandleAsync (
            ValidatedUnityIpcRequest request,
            IpcRequestCancellation cancellation)
        {
            cancellation.Token.ThrowIfCancellationRequested();
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!UnityIpcRequestCodec.TryDecodeBuildRunRequest(
                    request,
                    out IpcBuildRunRequest? buildRunRequest,
                    out var errorResponse))
            {
                return errorResponse!;
            }

            var executionRequest = BuildRunExecutionRequest.Create(buildRunRequest!);
            return await HandleDecodedAsync(
                request,
                executionRequest,
                progressSinkFactory: null,
                cancellation);
        }

        /// <inheritdoc />
        public async ValueTask<IpcResponse> HandleStreamingAsync (
            ValidatedUnityIpcRequest request,
            IIpcStreamFrameWriter streamWriter,
            IpcRequestCancellation cancellation)
        {
            if (streamWriter == null)
            {
                throw new ArgumentNullException(nameof(streamWriter));
            }

            cancellation.Token.ThrowIfCancellationRequested();
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!UnityIpcRequestCodec.TryDecodeBuildRunRequest(
                    request,
                    out IpcBuildRunRequest? buildRunRequest,
                    out var errorResponse))
            {
                return errorResponse!;
            }

            var executionRequest = BuildRunExecutionRequest.Create(buildRunRequest!);
            return await HandleDecodedAsync(
                request,
                executionRequest,
                executionCancellationToken => new UnityIpcBuildRunProgressSink(
                    streamWriter,
                    executionRequest.RunId,
                    executionCancellationToken),
                cancellation);
        }

        private async ValueTask<IpcResponse> HandleDecodedAsync (
            ValidatedUnityIpcRequest request,
            BuildRunExecutionRequest executionRequest,
            Func<CancellationToken, UnityIpcBuildRunProgressSink>? progressSinkFactory,
            IpcRequestCancellation cancellation)
        {
            if (!TryValidateRequest(
                    executionRequest,
                    projectIdentity,
                    out var validationErrorMessage))
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InvalidArgument,
                    validationErrorMessage!,
                    null);
            }

            UnityBuildPreconditionProbeResult? precondition = null;
            IpcUnityBuildProfileInput? unityBuildProfile = null;
            UnityIpcBuildRunProgressSink? progressSink = null;
            try
            {
                var executionCancellationToken = cancellation.Token;
                progressSink = progressSinkFactory?.Invoke(executionCancellationToken);
                UnityBuildPreconditionInput preconditionInput;
                IpcBuildOutputLayout? outputLayout;
                if (executionRequest is BuildRunExecutionRequest.UnityBuildProfile unityBuildProfileRequest)
                {
                    var profileResolution = await buildProfileInputResolver.ResolveAsync(
                        unityBuildProfileRequest,
                        executionCancellationToken);
                    unityBuildProfile = profileResolution.UnityBuildProfile;
                    if (!profileResolution.IsSuccess)
                    {
                        return CreateBuildProfileInputErrorResponse(request, profileResolution);
                    }

                    preconditionInput = profileResolution.PreconditionInput!;
                    outputLayout = profileResolution.OutputLayout!;
                }
                else if (executionRequest is BuildRunExecutionRequest.ExplicitBuildPipeline buildPipelineRequest)
                {
                    preconditionInput = new UnityBuildPreconditionInput(
                        InputKind: buildPipelineRequest.InputKind,
                        BuildTarget: buildPipelineRequest.BuildTarget,
                        SceneSource: buildPipelineRequest.SceneSource,
                        ScenePaths: buildPipelineRequest.ScenePaths,
                        Development: buildPipelineRequest.Development,
                        AllowedEditorModes: buildPipelineRequest.AllowedEditorModes);
                    outputLayout = buildPipelineRequest.OutputLayout;
                }
                else
                {
                    var explicitExecuteRequest = (BuildRunExecutionRequest.ExplicitExecuteMethod)executionRequest;
                    preconditionInput = new UnityBuildPreconditionInput(
                        InputKind: explicitExecuteRequest.InputKind,
                        BuildTarget: explicitExecuteRequest.BuildTarget,
                        SceneSource: explicitExecuteRequest.SceneSource,
                        ScenePaths: explicitExecuteRequest.ScenePaths,
                        Development: explicitExecuteRequest.Development,
                        AllowedEditorModes: explicitExecuteRequest.AllowedEditorModes);
                    outputLayout = null;
                }

                precondition = await preconditionProbe.ProbeBeforeBuildAsync(
                    preconditionInput,
                    executionCancellationToken);
                if (!precondition.IsSuccess)
                {
                    return CreatePreconditionErrorResponse(request, precondition, unityBuildProfile);
                }

                var resolvedInput = precondition.ResolvedInput!;
                var executeMethodRequest = executionRequest as BuildRunExecutionRequest.ExplicitExecuteMethod;
                PublishProgress(
                    progressSink,
                    executionRequest,
                    BuildRunProgressEventNames.ReadinessCompleted,
                    BuildRunProgressPhase.Readiness,
                    runnerKind: null,
                    runnerStatus: null);

                FileSystemAccessBoundary.EnsureSecureDirectory(executionRequest.OutputPath);
                if (outputLayout != null)
                {
                    EnsureBuildPipelineOutputLayoutReady(outputLayout);
                }

                if (executeMethodRequest == null)
                {
                    PublishProgress(
                        progressSink,
                        executionRequest,
                        BuildRunProgressEventNames.RunnerResolved,
                        BuildRunProgressPhase.RunnerResolution,
                        executionRequest.RunnerKind,
                        runnerStatus: null);
                }

                var logSourcePath = Application.consoleLogPath;
                var logStartOffset = GetLogLength(logSourcePath);
                var logStartSnapshot = unityLogStream.Snapshot();
                using var unityLogRedactionScope = executeMethodRequest != null
                    ? unityLogRedactionScopeProvider.BeginScope(executeMethodRequest.RunnerEnvironmentSecretValues.Values)
                    : null;
                var startedAtUtc = DateTimeOffset.UtcNow;
                var mutationBaseline = projectMutationAuditProbe.CaptureBaseline(projectIdentity.ProjectPath);
                executionCancellationToken.ThrowIfCancellationRequested();
                IpcBuildRunnerResultArtifact? runnerResult = null;
                IpcBuildReportArtifact? normalizedReport = null;
                IpcUnityEditorObservation lifecycleAfter;
                var mutationActivity = mutationLaneControl.BeginMutation();
                try
                {
                    if (executeMethodRequest != null)
                    {
                        var invocationResult = executeMethodRunner.Run(
                            executeMethodRequest,
                            projectIdentity,
                            resolvedInput,
                            new BuildRunExecuteMethodProgressSink(progressSink, executeMethodRequest));
                        if (!invocationResult.IsSuccess)
                        {
                            var error = invocationResult.Error!;
                            return CreateErrorResponse(
                                request,
                                error.Code,
                                error.Message,
                                precondition,
                                unityBuildProfile);
                        }

                        runnerResult = invocationResult.RunnerResult!;
                    }
                    else
                    {
                        PublishProgress(
                            progressSink,
                            executionRequest,
                            BuildRunProgressEventNames.RunnerStarted,
                            BuildRunProgressPhase.RunnerInvocation,
                            executionRequest.RunnerKind,
                            runnerStatus: null);
                        normalizedReport = executionRequest is BuildRunExecutionRequest.UnityBuildProfile
                            ? buildProfileBuildRunner.Run(unityBuildProfile!, resolvedInput, outputLayout!)
                            : buildPipelineRunner.Run(UnityBuildPlayerOptionsFactory.Create(
                                (BuildRunExecutionRequest.ExplicitBuildPipeline)executionRequest,
                                resolvedInput));
                        if (normalizedReport != null)
                        {
                            runnerResult = new IpcBuildRunnerResultArtifact(
                                Source: IpcBuildRunnerResultSource.BuildPipelineBuildReport,
                                Status: normalizedReport.Result,
                                DurationMilliseconds: normalizedReport.DurationMilliseconds,
                                ErrorCount: normalizedReport.ErrorCount,
                                WarningCount: normalizedReport.WarningCount,
                                Diagnostics: Array.Empty<IpcBuildRunnerDiagnostic>(),
                                Outputs: Array.Empty<BuildRunnerOutputPath>(),
                                BuildReport: null);
                        }
                    }
                }
                finally
                {
                    try
                    {
                        lifecycleAfter = preconditionProbe.CaptureAfterBuild();
                    }
                    finally
                    {
                        await editorUpdateAwaiter.WaitForNextUpdateAsync(CancellationToken.None);
                        mutationActivity.Complete();
                    }
                }

                executionCancellationToken.ThrowIfCancellationRequested();
                FileSystemAccessBoundary.EnsureSecureDirectory(executionRequest.OutputPath);
                var completedAtUtc = DateTimeOffset.UtcNow;
                var logEndSnapshot = unityLogStream.Snapshot();
                PublishLogEntries(progressSink, executionRequest.RunId, logStartSnapshot, logEndSnapshot);
                if (runnerResult != null)
                {
                    PublishProgress(
                        progressSink,
                        executionRequest,
                        BuildRunProgressEventNames.RunnerCompleted,
                        BuildRunProgressPhase.RunnerResult,
                        executionRequest.RunnerKind,
                        runnerResult.Status);
                }

                var projectMutation = projectMutationAuditProbe.Complete(
                    projectIdentity.ProjectPath,
                    executionRequest.ProjectMutationMode,
                    mutationBaseline);
                var logEndOffset = GetLogLength(logSourcePath);
                if (logEndOffset < logStartOffset)
                {
                    logStartOffset = 0;
                }

                if (normalizedReport == null && executeMethodRequest == null)
                {
                    return CreateErrorResponse(
                        request,
                        BuildErrorCodes.BuildReportMissing,
                        "Unity BuildPipeline did not return a BuildReport.",
                        precondition,
                        unityBuildProfile);
                }

                if (normalizedReport != null)
                {
                    await WriteJsonAtomicallyAsync(
                            executionRequest.BuildReportPath,
                            normalizedReport,
                            executionCancellationToken)
                        .ConfigureAwait(false);
                }

                var logSummaryCounts = await ExportBuildLogAsync(
                        logSourcePath,
                        executionRequest.BuildLogPath,
                        logStartOffset,
                        logEndOffset,
                        executeMethodRequest?.RunnerEnvironmentSecretValues,
                        executionCancellationToken)
                    .ConfigureAwait(false);
                var completionReason = UnityBuildReportNormalizer.ToCompletionReason(
                    normalizedReport?.Result ?? runnerResult!.Status);
                var logs = new IpcBuildLogSummary(
                    EntryCount: logSummaryCounts.EntryCount,
                    ErrorCount: logSummaryCounts.ErrorCount,
                    WarningCount: logSummaryCounts.WarningCount,
                    CompletionReason: completionReason,
                    Window: new IpcBuildLogWindow(
                        startedAtUtc,
                        completedAtUtc,
                        logStartSnapshot.NextCursor,
                        logEndSnapshot.NextCursor));
                var response = new IpcBuildRunResponse(
                    RunId: executionRequest.RunId,
                    ProjectFingerprint: precondition.Project.ProjectFingerprint,
                    LifecycleBefore: precondition.LifecycleBefore,
                    LifecycleAfter: lifecycleAfter,
                    DirtyState: precondition.DirtyState!,
                    Input: precondition.InputProbe!,
                    OutputLayout: outputLayout,
                    UnityBuildProfile: unityBuildProfile,
                    Report: normalizedReport,
                    Logs: logs,
                    ProjectMutation: projectMutation,
                    RunnerResult: runnerResult);
                return UnityIpcResponseFactory.CreateSuccessResponse(request, response);
            }
            catch (OperationCanceledException) when (cancellation.Reason == IpcRequestCancellationReason.ExecutionDeadline)
            {
                return CreateErrorResponse(
                    request,
                    IpcTransportErrorCodes.IpcTimeout,
                    "Unity build run reached its request deadline.",
                    precondition,
                    unityBuildProfile);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (UnityBuildProfileInputException exception)
            {
                return CreateErrorResponse(
                    request,
                    BuildErrorCodes.BuildUnityBuildProfileInvalid,
                    exception.Message,
                    precondition,
                    unityBuildProfile);
            }
            catch (ArgumentException exception)
            {
                return CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InvalidArgument,
                    exception.Message,
                    precondition,
                    unityBuildProfile);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
            {
                return CreateErrorResponse(
                    request,
                    BuildErrorCodes.BuildArtifactWriteFailed,
                    $"Unity build artifact write failed. {exception.Message}",
                    precondition,
                    unityBuildProfile);
            }
            catch (Exception exception)
            {
                return CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InternalError,
                    $"Unity build run failed. {exception.Message}",
                    precondition,
                    unityBuildProfile);
            }
            finally
            {
                if (progressSink != null)
                {
                    try
                    {
                        await progressSink.CompleteAndFlushAsync(cancellation.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellation.Reason == IpcRequestCancellationReason.ExecutionDeadline)
                    {
                        // The timeout response must not wait for progress frames accepted before the deadline.
                    }
                }
            }
        }

        private static IpcResponse CreatePreconditionErrorResponse (
            ValidatedUnityIpcRequest request,
            UnityBuildPreconditionProbeResult precondition,
            IpcUnityBuildProfileInput? unityBuildProfile)
        {
            var error = precondition.Error!;
            return UnityIpcResponseFactory.CreateErrorResponse(
                request,
                error.Code,
                error.Message,
                error.OpId,
                CreateErrorPayload(precondition, unityBuildProfile));
        }

        private static IpcResponse CreateBuildProfileInputErrorResponse (
            ValidatedUnityIpcRequest request,
            UnityBuildProfileInputResolutionResult result)
        {
            var error = result.Error!;
            var payload = new IpcBuildRunErrorPayload(
                Project: null,
                LifecycleBefore: result.LifecycleBefore,
                DirtyState: result.DirtyState,
                Input: null,
                UnityBuildProfile: result.UnityBuildProfile);
            return UnityIpcResponseFactory.CreateErrorResponse(
                request,
                error.Code,
                error.Message,
                error.OpId,
                payload);
        }

        private static void PublishProgress (
            UnityIpcBuildRunProgressSink? progressSink,
            BuildRunExecutionRequest request,
            string eventName,
            BuildRunProgressPhase phase,
            BuildRunnerKind? runnerKind,
            IpcBuildReportResult? runnerStatus)
        {
            if (progressSink == null)
            {
                return;
            }

            progressSink.Publish(
                eventName,
                new BuildProgressEntry(
                    RunId: request.RunId,
                    ProfileDigest: request.ProfileDigest,
                    Phase: phase,
                    RunnerKind: runnerKind,
                    RunnerStatus: runnerStatus,
                    Verdict: null,
                    ReportRefs: Array.Empty<BuildArtifactKind>(),
                    ErrorCode: null));
        }

        private static void PublishLogEntries (
            UnityIpcBuildRunProgressSink? progressSink,
            Guid runId,
            UnityLogSnapshot startSnapshot,
            UnityLogSnapshot endSnapshot)
        {
            if (progressSink == null
                || startSnapshot.NextCursor.StreamId != endSnapshot.NextCursor.StreamId)
            {
                return;
            }

            var startSequence = startSnapshot.NextCursor.Sequence;
            var endSequence = endSnapshot.NextCursor.Sequence;
            var events = endSnapshot.Events;
            for (var i = 0; i < events.Count; i++)
            {
                var unityLogEvent = events[i];
                if (unityLogEvent.Cursor.Sequence < startSequence
                    || unityLogEvent.Cursor.Sequence >= endSequence)
                {
                    continue;
                }

                progressSink.Publish(
                    BuildRunProgressEventNames.LogEntry,
                    new BuildLogEntry(
                        RunId: runId,
                        TimestampUtc: unityLogEvent.Timestamp,
                        Level: ParseBuildLogLevel(unityLogEvent.Level),
                        Message: LimitProgressLogMessage(unityLogEvent.Message),
                        Cursor: unityLogEvent.Cursor,
                        Source: BuildLogEntrySource.UnityLog));
            }
        }

        private static BuildLogEntryLevel ParseBuildLogLevel (IpcLogLevel value)
        {
            switch (value)
            {
                case IpcLogLevel.Error:
                    return BuildLogEntryLevel.Error;
                case IpcLogLevel.Warning:
                    return BuildLogEntryLevel.Warning;
                case IpcLogLevel.Info:
                    return BuildLogEntryLevel.Info;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Unity log stream produced an unsupported build log level.");
            }
        }

        private static string LimitProgressLogMessage (string message)
        {
            if (Utf8NoBomEncoding.GetByteCount(message) <= BuildLogEntryLimits.MaxMessageUtf8Bytes)
            {
                return message;
            }

            var suffixByteCount = Utf8NoBomEncoding.GetByteCount(ProgressLogEntryTruncatedSuffix);
            var byteBudget = BuildLogEntryLimits.MaxMessageUtf8Bytes - suffixByteCount;
            if (byteBudget <= 0)
            {
                return ProgressLogEntryTruncatedSuffix;
            }

            var byteCount = 0;
            var endIndex = 0;
            while (endIndex < message.Length)
            {
                var charCount = 1;
                if (char.IsHighSurrogate(message[endIndex])
                    && endIndex + 1 < message.Length
                    && char.IsLowSurrogate(message[endIndex + 1]))
                {
                    charCount = 2;
                }

                var nextByteCount = Utf8NoBomEncoding.GetByteCount(message, endIndex, charCount);
                if (byteCount + nextByteCount > byteBudget)
                {
                    break;
                }

                byteCount += nextByteCount;
                endIndex += charCount;
            }

            return message.Substring(0, endIndex) + ProgressLogEntryTruncatedSuffix;
        }

        private static IpcResponse CreateErrorResponse (
            ValidatedUnityIpcRequest request,
            UcliCode code,
            string message,
            UnityBuildPreconditionProbeResult? precondition,
            IpcUnityBuildProfileInput? unityBuildProfile)
        {
            if (precondition == null)
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    code,
                    message,
                    null,
                    CreateErrorPayload(unityBuildProfile));
            }

            return UnityIpcResponseFactory.CreateErrorResponse(
                request,
                code,
                message,
                null,
                CreateErrorPayload(precondition, unityBuildProfile));
        }

        private static IpcBuildRunErrorPayload CreateErrorPayload (
            UnityBuildPreconditionProbeResult precondition,
            IpcUnityBuildProfileInput? unityBuildProfile)
        {
            return new IpcBuildRunErrorPayload(
                Project: precondition.Project,
                LifecycleBefore: precondition.LifecycleBefore,
                DirtyState: precondition.DirtyState,
                Input: precondition.InputProbe,
                UnityBuildProfile: unityBuildProfile);
        }

        private static IpcBuildRunErrorPayload? CreateErrorPayload (IpcUnityBuildProfileInput? unityBuildProfile)
        {
            if (unityBuildProfile == null)
            {
                return null;
            }

            var applyAudit = unityBuildProfile.ApplyAudit;
            return new IpcBuildRunErrorPayload(
                Project: null,
                LifecycleBefore: applyAudit?.LifecycleAfter,
                DirtyState: applyAudit?.DirtyStateAfter,
                Input: null,
                UnityBuildProfile: unityBuildProfile);
        }

        private async Task<EditorLogRangeExportResult> ExportBuildLogAsync (
            string sourcePath,
            string destinationPath,
            long startOffset,
            long endOffset,
            IReadOnlyDictionary<string, string>? sensitiveValues,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                await WriteTextAtomicallyAsync(destinationPath, string.Empty, cancellationToken).ConfigureAwait(false);
                return new EditorLogRangeExportResult(0, 0, 0);
            }

            if (!HasRedactableSensitiveValues(sensitiveValues))
            {
                return await editorLogRangeExporter.ExportRangeAsync(
                        sourcePath,
                        destinationPath,
                        startOffset,
                        endOffset,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }

            return await editorLogRangeExporter.ExportRangeAsync(
                    sourcePath,
                    destinationPath,
                    startOffset,
                    endOffset,
                    sensitiveValues!.Values,
                    cancellationToken)
                    .ConfigureAwait(false);
        }

        private static bool HasRedactableSensitiveValues (IReadOnlyDictionary<string, string>? sensitiveValues)
        {
            if (sensitiveValues == null || sensitiveValues.Count == 0)
            {
                return false;
            }

            foreach (var sensitiveValue in sensitiveValues.Values)
            {
                if (!string.IsNullOrEmpty(sensitiveValue))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool TryValidateRequest (
            BuildRunExecutionRequest request,
            IpcProjectIdentity projectIdentity,
            out string? errorMessage)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (projectIdentity == null)
            {
                throw new ArgumentNullException(nameof(projectIdentity));
            }

            if (!TryResolveExpectedArtifactPaths(
                    request.RunId,
                    projectIdentity,
                    out var expectedOutputPath,
                    out var expectedBuildReportPath,
                    out var expectedBuildLogPath,
                    out errorMessage))
            {
                return false;
            }

            if (!PathEquals(request.OutputPath, expectedOutputPath!)
                || !PathEquals(request.BuildReportPath, expectedBuildReportPath!)
                || !PathEquals(request.BuildLogPath, expectedBuildLogPath!))
            {
                errorMessage = "Build output and artifact paths must match the expected uCLI build artifact layout.";
                return false;
            }

            if (!(request is BuildRunExecutionRequest.ExplicitBuildPipeline buildPipelineRequest))
            {
                errorMessage = null;
                return true;
            }

            var buildTarget = buildPipelineRequest.BuildTarget;
            if (!IpcBuildOutputLayoutResolver.TryResolve(
                expectedOutputPath!,
                buildTarget,
                androidAppBundle: false,
                out var expectedOutputLayout))
            {
                errorMessage = $"Build outputLayout could not be resolved for build target: {ContractLiteralCodec.ToValue(buildTarget)}.";
                return false;
            }

            if (buildPipelineRequest.OutputLayout.Shape != expectedOutputLayout!.Shape
                || !PathEquals(buildPipelineRequest.OutputLayout.LocationPathName, expectedOutputLayout.LocationPathName))
            {
                errorMessage = "Build outputLayout must match the expected uCLI build artifact layout.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        private static bool TryResolveExpectedArtifactPaths (
            Guid runId,
            IpcProjectIdentity projectIdentity,
            out string? expectedOutputPath,
            out string? expectedBuildReportPath,
            out string? expectedBuildLogPath,
            out string? errorMessage)
        {
            expectedOutputPath = null;
            expectedBuildReportPath = null;
            expectedBuildLogPath = null;

            try
            {
                var storageRoot = UcliStoragePathResolver.ResolveStorageRoot(projectIdentity.ProjectPath);
                var expectedArtifactsDirectory = UcliStoragePathResolver.ResolveBuildRunArtifactsDirectory(
                    storageRoot,
                    runId);
                expectedOutputPath = UcliStoragePathResolver.ResolveBuildRunOutputDirectory(
                    storageRoot,
                    runId);
                expectedBuildReportPath = Path.GetFullPath(Path.Combine(
                    expectedArtifactsDirectory,
                    UcliStoragePathNames.BuildReportFileName));
                expectedBuildLogPath = Path.GetFullPath(Path.Combine(
                    expectedArtifactsDirectory,
                    UcliStoragePathNames.BuildLogFileName));
                errorMessage = null;
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
            {
                errorMessage = $"Build artifact paths could not be resolved. {exception.Message}";
                return false;
            }
        }

        private static bool PathEquals (
            string actualPath,
            string expectedPath)
        {
            if (string.IsNullOrWhiteSpace(actualPath))
            {
                return false;
            }

            if (!Path.IsPathFullyQualified(actualPath))
            {
                return false;
            }

            try
            {
                return PathIdentity.IsSamePath(actualPath, expectedPath);
            }
            catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
            {
                return false;
            }
        }

        private static void EnsureBuildPipelineOutputLayoutReady (IpcBuildOutputLayout outputLayout)
        {
            if (outputLayout == null)
            {
                throw new InvalidOperationException("BuildPipeline outputLayout must be specified.");
            }

            var parentDirectory = Path.GetDirectoryName(outputLayout.LocationPathName);
            if (string.IsNullOrWhiteSpace(parentDirectory))
            {
                throw new InvalidOperationException(
                    $"BuildPipeline output parent directory could not be resolved: {outputLayout.LocationPathName}");
            }

            FileSystemAccessBoundary.EnsureSecureDirectory(parentDirectory);
            EnsureBuildPipelineOutputTargetDoesNotExist(outputLayout.LocationPathName);
        }

        private static void EnsureBuildPipelineOutputTargetDoesNotExist (string locationPathName)
        {
            if (!File.Exists(locationPathName) && !Directory.Exists(locationPathName))
            {
                return;
            }

            var attributes = File.GetAttributes(locationPathName);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"BuildPipeline output target must not be a reparse point: {locationPathName}");
            }

            throw new IOException($"BuildPipeline output target already exists: {locationPathName}");
        }

        private static long GetLogLength (string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return 0;
            }

            return new FileInfo(path).Length;
        }

        private static async Task WriteJsonAtomicallyAsync<T> (
            string path,
            T value,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directoryPath = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new InvalidOperationException($"Artifact directory path could not be resolved: {path}");
            }

            FileSystemAccessBoundary.EnsureSecureDirectory(directoryPath);
            var temporaryStream = FileUtilities.OpenAtomicWriteTemporaryFileInDirectory(directoryPath, out var tempPath);
            var temporaryFileOwned = true;
            try
            {
                using (temporaryStream)
                {
                    await JsonSerializer.SerializeAsync(
                            temporaryStream,
                            value,
                            IpcJsonSerializerOptions.Default,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                cancellationToken.ThrowIfCancellationRequested();
                FileSystemAccessBoundary.EnsureSecureFile(tempPath);
                await FileUtilities.PublishAtomicWriteTemporaryFileAsync(
                    tempPath,
                    path,
                    cancellationToken);
                temporaryFileOwned = false;
                FileSystemAccessBoundary.EnsureSecureFile(path);
            }
            finally
            {
                if (temporaryFileOwned)
                {
                    FileUtilities.DeleteIfExists(tempPath);
                }
            }
        }

        private static async Task WriteTextAtomicallyAsync (
            string path,
            string value,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directoryPath = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new InvalidOperationException($"Artifact directory path could not be resolved: {path}");
            }

            FileSystemAccessBoundary.EnsureSecureDirectory(directoryPath);
            var temporaryStream = FileUtilities.OpenAtomicWriteTemporaryFileInDirectory(directoryPath, out var tempPath);
            var temporaryFileOwned = true;
            try
            {
                using (temporaryStream)
                {
                    var byteCount = Utf8NoBomEncoding.GetByteCount(value);
                    if (byteCount > 0)
                    {
                        var buffer = ArrayPool<byte>.Shared.Rent(byteCount);
                        try
                        {
                            var bytesWritten = EncodeUtf8(value, buffer, byteCount);
                            await temporaryStream.WriteAsync(buffer, 0, bytesWritten, cancellationToken);
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(buffer);
                        }
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                FileSystemAccessBoundary.EnsureSecureFile(tempPath);
                await FileUtilities.PublishAtomicWriteTemporaryFileAsync(
                    tempPath,
                    path,
                    cancellationToken);
                temporaryFileOwned = false;
                FileSystemAccessBoundary.EnsureSecureFile(path);
            }
            finally
            {
                if (temporaryFileOwned)
                {
                    FileUtilities.DeleteIfExists(tempPath);
                }
            }
        }

        private static int EncodeUtf8 (
            string value,
            byte[] buffer,
            int byteCount)
        {
            return Utf8NoBomEncoding.GetBytes(value.AsSpan(), buffer.AsSpan(0, byteCount));
        }


    }
}
