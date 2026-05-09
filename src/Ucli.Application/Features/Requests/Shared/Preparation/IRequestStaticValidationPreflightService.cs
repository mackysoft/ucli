using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;

/// <summary> Executes shared static-validation preflight for request-driven commands. </summary>
internal interface IRequestStaticValidationPreflightService
{
    /// <summary> Resolves read-index backed metadata and statically validates one prepared request. </summary>
    /// <param name="preparedRequest"> The request that has already been read, parsed, and bound to project context. </param>
    /// <param name="readIndexMode"> The optional normalized <c>--readIndexMode</c> value. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The shared static-validation preflight result. </returns>
    ValueTask<RequestStaticValidationPreflightResult> PrepareAsync (
        PreparedRequestContext preparedRequest,
        ReadIndexMode? readIndexMode,
        CancellationToken cancellationToken = default);
}
