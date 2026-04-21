using System.Security;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Paths;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Hosting.Cli.Requests.Input;

/// <summary> Reads JSON request input from one source and validates basic input constraints. </summary>
internal sealed class RequestInputReader : IRequestInputReader
{
    private readonly Func<bool> isStandardInputRedirected;
    private readonly Func<CancellationToken, Task<string>> readStandardInputAsync;
    private readonly Func<string, CancellationToken, Task<string>> readRequestFileAsync;

    /// <summary> Initializes a new instance of the <see cref="RequestInputReader" /> class. </summary>
    public RequestInputReader ()
        : this(
            isStandardInputRedirected: static () => Console.IsInputRedirected,
            readStandardInputAsync: static cancellationToken => Console.In.ReadToEndAsync(cancellationToken),
            readRequestFileAsync: static (path, cancellationToken) => File.ReadAllTextAsync(path, cancellationToken))
    {
    }

    /// <summary> Initializes a new instance of the <see cref="RequestInputReader" /> class for tests. </summary>
    /// <param name="isStandardInputRedirected"> Delegate that reports whether standard input is redirected. </param>
    /// <param name="readStandardInputAsync"> Delegate that reads standard input content. </param>
    /// <param name="readRequestFileAsync"> Delegate that reads request file content. </param>
    /// <exception cref="ArgumentNullException"> Thrown when any delegate is <see langword="null" />. </exception>
    internal RequestInputReader (
        Func<bool> isStandardInputRedirected,
        Func<CancellationToken, Task<string>> readStandardInputAsync,
        Func<string, CancellationToken, Task<string>> readRequestFileAsync)
    {
        this.isStandardInputRedirected = isStandardInputRedirected ?? throw new ArgumentNullException(nameof(isStandardInputRedirected));
        this.readStandardInputAsync = readStandardInputAsync ?? throw new ArgumentNullException(nameof(readStandardInputAsync));
        this.readRequestFileAsync = readRequestFileAsync ?? throw new ArgumentNullException(nameof(readRequestFileAsync));
    }

    /// <summary> Reads JSON request input from one source under strict source-selection rules. </summary>
    /// <param name="requestPath"> The optional request file path specified by <c>--requestPath</c>. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The read result containing either request JSON and source metadata, or a structured error. </returns>
    public async ValueTask<RequestInputReadResult> ReadAsync (
        string? requestPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var hasRequestPath = !string.IsNullOrWhiteSpace(requestPath);
        var hasRedirectedStandardInput = isStandardInputRedirected();

        if (hasRequestPath && hasRedirectedStandardInput)
        {
            return RequestInputReadResult.Failure(ExecutionError.InvalidArgument(
                "Request input source is ambiguous. Specify either --requestPath or redirected standard input."));
        }

        if (hasRequestPath)
        {
            return await ReadFromRequestPathAsync(requestPath!, cancellationToken);
        }

        if (!hasRedirectedStandardInput)
        {
            return RequestInputReadResult.Failure(ExecutionError.InvalidArgument(
                "Request input was not provided. Use redirected standard input or --requestPath."));
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

        return ValidateJson(json, RequestInputSource.StandardInput, "standard input");
    }

    /// <summary> Reads and validates request JSON from a request file path. </summary>
    /// <param name="requestPath"> The request file path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The read result. </returns>
    private async ValueTask<RequestInputReadResult> ReadFromRequestPathAsync (
        string requestPath,
        CancellationToken cancellationToken)
    {
        string json;
        try
        {
            json = await readRequestFileAsync(requestPath, cancellationToken);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return RequestInputReadResult.Failure(ExecutionError.InvalidArgument(
                $"Request path is invalid: {requestPath}."));
        }
        catch (FileNotFoundException)
        {
            return RequestInputReadResult.Failure(ExecutionError.InvalidArgument(
                $"Request file was not found: {requestPath}."));
        }
        catch (DirectoryNotFoundException)
        {
            return RequestInputReadResult.Failure(ExecutionError.InvalidArgument(
                $"Request directory was not found: {requestPath}."));
        }
        catch (Exception exception) when (IsIoFailure(exception))
        {
            return RequestInputReadResult.Failure(ExecutionError.InternalError(
                $"Failed to read request JSON from file: {requestPath}. {exception.Message}"));
        }

        return ValidateJson(json, RequestInputSource.RequestPath, $"request file '{requestPath}'");
    }

    /// <summary> Validates that request input is non-empty and JSON-parseable. </summary>
    /// <param name="json"> The JSON content to validate. </param>
    /// <param name="source"> The source where the input was read from. </param>
    /// <param name="sourceLabel"> The source label used in error messages. </param>
    /// <returns> The read result. </returns>
    private static RequestInputReadResult ValidateJson (
        string json,
        RequestInputSource source,
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

        return RequestInputReadResult.Success(json, source);
    }

    /// <summary> Determines whether an exception indicates I/O access failure. </summary>
    /// <param name="exception"> The exception to classify. </param>
    /// <returns> <see langword="true" /> when the exception represents I/O access failure; otherwise, <see langword="false" />. </returns>
    private static bool IsIoFailure (Exception exception)
    {
        return exception is IOException
            || exception is UnauthorizedAccessException
            || exception is SecurityException;
    }

}