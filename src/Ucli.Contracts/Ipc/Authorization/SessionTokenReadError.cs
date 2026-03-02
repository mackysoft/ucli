using MackySoft.Ucli.Contracts.Ipc.Validation;

namespace MackySoft.Ucli.Contracts.Ipc.Authorization;

/// <summary> Represents one session-token contract read error. </summary>
/// <param name="IsRootTypeMismatch"> Whether root element is not a JSON object. </param>
/// <param name="JsonStringReadErrorKind"> The JSON string-contract error kind when root shape is valid. </param>
public readonly record struct SessionTokenReadError (
    bool IsRootTypeMismatch,
    JsonStringReadErrorKind JsonStringReadErrorKind)
{
    /// <summary> Gets an empty error value that indicates success. </summary>
    public static SessionTokenReadError None => new(false, JsonStringReadErrorKind.None);
}