using MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.Features.Assurance.Build;

/// <summary> Reads build profile JSON files from host storage. </summary>
internal sealed class FileBuildProfileFileReader : IBuildProfileFileReader
{
    private const long MaxBuildProfileBytes = 1024 * 1024;

    /// <inheritdoc />
    public async ValueTask<BuildProfileFileReadResult> ReadAsync (
        string profilePath,
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profilePath);
        ArgumentNullException.ThrowIfNull(unityProject);
        cancellationToken.ThrowIfCancellationRequested();

        string resolvedPath;
        try
        {
            resolvedPath = Path.IsPathFullyQualified(profilePath)
                ? Path.GetFullPath(profilePath)
                : Path.GetFullPath(profilePath, unityProject.RepositoryRoot);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return BuildProfileFileReadResult.Failure(ExecutionError.InvalidArgument(
                $"Build profile path is invalid. {exception.Message}",
                BuildErrorCodes.BuildProfileInvalid));
        }

        try
        {
            EnsureReadableProfilePath(resolvedPath);
            var json = await ReadAllTextBoundedAsync(resolvedPath, cancellationToken).ConfigureAwait(false);
            return BuildProfileFileReadResult.Success(json, resolvedPath);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return BuildProfileFileReadResult.Failure(ExecutionError.InvalidArgument(
                $"Build profile file was not found: {resolvedPath}.",
                BuildErrorCodes.BuildProfileInvalid));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return BuildProfileFileReadResult.Failure(ExecutionError.InvalidArgument(
                $"Failed to read build profile file: {resolvedPath}. {exception.Message}",
                BuildErrorCodes.BuildProfileInvalid));
        }
    }

    private static async ValueTask<string> ReadAllTextBoundedAsync (
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, useAsync: true);
        if (stream.Length > MaxBuildProfileBytes)
        {
            throw new IOException($"Build profile exceeded {MaxBuildProfileBytes} bytes: {path}");
        }

        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void EnsureReadableProfilePath (string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            throw new FileNotFoundException($"Build profile was not found: {path}", path);
        }

        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Build profile source must not be a reparse point: {path}");
        }

        if ((attributes & FileAttributes.Directory) != 0)
        {
            throw new IOException($"Build profile source must not be a directory: {path}");
        }

        var fileInfo = new FileInfo(path);
        if (fileInfo.Length > MaxBuildProfileBytes)
        {
            throw new IOException($"Build profile exceeded {MaxBuildProfileBytes} bytes: {path}");
        }
    }
}
