using System.Text.Json;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Hosting.Cli.Requests.Input;

/// <summary> Reads JSON request input from one source and validates basic input constraints. </summary>
internal sealed class RequestInputReader : IRequestInputReader
{
    private readonly Func<bool> isStandardInputRedirected;
    private readonly Func<CancellationToken, Task<string>> readStandardInputAsync;

    /// <summary> Initializes a new instance of the <see cref="RequestInputReader" /> class. </summary>
    public RequestInputReader ()
        : this(
            isStandardInputRedirected: static () => Console.IsInputRedirected,
            readStandardInputAsync: static cancellationToken => Console.In.ReadToEndAsync(cancellationToken))
    {
    }

    /// <summary> Initializes a new instance of the <see cref="RequestInputReader" /> class for tests. </summary>
    /// <param name="isStandardInputRedirected"> Delegate that reports whether standard input is redirected. </param>
    /// <param name="readStandardInputAsync"> Delegate that reads standard input content. </param>
    /// <exception cref="ArgumentNullException"> Thrown when any delegate is <see langword="null" />. </exception>
    internal RequestInputReader (
        Func<bool> isStandardInputRedirected,
        Func<CancellationToken, Task<string>> readStandardInputAsync)
    {
        this.isStandardInputRedirected = isStandardInputRedirected ?? throw new ArgumentNullException(nameof(isStandardInputRedirected));
        this.readStandardInputAsync = readStandardInputAsync ?? throw new ArgumentNullException(nameof(readStandardInputAsync));
    }

    /// <summary> Reads JSON request input from redirected standard input. </summary>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The read result containing either request JSON or a structured error. </returns>
    public async ValueTask<RequestInputReadResult> ReadAsync (CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var hasRedirectedStandardInput = isStandardInputRedirected();
        if (!hasRedirectedStandardInput)
        {
            return RequestInputReadResult.Failure(ExecutionError.InvalidArgument(
                "Request input was not provided. Use redirected standard input."));
        }

        return await ReadFromStandardInputAsync(cancellationToken);
    }

    /// <summary> Reads and validates request JSON from standard input. </summary>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The read result. </returns>
    private async ValueTask<RequestInputReadResult> ReadFromStandardInputAsync (CancellationToken cancellationToken)
    {
        string json;
        try
        {
            json = await readStandardInputAsync(cancellationToken);
        }
        catch (IOException exception)
        {
            return RequestInputReadResult.Failure(ExecutionError.InternalError(
                $"Failed to read request JSON from standard input. {exception.Message}"));
        }

        return ValidateJson(json, "standard input");
    }

    /// <summary> Validates that request input is non-empty and JSON-parseable. </summary>
    /// <param name="json"> The JSON content to validate. </param>
    /// <param name="sourceLabel"> The source label used in error messages. </param>
    /// <returns> The read result. </returns>
    private static RequestInputReadResult ValidateJson (
        string json,
        string sourceLabel)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return RequestInputReadResult.Failure(ExecutionError.InvalidArgument(
                $"Request JSON from {sourceLabel} must not be empty."));
        }

        try
        {
            using var _ = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            return RequestInputReadResult.Failure(ExecutionError.InvalidArgument(
                $"Request JSON from {sourceLabel} is invalid. {exception.Message}"));
        }

        return RequestInputReadResult.Success(json);
    }

}
