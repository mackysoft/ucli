using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;

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
        ArgumentNullException.ThrowIfNull(unityProject);
        cancellationToken.ThrowIfCancellationRequested();

        if (!AbsolutePath.TryResolve(
                unityProject.RepositoryRoot,
                profilePath,
                out var resolvedPath,
                out var failure))
        {
            return BuildProfileFileReadResult.Failure(ExecutionError.InvalidArgument(
                $"Build profile path is invalid. {failure.Message}",
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
                $"Build profile file was not found: {resolvedPath.Value}.",
                BuildErrorCodes.BuildProfileInvalid));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return BuildProfileFileReadResult.Failure(ExecutionError.InvalidArgument(
                $"Failed to read build profile file: {resolvedPath.Value}. {exception.Message}",
                BuildErrorCodes.BuildProfileInvalid));
        }
    }

    private static async ValueTask<string> ReadAllTextBoundedAsync (
        AbsolutePath path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path.Value,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            8192,
            useAsync: true);
        if (stream.Length > MaxBuildProfileBytes)
        {
            throw new IOException($"Build profile exceeded {MaxBuildProfileBytes} bytes: {path.Value}");
        }

        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void EnsureReadableProfilePath (AbsolutePath path)
    {
        if (!File.Exists(path.Value) && !Directory.Exists(path.Value))
        {
            throw new FileNotFoundException($"Build profile was not found: {path.Value}", path.Value);
        }

        var attributes = File.GetAttributes(path.Value);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Build profile source must not be a reparse point: {path.Value}");
        }

        if ((attributes & FileAttributes.Directory) != 0)
        {
            throw new IOException($"Build profile source must not be a directory: {path.Value}");
        }

        var fileInfo = new FileInfo(path.Value);
        if (fileInfo.Length > MaxBuildProfileBytes)
        {
            throw new IOException($"Build profile exceeded {MaxBuildProfileBytes} bytes: {path.Value}");
        }
    }
}
