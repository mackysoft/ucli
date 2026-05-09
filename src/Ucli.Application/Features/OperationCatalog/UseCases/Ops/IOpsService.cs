using MackySoft.Ucli.Application.Features.OperationCatalog.Common.Contracts;

namespace MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops;

/// <summary> Provides the <c>ops</c> command workflow service. </summary>
internal interface IOpsService
{
    /// <summary> Gets all operations for <c>ops list</c>. </summary>
    /// <param name="input"> The interpreted command input values. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the normalized command result. </returns>
    ValueTask<OpsListServiceResult> GetAllAsync (
        OpsCommandInput input,
        CancellationToken cancellationToken = default);

    /// <summary> Executes <c>ops describe</c>. </summary>
    /// <param name="input"> The interpreted command input values. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the normalized command result. </returns>
    ValueTask<OpsDescribeServiceResult> DescribeAsync (
        OpsDescribeCommandInput input,
        CancellationToken cancellationToken = default);
}
