using System.Text;
using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Artifacts;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Assurance.Compile;

/// <summary> Reads and writes compile run artifacts from local uCLI storage. </summary>
internal sealed class FileCompileRunArtifactReader : ICompileRunArtifactStore
{
    private const long MaxCompileArtifactBytes = 1024 * 1024;

    /// <inheritdoc />
    public async ValueTask<CompileRunArtifactReadResult> ReadSummaryAsync (
        ResolvedUnityProjectContext unityProject,
        string runId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        cancellationToken.ThrowIfCancellationRequested();

        string summaryPath;
        try
        {
            summaryPath = ResolveSummaryPath(unityProject, runId);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return CompileRunArtifactReadResult.Failure(ExecutionError.InvalidArgument(
                $"Compile summary path is invalid. {exception.Message}"));
        }

        if (!File.Exists(summaryPath) && !Directory.Exists(summaryPath))
        {
            return CompileRunArtifactReadResult.Missing();
        }

        try
        {
            var json = await ReadAllTextBoundedAsync(
                    summaryPath,
                    MaxCompileArtifactBytes,
                    cancellationToken)
                .ConfigureAwait(false);
            var summary = JsonSerializer.Deserialize<IpcCompileSummary>(json, IpcJsonSerializerOptions.Default);
            return summary is null
                ? CompileRunArtifactReadResult.Failure(ExecutionError.InternalError($"Compile summary artifact is empty: {summaryPath}."))
                : CompileRunArtifactReadResult.Success(summary);
        }
        catch (JsonException exception)
        {
            return CompileRunArtifactReadResult.Failure(ExecutionError.InternalError(
                $"Compile summary artifact is invalid: {summaryPath}. {exception.Message}"));
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return CompileRunArtifactReadResult.Missing();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return CompileRunArtifactReadResult.Failure(ExecutionError.InternalError(
                $"Failed to read compile summary artifact: {summaryPath}. {exception.Message}"));
        }
    }

    /// <inheritdoc />
    public ValueTask<ExecutionError?> WriteArtifactsAsync (
        ResolvedUnityProjectContext unityProject,
        string runId,
        IpcCompileSummary summary,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentNullException.ThrowIfNull(summary);
        cancellationToken.ThrowIfCancellationRequested();

        string diagnosticsPath;
        string summaryPath;
        try
        {
            diagnosticsPath = ResolveDiagnosticsPath(unityProject, runId);
            summaryPath = ResolveSummaryPath(unityProject, runId);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return ValueTask.FromResult<ExecutionError?>(ExecutionError.InvalidArgument(
                $"Compile artifact path is invalid. {exception.Message}"));
        }

        try
        {
            WriteJsonAtomically(
                diagnosticsPath,
                new CompileDiagnosticsArtifact(
                    summary.RunId,
                    summary.ScriptCompilation.Diagnostics.ErrorCount,
                    summary.ScriptCompilation.Diagnostics.WarningCount,
                    summary.ScriptCompilation.Diagnostics.PrimaryDiagnostic));
            cancellationToken.ThrowIfCancellationRequested();

            WriteJsonAtomically(summaryPath, summary);
            return ValueTask.FromResult<ExecutionError?>(null);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return ValueTask.FromResult<ExecutionError?>(ExecutionError.InvalidArgument(
                $"Compile artifact path is invalid. {exception.Message}"));
        }
        catch (JsonException exception)
        {
            return ValueTask.FromResult<ExecutionError?>(ExecutionError.InternalError(
                $"Failed to write compile artifacts. {exception.Message}"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return ValueTask.FromResult<ExecutionError?>(ExecutionError.InternalError(
                $"Failed to write compile artifacts. {exception.Message}"));
        }
    }

    /// <inheritdoc />
    public string ResolveSummaryPath (
        ResolvedUnityProjectContext unityProject,
        string runId)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        return Path.Combine(
            ResolveRunDirectory(unityProject, runId),
            UcliStoragePathNames.CompileSummaryFileName);
    }

    /// <inheritdoc />
    public string ResolveDiagnosticsPath (
        ResolvedUnityProjectContext unityProject,
        string runId)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        return Path.Combine(
            ResolveRunDirectory(unityProject, runId),
            UcliStoragePathNames.CompileDiagnosticsFileName);
    }

    private static string ResolveRunDirectory (
        ResolvedUnityProjectContext unityProject,
        string runId)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        return UcliStoragePathResolver.ResolveCompileRunArtifactsDirectory(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint,
            runId);
    }

    private static void WriteJsonAtomically<T> (
        string path,
        T value)
    {
        var directoryPath = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new InvalidOperationException($"Artifact directory path could not be resolved: {path}");
        }

        FileSystemAccessBoundary.EnsureSecureDirectory(directoryPath);
        var tempPath = path + $".tmp.{Guid.NewGuid():N}";

        try
        {
            EnsureWritableArtifactPath(tempPath);
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                writer.Write(JsonSerializer.Serialize(value, IpcJsonSerializerOptions.Default));
            }

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

    private static async ValueTask<string> ReadAllTextBoundedAsync (
        string path,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        EnsureReadableArtifactPath(path, maxBytes);
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, useAsync: true))
        using (var memoryStream = new MemoryStream())
        {
            if (stream.Length > maxBytes)
            {
                throw new IOException($"Compile artifact exceeded {maxBytes} bytes: {path}");
            }

            var buffer = new byte[8192];
            long totalBytesRead = 0;
            while (true)
            {
                var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    break;
                }

                totalBytesRead += bytesRead;
                if (totalBytesRead > maxBytes)
                {
                    throw new IOException($"Compile artifact exceeded {maxBytes} bytes: {path}");
                }

                memoryStream.Write(buffer, 0, bytesRead);
            }

            return Encoding.UTF8.GetString(memoryStream.ToArray());
        }
    }

    private static void EnsureReadableArtifactPath (
        string path,
        long maxBytes)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            throw new FileNotFoundException($"Compile artifact was not found: {path}", path);
        }

        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Compile artifact source must not be a reparse point: {path}");
        }

        if ((attributes & FileAttributes.Directory) != 0)
        {
            throw new IOException($"Compile artifact source must not be a directory: {path}");
        }

        var fileInfo = new FileInfo(path);
        if (fileInfo.Length > maxBytes)
        {
            throw new IOException($"Compile artifact exceeded {maxBytes} bytes: {path}");
        }
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
            throw new IOException($"Compile artifact target must not be a reparse point: {path}");
        }

        if ((attributes & FileAttributes.Directory) != 0)
        {
            throw new IOException($"Compile artifact target must not be a directory: {path}");
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

    private sealed record CompileDiagnosticsArtifact (
        string RunId,
        int ErrorCount,
        int WarningCount,
        IpcPrimaryDiagnostic? PrimaryDiagnostic);
}
