using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Hosting.Cli.Requests.Input;

/// <summary> Represents source text read for the <c>eval</c> command. </summary>
internal sealed record EvalSourceInputReadResult
{
    private EvalSourceInputReadResult (
        string? source,
        ExecutionError? error)
    {
        Source = source;
        Error = error;
    }

    /// <summary> Gets the C# source text when reading succeeded. </summary>
    public string? Source { get; }

    /// <summary> Gets the source input error when reading failed. </summary>
    public ExecutionError? Error { get; }

    /// <summary> Gets a value indicating whether source input was read successfully. </summary>
    public bool IsSuccess => Error == null;

    /// <summary> Creates a successful source-read result. </summary>
    /// <param name="source"> The C# source text. </param>
    /// <returns> The successful result. </returns>
    public static EvalSourceInputReadResult Success (string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        return new EvalSourceInputReadResult(source, null);
    }

    /// <summary> Creates a failed source-read result. </summary>
    /// <param name="error"> The source input error. </param>
    /// <returns> The failed result. </returns>
    public static EvalSourceInputReadResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new EvalSourceInputReadResult(null, error);
    }
}
