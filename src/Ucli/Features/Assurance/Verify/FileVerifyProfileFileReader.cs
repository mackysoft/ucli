using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Profiles;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.Features.Assurance.Verify;

/// <summary> Reads verify profile JSON from repository-local files. </summary>
internal sealed class FileVerifyProfileFileReader : IVerifyProfileFileReader
{
    /// <inheritdoc />
    public async ValueTask<VerifyProfileFileReadResult> ReadAsync (
        string profilePath,
        AbsolutePath repositoryRoot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!VerifyRepositoryFilePathResolver.TryResolve(
                repositoryRoot,
                profilePath,
                out var resolvedPath,
                out _))
        {
            return VerifyProfileFileReadResult.Failure(ExecutionError.InvalidArgument(
                "--profilePath must resolve to a file under the repository root."));
        }

        if (!File.Exists(resolvedPath!.Target.Value))
        {
            return VerifyProfileFileReadResult.Failure(ExecutionError.InvalidArgument(
                $"--profilePath does not exist: {resolvedPath.RelativePath}."));
        }

        if (!UcliPortablePathAdapter.TryFormat(
                resolvedPath.RelativePath,
                out var portableRepositoryRelativePath))
        {
            return VerifyProfileFileReadResult.Failure(ExecutionError.InvalidArgument(
                "--profilePath cannot be represented as a portable repository-relative path."));
        }

        try
        {
            var json = await File.ReadAllTextAsync(
                    resolvedPath.Target.Value,
                    cancellationToken)
                .ConfigureAwait(false);
            return VerifyProfileFileReadResult.Success(json, portableRepositoryRelativePath);
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
