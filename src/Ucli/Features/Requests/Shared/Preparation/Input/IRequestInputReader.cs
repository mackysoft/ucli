namespace MackySoft.Ucli.Features.Requests.Shared.Preparation.Input;

/// <summary> Reads JSON request input for request commands. </summary>
internal interface IRequestInputReader
{
    /// <summary> Reads request JSON for command execution. </summary>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The read result containing either request JSON or a structured error. </returns>
    ValueTask<RequestInputReadResult> ReadAsync (CancellationToken cancellationToken = default);
}
