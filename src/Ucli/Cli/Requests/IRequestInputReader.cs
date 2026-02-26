namespace MackySoft.Ucli.Cli.Requests;

/// <summary> Reads JSON request input from one configured source. </summary>
internal interface IRequestInputReader
{
    /// <summary> Reads request JSON from <c>--requestPath</c> or standard input according to input rules. </summary>
    /// <param name="requestPath"> The optional request file path specified by <c>--requestPath</c>. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The read result containing either request JSON and source metadata, or a structured error. </returns>
    ValueTask<RequestInputReadResult> ReadAsync (
        string? requestPath,
        CancellationToken cancellationToken = default);
}