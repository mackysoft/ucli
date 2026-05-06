using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;
using MackySoft.Ucli.Contracts.Ipc.Validation;

namespace MackySoft.Ucli.Contracts.Ipc.Authorization;

/// <summary> Provides session-token contract reader for daemon <c>session.json</c> payloads. </summary>
public static class SessionTokenContractReader
{
    /// <summary> Tries to read one required <c>sessionToken</c> string value from the root JSON object. </summary>
    /// <param name="root"> The source root JSON element. </param>
    /// <param name="token"> The parsed session-token string when operation succeeds. </param>
    /// <param name="error"> The machine-readable read error when operation fails. </param>
    /// <returns> <see langword="true" /> when session token is present and valid; otherwise <see langword="false" />. </returns>
    public static bool TryReadSessionToken (
        JsonElement root,
        out string token,
        out SessionTokenReadError error)
    {
        token = string.Empty;
        if (root.ValueKind != JsonValueKind.Object)
        {
            error = new SessionTokenReadError(
                IsRootTypeMismatch: true,
                JsonStringReadErrorKind: JsonStringReadErrorKind.None);
            return false;
        }

        if (!JsonStringContractReader.TryRead(
            jsonObject: root,
            propertyName: "sessionToken",
            presenceRequirement: JsonStringPresenceRequirement.Required,
            rejectEmptyOrWhitespace: true,
            rejectOuterWhitespace: false,
            value: out var parsedToken,
            error: out var readError))
        {
            error = new SessionTokenReadError(
                IsRootTypeMismatch: false,
                JsonStringReadErrorKind: readError.Kind);
            return false;
        }

        token = parsedToken!;
        error = SessionTokenReadError.None;
        return true;
    }
}
