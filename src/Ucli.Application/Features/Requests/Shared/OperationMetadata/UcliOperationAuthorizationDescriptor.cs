using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

/// <summary> Carries only the operation facts required by product authorization. </summary>
internal readonly record struct UcliOperationAuthorizationDescriptor (
    string Name,
    OperationPolicy Policy,
    UcliOperationExposure Exposure)
{
    /// <summary> Projects authorization facts from one registered operation contract. </summary>
    public static UcliOperationAuthorizationDescriptor From (
        UcliOperationDescriptor operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return new UcliOperationAuthorizationDescriptor(
            operation.Name,
            operation.Policy,
            operation.Exposure);
    }
}
