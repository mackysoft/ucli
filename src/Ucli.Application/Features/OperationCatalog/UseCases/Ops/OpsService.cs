using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Application.Features.OperationCatalog.Common.Contracts;
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
    public async ValueTask<OpsListServiceResult> GetAll (
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

        var preflightResult = await preflightService.Execute(input, cancellationToken).ConfigureAwait(false);
        if (!preflightResult.IsSuccess)
        {
            return OpsListServiceResult.Failure(
                preflightResult.Message,
                preflightResult.ErrorCode!.Value);
        }

        var catalogResult = await catalogAccessService.ReadList(
                preflightResult.Context!,
                cancellationToken)
            .ConfigureAwait(false);
        if (!catalogResult.IsSuccess)
        {
            return OpsListServiceResult.Failure(
                catalogResult.Message,
                catalogResult.ErrorCode!.Value);
        }

        return listResultMapper.Map(catalogResult.Output!, filter!);
    }

    /// <inheritdoc />
    public async ValueTask<OpsDescribeServiceResult> Describe (
        OpsDescribeCommandInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(input);

        var preflightResult = await preflightService.Execute(
                new OpsCommandInput(
                    ProjectPath: input.ProjectPath,
                    Mode: input.Mode,
                    TimeoutMilliseconds: input.TimeoutMilliseconds,
                    ReadIndexMode: input.ReadIndexMode,
                    NameRegex: null,
                    Kind: null,
                    MaxPolicy: null,
                    FailFast: input.FailFast),
                cancellationToken)
            .ConfigureAwait(false);
        if (!preflightResult.IsSuccess)
        {
            return OpsDescribeServiceResult.Failure(
                preflightResult.Message,
                preflightResult.ErrorCode!.Value);
        }

        var catalogResult = await catalogAccessService.ReadDescribe(
                preflightResult.Context!,
                input.OperationName,
                cancellationToken)
            .ConfigureAwait(false);
        if (!catalogResult.IsSuccess)
        {
            return OpsDescribeServiceResult.Failure(
                catalogResult.Message,
                catalogResult.ErrorCode!.Value);
        }

        return describeResultMapper.Map(catalogResult.Output!);
    }
}
