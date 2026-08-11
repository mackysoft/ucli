namespace MackySoft.Ucli.Contracts.Projects;

/// <summary>
/// Defines the active color space setting of a Unity project, not the color space of an image or video encoding.
/// </summary>
[VocabularyDefinition]
public enum UnityProjectColorSpace
{
    /// <summary>Uses the gamma Unity project color space.</summary>
    [VocabularyText("gamma")]
    Gamma = 1,

    /// <summary>Uses the linear Unity project color space.</summary>
    [VocabularyText("linear")]
    Linear = 2,
}
