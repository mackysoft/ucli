using MackySoft.Ucli.Application.Features.Assurance.Build.Catalog;
using MackySoft.Ucli.Application.Features.Assurance.Build.Semantics;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Catalog;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Semantics;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Catalog;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Semantics;
using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;

namespace MackySoft.Ucli.Tests.Helpers.Assurance;

internal static class CliAssuranceSemanticInvariantValidatorFactory
{
    public static AssuranceSemanticInvariantValidator CreateReadyValidator ()
    {
        return new AssuranceSemanticInvariantValidator(
            new CodeCatalog(
            [
                new ContractsCodeCatalogContributor(),
                new ApplicationCodeCatalogContributor(),
                new ReadyCodeCatalogContributor(),
            ]),
            [new BuildAssuranceSemanticInvariantRule()],
            [new ReadyAssuranceSemanticInvariantRule()]);
    }

    public static AssuranceSemanticInvariantValidator CreateCompileValidator ()
    {
        return new AssuranceSemanticInvariantValidator(
            new CodeCatalog(
            [
                new ContractsCodeCatalogContributor(),
                new ApplicationCodeCatalogContributor(),
                new ReadyCodeCatalogContributor(),
                new CompileCodeCatalogContributor(),
            ]),
            [new BuildAssuranceSemanticInvariantRule()],
            [
                new ReadyAssuranceSemanticInvariantRule(),
                new CompileAssuranceSemanticInvariantRule(),
            ]);
    }

    public static AssuranceSemanticInvariantValidator CreateBuildValidator ()
    {
        return new AssuranceSemanticInvariantValidator(
            new CodeCatalog([new BuildCodeCatalogContributor()]),
            [new BuildAssuranceSemanticInvariantRule()],
            [new BuildAssuranceSemanticInvariantRule()]);
    }

    public static AssuranceSemanticInvariantValidator CreateVerifyValidator ()
    {
        return new AssuranceSemanticInvariantValidator(
            new CodeCatalog(
            [
                new ContractsCodeCatalogContributor(),
                new ApplicationCodeCatalogContributor(),
                new ReadyCodeCatalogContributor(),
                new CompileCodeCatalogContributor(),
                new VerifyCodeCatalogContributor(),
            ]),
            [new BuildAssuranceSemanticInvariantRule()],
            [
                new ReadyAssuranceSemanticInvariantRule(),
                new CompileAssuranceSemanticInvariantRule(),
                new VerifyAssuranceSemanticInvariantRule(),
            ]);
    }

    public static AssuranceSemanticInvariantValidator CreateAllAssuranceCommandValidator ()
    {
        return new AssuranceSemanticInvariantValidator(
            CreateAllAssuranceCommandCodeCatalog(),
            [new BuildAssuranceSemanticInvariantRule()],
            [
                new ReadyAssuranceSemanticInvariantRule(),
                new CompileAssuranceSemanticInvariantRule(),
                new BuildAssuranceSemanticInvariantRule(),
                new VerifyAssuranceSemanticInvariantRule(),
            ]);
    }

    public static CodeCatalog CreateAllAssuranceCommandCodeCatalog ()
    {
        return new CodeCatalog(
        [
            new ContractsCodeCatalogContributor(),
            new ApplicationCodeCatalogContributor(),
            new ReadyCodeCatalogContributor(),
            new CompileCodeCatalogContributor(),
            new BuildCodeCatalogContributor(),
            new VerifyCodeCatalogContributor(),
        ]);
    }
}
