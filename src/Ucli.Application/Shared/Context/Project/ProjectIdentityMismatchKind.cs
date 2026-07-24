
namespace MackySoft.Ucli.Application.Shared.Context.Project;

/// <summary> Identifies the component of a Unity host project identity that differs from the requested project. </summary>
[VocabularyDefinition]
internal enum ProjectIdentityMismatchKind
{
    /// <summary> The project fingerprint differs. </summary>
    [VocabularyText("projectFingerprint")]
    ProjectFingerprint = 1,

    /// <summary> The Unity project root path differs. </summary>
    [VocabularyText("projectPath")]
    ProjectPath = 2,

    /// <summary> The Unity editor version differs. </summary>
    [VocabularyText("unityVersion")]
    UnityVersion = 3,
}
