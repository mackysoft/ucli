
namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Identifies a verifier step in a verify profile. </summary>
[VocabularyDefinition]
public enum VerifyStepKind
{
    /// <summary> Evaluates Unity readiness. </summary>
    [VocabularyText("ready")]
    Ready = 1,

    /// <summary> Runs compile assurance. </summary>
    [VocabularyText("compile")]
    Compile = 2,

    /// <summary> Evaluates post-read operation evidence. </summary>
    [VocabularyText("postRead")]
    PostRead = 3,

    /// <summary> Runs Unity tests. </summary>
    [VocabularyText("test")]
    Test = 4,

    /// <summary> Collects Unity logs after non-passing claims. </summary>
    [VocabularyText("logs")]
    Logs = 5,
}
