
namespace MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

/// <summary> Defines build profile runtime execution-mode literals. </summary>
[VocabularyDefinition]
internal enum BuildProfileRuntimeExecutionMode
{
    /// <summary> Allows daemon execution. </summary>
    [VocabularyText("daemon")]
    Daemon = 0,

    /// <summary> Allows oneshot execution. </summary>
    [VocabularyText("oneshot")]
    Oneshot = 1,
}
