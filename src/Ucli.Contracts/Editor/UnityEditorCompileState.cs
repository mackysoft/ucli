
namespace MackySoft.Ucli.Contracts.Editor;

/// <summary> Defines normalized Unity Editor script-compilation states. </summary>
[VocabularyDefinition]
public enum UnityEditorCompileState
{
    /// <summary> Script compilation is inactive and no compile failure is reported. </summary>
    [VocabularyText("ready")]
    Ready = 0,

    /// <summary> Script compilation is active. </summary>
    [VocabularyText("compiling")]
    Compiling = 1,

    /// <summary> The latest completed script compilation failed. </summary>
    [VocabularyText("failed")]
    Failed = 2,
}
