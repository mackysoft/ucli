
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines script-compilation states exposed by lifecycle-bearing IPC contracts. </summary>
[VocabularyDefinition]
public enum IpcCompileState
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
