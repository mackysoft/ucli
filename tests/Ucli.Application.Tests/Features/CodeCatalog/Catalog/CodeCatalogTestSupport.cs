using MackySoft.Ucli.Application.Features.Assurance.Build.Catalog;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Catalog;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Catalog;
using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;
using MackySoft.Ucli.Application.Shared.Foundation;
using CodeCatalogModel = MackySoft.Ucli.Application.Features.CodeCatalog.Catalog.CodeCatalog;

namespace MackySoft.Ucli.Application.Tests.Features.CodeCatalog.Catalog;

internal static class CodeCatalogTestSupport
{
    internal static CodeCatalogModel CreateCoreCatalog ()
    {
        return new CodeCatalogModel(
            [
                new ContractsCodeCatalogContributor(),
                new ApplicationCodeCatalogContributor(),
            ]);
    }

    internal static CodeCatalogModel CreateProductionCatalog ()
    {
        return new CodeCatalogModel(
            [
                new ContractsCodeCatalogContributor(),
                new ApplicationCodeCatalogContributor(),
                new ReadyCodeCatalogContributor(),
                new BuildCodeCatalogContributor(),
                new CompileCodeCatalogContributor(),
                new VerifyCodeCatalogContributor(),
            ]);
    }

    internal static CodeCatalogService CreateService ()
    {
        return new CodeCatalogService(CreateCoreCatalog());
    }

    internal static void AssertInvalidArgument (CodeCatalogListResult result)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Descriptors);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.Error.Code);
    }

    internal static void AssertInvalidArgument (CodeCatalogDescribeResult result)
    {
        Assert.False(result.IsSuccess);
        Assert.False(result.Known);
        Assert.Null(result.Descriptor);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.Error.Code);
    }
}
