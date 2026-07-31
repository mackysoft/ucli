
namespace MackySoft.Ucli.Contracts.Editor;

/// <summary> Identifies the action required to clear a Unity Editor lifecycle blocker. </summary>
[VocabularyDefinition]
public enum UnityEditorActionRequired
{
    /// <summary> Script compilation errors must be fixed. </summary>
    [VocabularyText("fixCompileErrors")]
    FixCompileErrors = 1,

    /// <summary> Unity package resolution errors must be resolved. </summary>
    [VocabularyText("resolvePackages")]
    ResolvePackages = 2,

    /// <summary> A blocking Unity dialog must be resolved. </summary>
    [VocabularyText("resolveUnityDialog")]
    ResolveUnityDialog = 3,

    /// <summary> The Unity log must be inspected to determine the required recovery action. </summary>
    [VocabularyText("inspectUnityLog")]
    InspectUnityLog = 4,
}
