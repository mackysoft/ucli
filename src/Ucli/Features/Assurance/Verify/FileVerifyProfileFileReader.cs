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

        if (!VerifyRepositoryFilePathResolver.TryResolve(
                repositoryRoot,
                profilePath,
                out var fullPath,
                out var repositoryRelativePath,
                out _))
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
}
