using MackySoft.Ucli.Application.Features.Assurance.Build.Diagnostics;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Diagnostics;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

namespace MackySoft.Ucli.Application.Diagnostics;

/// <summary> Provides bundled descriptors for error codes owned by application-layer workflows. </summary>
internal static class ApplicationErrorCodeDescriptors
{
    /// <summary> Gets application-owned descriptors sorted by error code value. </summary>
    public static IReadOnlyList<UcliErrorDescriptor> All { get; } = CreateAll();

    private static UcliErrorDescriptor[] CreateAll ()
    {
        return ExecutionErrorCodeDescriptors.All
            .Concat(UnityProcessErrorCodeDescriptors.All)
            .Concat(UnityExecutionModeDecisionErrorCodeDescriptors.All)
            .Concat(ProjectContextErrorCodeDescriptors.All)
            .Concat(BuildErrorCodeDescriptors.All)
            .Concat(VerifyErrorCodeDescriptors.All)
            .Concat(ValidationErrorCodeDescriptors.All)
            .Concat(TestRunErrorCodeDescriptors.All)
            .OrderBy(static descriptor => descriptor.Code.Value, StringComparer.Ordinal)
            .ToArray();
    }
}
