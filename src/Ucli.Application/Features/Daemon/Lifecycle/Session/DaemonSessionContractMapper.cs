using System.Diagnostics.CodeAnalysis;
using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

/// <summary>
/// Converts one transport-specific endpoint text value to its normalized contract and guarded runtime path.
/// </summary>
/// <param name="transportKind"> The endpoint transport kind declared by the session contract. </param>
/// <param name="address"> The raw endpoint address declared by the session contract. </param>
/// <returns> The normalized endpoint contract and guarded Unix socket path produced at the input boundary. </returns>
internal delegate DaemonSessionEndpointBinding DaemonSessionEndpointBindingFactory (
    IpcTransportKind transportKind,
    string address);

/// <summary> Carries one normalized endpoint contract and its guarded Unix socket path into session creation. </summary>
/// <param name="Contract"> The normalized endpoint contract. </param>
/// <param name="UnixSocketPath"> The guarded Unix socket path, or <see langword="null" /> for a Named Pipe endpoint. </param>
internal sealed record DaemonSessionEndpointBinding (
    IpcEndpoint Contract,
    AbsolutePath? UnixSocketPath);

/// <summary> Converts raw daemon session contracts to and from validated runtime sessions. </summary>
internal static class DaemonSessionContractMapper
{
    /// <summary> Tries to create a validated runtime session from one raw contract. </summary>
    /// <param name="contract"> The raw session contract. </param>
    /// <param name="expectedProjectFingerprint"> The project fingerprint expected by the consuming boundary. </param>
    /// <param name="sourceDescription"> The contract source included in diagnostic errors. </param>
    /// <param name="endpointBindingFactory">
    /// The boundary adapter that validates the raw endpoint address once and returns its guarded runtime representation.
    /// </param>
    /// <param name="session"> The validated runtime session when successful. </param>
    /// <param name="error"> The validation error when unsuccessful. </param>
    /// <returns> <see langword="true" /> when the contract represents a valid session; otherwise <see langword="false" />. </returns>
    public static bool TryCreate (
        DaemonSessionJsonContract contract,
        ProjectFingerprint expectedProjectFingerprint,
        string sourceDescription,
        DaemonSessionEndpointBindingFactory endpointBindingFactory,
        [NotNullWhen(true)] out DaemonSession? session,
        [NotNullWhen(false)] out ExecutionError? error)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(expectedProjectFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDescription);
        ArgumentNullException.ThrowIfNull(endpointBindingFactory);

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

        if (contract.ProjectFingerprint is null)
        {
            error = Invalid("projectFingerprint is missing.", sourceDescription);
            return false;
        }

        if (contract.ProjectFingerprint != expectedProjectFingerprint)
        {
            error = Invalid(
                $"projectFingerprint mismatch. Requested={expectedProjectFingerprint}, Actual={contract.ProjectFingerprint}.",
                sourceDescription);
            return false;
        }

        if (contract.EditorMode is not DaemonEditorMode editorMode)
        {
            error = Invalid("editorMode is missing.", sourceDescription);
            return false;
        }

        if (contract.OwnerKind is not DaemonSessionOwnerKind ownerKind)
        {
            error = Invalid("ownerKind is missing.", sourceDescription);
            return false;
        }

        if (contract.EndpointTransportKind is not IpcTransportKind transportKind)
        {
            error = Invalid("endpointTransportKind is missing.", sourceDescription);
            return false;
        }

        if (contract.OwnerProcessId is not int ownerProcessId)
        {
            error = Invalid("ownerProcessId is missing.", sourceDescription);
            return false;
        }

        try
        {
            var endpointBinding = endpointBindingFactory(
                    transportKind,
                    contract.EndpointAddress!)
                ?? throw new ArgumentException(
                    "Daemon session endpoint adapter returned no endpoint binding.",
                    nameof(endpointBindingFactory));

            session = new DaemonSession(
                contract.SessionGenerationId,
                sessionToken,
                contract.ProjectFingerprint,
                contract.IssuedAtUtc,
                editorMode,
                ownerKind,
                contract.CanShutdownProcess,
                endpointBinding.Contract,
                endpointBinding.UnixSocketPath,
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
            SessionGenerationId: session.SessionGenerationId,
            SessionToken: session.SessionToken.GetEncodedValue(),
            ProjectFingerprint: session.ProjectFingerprint,
            IssuedAtUtc: session.IssuedAtUtc,
            EditorMode: session.EditorMode,
            OwnerKind: session.OwnerKind,
            CanShutdownProcess: session.CanShutdownProcess,
            EndpointTransportKind: session.EndpointContract.TransportKind,
            EndpointAddress: session.EndpointContract.Address,
            ProcessId: session.ProcessId,
            ProcessStartedAtUtc: session.ProcessStartedAtUtc,
            OwnerProcessId: session.OwnerProcessId,
            EditorInstanceId: session.EditorInstanceId);
    }

    private static ExecutionError Invalid (string reason, string sourceDescription)
    {
        return ExecutionError.InvalidArgument($"Daemon session {reason} {sourceDescription}");
    }
}
