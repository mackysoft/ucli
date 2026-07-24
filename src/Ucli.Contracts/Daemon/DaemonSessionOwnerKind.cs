
namespace MackySoft.Ucli.Contracts.Daemon;

/// <summary> Defines supported daemon session owner kinds. </summary>
[VocabularyDefinition]
public enum DaemonSessionOwnerKind
{
    /// <summary> The daemon session is owned by uCLI. </summary>
    [VocabularyText("cli")]
    Cli = 0,

    /// <summary> The daemon session is owned by the user-controlled Unity Editor process. </summary>
    [VocabularyText("user")]
    User = 1,
}
