using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Play.Common.Contracts;
using MackySoft.Ucli.Application.Shared.CommandContracts.Projection;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Play.Common.Projection;

/// <summary> Creates public Play Mode command projection values from IPC lifecycle contracts. </summary>
internal static class PlayOutputProjectionFactory
{
    /// <summary> Creates one lifecycle snapshot output from a Unity Play Mode lifecycle snapshot. </summary>
    /// <param name="snapshot"> The IPC lifecycle snapshot. </param>
    /// <returns> The public lifecycle snapshot output. </returns>
    public static PlayLifecycleSnapshotOutput CreateSnapshotOutput (IpcPlayLifecycleSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var lifecycle = LifecycleProjectionFactory.Create(snapshot);
        return new PlayLifecycleSnapshotOutput(
            ServerVersion: lifecycle.ServerVersion,
            EditorMode: lifecycle.EditorMode,
            UnityVersion: lifecycle.UnityVersion,
            ProjectFingerprint: snapshot.ProjectFingerprint,
            LifecycleState: lifecycle.LifecycleState,
            BlockingReason: lifecycle.BlockingReason,
            CompileState: lifecycle.CompileState,
            CompileGeneration: lifecycle.CompileGeneration,
            DomainReloadGeneration: lifecycle.DomainReloadGeneration,
            CanAcceptExecutionRequests: lifecycle.CanAcceptExecutionRequests,
            ObservedAtUtc: lifecycle.ObservedAtUtc,
            ActionRequired: lifecycle.ActionRequired,
            PrimaryDiagnostic: CreatePrimaryDiagnosticOutput(lifecycle.PrimaryDiagnostic),
            PlayMode: lifecycle.PlayMode!);
    }

    /// <summary> Creates one public primary diagnostic projection. </summary>
    /// <param name="diagnostic"> The IPC diagnostic contract. </param>
    /// <returns> The diagnostic output, or <see langword="null" /> when absent or invalid. </returns>
    public static DaemonPrimaryDiagnosticOutput? CreatePrimaryDiagnosticOutput (IpcPrimaryDiagnostic? diagnostic)
    {
        if (diagnostic is null || !StringValueNormalizer.TryTrimToNonEmpty(diagnostic.Kind, out var kind))
        {
            return null;
        }

        return new DaemonPrimaryDiagnosticOutput(
            Kind: kind,
            Code: StringValueNormalizer.TrimToNull(diagnostic.Code),
            File: StringValueNormalizer.TrimToNull(diagnostic.File),
            Line: diagnostic.Line,
            Column: diagnostic.Column,
            Message: StringValueNormalizer.TrimToNull(diagnostic.Message));
    }
}
