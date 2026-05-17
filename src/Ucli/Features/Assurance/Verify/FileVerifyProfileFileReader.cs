using MackySoft.Ucli.Application.Features.Assurance.Verify.Profiles;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Features.Assurance.Verify;

/// <summary> Reads verify profile JSON from repository-local files. </summary>
internal sealed class FileVerifyProfileFileReader : IVerifyProfileFileReader
{
    /// <inheritdoc />
    public async ValueTask<VerifyProfileFileReadResult> ReadAsync (
        string profilePath,
        string repositoryRoot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryResolveRepositoryFile(repositoryRoot, profilePath, out var fullPath, out var repositoryRelativePath))
        {
            return VerifyProfileFileReadResult.Failure(ExecutionError.InvalidArgument(
                "--profilePath must resolve to a file under the repository root."));
        }

        if (!File.Exists(fullPath))
        {
            return VerifyProfileFileReadResult.Failure(ExecutionError.InvalidArgument(
                $"--profilePath does not exist: {repositoryRelativePath}."));
        }

        try
        {
            var json = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
            return VerifyProfileFileReadResult.Success(json, repositoryRelativePath);
        }
        catch (IOException exception)
        {
            return VerifyProfileFileReadResult.Failure(ExecutionError.InternalError(
                $"Failed to read verify profile file. {exception.Message}"));
        }
        catch (UnauthorizedAccessException exception)
        {
            return VerifyProfileFileReadResult.Failure(ExecutionError.InternalError(
                $"Failed to read verify profile file. {exception.Message}"));
        }
    }

    private static bool TryResolveRepositoryFile (
        string repositoryRoot,
        string path,
        out string fullPath,
        out string repositoryRelativePath)
    {
        fullPath = string.Empty;
        repositoryRelativePath = string.Empty;

        if (string.IsNullOrWhiteSpace(repositoryRoot) || string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            var normalizedRepositoryRoot = Path.GetFullPath(repositoryRoot);
            var normalizedPath = Path.GetFullPath(path, normalizedRepositoryRoot);
            var rootWithSeparator = normalizedRepositoryRoot.EndsWith(Path.DirectorySeparatorChar)
                ? normalizedRepositoryRoot
                : string.Concat(normalizedRepositoryRoot, Path.DirectorySeparatorChar);
            if (!string.Equals(normalizedPath, normalizedRepositoryRoot, StringComparison.Ordinal)
                && !normalizedPath.StartsWith(rootWithSeparator, StringComparison.Ordinal))
            {
                return false;
            }

            fullPath = normalizedPath;
            repositoryRelativePath = Path.GetRelativePath(normalizedRepositoryRoot, normalizedPath).Replace('\\', '/');
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}
