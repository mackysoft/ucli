
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines stable dirty-state coverage literals for build probes. </summary>
[VocabularyDefinition]
public enum IpcBuildDirtyStateCoverage
{
    /// <summary> All configured dirty-state sources were checked. </summary>
    [VocabularyText("full")]
    Full = 1,

    /// <summary> At least one dirty-state source could not be checked completely. </summary>
    [VocabularyText("partial")]
    Partial = 2,
}
