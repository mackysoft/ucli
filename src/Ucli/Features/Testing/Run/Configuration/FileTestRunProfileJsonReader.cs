using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Features.Testing.Run.Configuration;

/// <summary> Reads test-run profile JSON text from the local filesystem. </summary>
internal sealed class FileTestRunProfileJsonReader : ITestRunProfileJsonReader
{
    /// <inheritdoc />
    public async ValueTask<TestRunProfileJsonReadResult> ReadTextAsync (
        AbsolutePath profilePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(profilePath.Value))
        {
            return TestRunProfileJsonReadResult.Failure(ExecutionError.InvalidArgument(
                $"profilePath does not exist: {profilePath.Value}"));
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var json = await File.ReadAllTextAsync(profilePath.Value, cancellationToken).ConfigureAwait(false);
            return TestRunProfileJsonReadResult.Success(json);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return TestRunProfileJsonReadResult.Failure(ExecutionError.InternalError(
                $"Failed to read profile file: {profilePath.Value}. {exception.Message}"));
        }
    }
}
