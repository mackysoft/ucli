using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Tests.Ops.Access;

internal static class OpsCatalogAccessServiceTestSupport
{
    public static OpsPreflightContext CreatePreflightContext (ReadIndexMode readIndexMode)
    {
        return new OpsPreflightContext(
            ProjectContextTestFactory.CreateRepositoryFixtureProject(),
            readIndexMode,
            UnityExecutionMode.Auto,
            TimeSpan.FromMilliseconds(1200),
            true);
    }
}
