using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Build;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles <c>build.run</c> IPC method requests. </summary>
    internal sealed class BuildRunUnityIpcMethodHandler : IUnityIpcMethodHandler
    {
        private static readonly UTF8Encoding Utf8NoBomEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private readonly UnityBuildPreconditionProbe preconditionProbe;

        private readonly IUnityBuildPipelineRunner buildPipelineRunner;

        private readonly IEditorLogRangeExporter editorLogRangeExporter;

        private readonly IpcProjectIdentity projectIdentity;

        private readonly IIpcRequestTimeoutScopeFactory timeoutScopeFactory;

        /// <summary> Initializes a new instance of the <see cref="BuildRunUnityIpcMethodHandler" /> class. </summary>
        public BuildRunUnityIpcMethodHandler (
            UnityBuildPreconditionProbe preconditionProbe,
            IUnityBuildPipelineRunner buildPipelineRunner,
            IEditorLogRangeExporter editorLogRangeExporter,
            IpcProjectIdentity projectIdentity,
            IIpcRequestTimeoutScopeFactory timeoutScopeFactory)
        {
            this.preconditionProbe = preconditionProbe ?? throw new ArgumentNullException(nameof(preconditionProbe));
            this.buildPipelineRunner = buildPipelineRunner ?? throw new ArgumentNullException(nameof(buildPipelineRunner));
            this.editorLogRangeExporter = editorLogRangeExporter ?? throw new ArgumentNullException(nameof(editorLogRangeExporter));
            this.projectIdentity = projectIdentity ?? throw new ArgumentNullException(nameof(projectIdentity));
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

            if (!TryValidateRequest(buildRunRequest!, projectIdentity, out var validationErrorMessage))
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

                FileSystemAccessBoundary.EnsureSecureDirectory(buildRunRequest.OutputPath);
                var logSourcePath = Application.consoleLogPath;
                var logStartOffset = GetLogLength(logSourcePath);
                var startedAtUtc = DateTimeOffset.UtcNow;
                var buildOptions = UnityBuildPlayerOptionsFactory.Create(buildRunRequest, precondition.ResolvedInput!);
                executionCancellationToken.ThrowIfCancellationRequested();
                var normalizedReport = buildPipelineRunner.Run(buildOptions);
                executionCancellationToken.ThrowIfCancellationRequested();
                FileSystemAccessBoundary.EnsureSecureDirectory(buildRunRequest.OutputPath);
                var completedAtUtc = DateTimeOffset.UtcNow;
                var lifecycleAfter = preconditionProbe.CaptureAfterBuild();
                var logEndOffset = GetLogLength(logSourcePath);
                if (logEndOffset < logStartOffset)
                {
                    logStartOffset = 0;
                }

                if (normalizedReport == null)
                {
                    return CreateErrorResponse(
                        request,
                        BuildErrorCodes.BuildReportMissing,
                        "Unity BuildPipeline did not return a BuildReport.",
                        precondition);
                }

                await WriteJsonAtomicallyAsync(
                        buildRunRequest.BuildReportPath,
                        normalizedReport,
                        executionCancellationToken)
                    .ConfigureAwait(false);
                var logSummaryCounts = await ExportBuildLogAsync(
                        logSourcePath,
                        buildRunRequest.BuildLogPath,
                        logStartOffset,
                        logEndOffset,
                        executionCancellationToken)
                    .ConfigureAwait(false);
                var completionReason = UnityBuildReportNormalizer.ToCompletionReason(normalizedReport.Result);
                var logs = new IpcBuildLogSummary(
                    EntryCount: logSummaryCounts.EntryCount,
                    ErrorCount: logSummaryCounts.ErrorCount,
                    WarningCount: logSummaryCounts.WarningCount,
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

        private async Task<EditorLogRangeExportResult> ExportBuildLogAsync (
            string sourcePath,
            string destinationPath,
            long startOffset,
            long endOffset,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                await WriteTextAtomicallyAsync(destinationPath, string.Empty, cancellationToken).ConfigureAwait(false);
                return new EditorLogRangeExportResult(0, 0, 0);
            }

            return await editorLogRangeExporter.ExportRangeAsync(
                    sourcePath,
                    destinationPath,
                    startOffset,
                    endOffset,
                    cancellationToken)
                .ConfigureAwait(false);
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

            if (string.IsNullOrWhiteSpace(request.TargetStableName)
                || string.IsNullOrWhiteSpace(request.UnityBuildTarget)
                || string.IsNullOrWhiteSpace(request.SceneSource))
            {
                errorMessage = "Build target and scene source values must not be empty.";
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

            errorMessage = null;
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
                expectedOutputPath = Path.GetFullPath(Path.Combine(
                    expectedArtifactsDirectory,
                    UcliStoragePathNames.BuildOutputDirectoryName));
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

            if (!IsFullyQualifiedPath(actualPath))
            {
                return false;
            }

            try
            {
                return string.Equals(
                    Path.GetFullPath(actualPath),
                    expectedPath,
                    Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
            }
            catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
            {
                return false;
            }
        }

        private static bool IsFullyQualifiedPath (string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
            {
                return false;
            }

            if (Path.DirectorySeparatorChar == '\\')
            {
                return IsWindowsDriveAbsolutePath(path) || IsWindowsUncAbsolutePath(path);
            }

            return path[0] == Path.DirectorySeparatorChar;
        }

        private static bool IsWindowsDriveAbsolutePath (string path)
        {
            return path.Length >= 2
                && char.IsLetter(path[0])
                && path[1] == ':'
                && path.Length >= 3
                && IsDirectorySeparator(path[2]);
        }

        private static bool IsWindowsUncAbsolutePath (string path)
        {
            return path.Length >= 5
                && IsDirectorySeparator(path[0])
                && IsDirectorySeparator(path[1])
                && !IsDirectorySeparator(path[2]);
        }

        private static bool IsDirectorySeparator (char value)
        {
            return value == '\\' || value == '/';
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
