using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Application.Features.OperationCatalog.Common.Contracts;
using MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Projection;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingOpsDescribeResultMapper : IOpsDescribeResultMapper
{
    private readonly List<Invocation> invocations = [];

    public IReadOnlyList<Invocation> Invocations => invocations;

    public OpsDescribeServiceResult Result { get; set; } =
        OpsDescribeServiceResult.Failure("not configured", UcliCoreErrorCodes.InternalError);

    public OpsDescribeServiceResult Map (OpsDescribeReadOutput output)
    {
        invocations.Add(new Invocation(output));
        return Result;
    }

    internal readonly record struct Invocation (OpsDescribeReadOutput Output);
}
