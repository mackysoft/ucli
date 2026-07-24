
namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Defines <c>build.log.entry</c> source literals. </summary>
[VocabularyDefinition]
public enum BuildLogEntrySource
{
    /// <summary> Unity log stream entry source. </summary>
    [VocabularyText("unityLog")]
    UnityLog = 1,

    /// <summary> Application-side ucli entry source. </summary>
    [VocabularyText("ucli")]
    Ucli = 2,
}
