using MackySoft.Ucli.Application.Shared.Configuration;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

/// <summary> Evaluates whether one operation can be executed under current configuration. </summary>
internal interface IOperationAuthorizationService
{
    /// <summary> Asynchronously evaluates operation policy and allowlist constraints for one operation. </summary>
    /// <param name="operation"> The operation descriptor to evaluate. </param>
    /// <param name="config"> The configuration values that define execution constraints. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the authorization evaluation result. </returns>
    ValueTask<OperationAuthorizationResult> AuthorizeAsync (
        UcliOperationDescriptor operation,
        UcliConfig config,
        CancellationToken cancellationToken = default);
}
