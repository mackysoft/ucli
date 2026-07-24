
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines the closed set of IPC response outcomes. </summary>
[VocabularyDefinition]
public enum IpcResponseStatus
{
    /// <summary> Indicates that request processing completed successfully. </summary>
    [VocabularyText("ok")]
    Ok = 1,

    /// <summary> Indicates that request processing failed with one or more errors. </summary>
    [VocabularyText("error")]
    Error = 2,
}
