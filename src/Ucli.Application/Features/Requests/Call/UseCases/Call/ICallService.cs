using MackySoft.Ucli.Application.Features.Requests.Call.Common.Contracts;

namespace MackySoft.Ucli.Application.Features.Requests.Call.UseCases.Call;

/// <summary> Defines the request-driven <c>call</c> workflow service. </summary>
internal interface ICallService
{
    /// <summary> Executes one <c>call</c> command input and returns the normalized service result. </summary>
    /// <param name="requestId"> The non-empty correlation identifier owned by the CLI command invocation. </param>
    /// <param name="input"> The normalized command input. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The normalized service result. </returns>
    ValueTask<CallServiceResult> ExecuteAsync (
        Guid requestId,
        CallCommandInput input,
        CancellationToken cancellationToken = default);
}
