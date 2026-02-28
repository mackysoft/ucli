namespace MackySoft.Ucli.Contracts.Ipc.Validation;

/// <summary> Represents one JSON string-property read error. </summary>
/// <param name="Kind"> The machine-readable error kind. </param>
/// <param name="PropertyName"> The property name that failed contract validation. </param>
internal readonly record struct JsonStringReadError (
    JsonStringReadErrorKind Kind,
    string PropertyName)
{
    /// <summary> Gets an empty error value that indicates success. </summary>
    public static JsonStringReadError None => new(JsonStringReadErrorKind.None, string.Empty);
}