using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Application.Features.OperationCatalog.Common.Contracts;
using MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Projection;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingOpsListResultMapper : IOpsListResultMapper
{
    private readonly List<Invocation> invocations = [];

    public IReadOnlyList<Invocation> Invocations => invocations;

    public OpsListServiceResult Result { get; set; } =
        OpsListServiceResult.Failure("not configured", UcliCoreErrorCodes.InternalError);

    public OpsListServiceResult Map (
        OpsListReadOutput output,
        IReadOnlyList<OpsCatalogListEntry> operations)
    {
        invocations.Add(new Invocation(output, operations));
        return Result;
    }

    internal readonly record struct Invocation (
        OpsListReadOutput Output,
        IReadOnlyList<OpsCatalogListEntry> Operations);
}
