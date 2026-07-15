using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Application.Features.OperationCatalog.Common.Contracts;
using MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Filtering;
using MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Preflight;
using MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Projection;

namespace MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops;

/// <summary> Implements the core <c>ops</c> orchestration flow. </summary>
internal sealed class OpsService : IOpsService
{
    private readonly IOpsPreflightService preflightService;

    private readonly IOpsCatalogAccessService catalogAccessService;

    private readonly IOpsListResultMapper listResultMapper;

    private readonly IOpsDescribeResultMapper describeResultMapper;

    /// <summary> Initializes a new instance of the <see cref="OpsService" /> class. </summary>
    /// <param name="preflightService"> The ops preflight service dependency. </param>
    /// <param name="catalogAccessService"> The ops catalog access service dependency. </param>
    /// <param name="listResultMapper"> The <c>ops list</c> result mapper dependency. </param>
    /// <param name="describeResultMapper"> The <c>ops describe</c> result mapper dependency. </param>
    public OpsService (
        IOpsPreflightService preflightService,
        IOpsCatalogAccessService catalogAccessService,
        IOpsListResultMapper listResultMapper,
        IOpsDescribeResultMapper describeResultMapper)
    {
        this.preflightService = preflightService ?? throw new ArgumentNullException(nameof(preflightService));
        this.catalogAccessService = catalogAccessService ?? throw new ArgumentNullException(nameof(catalogAccessService));
        this.listResultMapper = listResultMapper ?? throw new ArgumentNullException(nameof(listResultMapper));
        this.describeResultMapper = describeResultMapper ?? throw new ArgumentNullException(nameof(describeResultMapper));
    }

    /// <inheritdoc />
    public async ValueTask<OpsListServiceResult> GetAllAsync (
        OpsCommandInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(input);

        if (!OpsListFilter.TryCreate(input, out var filter, out var filterError))
        {
            return OpsListServiceResult.Failure(
                filterError!,
                UcliCoreErrorCodes.InvalidArgument);
        }

        var preflightResult = await preflightService.ExecuteAsync(
                OpsPreflightInput.From(input),
                cancellationToken)
            .ConfigureAwait(false);
        if (!preflightResult.IsSuccess)
        {
            return OpsListServiceResult.Failure(
                preflightResult.Message,
                preflightResult.ErrorCode!);
        }

        var catalogResult = await catalogAccessService.ReadListAsync(
                preflightResult.Context!,
                cancellationToken)
            .ConfigureAwait(false);
        if (!catalogResult.IsSuccess)
        {
            return OpsListServiceResult.Failure(
                catalogResult.Message,
                catalogResult.ErrorCode!,
                catalogResult.StartupFailure);
        }

        var filterResult = filter!.Apply(catalogResult.Output!.Snapshot.Operations);
        if (!filterResult.IsSuccess)
        {
            return OpsListServiceResult.Failure(
                filterResult.ErrorMessage!,
                UcliCoreErrorCodes.InvalidArgument);
        }

        return listResultMapper.Map(catalogResult.Output, filterResult.Operations!);
    }

    /// <inheritdoc />
    public async ValueTask<OpsDescribeServiceResult> DescribeAsync (
        OpsDescribeCommandInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(input);

        var preflightResult = await preflightService.ExecuteAsync(
                OpsPreflightInput.From(input),
                cancellationToken)
            .ConfigureAwait(false);
        if (!preflightResult.IsSuccess)
        {
            return OpsDescribeServiceResult.Failure(
                preflightResult.Message,
                preflightResult.ErrorCode!);
        }

        var catalogResult = await catalogAccessService.ReadDescribeAsync(
                preflightResult.Context!,
                input.OperationName,
                cancellationToken)
            .ConfigureAwait(false);
        if (!catalogResult.IsSuccess)
        {
            return OpsDescribeServiceResult.Failure(
                catalogResult.Message,
                catalogResult.ErrorCode!,
                catalogResult.StartupFailure);
        }

        return describeResultMapper.Map(catalogResult.Output!);
    }
}
