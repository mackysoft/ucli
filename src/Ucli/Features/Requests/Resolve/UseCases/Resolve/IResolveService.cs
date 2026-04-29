namespace MackySoft.Ucli.Features.Requests.Resolve.UseCases.Resolve;

/// <summary> Executes the <c>resolve</c> command workflow. </summary>
internal interface IResolveService
{
    /// <summary> Executes one <c>resolve</c> workflow and returns the normalized execution result. </summary>
    ValueTask<ResolveServiceResult> Execute (
        ResolveCommandInput input,
        CancellationToken cancellationToken = default);
}
