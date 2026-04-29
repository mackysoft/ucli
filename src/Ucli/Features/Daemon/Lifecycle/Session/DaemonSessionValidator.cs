using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Features.Daemon.UseCases.Stop;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Session;

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
