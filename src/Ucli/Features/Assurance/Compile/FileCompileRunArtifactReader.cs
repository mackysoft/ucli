using System.Text;
using System.Text.Json;
using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Artifacts;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Assurance.Compile;

/// <summary> Reads and writes compile run artifacts from local uCLI storage. </summary>
internal sealed class FileCompileRunArtifactReader : ICompileRunArtifactStore
{
    private const long MaxCompileArtifactBytes = 1024 * 1024;

    /// <inheritdoc />
    public async ValueTask<CompileRunArtifactReadResult> ReadSummaryAsync (
        ResolvedUnityProjectContext unityProject,
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        cancellationToken.ThrowIfCancellationRequested();
        if (runId == Guid.Empty)
        {
            return CompileRunArtifactReadResult.Failure(
                ExecutionError.InvalidArgument("Run id must not be empty."));
        }

        var summaryPath = ResolveSummaryAbsolutePath(unityProject, runId);

        if (!File.Exists(summaryPath.Value) && !Directory.Exists(summaryPath.Value))
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
        catch (Exception exception) when (exception is JsonException or ArgumentException)
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
        Guid runId,
        IpcCompileSummary summary,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(summary);
        cancellationToken.ThrowIfCancellationRequested();

        if (summary.RunId != runId)
        {
            return ValueTask.FromResult<ExecutionError?>(ExecutionError.InvalidArgument(
                "Compile summary run identifier must match its artifact path run identifier."));
        }

        var runDirectory = ResolveRunDirectory(unityProject, runId);
        var diagnosticsPath = ResolveDiagnosticsAbsolutePath(runDirectory);
        var summaryPath = ResolveSummaryAbsolutePath(runDirectory);

        try
        {
            FileSystemAccessBoundary.EnsureSecureDirectory(runDirectory);
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
    public AbsolutePath ResolveSummaryPath (
        ResolvedUnityProjectContext unityProject,
        Guid runId)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        return ResolveSummaryAbsolutePath(unityProject, runId);
    }

    /// <inheritdoc />
    public AbsolutePath ResolveDiagnosticsPath (
        ResolvedUnityProjectContext unityProject,
        Guid runId)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        return ResolveDiagnosticsAbsolutePath(ResolveRunDirectory(unityProject, runId));
    }

    private static AbsolutePath ResolveRunDirectory (
        ResolvedUnityProjectContext unityProject,
        Guid runId)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        return UcliStoragePathResolver.ResolveCompileRunArtifactsDirectory(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint,
            runId);
    }

    private static AbsolutePath ResolveSummaryAbsolutePath (
        ResolvedUnityProjectContext unityProject,
        Guid runId)
    {
        return ResolveSummaryAbsolutePath(ResolveRunDirectory(unityProject, runId));
    }

    private static AbsolutePath ResolveSummaryAbsolutePath (AbsolutePath runDirectory)
    {
        return ContainedPath.Create(
            runDirectory,
            RootRelativePath.Parse(UcliStoragePathNames.CompileSummaryFileName)).Target;
    }

    private static AbsolutePath ResolveDiagnosticsAbsolutePath (AbsolutePath runDirectory)
    {
        return ContainedPath.Create(
            runDirectory,
            RootRelativePath.Parse(UcliStoragePathNames.CompileDiagnosticsFileName)).Target;
    }

    private static void WriteJsonAtomically<T> (
        AbsolutePath path,
        T value)
    {
        FileUtilities.WriteAllTextAtomically(
            path,
            JsonSerializer.Serialize(value, IpcJsonSerializerOptions.Default));
        FileSystemAccessBoundary.EnsureSecureFile(path);
    }

    private static async ValueTask<string> ReadAllTextBoundedAsync (
        AbsolutePath path,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        EnsureReadableArtifactPath(path, maxBytes);
        using (var stream = new FileStream(
                   path.Value,
                   FileMode.Open,
                   FileAccess.Read,
                   FileShare.Read,
                   8192,
                   useAsync: true))
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
        AbsolutePath path,
        long maxBytes)
    {
        if (!File.Exists(path.Value) && !Directory.Exists(path.Value))
        {
            throw new FileNotFoundException($"Compile artifact was not found: {path}", path.Value);
        }

        var attributes = File.GetAttributes(path.Value);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Compile artifact source must not be a reparse point: {path}");
        }

        if ((attributes & FileAttributes.Directory) != 0)
        {
            throw new IOException($"Compile artifact source must not be a directory: {path}");
        }

        var fileInfo = new FileInfo(path.Value);
        if (fileInfo.Length > maxBytes)
        {
            throw new IOException($"Compile artifact exceeded {maxBytes} bytes: {path}");
        }
    }

    private sealed record CompileDiagnosticsArtifact (
        Guid RunId,
        int ErrorCount,
        int WarningCount,
        IpcPrimaryDiagnostic? PrimaryDiagnostic);
}
