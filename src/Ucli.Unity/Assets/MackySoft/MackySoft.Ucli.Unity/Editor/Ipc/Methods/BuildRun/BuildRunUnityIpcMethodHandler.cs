using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Unity.Build;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles <c>build.run</c> IPC method requests. </summary>
    internal sealed class BuildRunUnityIpcMethodHandler : IUnityIpcMethodHandler
    {
        private readonly UnityBuildPreconditionProbe preconditionProbe;

        private readonly IUnityBuildPipelineRunner buildPipelineRunner;

        private readonly IEditorLogRangeExporter editorLogRangeExporter;

        private readonly IIpcRequestTimeoutScopeFactory timeoutScopeFactory;

        /// <summary> Initializes a new instance of the <see cref="BuildRunUnityIpcMethodHandler" /> class. </summary>
        public BuildRunUnityIpcMethodHandler (
            UnityBuildPreconditionProbe preconditionProbe,
            IUnityBuildPipelineRunner buildPipelineRunner,
            IEditorLogRangeExporter editorLogRangeExporter,
            IIpcRequestTimeoutScopeFactory timeoutScopeFactory)
        {
            this.preconditionProbe = preconditionProbe ?? throw new ArgumentNullException(nameof(preconditionProbe));
            this.buildPipelineRunner = buildPipelineRunner ?? throw new ArgumentNullException(nameof(buildPipelineRunner));
            this.editorLogRangeExporter = editorLogRangeExporter ?? throw new ArgumentNullException(nameof(editorLogRangeExporter));
            this.timeoutScopeFactory = timeoutScopeFactory ?? throw new ArgumentNullException(nameof(timeoutScopeFactory));
        }

        /// <inheritdoc />
        public string Method => IpcMethodNames.BuildRun;

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

            if (!TryValidateRequest(buildRunRequest!, out var validationErrorMessage))
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InvalidArgument,
                    validationErrorMessage!,
                    null);
            }

            IIpcRequestTimeoutScope? requestTimeoutScope = null;
            UnityBuildPreconditionProbeResult? precondition = null;
            try
            {
                requestTimeoutScope = timeoutScopeFactory.CreateLinked(
                    buildRunRequest!.TimeoutMilliseconds,
                    cancellationToken);
                var executionCancellationToken = requestTimeoutScope.Token;
                precondition = await preconditionProbe.ProbeBeforeBuildAsync(
                    new UnityBuildPreconditionInput(
                        TargetStableName: buildRunRequest.TargetStableName,
                        UnityBuildTarget: buildRunRequest.UnityBuildTarget,
                        SceneSource: buildRunRequest.SceneSource,
                        ScenePaths: buildRunRequest.ScenePaths,
                        Development: buildRunRequest.Development),
                    executionCancellationToken);
                if (!precondition.IsSuccess)
                {
                    return CreatePreconditionErrorResponse(request, precondition);
                }

                var logSourcePath = Application.consoleLogPath;
                var logStartOffset = GetLogLength(logSourcePath);
                var startedAtUtc = DateTimeOffset.UtcNow;
                var buildOptions = UnityBuildPlayerOptionsFactory.Create(buildRunRequest, precondition.ResolvedInput!);
                var report = buildPipelineRunner.Run(buildOptions);
                executionCancellationToken.ThrowIfCancellationRequested();
                var completedAtUtc = DateTimeOffset.UtcNow;
                var lifecycleAfter = preconditionProbe.CaptureAfterBuild();
                var logEndOffset = GetLogLength(logSourcePath);
                if (logEndOffset < logStartOffset)
                {
                    logStartOffset = 0;
                }

                if (report == null)
                {
                    return CreateErrorResponse(
                        request,
                        BuildErrorCodes.BuildReportMissing,
                        "Unity BuildPipeline did not return a BuildReport.",
                        precondition);
                }

                var normalizedReport = UnityBuildReportNormalizer.Normalize(report);
                WriteJsonAtomically(buildRunRequest.BuildReportPath, normalizedReport);
                await ExportBuildLogAsync(
                        logSourcePath,
                        buildRunRequest.BuildLogPath,
                        logStartOffset,
                        logEndOffset,
                        executionCancellationToken)
                    .ConfigureAwait(false);
                var completionReason = UnityBuildReportNormalizer.ToCompletionReason(normalizedReport.Result);
                var logs = new IpcBuildLogSummary(
                    EntryCount: CountLogEntries(buildRunRequest.BuildLogPath),
                    ErrorCount: normalizedReport.ErrorCount,
                    WarningCount: normalizedReport.WarningCount,
                    CompletionReason: ContractLiteralCodec.ToValue(completionReason),
                    Window: new IpcBuildLogWindow(startedAtUtc, completedAtUtc));
                var response = new IpcBuildRunResponse(
                    RunId: buildRunRequest.RunId,
                    ProjectFingerprint: precondition.Project.ProjectFingerprint,
                    LifecycleBefore: precondition.LifecycleBefore,
                    LifecycleAfter: lifecycleAfter,
                    DirtyState: precondition.DirtyState!,
                    Input: precondition.InputProbe!,
                    Report: normalizedReport,
                    Logs: logs);
                return UnityIpcResponseFactory.CreateSuccessResponse(request, response);
            }
            catch (OperationCanceledException) when (IsRequestTimeout(requestTimeoutScope, cancellationToken))
            {
                return CreateErrorResponse(
                    request,
                    IpcTransportErrorCodes.IpcTimeout,
                    $"Unity build run timed out after {buildRunRequest!.TimeoutMilliseconds!.Value} milliseconds.",
                    precondition);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ArgumentException exception)
            {
                return CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InvalidArgument,
                    exception.Message,
                    precondition);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
            {
                return CreateErrorResponse(
                    request,
                    BuildErrorCodes.BuildArtifactWriteFailed,
                    $"Unity build artifact write failed. {exception.Message}",
                    precondition);
            }
            catch (Exception exception)
            {
                return CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InternalError,
                    $"Unity build run failed. {exception.Message}",
                    precondition);
            }
            finally
            {
                requestTimeoutScope?.Dispose();
            }
        }

        private static IpcResponse CreatePreconditionErrorResponse (
            IpcRequest request,
            UnityBuildPreconditionProbeResult precondition)
        {
            var error = precondition.Error!;
            return UnityIpcResponseFactory.CreateErrorResponse(
                request,
                error.Code,
                error.Message,
                error.OpId,
                CreateErrorPayload(precondition));
        }

        private static IpcResponse CreateErrorResponse (
            IpcRequest request,
            UcliCode code,
            string message,
            UnityBuildPreconditionProbeResult? precondition)
        {
            if (precondition == null)
            {
                return UnityIpcResponseFactory.CreateErrorResponse(request, code, message, null);
            }

            return UnityIpcResponseFactory.CreateErrorResponse(
                request,
                code,
                message,
                null,
                CreateErrorPayload(precondition));
        }

        private static IpcBuildRunErrorPayload CreateErrorPayload (UnityBuildPreconditionProbeResult precondition)
        {
            return new IpcBuildRunErrorPayload(
                Project: precondition.Project,
                LifecycleBefore: precondition.LifecycleBefore,
                DirtyState: precondition.DirtyState,
                Input: precondition.InputProbe);
        }

        private async Task ExportBuildLogAsync (
            string sourcePath,
            string destinationPath,
            long startOffset,
            long endOffset,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                WriteTextAtomically(destinationPath, string.Empty);
                return;
            }

            await editorLogRangeExporter.ExportRangeAsync(
                    sourcePath,
                    destinationPath,
                    startOffset,
                    endOffset,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        private static bool TryValidateRequest (
            IpcBuildRunRequest request,
            out string? errorMessage)
        {
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

            if (string.IsNullOrWhiteSpace(request.TargetStableName)
                || string.IsNullOrWhiteSpace(request.UnityBuildTarget)
                || string.IsNullOrWhiteSpace(request.SceneSource))
            {
                errorMessage = "Build target and scene source values must not be empty.";
                return false;
            }

            if (!IsFullyQualifiedPath(request.OutputPath)
                || !IsFullyQualifiedPath(request.BuildReportPath)
                || !IsFullyQualifiedPath(request.BuildLogPath))
            {
                errorMessage = "Build output and artifact paths must be absolute paths.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        private static bool IsValidPathSegment (string value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.IndexOfAny(new[] { '/', '\\', Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }) < 0;
        }

        private static bool IsFullyQualifiedPath (string value)
        {
            return !string.IsNullOrWhiteSpace(value) && Path.IsPathRooted(value);
        }

        private static long GetLogLength (string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return 0;
            }

            return new FileInfo(path).Length;
        }

        private static int CountLogEntries (string path)
        {
            if (!File.Exists(path))
            {
                return 0;
            }

            var count = 0;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var previousWasNewLine = false;
                while (true)
                {
                    var value = stream.ReadByte();
                    if (value < 0)
                    {
                        break;
                    }

                    if (value == '\n')
                    {
                        count++;
                        previousWasNewLine = true;
                    }
                    else
                    {
                        previousWasNewLine = false;
                    }
                }

                if (stream.Length > 0 && !previousWasNewLine)
                {
                    count++;
                }
            }

            return count;
        }

        private static void WriteJsonAtomically<T> (
            string path,
            T value)
        {
            WriteTextAtomically(path, JsonSerializer.Serialize(value, IpcJsonSerializerOptions.Default));
        }

        private static void WriteTextAtomically (
            string path,
            string value)
        {
            var directoryPath = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new InvalidOperationException($"Artifact directory path could not be resolved: {path}");
            }

            Directory.CreateDirectory(directoryPath);
            var tempPath = path + $".tmp.{Guid.NewGuid():N}";
            try
            {
                File.WriteAllText(tempPath, value, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                if (File.Exists(path))
                {
                    File.Replace(tempPath, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(tempPath, path);
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
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
