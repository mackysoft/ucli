using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Application.Features.OperationCatalog.Common.Contracts;
using MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Preflight;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Validate.Common.Contracts;

namespace MackySoft.Ucli.Application.Tests.Shared.Execution;

public sealed class UcliCodeFailureResultContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void FailureFactories_WithNullRequiredCode_ThrowArgumentNullException ()
    {
        var factories = new Action[]
        {
            static () => AssetLookupRefreshResult.Failure("failure", null!),
            static () => AssetSearchLookupReadResult.Failure("failure", null!),
            static () => SceneTreeLiteRefreshResult.Failure("failure", null!),
            static () => SceneTreeLiteReadResult.Failure("failure", null!),
            static () => OpsPreflightResult.Failure("failure", null!),
            static () => OpsDescribeServiceResult.Failure("failure", null!),
            static () => OpsListServiceResult.Failure("failure", null!),
            static () => OpsCatalogFetchResult.Failure("failure", null!),
            static () => OpsCatalogSourceRefreshResult.Failure("failure", null!),
            static () => OpsDescribeReadResult.Failure("failure", null!),
            static () => OpsListReadResult.Failure("failure", null!),
            static () => OperationAuthorizationResult.Denied(null!, "failure"),
            static () => ValidateServiceResult.Failure("failure", null!),
        };

        Assert.All(factories, factory => Assert.Throws<ArgumentNullException>(factory));
    }
}
