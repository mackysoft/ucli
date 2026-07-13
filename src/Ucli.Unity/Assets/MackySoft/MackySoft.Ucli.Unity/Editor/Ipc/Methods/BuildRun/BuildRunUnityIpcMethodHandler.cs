using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
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

        private readonly IIpcRequestTimeoutScopeFactory timeoutScopeFactory;

        private readonly UnityLogRedactionScopeProvider unityLogRedactionScopeProvider;

        private readonly IUnityLogStream unityLogStream;

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
            IIpcRequestTimeoutScopeFactory timeoutScopeFactory,
            UnityLogRedactionScopeProvider unityLogRedactionScopeProvider,
            IUnityLogStream unityLogStream)
        {
            this.preconditionProbe = preconditionProbe ?? throw new ArgumentNullException(nameof(preconditionProbe));
            this.buildProfileInputResolver = buildProfileInputResolver ?? throw new ArgumentNullException(nameof(buildProfileInputResolver));
            this.projectMutationAuditProbe = projectMutationAuditProbe ?? throw new ArgumentNullException(nameof(projectMutationAuditProbe));
            this.buildPipelineRunner = buildPipelineRunner ?? throw new ArgumentNullException(nameof(buildPipelineRunner));
            this.buildProfileBuildRunner = buildProfileBuildRunner ?? throw new ArgumentNullException(nameof(buildProfileBuildRunner));
            this.executeMethodRunner = executeMethodRunner ?? throw new ArgumentNullException(nameof(executeMethodRunner));
            this.editorLogRangeExporter = editorLogRangeExporter ?? throw new ArgumentNullException(nameof(editorLogRangeExporter));
            this.projectIdentity = projectIdentity ?? throw new ArgumentNullException(nameof(projectIdentity));
            this.timeoutScopeFactory = timeoutScopeFactory ?? throw new ArgumentNullException(nameof(timeoutScopeFactory));
            this.unityLogRedactionScopeProvider = unityLogRedactionScopeProvider ?? throw new ArgumentNullException(nameof(unityLogRedactionScopeProvider));
            this.unityLogStream = unityLogStream ?? throw new ArgumentNullException(nameof(unityLogStream));
        }

        /// <inheritdoc />
        public UnityIpcMethod Method => UnityIpcMethod.BuildRun;

        /// <inheritdoc />
        public async ValueTask<IpcResponse> HandleAsync (
            IpcRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
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

            return await HandleDecodedAsync(
                request,
                buildRunRequest!,
                progressSinkFactory: null,
                cancellationToken);
        }

        /// <inheritdoc />
        public async ValueTask<IpcResponse> HandleStreamingAsync (
            IpcRequest request,
            IIpcStreamFrameWriter streamWriter,
            CancellationToken cancellationToken)
        {
            if (streamWriter == null)
            {
                throw new ArgumentNullException(nameof(streamWriter));
            }

            cancellationToken.ThrowIfCancellationRequested();
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

            return await HandleDecodedAsync(
                request,
                buildRunRequest!,
                executionCancellationToken => new UnityIpcBuildRunProgressSink(
                    streamWriter,
                    buildRunRequest!.RunId,
                    executionCancellationToken),
                cancellationToken);
        }

        private async ValueTask<IpcResponse> HandleDecodedAsync (
            IpcRequest request,
            IpcBuildRunRequest buildRunRequest,
            Func<CancellationToken, UnityIpcBuildRunProgressSink>? progressSinkFactory,
            CancellationToken cancellationToken)
        {
            if (!TryValidateRequest(buildRunRequest!, projectIdentity, out var validationErrorMessage))
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InvalidArgument,
                    validationErrorMessage!,
                    null);
            }

            if (!ContractLiteralCodec.TryParse<BuildProfileInputsKind>(buildRunRequest.InputKind, out var inputKind)
                || !ContractLiteralCodec.TryParse<IpcBuildRunnerKind>(buildRunRequest.RunnerKind, out var runnerKind))
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InternalError,
                    "Validated build.run request contained unsupported contract literals.",
                    null);
            }

            IIpcRequestTimeoutScope? requestTimeoutScope = null;
            UnityBuildPreconditionProbeResult? precondition = null;
            IpcUnityBuildProfileInput? unityBuildProfile = null;
            UnityIpcBuildRunProgressSink? progressSink = null;
            try
            {
                requestTimeoutScope = timeoutScopeFactory.CreateLinked(
                    buildRunRequest!.TimeoutMilliseconds,
                    cancellationToken);
                var executionCancellationToken = requestTimeoutScope.Token;
                progressSink = progressSinkFactory?.Invoke(executionCancellationToken);
                UnityBuildPreconditionInput preconditionInput;
                IpcBuildOutputLayout? outputLayout;
                if (inputKind == BuildProfileInputsKind.UnityBuildProfile)
                {
                    var profileResolution = await buildProfileInputResolver.ResolveAsync(
                        buildRunRequest,
                        executionCancellationToken);
                    unityBuildProfile = profileResolution.UnityBuildProfile;
                    if (!profileResolution.IsSuccess)
                    {
                        return CreateBuildProfileInputErrorResponse(request, profileResolution);
                    }

                    preconditionInput = profileResolution.PreconditionInput!;
                    outputLayout = profileResolution.OutputLayout!;
                }
                else
                {
                    preconditionInput = new UnityBuildPreconditionInput(
                        InputKind: buildRunRequest.InputKind,
                        BuildTarget: buildRunRequest.BuildTarget!,
                        UnityBuildTarget: buildRunRequest.UnityBuildTarget!,
                        SceneSource: buildRunRequest.SceneSource!,
                        ScenePaths: buildRunRequest.ScenePaths,
                        Development: buildRunRequest.Development,
                        AllowedEditorModes: buildRunRequest.AllowedEditorModes);
                    outputLayout = buildRunRequest.OutputLayout!;
                }

                precondition = await preconditionProbe.ProbeBeforeBuildAsync(
                    preconditionInput,
                    executionCancellationToken);
                if (!precondition.IsSuccess)
                {
                    return CreatePreconditionErrorResponse(request, precondition, unityBuildProfile);
                }

                PublishProgress(
                    progressSink,
                    buildRunRequest,
                    BuildRunProgressEventNames.ReadinessCompleted,
                    ContractLiteralCodec.ToValue(BuildRunProgressPhase.Readiness),
                    runnerKind: null,
                    runnerStatus: null,
                    errorCode: null);

                FileSystemAccessBoundary.EnsureSecureDirectory(buildRunRequest.OutputPath);
                if (inputKind == BuildProfileInputsKind.UnityBuildProfile)
                {
                    EnsureBuildPipelineOutputLayoutReady(outputLayout!);
                }
                else if (runnerKind == IpcBuildRunnerKind.BuildPipeline)
                {
                    EnsureBuildPipelineOutputLayoutReady(buildRunRequest.OutputLayout!);
                }

                if (runnerKind == IpcBuildRunnerKind.BuildPipeline)
                {
                    PublishProgress(
                        progressSink,
                        buildRunRequest,
                        BuildRunProgressEventNames.RunnerResolved,
                        ContractLiteralCodec.ToValue(BuildRunProgressPhase.RunnerResolution),
                        buildRunRequest.RunnerKind,
                        runnerStatus: null,
                        errorCode: null);
                }

                var logSourcePath = Application.consoleLogPath;
                var logStartOffset = GetLogLength(logSourcePath);
                var logStartSnapshot = unityLogStream.Snapshot();
                using var unityLogRedactionScope = runnerKind == IpcBuildRunnerKind.ExecuteMethod
                    ? unityLogRedactionScopeProvider.BeginScope(buildRunRequest.RunnerEnvironmentSecretValues.Values)
                    : null;
                var startedAtUtc = DateTimeOffset.UtcNow;
                var mutationBaseline = projectMutationAuditProbe.CaptureBaseline(projectIdentity.ProjectPath);
                executionCancellationToken.ThrowIfCancellationRequested();
                IpcBuildRunnerResultArtifact? runnerResult = null;
                IpcBuildReportArtifact? normalizedReport = null;
                if (runnerKind == IpcBuildRunnerKind.ExecuteMethod)
                {
                    var invocationResult = executeMethodRunner.Run(
                        buildRunRequest,
                        projectIdentity,
                        precondition.ResolvedInput!,
                        new BuildRunExecuteMethodProgressSink(progressSink, buildRunRequest));
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
                        buildRunRequest,
                        BuildRunProgressEventNames.RunnerStarted,
                        ContractLiteralCodec.ToValue(BuildRunProgressPhase.RunnerInvocation),
                        buildRunRequest.RunnerKind,
                        runnerStatus: null,
                        errorCode: null);
                    normalizedReport = inputKind == BuildProfileInputsKind.UnityBuildProfile
                        ? buildProfileBuildRunner.Run(unityBuildProfile!, precondition.ResolvedInput!, outputLayout!)
                        : buildPipelineRunner.Run(UnityBuildPlayerOptionsFactory.Create(buildRunRequest, precondition.ResolvedInput!));
                    if (normalizedReport != null)
                    {
                        runnerResult = new IpcBuildRunnerResultArtifact(
                            Source: ContractLiteralCodec.ToValue(IpcBuildRunnerResultSource.BuildPipelineBuildReport),
                            Status: normalizedReport.Result,
                            DurationMilliseconds: normalizedReport.DurationMilliseconds,
                            ErrorCount: normalizedReport.ErrorCount,
                            WarningCount: normalizedReport.WarningCount,
                            Diagnostics: Array.Empty<IpcBuildRunnerDiagnostic>());
                    }
                }

                executionCancellationToken.ThrowIfCancellationRequested();
                FileSystemAccessBoundary.EnsureSecureDirectory(buildRunRequest.OutputPath);
                var completedAtUtc = DateTimeOffset.UtcNow;
                var logEndSnapshot = unityLogStream.Snapshot();
                PublishLogEntries(progressSink, buildRunRequest.RunId, logStartSnapshot, logEndSnapshot);
                if (runnerResult != null)
                {
                    PublishProgress(
                        progressSink,
                        buildRunRequest,
                        BuildRunProgressEventNames.RunnerCompleted,
                        ContractLiteralCodec.ToValue(BuildRunProgressPhase.RunnerResult),
                        buildRunRequest.RunnerKind,
                        runnerResult.Status,
                        errorCode: null);
                }

                var lifecycleAfter = preconditionProbe.CaptureAfterBuild();
                var projectMutation = projectMutationAuditProbe.Complete(
                    projectIdentity.ProjectPath,
                    buildRunRequest.ProjectMutationMode,
                    mutationBaseline);
                var logEndOffset = GetLogLength(logSourcePath);
                if (logEndOffset < logStartOffset)
                {
                    logStartOffset = 0;
                }

                if (normalizedReport == null && runnerKind == IpcBuildRunnerKind.BuildPipeline)
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
                            buildRunRequest.BuildReportPath,
                            normalizedReport,
                            executionCancellationToken)
                        .ConfigureAwait(false);
                }

                var logSummaryCounts = await ExportBuildLogAsync(
                        logSourcePath,
                        buildRunRequest.BuildLogPath,
                        logStartOffset,
                        logEndOffset,
                        runnerKind == IpcBuildRunnerKind.ExecuteMethod ? buildRunRequest.RunnerEnvironmentSecretValues : null,
                        executionCancellationToken)
                    .ConfigureAwait(false);
                var completionReason = UnityBuildReportNormalizer.ToCompletionReason(
                    normalizedReport?.Result ?? runnerResult!.Status);
                var logs = new IpcBuildLogSummary(
                    EntryCount: logSummaryCounts.EntryCount,
                    ErrorCount: logSummaryCounts.ErrorCount,
                    WarningCount: logSummaryCounts.WarningCount,
                    CompletionReason: ContractLiteralCodec.ToValue(completionReason),
                    Window: new IpcBuildLogWindow(
                        startedAtUtc,
                        completedAtUtc,
                        logStartSnapshot.NextCursor,
                        logEndSnapshot.NextCursor));
                var response = new IpcBuildRunResponse(
                    RunId: buildRunRequest.RunId,
                    ProjectFingerprint: precondition.Project.ProjectFingerprint,
                    LifecycleBefore: precondition.LifecycleBefore,
                    LifecycleAfter: lifecycleAfter,
                    DirtyState: precondition.DirtyState!,
                    Input: precondition.InputProbe!,
                    OutputLayout: outputLayout,
                    UnityBuildProfile: unityBuildProfile,
                    Report: normalizedReport,
                    Logs: logs,
                    ProjectMutation: projectMutation);
                return UnityIpcResponseFactory.CreateSuccessResponse(request, response with
                {
                    RunnerResult = runnerResult,
                });
            }
            catch (OperationCanceledException) when (IsRequestTimeout(requestTimeoutScope, cancellationToken))
            {
                return CreateErrorResponse(
                    request,
                    IpcTransportErrorCodes.IpcTimeout,
                    $"Unity build run timed out after {buildRunRequest!.TimeoutMilliseconds!.Value} milliseconds.",
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
                try
                {
                    if (progressSink != null)
                    {
                        try
                        {
                            await progressSink.CompleteAndFlushAsync(requestTimeoutScope!.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (IsRequestTimeout(requestTimeoutScope, cancellationToken))
                        {
                            // The timeout response must not wait for progress frames accepted before the deadline.
                        }
                    }
                }
                finally
                {
                    requestTimeoutScope?.Dispose();
                }
            }
        }

        private static IpcResponse CreatePreconditionErrorResponse (
            IpcRequest request,
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
            IpcRequest request,
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
            IpcBuildRunRequest request,
            string eventName,
            string phase,
            string? runnerKind,
            string? runnerStatus,
            string? errorCode)
        {
            if (progressSink == null)
            {
                return;
            }

            progressSink.Publish(
                eventName,
                new BuildProgressEntry(
                    RunId: request.RunId,
                    ProfileDigest: request.ProfileDigest!,
                    Phase: phase,
                    RunnerKind: runnerKind,
                    RunnerStatus: runnerStatus,
                    Verdict: null,
                    ReportRefs: Array.Empty<string>(),
                    ErrorCode: errorCode));
        }

        private static void PublishLogEntries (
            UnityIpcBuildRunProgressSink? progressSink,
            string runId,
            UnityLogSnapshot startSnapshot,
            UnityLogSnapshot endSnapshot)
        {
            if (progressSink == null
                || !IpcLogCursorCodec.TryParse(startSnapshot.NextCursor, out var startStreamId, out var startSequence)
                || !IpcLogCursorCodec.TryParse(endSnapshot.NextCursor, out var endStreamId, out var endSequence)
                || !string.Equals(startStreamId, endStreamId, StringComparison.Ordinal))
            {
                return;
            }

            var events = endSnapshot.Events;
            for (var i = 0; i < events.Count; i++)
            {
                var unityLogEvent = events[i];
                if (unityLogEvent.Sequence < startSequence || unityLogEvent.Sequence >= endSequence)
                {
                    continue;
                }

                progressSink.Publish(
                    BuildRunProgressEventNames.LogEntry,
                    new BuildLogEntry(
                        RunId: runId,
                        TimestampUtc: TryParseLogTimestamp(unityLogEvent.Timestamp, out var timestamp)
                            ? timestamp
                            : DateTimeOffset.UtcNow,
                        Level: unityLogEvent.Level,
                        Message: LimitProgressLogMessage(unityLogEvent.Message),
                        Cursor: unityLogEvent.Cursor,
                        Source: ContractLiteralCodec.ToValue(BuildLogEntrySource.UnityLog)));
            }
        }

        private static bool TryParseLogTimestamp (
            string value,
            out DateTimeOffset timestamp)
        {
            return DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out timestamp);
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
            IpcRequest request,
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
            IpcBuildRunRequest request,
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

            if (request.TimeoutMilliseconds.HasValue && request.TimeoutMilliseconds.Value <= 0)
            {
                errorMessage = "Build run timeoutMilliseconds must be greater than zero when specified.";
                return false;
            }

            if (!IsValidPathSegment(request.RunId))
            {
                errorMessage = "Build runId must be one non-empty path segment.";
                return false;
            }

            if (!ContractLiteralCodec.TryParse<BuildProfileInputsKind>(request.InputKind, out var inputKind))
            {
                errorMessage = $"Build inputKind is invalid: {request.InputKind}.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.ProjectMutationMode))
            {
                errorMessage = "Build project mutation mode value must not be empty.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.ProfileDigest))
            {
                errorMessage = "Build profileDigest must not be empty.";
                return false;
            }

            if (!ContractLiteralCodec.IsDefined<BuildProfileProjectMutationMode>(request.ProjectMutationMode))
            {
                errorMessage = $"Build projectMutationMode is invalid: {request.ProjectMutationMode}.";
                return false;
            }

            if (!HasValidAllowedEditorModes(request.AllowedEditorModes))
            {
                errorMessage = "Build allowedEditorModes must contain at least one supported editor mode.";
                return false;
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

            if (!ContractLiteralCodec.TryParse<IpcBuildRunnerKind>(request.RunnerKind, out var runnerKind))
            {
                errorMessage = $"Build runnerKind is invalid: {request.RunnerKind}.";
                return false;
            }

            if (!HasValidRunnerContract(request, runnerKind, out errorMessage))
            {
                return false;
            }

            if (inputKind == BuildProfileInputsKind.UnityBuildProfile)
            {
                return TryValidateUnityBuildProfileRequest(request, runnerKind, out errorMessage);
            }

            if (inputKind != BuildProfileInputsKind.Explicit)
            {
                errorMessage = $"Build inputKind is unsupported: {request.InputKind}.";
                return false;
            }

            return TryValidateExplicitRequest(request, expectedOutputPath!, runnerKind, out errorMessage);
        }

        private static bool TryValidateExplicitRequest (
            IpcBuildRunRequest request,
            string expectedOutputPath,
            IpcBuildRunnerKind runnerKind,
            out string? errorMessage)
        {
            if (request.UnityBuildProfile != null)
            {
                errorMessage = "Build unityBuildProfile input must not be specified for explicit build input.";
                return false;
            }

            var buildTarget = request.BuildTarget;
            if (buildTarget == null
                || string.IsNullOrWhiteSpace(buildTarget)
                || string.IsNullOrWhiteSpace(request.UnityBuildTarget)
                || string.IsNullOrWhiteSpace(request.SceneSource))
            {
                errorMessage = "BuildTarget and scene source values must not be empty for explicit build input.";
                return false;
            }

            if (!BuildTargetStableNameUnityBuildTargetResolver.TryResolve(buildTarget, out var expectedUnityBuildTarget))
            {
                errorMessage = $"Build buildTarget stable name is unsupported: {buildTarget}.";
                return false;
            }

            if (!string.Equals(request.UnityBuildTarget, expectedUnityBuildTarget, StringComparison.Ordinal))
            {
                errorMessage = $"Build UnityBuildTarget must match buildTarget. Expected={expectedUnityBuildTarget}, Actual={request.UnityBuildTarget}.";
                return false;
            }

            if (runnerKind == IpcBuildRunnerKind.BuildPipeline && request.OutputLayout == null)
            {
                errorMessage = "Build outputLayout must match the expected uCLI build artifact layout.";
                return false;
            }

            IpcBuildOutputLayout? expectedOutputLayout = null;
            if (runnerKind == IpcBuildRunnerKind.BuildPipeline
                && !IpcBuildOutputLayoutResolver.TryResolve(expectedOutputPath, buildTarget, out expectedOutputLayout))
            {
                errorMessage = $"Build outputLayout could not be resolved for build target: {buildTarget}.";
                return false;
            }

            if (runnerKind == IpcBuildRunnerKind.BuildPipeline
                && (!string.Equals(request.OutputLayout!.Shape, expectedOutputLayout!.Shape, StringComparison.Ordinal)
                    || !PathEquals(request.OutputLayout.LocationPathName, expectedOutputLayout.LocationPathName)))
            {
                errorMessage = "Build outputLayout must match the expected uCLI build artifact layout.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        private static bool TryValidateUnityBuildProfileRequest (
            IpcBuildRunRequest request,
            IpcBuildRunnerKind runnerKind,
            out string? errorMessage)
        {
            if (request.BuildTarget != null
                || request.UnityBuildTarget != null
                || request.SceneSource != null
                || request.OutputLayout != null
                || request.Development
                || request.ScenePaths == null
                || request.ScenePaths.Count != 0)
            {
                errorMessage = "Build target, scene, option, and outputLayout values must not be specified for unityBuildProfile input.";
                return false;
            }

            if (runnerKind != IpcBuildRunnerKind.BuildPipeline)
            {
                errorMessage = "unityBuildProfile input requires buildPipeline runner.";
                return false;
            }

            if (request.UnityBuildProfile == null
                || !UnityAssetPathContract.IsNormalizedBuildProfileAssetPath(request.UnityBuildProfile.Path))
            {
                errorMessage = "Build unityBuildProfile.path must be a normalized project-relative asset path under Assets and must not reference a .meta file.";
                return false;
            }

            if (request.UnityBuildProfile.Digest != null
                || request.UnityBuildProfile.ApplyAudit != null)
            {
                errorMessage = "Build unityBuildProfile input may only specify path.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        private static bool HasValidRunnerContract (
            IpcBuildRunRequest request,
            IpcBuildRunnerKind runnerKind,
            out string? errorMessage)
        {
            if (runnerKind == IpcBuildRunnerKind.BuildPipeline)
            {
                if (!string.IsNullOrWhiteSpace(request.RunnerMethod)
                    || request.RunnerArguments.Count != 0
                    || request.RunnerEnvironmentVariables.Count != 0
                    || request.RunnerEnvironmentSecrets.Count != 0
                    || request.RunnerEnvironmentVariableValues.Count != 0
                    || request.RunnerEnvironmentSecretValues.Count != 0)
                {
                    errorMessage = "BuildPipeline runner must not include executeMethod invocation values.";
                    return false;
                }

                errorMessage = null;
                return true;
            }

            if (runnerKind != IpcBuildRunnerKind.ExecuteMethod)
            {
                errorMessage = $"Build runnerKind is invalid: {request.RunnerKind}.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.ProfilePath)
                || string.IsNullOrWhiteSpace(request.ProfileDigest)
                || string.IsNullOrWhiteSpace(request.RunnerMethod))
            {
                errorMessage = "executeMethod runner requires profilePath, profileDigest, and runnerMethod.";
                return false;
            }

            if (request.OutputLayout != null)
            {
                errorMessage = "executeMethod runner outputLayout must be null.";
                return false;
            }

            if (!HasMatchingEnvironmentValues(
                    request.RunnerEnvironmentVariables,
                    request.RunnerEnvironmentVariableValues)
                || !HasMatchingEnvironmentValues(
                    request.RunnerEnvironmentSecrets,
                    request.RunnerEnvironmentSecretValues))
            {
                errorMessage = "executeMethod runner environment names and values must match.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        private static bool HasMatchingEnvironmentValues (
            IReadOnlyList<string> environmentNames,
            IReadOnlyDictionary<string, string> environmentValues)
        {
            if (environmentNames.Count != environmentValues.Count)
            {
                return false;
            }

            for (var i = 0; i < environmentNames.Count; i++)
            {
                if (!environmentValues.ContainsKey(environmentNames[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasValidAllowedEditorModes (IReadOnlyList<string> allowedEditorModes)
        {
            if (allowedEditorModes == null || allowedEditorModes.Count == 0)
            {
                return false;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < allowedEditorModes.Count; i++)
            {
                var allowedEditorMode = allowedEditorModes[i];
                if (!ContractLiteralCodec.IsDefined<DaemonEditorMode>(allowedEditorMode)
                    || !seen.Add(allowedEditorMode))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsValidPathSegment (string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var span = value.AsSpan();
            for (var i = 0; i < span.Length; i++)
            {
                var character = span[i];
                if (character == '/'
                    || character == '\\'
                    || character == Path.DirectorySeparatorChar
                    || character == Path.AltDirectorySeparatorChar)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryResolveExpectedArtifactPaths (
            string runId,
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
                    projectIdentity.ProjectFingerprint,
                    runId);
                expectedOutputPath = UcliStoragePathResolver.ResolveBuildRunOutputDirectory(
                    storageRoot,
                    projectIdentity.ProjectFingerprint,
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

            if (!PathNormalizer.IsFullyQualifiedPath(actualPath))
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
            var tempPath = path + $".tmp.{Guid.NewGuid():N}";
            try
            {
                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                {
                    await JsonSerializer.SerializeAsync(
                            stream,
                            value,
                            IpcJsonSerializerOptions.Default,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                cancellationToken.ThrowIfCancellationRequested();
                FileSystemAccessBoundary.EnsureSecureFile(tempPath);
                EnsureWritableArtifactPath(path);
                ReplaceFile(tempPath, path);
                FileSystemAccessBoundary.EnsureSecureFile(path);
            }
            finally
            {
                DeleteTemporaryFileIfExists(tempPath);
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
            var tempPath = path + $".tmp.{Guid.NewGuid():N}";
            try
            {
                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                {
                    var byteCount = Utf8NoBomEncoding.GetByteCount(value);
                    if (byteCount > 0)
                    {
                        var buffer = ArrayPool<byte>.Shared.Rent(byteCount);
                        try
                        {
                            var bytesWritten = EncodeUtf8(value, buffer, byteCount);
                            await stream.WriteAsync(buffer, 0, bytesWritten, cancellationToken);
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(buffer);
                        }
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                FileSystemAccessBoundary.EnsureSecureFile(tempPath);
                EnsureWritableArtifactPath(path);
                ReplaceFile(tempPath, path);
                FileSystemAccessBoundary.EnsureSecureFile(path);
            }
            finally
            {
                DeleteTemporaryFileIfExists(tempPath);
            }
        }

        private static int EncodeUtf8 (
            string value,
            byte[] buffer,
            int byteCount)
        {
            return Utf8NoBomEncoding.GetBytes(value.AsSpan(), buffer.AsSpan(0, byteCount));
        }

        private static void EnsureWritableArtifactPath (string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                return;
            }

            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"Build artifact target must not be a reparse point: {path}");
            }

            if ((attributes & FileAttributes.Directory) != 0)
            {
                throw new IOException($"Build artifact target must not be a directory: {path}");
            }
        }

        private static void ReplaceFile (
            string temporaryPath,
            string path)
        {
            try
            {
                File.Replace(temporaryPath, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            catch (FileNotFoundException)
            {
                MoveOrReplaceWhenCreatedConcurrently(temporaryPath, path);
            }
            catch (IOException) when (!File.Exists(path))
            {
                MoveOrReplaceWhenCreatedConcurrently(temporaryPath, path);
            }
        }

        private static void MoveOrReplaceWhenCreatedConcurrently (
            string temporaryPath,
            string path)
        {
            try
            {
                File.Move(temporaryPath, path);
            }
            catch (IOException) when (File.Exists(path))
            {
                EnsureWritableArtifactPath(path);
                File.Replace(temporaryPath, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
        }

        private static void DeleteTemporaryFileIfExists (string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static bool IsRequestTimeout (
            IIpcRequestTimeoutScope? requestTimeoutScope,
            CancellationToken callerCancellationToken)
        {
            return requestTimeoutScope != null
                && requestTimeoutScope.IsTimeoutCancellationRequested
                && !callerCancellationToken.IsCancellationRequested;
        }
    }
}
