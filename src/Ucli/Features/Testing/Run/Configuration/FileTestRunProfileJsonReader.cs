using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.Features.Testing.Run.Configuration;

/// <summary> Reads test-run profile JSON text from the local filesystem. </summary>
internal sealed class FileTestRunProfileJsonReader : ITestRunProfileJsonReader
{
    /// <inheritdoc />
    public async ValueTask<TestRunProfileJsonReadResult> ReadTextAsync (
        string profilePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(profilePath))
        {
            return TestRunProfileJsonReadResult.Failure(ExecutionError.InvalidArgument("profilePath is empty."));
        }

        var normalizedProfilePath = profilePath;
        try
        {
            normalizedProfilePath = Path.GetFullPath(normalizedProfilePath);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return TestRunProfileJsonReadResult.Failure(ExecutionError.InvalidArgument(
                $"profilePath is invalid: {profilePath}."));
        }

        if (!File.Exists(normalizedProfilePath))
        {
            return TestRunProfileJsonReadResult.Failure(ExecutionError.InvalidArgument(
                $"profilePath does not exist: {normalizedProfilePath}"));
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var json = await File.ReadAllTextAsync(normalizedProfilePath, cancellationToken).ConfigureAwait(false);
            return TestRunProfileJsonReadResult.Success(json);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return TestRunProfileJsonReadResult.Failure(ExecutionError.InternalError(
                $"Failed to read profile file: {normalizedProfilePath}. {exception.Message}"));
        }
    }
}
