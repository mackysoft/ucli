using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Application.Shared.Foundation;

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

        var currentDirectory = AbsolutePath.Parse(Directory.GetCurrentDirectory());
        if (!AbsolutePath.TryResolve(currentDirectory, profilePath, out var normalizedProfilePath, out _))
        {
            return TestRunProfileJsonReadResult.Failure(ExecutionError.InvalidArgument(
                "profilePath is invalid: Path format is invalid."));
        }

        if (!File.Exists(normalizedProfilePath.Value))
        {
            return TestRunProfileJsonReadResult.Failure(ExecutionError.InvalidArgument(
                $"profilePath does not exist: {normalizedProfilePath.Value}"));
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var json = await File.ReadAllTextAsync(normalizedProfilePath.Value, cancellationToken).ConfigureAwait(false);
            return TestRunProfileJsonReadResult.Success(json);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return TestRunProfileJsonReadResult.Failure(ExecutionError.InternalError(
                $"Failed to read profile file: {normalizedProfilePath.Value}. {exception.Message}"));
        }
    }
}
