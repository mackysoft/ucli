namespace MackySoft.Ucli.Infrastructure.Paths;

/// <summary> Defines machine-readable path normalization failure kinds. </summary>
internal enum PathNormalizationFailureKind
{
    /// <summary> No failure occurred. </summary>
    None,

    /// <summary> The input path value was null, empty, or whitespace. </summary>
    EmptyPath,

    /// <summary> The input path value could not be converted to a full path. </summary>
    InvalidFormat,

    /// <summary> The normalized path is outside the repository root boundary. </summary>
    OutsideRepositoryRoot,
}
