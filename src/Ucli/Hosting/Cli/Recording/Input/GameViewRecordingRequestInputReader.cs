using System.Text;
using System.Text.Json;
using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Hosting.Cli.Recording.Input;

/// <summary>Reads a bounded UTF-8 recording request from a path or redirected standard input.</summary>
internal sealed class GameViewRecordingRequestInputReader : IGameViewRecordingRequestInputReader
{
    private const int MaximumRequestBytes = 64 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public async ValueTask<GameViewRecordingRequestInputReadResult> ReadAsync (
        AbsolutePath? requestPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var hasRequestPath = requestPath is not null;
        var hasRedirectedStandardInput = Console.IsInputRedirected;
        if (hasRequestPath == hasRedirectedStandardInput)
        {
            return Invalid(
                "Provide a recording request through exactly one of --requestPath or redirected standard input.");
        }

        string json;
        try
        {
            json = requestPath is { } path
                ? await ReadUtf8FileAsync(path, cancellationToken).ConfigureAwait(false)
                : await ReadStandardInputAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or DecoderFallbackException)
        {
            return Invalid($"Recording request could not be read as UTF-8 JSON. {exception.Message}");
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return Invalid("Recording request JSON must not be empty.");
        }

        int byteCount;
        try
        {
            byteCount = StrictUtf8.GetByteCount(json);
        }
        catch (EncoderFallbackException exception)
        {
            return Invalid($"Recording request could not be encoded as UTF-8 JSON. {exception.Message}");
        }

        if (byteCount > MaximumRequestBytes)
        {
            return Invalid($"Recording request JSON exceeds {MaximumRequestBytes} UTF-8 bytes.");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Invalid("Recording request JSON root must be an object.");
            }
        }
        catch (JsonException exception)
        {
            return Invalid($"Recording request JSON is invalid. {exception.Message}");
        }

        return GameViewRecordingRequestInputReadResult.Success(json);
    }

    private static async Task<string> ReadUtf8FileAsync (
        AbsolutePath path,
        CancellationToken cancellationToken)
    {
        var bytes = await FileUtilities.ReadBytesOrNullWithinLimitAsync(
                path,
                MaximumRequestBytes,
                cancellationToken)
            .ConfigureAwait(false);
        if (!bytes.HasValue)
        {
            throw new FileNotFoundException(
                "Recording request file was not found.",
                path.Value);
        }

        return StrictUtf8.GetString(bytes.Value.Span);
    }

    private static async Task<string> ReadStandardInputAsync (CancellationToken cancellationToken)
    {
        var builder = new StringBuilder(capacity: 4096);
        var buffer = new char[4096];
        while (true)
        {
            var read = await Console.In
                .ReadAsync(buffer.AsMemory(), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                return builder.ToString();
            }

            builder.Append(buffer, 0, read);
            if (builder.Length > MaximumRequestBytes)
            {
                throw new IOException(
                    $"Recording request standard input exceeds {MaximumRequestBytes} UTF-8 bytes.");
            }
        }
    }

    private static GameViewRecordingRequestInputReadResult Invalid (string message) =>
        GameViewRecordingRequestInputReadResult.Failure(
            ExecutionError.InvalidArgument(
                message,
                UcliCoreErrorCodes.InvalidArgument));
}
