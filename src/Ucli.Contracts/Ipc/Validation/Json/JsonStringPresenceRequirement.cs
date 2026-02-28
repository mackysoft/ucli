namespace MackySoft.Ucli.Contracts.Ipc.Validation;

/// <summary> Specifies presence requirements for JSON string properties. </summary>
internal enum JsonStringPresenceRequirement
{
    /// <summary> Property must be present and must be a JSON string. </summary>
    Required,

    /// <summary> Property may be absent, but must be a JSON string when specified. </summary>
    OptionalStrict,

    /// <summary> Property may be absent or non-string, and non-string values are treated as unspecified. </summary>
    OptionalLoose,
}