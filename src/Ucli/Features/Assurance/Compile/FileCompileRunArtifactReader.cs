using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Compile;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Assurance.Compile;

/// <summary> Reads compile run artifacts from local uCLI storage. </summary>
internal sealed class FileCompileRunArtifactReader : ICompileRunArtifactReader
{
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

        if (!File.Exists(summaryPath))
        {
            return CompileRunArtifactReadResult.Missing();
        }

        try
        {
            var json = await File.ReadAllTextAsync(summaryPath, cancellationToken).ConfigureAwait(false);
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
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return CompileRunArtifactReadResult.Failure(ExecutionError.InternalError(
                $"Failed to read compile summary artifact: {summaryPath}. {exception.Message}"));
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
}
