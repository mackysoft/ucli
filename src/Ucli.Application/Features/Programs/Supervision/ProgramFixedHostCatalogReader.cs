using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Programs.Supervision;

/// <summary> Reads operation metadata only from the fixed host selected for a Program. </summary>
internal interface IProgramFixedHostCatalogReader
{
    ValueTask<ProgramFixedHostCatalogReadResult> ReadAsync (
        ProjectContext project,
        IUnityExecutionHostBinding binding,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default);
}

/// <summary> Reads and validates operation metadata from the Program's fixed host only. </summary>
internal sealed class ProgramFixedHostCatalogReader : IProgramFixedHostCatalogReader
{
    public async ValueTask<ProgramFixedHostCatalogReadResult> ReadAsync (
        ProjectContext project,
        IUnityExecutionHostBinding binding,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(deadline);
        var execution = await binding.ExecuteAsync(
                UcliCommandIds.Ops,
                new UnityRequestPayload.OpsRead(FailFast: false, RequireReadinessGate: false, IncludeEditLoweringOnly: true),
                deadline,
                cancellationToken)
            .ConfigureAwait(false);
        if (!execution.IsSuccess || execution.Response!.Errors.Count != 0)
        {
            return ProgramFixedHostCatalogReadResult.Failed();
        }
        if (!IpcPayloadCodec.TryDeserialize(execution.Response.Payload, out IpcOpsReadResponse payload, out _)
            || !OpsCatalogSnapshot.TryCreate(payload.GeneratedAtUtc, payload.Operations, "operations", true, out var snapshot, out _))
        {
            return ProgramFixedHostCatalogReadResult.Failed();
        }
        var descriptors = OperationDescriptorMapper.Map(snapshot!.Operations, cancellationToken);
        return ProgramFixedHostCatalogReadResult.Success(descriptors);
    }
}

internal sealed record ProgramFixedHostCatalogReadResult (IReadOnlyList<UcliOperationDescriptor>? Descriptors)
{
    public bool IsSuccess => Descriptors is not null;
    public static ProgramFixedHostCatalogReadResult Success (IReadOnlyList<UcliOperationDescriptor> descriptors) => new(descriptors);
    public static ProgramFixedHostCatalogReadResult Failed () => new((IReadOnlyList<UcliOperationDescriptor>?)null);
}
