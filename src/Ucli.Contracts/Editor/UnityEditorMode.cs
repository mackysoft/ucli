
namespace MackySoft.Ucli.Contracts.Editor;

/// <summary> Defines supported Unity Editor hosting modes. </summary>
[VocabularyDefinition]
public enum UnityEditorMode
{
    /// <summary> Unity Editor running in batchmode. </summary>
    [VocabularyText("batchmode")]
    Batchmode = 1,

    /// <summary> Unity Editor running with the graphical user interface. </summary>
    [VocabularyText("gui")]
    Gui = 2,
}
