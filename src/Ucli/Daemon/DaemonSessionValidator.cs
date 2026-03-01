using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Daemon;

/// <summary> Implements contract validation for daemon session models. </summary>
internal sealed class DaemonSessionValidator : IDaemonSessionValidator
{
    /// <summary> Validates one daemon session model. </summary>
    /// <param name="session"> The daemon session model. </param>
    /// <param name="sessionPath"> The related session JSON path for diagnostics. </param>
    /// <returns> The structured error when validation fails; otherwise <see langword="null" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="session" /> is <see langword="null" />. </exception>
    public ExecutionError? Validate (
        DaemonSession session,
        string sessionPath)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.SchemaVersion != DaemonSession.CurrentSchemaVersion)
        {
            return ExecutionError.InvalidArgument(
                $"Daemon session schemaVersion must be {DaemonSession.CurrentSchemaVersion}. Actual: {session.SchemaVersion}. {sessionPath}");
        }

        if (string.IsNullOrWhiteSpace(session.SessionToken)
            || string.IsNullOrWhiteSpace(session.ProjectFingerprint)
            || string.IsNullOrWhiteSpace(session.RuntimeKind)
            || string.IsNullOrWhiteSpace(session.OwnerKind)
            || string.IsNullOrWhiteSpace(session.EndpointTransportKind)
            || string.IsNullOrWhiteSpace(session.EndpointAddress))
        {
            return ExecutionError.InvalidArgument($"Daemon session contains required empty values: {sessionPath}");
        }

        if (!DaemonSessionTransportKindCodec.TryParse(session.EndpointTransportKind, out _))
        {
            return ExecutionError.InvalidArgument(
                $"Daemon session endpointTransportKind is invalid: {session.EndpointTransportKind}. {sessionPath}");
        }

        return null;
    }
}