using MackySoft.Ucli.Application.Features.Programs.Resolution;

namespace MackySoft.Ucli.Features.Programs.Resolution;

/// <summary> Reads Program definition documents from the local file system. </summary>
internal sealed class FileProgramDefinitionFileReader : IProgramDefinitionFileReader
{
    public async ValueTask<ProgramDefinitionFileReadResult> ReadAsync (string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return ProgramDefinitionFileReadResult.Success(await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return ProgramDefinitionFileReadResult.Failure($"Failed to read Program definition file '{path}'. {exception.Message}");
        }
    }
}
