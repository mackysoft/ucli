using MackySoft.Ucli.Application.Shared.CommandContracts.Projection;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Converts ping payloads into ready lifecycle evidence. </summary>
internal static class ReadyLifecycleOutputFactory
{
    /// <summary> Creates lifecycle evidence from one ping response. </summary>
    public static ReadyLifecycleOutput Create (IpcPingResponse pingResponse)
    {
        ArgumentNullException.ThrowIfNull(pingResponse);

        var projection = LifecycleProjectionFactory.Create(pingResponse);

        return new ReadyLifecycleOutput(
            ServerVersion: projection.ServerVersion,
            UnityVersion: projection.UnityVersion,
            EditorMode: projection.EditorMode,
            LifecycleState: projection.LifecycleState.HasValue
                ? ContractLiteralCodec.ToValue(projection.LifecycleState.Value)
                : null,
            BlockingReason: projection.BlockingReason.HasValue
                ? ContractLiteralCodec.ToValue(projection.BlockingReason.Value)
                : null,
            CompileState: projection.CompileState.HasValue
                ? ContractLiteralCodec.ToValue(projection.CompileState.Value)
                : null,
            CompileGeneration: projection.CompileGeneration,
            DomainReloadGeneration: projection.DomainReloadGeneration,
            CanAcceptExecutionRequests: projection.CanAcceptExecutionRequests,
            ObservedAtUtc: projection.ObservedAtUtc,
            ActionRequired: projection.ActionRequired,
            PrimaryDiagnostic: ToOutput(projection.PrimaryDiagnostic),
            PlayMode: projection.PlayMode);
    }

    private static ReadyPrimaryDiagnosticOutput? ToOutput (IpcPrimaryDiagnostic? diagnostic)
    {
        if (diagnostic is null || !StringValueNormalizer.TryTrimToNonEmpty(diagnostic.Kind, out var kind))
        {
            return null;
        }

        return new ReadyPrimaryDiagnosticOutput(
            Kind: kind,
            Code: StringValueNormalizer.TrimToNull(diagnostic.Code),
            File: StringValueNormalizer.TrimToNull(diagnostic.File),
            Line: diagnostic.Line,
            Column: diagnostic.Column,
            Message: StringValueNormalizer.TrimToNull(diagnostic.Message));
    }
}
