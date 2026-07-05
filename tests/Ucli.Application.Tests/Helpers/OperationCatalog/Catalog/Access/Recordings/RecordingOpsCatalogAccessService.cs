using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingOpsCatalogAccessService : IOpsCatalogAccessService
{
    private readonly List<ListReadInvocation> listReadInvocations = [];
    private readonly List<DescribeInvocation> describeInvocations = [];

    public IReadOnlyList<ListReadInvocation> ListReadInvocations => listReadInvocations;

    public IReadOnlyList<DescribeInvocation> DescribeInvocations => describeInvocations;

    public OpsListReadResult ListResult { get; set; } =
        OpsListReadResult.Failure("not configured", UcliCoreErrorCodes.InternalError);

    public OpsDescribeReadResult DescribeResult { get; set; } =
        OpsDescribeReadResult.Failure("not configured", UcliCoreErrorCodes.InternalError);

    public ValueTask<OpsListReadResult> ReadListAsync (
        OpsPreflightContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        listReadInvocations.Add(new ListReadInvocation(context, cancellationToken));
        return ValueTask.FromResult(ListResult);
    }

    public ValueTask<OpsDescribeReadResult> ReadDescribeAsync (
        OpsPreflightContext context,
        string? operationName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        describeInvocations.Add(new DescribeInvocation(context, operationName, cancellationToken));
        return ValueTask.FromResult(DescribeResult);
    }

    internal readonly record struct ListReadInvocation (
        OpsPreflightContext Context,
        CancellationToken CancellationToken);

    internal readonly record struct DescribeInvocation (
        OpsPreflightContext Context,
        string? OperationName,
        CancellationToken CancellationToken);
}
