
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines supported IPC transport kinds. </summary>
[VocabularyDefinition]
public enum IpcTransportKind
{
    /// <summary> Uses Windows named pipe transport. </summary>
    [VocabularyText("namedPipe")]
    NamedPipe = 0,

    /// <summary> Uses Unix domain socket transport. </summary>
    [VocabularyText("unixDomainSocket")]
    UnixDomainSocket = 1,
}
