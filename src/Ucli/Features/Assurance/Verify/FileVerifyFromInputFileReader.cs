using MackySoft.Ucli.Application.Features.Assurance.Verify.Input;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Vocabulary;
using MackySoft.Ucli.Application.Shared.Execution;

namespace MackySoft.Ucli.Features.Assurance.Verify;

/// <summary> Reads <c>verify --from</c> public result JSON from repository-local files. </summary>
internal sealed class FileVerifyFromInputFileReader : IVerifyFromInputFileReader
{
    /// <inheritdoc />
    public async ValueTask<VerifyFromInputFileReadResult> ReadAsync (
        string fromPath,
        string repositoryRoot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!VerifyRepositoryFilePathResolver.TryResolve(
                repositoryRoot,
                fromPath,
                out var fullPath,
                out _,
                out _))
        {
            return Failure("The --from path must resolve to a file under the repository root.");
        }

        if (!File.Exists(fullPath))
        {
            return Failure($"The --from path does not exist: {fromPath}.");
        }

        try
        {
            var json = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
            return VerifyFromInputFileReadResult.Success(json);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failure($"Failed to read --from input. {exception.Message}");
        }
    }

    private static VerifyFromInputFileReadResult Failure (string message)
    {
        return VerifyFromInputFileReadResult.Failure(ApplicationFailure.InvalidInput(
            message,
            VerifyErrorCodes.VerifyInputPayloadInvalid));
    }
}
