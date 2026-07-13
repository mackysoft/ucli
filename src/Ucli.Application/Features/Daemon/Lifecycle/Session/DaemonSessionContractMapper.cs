using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

/// <summary> Converts raw daemon session contracts to and from validated runtime sessions. </summary>
internal static class DaemonSessionContractMapper
{
    /// <summary> Tries to create a validated runtime session from one raw contract. </summary>
    /// <param name="contract"> The raw session contract. </param>
    /// <param name="expectedProjectFingerprint"> The project fingerprint expected by the consuming boundary. </param>
    /// <param name="sourceDescription"> The contract source included in diagnostic errors. </param>
    /// <param name="session"> The validated runtime session when successful. </param>
    /// <param name="error"> The validation error when unsuccessful. </param>
    /// <returns> <see langword="true" /> when the contract represents a valid session; otherwise <see langword="false" />. </returns>
    public static bool TryCreate (
        DaemonSessionJsonContract contract,
        string expectedProjectFingerprint,
        string sourceDescription,
        [NotNullWhen(true)] out DaemonSession? session,
        [NotNullWhen(false)] out ExecutionError? error)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedProjectFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDescription);

        session = null;

        if (contract.SchemaVersion != DaemonSessionStorageContract.CurrentSchemaVersion)
        {
            error = Invalid(
                $"schemaVersion must be {DaemonSessionStorageContract.CurrentSchemaVersion}. Actual: {contract.SchemaVersion}.",
                sourceDescription);
            return false;
        }

        if (!IpcSessionToken.TryParse(contract.SessionToken, out var sessionToken))
        {
            error = Invalid("sessionToken is invalid.", sourceDescription);
            return false;
        }

        if (string.IsNullOrWhiteSpace(contract.ProjectFingerprint))
        {
            error = Invalid("projectFingerprint is missing.", sourceDescription);
            return false;
        }

        if (!string.Equals(contract.ProjectFingerprint, expectedProjectFingerprint, StringComparison.Ordinal))
        {
            error = Invalid(
                $"projectFingerprint mismatch. Requested={expectedProjectFingerprint}, Actual={contract.ProjectFingerprint}.",
                sourceDescription);
            return false;
        }

        if (contract.IssuedAtUtc == default)
        {
            error = Invalid("issuedAtUtc is invalid.", sourceDescription);
            return false;
        }

        if (!ContractLiteralCodec.TryParse<DaemonEditorMode>(contract.EditorMode, out var editorMode))
        {
            error = Invalid($"editorMode is invalid. Actual: {contract.EditorMode ?? "null"}.", sourceDescription);
            return false;
        }

        if (!ContractLiteralCodec.TryParse<DaemonSessionOwnerKind>(contract.OwnerKind, out var ownerKind))
        {
            error = Invalid($"ownerKind is invalid. Actual: {contract.OwnerKind ?? "null"}.", sourceDescription);
            return false;
        }

        if (!ContractLiteralCodec.TryParse<IpcTransportKind>(contract.EndpointTransportKind, out var transportKind))
        {
            error = Invalid($"endpointTransportKind is invalid. Actual: {contract.EndpointTransportKind ?? "null"}.", sourceDescription);
            return false;
        }

        if (string.IsNullOrWhiteSpace(contract.EndpointAddress))
        {
            error = Invalid("endpointAddress is missing.", sourceDescription);
            return false;
        }

        if (contract.OwnerProcessId is not int ownerProcessId)
        {
            error = Invalid("ownerProcessId is missing.", sourceDescription);
            return false;
        }

        try
        {
            session = new DaemonSession(
                sessionToken,
                contract.ProjectFingerprint,
                contract.IssuedAtUtc,
                editorMode,
                ownerKind,
                contract.CanShutdownProcess,
                new IpcEndpoint(transportKind, contract.EndpointAddress),
                contract.ProcessId,
                contract.ProcessStartedAtUtc,
                ownerProcessId,
                contract.EditorInstanceId);
        }
        catch (ArgumentException exception)
        {
            error = Invalid(exception.Message, sourceDescription);
            return false;
        }

        error = null;
        return true;
    }

    /// <summary> Creates the raw contract for a validated runtime session. </summary>
    /// <param name="session"> The validated runtime session. </param>
    /// <returns> The raw contract using the current schema version. </returns>
    public static DaemonSessionJsonContract ToContract (DaemonSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return new DaemonSessionJsonContract(
            SchemaVersion: DaemonSessionStorageContract.CurrentSchemaVersion,
            SessionToken: session.SessionToken.GetEncodedValue(),
            ProjectFingerprint: session.ProjectFingerprint,
            IssuedAtUtc: session.IssuedAtUtc,
            EditorMode: ContractLiteralCodec.ToValue(session.EditorMode),
            OwnerKind: ContractLiteralCodec.ToValue(session.OwnerKind),
            CanShutdownProcess: session.CanShutdownProcess,
            EndpointTransportKind: ContractLiteralCodec.ToValue(session.Endpoint.TransportKind),
            EndpointAddress: session.Endpoint.Address,
            ProcessId: session.ProcessId,
            ProcessStartedAtUtc: session.ProcessStartedAtUtc,
            OwnerProcessId: session.OwnerProcessId)
        {
            EditorInstanceId = session.EditorInstanceId,
        };
    }

    private static ExecutionError Invalid (string reason, string sourceDescription)
    {
        return ExecutionError.InvalidArgument($"Daemon session {reason} {sourceDescription}");
    }
}
