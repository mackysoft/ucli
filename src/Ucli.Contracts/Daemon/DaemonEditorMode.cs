
namespace MackySoft.Ucli.Contracts.Daemon;

/// <summary> Defines supported daemon Editor modes. </summary>
[VocabularyDefinition]
public enum DaemonEditorMode
{
    /// <summary> Unity Editor running in batchmode. </summary>
    [VocabularyText("batchmode")]
    Batchmode = 1,

    /// <summary> Unity Editor running with the graphical user interface. </summary>
    [VocabularyText("gui")]
    Gui = 2,
}
