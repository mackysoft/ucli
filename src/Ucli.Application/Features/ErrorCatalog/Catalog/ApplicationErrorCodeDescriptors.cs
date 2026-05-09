using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Testing.Run.Common.Contracts;

namespace MackySoft.Ucli.Application.Features.ErrorCatalog.Catalog;

internal static class ApplicationErrorCodeDescriptors
{
    public static IReadOnlyList<UcliErrorCodeDescriptor> All { get; } = CreateAll();

    private static UcliErrorCodeDescriptor[] CreateAll ()
    {
        return ExecutionErrorCodeDescriptors.All
            .Concat(UnityProcessErrorCodeDescriptors.All)
            .Concat(UnityExecutionModeDecisionErrorCodeDescriptors.All)
            .Concat(ProjectContextErrorCodeDescriptors.All)
            .Concat(ValidationErrorCodeDescriptors.All)
            .Concat(TestRunErrorCodeDescriptors.All)
            .OrderBy(static descriptor => descriptor.Code.Value, StringComparer.Ordinal)
            .ToArray();
    }
}
