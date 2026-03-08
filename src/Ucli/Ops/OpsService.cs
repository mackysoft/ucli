using MackySoft.Ucli.Ops.Access;
using MackySoft.Ucli.Ops.Mapping;
using MackySoft.Ucli.Ops.Preflight;

namespace MackySoft.Ucli.Ops;

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

        var preflightResult = await preflightService.Execute(input, cancellationToken).ConfigureAwait(false);
        if (!preflightResult.IsSuccess)
        {
            return OpsListServiceResult.Failure(
                preflightResult.Message,
                preflightResult.ErrorCode!);
        }

        var catalogResult = await catalogAccessService.Read(
                preflightResult.Context!,
                input,
                cancellationToken)
            .ConfigureAwait(false);
        if (!catalogResult.IsSuccess)
        {
            return OpsListServiceResult.Failure(
                catalogResult.Message,
                catalogResult.ErrorCode!);
        }

        return listResultMapper.Map(catalogResult.Output!);
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
                    Timeout: input.Timeout,
                    ReadIndexMode: input.ReadIndexMode),
                cancellationToken)
            .ConfigureAwait(false);
        if (!preflightResult.IsSuccess)
        {
            return OpsDescribeServiceResult.Failure(
                preflightResult.Message,
                preflightResult.ErrorCode!);
        }

        var catalogResult = await catalogAccessService.Read(
                preflightResult.Context!,
                new OpsCommandInput(
                    ProjectPath: input.ProjectPath,
                    Mode: input.Mode,
                    Timeout: input.Timeout,
                    ReadIndexMode: input.ReadIndexMode),
                cancellationToken)
            .ConfigureAwait(false);
        if (!catalogResult.IsSuccess)
        {
            return OpsDescribeServiceResult.Failure(
                catalogResult.Message,
                catalogResult.ErrorCode!);
        }

        return describeResultMapper.Map(catalogResult.Output!, input.OperationName);
    }
}