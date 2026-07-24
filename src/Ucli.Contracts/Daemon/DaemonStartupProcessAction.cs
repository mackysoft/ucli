
namespace MackySoft.Ucli.Contracts.Daemon;

/// <summary> Defines daemon startup process-action literals. </summary>
[VocabularyDefinition]
public enum DaemonStartupProcessAction
{
    /// <summary> No Unity process action was required. </summary>
    [VocabularyText("none")]
    None = 0,

    /// <summary> The Unity process was preserved. </summary>
    [VocabularyText("kept")]
    Kept = 1,

    /// <summary> The Unity process was terminated. </summary>
    [VocabularyText("terminated")]
    Terminated = 2,

    /// <summary> The process action outcome is unknown. </summary>
    [VocabularyText("unknown")]
    Unknown = 3,
}
