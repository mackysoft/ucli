namespace MackySoft.Ucli.Features.Requests.Shared.Preparation.Input;

/// <summary> Reads JSON request input from one configured source. </summary>
internal interface IRequestInputReader
{
    /// <summary> Reads request JSON from the configured input source. </summary>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The read result containing either request JSON or a structured error. </returns>
    ValueTask<RequestInputReadResult> ReadAsync (CancellationToken cancellationToken = default);
}
