
namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Identifies the verifier behavior represented by an assurance verifier entry. </summary>
[VocabularyDefinition]
public enum AssuranceVerifierKind
{
    /// <summary> Evaluates Unity readiness. </summary>
    [VocabularyText("ready")]
    Ready = 1,

    /// <summary> Evaluates Unity script compilation. </summary>
    [VocabularyText("compile")]
    Compile = 2,

    /// <summary> Evaluates a Unity player build. </summary>
    [VocabularyText("build")]
    Build = 3,

    /// <summary> Evaluates the read surface after mutation. </summary>
    [VocabularyText("postRead")]
    PostRead = 4,

    /// <summary> Evaluates Unity tests. </summary>
    [VocabularyText("test")]
    Test = 5,

    /// <summary> Captures Unity logs as assurance evidence. </summary>
    [VocabularyText("logs")]
    Logs = 6,
}
