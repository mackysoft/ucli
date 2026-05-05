using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

/// <summary> Implements contract validation for daemon session models. </summary>
internal sealed class DaemonSessionValidator : IDaemonSessionValidator
{
    /// <summary> Validates one daemon session model. </summary>
    /// <param name="session"> The daemon session model. </param>
    /// <param name="sessionPath"> The related session JSON path for diagnostics. </param>
    /// <param name="error"> The structured validation error when validation fails; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when validation succeeds; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="session" /> is <see langword="null" />. </exception>
    public bool TryValidate (
        DaemonSession session,
        string sessionPath,
        [NotNullWhen(false)] out ExecutionError? error)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.SchemaVersion != DaemonSession.CurrentSchemaVersion)
        {
            error = ExecutionError.InvalidArgument(
                $"Daemon session schemaVersion must be {DaemonSession.CurrentSchemaVersion}. Actual: {session.SchemaVersion}. {sessionPath}");
            return false;
        }

        if (string.IsNullOrWhiteSpace(session.SessionToken)
            || string.IsNullOrWhiteSpace(session.ProjectFingerprint)
            || string.IsNullOrWhiteSpace(session.RuntimeKind)
            || string.IsNullOrWhiteSpace(session.OwnerKind)
            || string.IsNullOrWhiteSpace(session.EndpointTransportKind)
            || string.IsNullOrWhiteSpace(session.EndpointAddress))
        {
            error = ExecutionError.InvalidArgument($"Daemon session contains required empty values: {sessionPath}");
            return false;
        }

        if (session.IssuedAtUtc == default)
        {
            error = ExecutionError.InvalidArgument($"Daemon session issuedAtUtc is invalid: {sessionPath}");
            return false;
        }

        if (session.ProcessId is int processId && processId <= 0)
        {
            error = ExecutionError.InvalidArgument($"Daemon session processId must be greater than zero when specified. Actual: {processId}. {sessionPath}");
            return false;
        }

        if (session.OwnerProcessId is not int ownerProcessId || ownerProcessId <= 0)
        {
            error = ExecutionError.InvalidArgument(
                $"Daemon session ownerProcessId must be greater than zero. Actual: {session.OwnerProcessId?.ToString() ?? "null"}. {sessionPath}");
            return false;
        }

        if (!string.Equals(session.RuntimeKind, DaemonSession.RuntimeKindBatchmode, StringComparison.Ordinal))
        {
            error = ExecutionError.InvalidArgument(
                $"Daemon session runtimeKind must be '{DaemonSession.RuntimeKindBatchmode}'. Actual: {session.RuntimeKind}. {sessionPath}");
            return false;
        }

        if (!string.Equals(session.OwnerKind, DaemonSession.OwnerKindSupervisor, StringComparison.Ordinal))
        {
            error = ExecutionError.InvalidArgument(
                $"Daemon session ownerKind must be '{DaemonSession.OwnerKindSupervisor}'. Actual: {session.OwnerKind}. {sessionPath}");
            return false;
        }

        if (!session.CanShutdownProcess)
        {
            error = ExecutionError.InvalidArgument(
                $"Daemon session canShutdownProcess must be true for supervisor-owned daemon sessions. {sessionPath}");
            return false;
        }

        if (!IpcTransportKindCodec.TryParse(session.EndpointTransportKind, out _))
        {
            error = ExecutionError.InvalidArgument(
                $"Daemon session endpointTransportKind is invalid: {session.EndpointTransportKind}. {sessionPath}");
            return false;
        }

        error = null;
        return true;
    }
}
