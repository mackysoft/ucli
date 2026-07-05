using MackySoft.Ucli.Application.Features.OperationCatalog.Common.Contracts;
using MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops;
using MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Contracts;

namespace MackySoft.Tests;

internal sealed class RecordingOpsService : IOpsService
{
    private readonly List<ListInvocation> listInvocations = [];
    private readonly List<DescribeInvocation> describeInvocations = [];

    public RecordingOpsService (
        OpsListServiceResult listResult,
        OpsDescribeServiceResult describeResult)
    {
        ListResult = listResult ?? throw new ArgumentNullException(nameof(listResult));
        DescribeResult = describeResult ?? throw new ArgumentNullException(nameof(describeResult));
    }

    public IReadOnlyList<ListInvocation> ListInvocations => listInvocations;

    public IReadOnlyList<DescribeInvocation> DescribeInvocations => describeInvocations;

    public OpsListServiceResult ListResult { get; set; }

    public OpsDescribeServiceResult DescribeResult { get; set; }

    public ValueTask<OpsListServiceResult> GetAllAsync (
        OpsCommandInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        listInvocations.Add(new ListInvocation(input, cancellationToken));
        return ValueTask.FromResult(ListResult);
    }

    public ValueTask<OpsDescribeServiceResult> DescribeAsync (
        OpsDescribeCommandInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        describeInvocations.Add(new DescribeInvocation(input, cancellationToken));
        return ValueTask.FromResult(DescribeResult);
    }

    public readonly record struct ListInvocation (
        OpsCommandInput Input,
        CancellationToken CancellationToken);

    public readonly record struct DescribeInvocation (
        OpsDescribeCommandInput Input,
        CancellationToken CancellationToken);
}
