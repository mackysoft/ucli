
namespace MackySoft.Ucli.Contracts.Testing;

/// <summary> Defines the finite outcomes reported for one completed test case. </summary>
[VocabularyDefinition]
public enum TestCaseResult
{
    /// <summary> The test case passed. </summary>
    [VocabularyText("pass")]
    Pass = 1,

    /// <summary> The test case failed. </summary>
    [VocabularyText("fail")]
    Fail = 2,

    /// <summary> The test case was skipped. </summary>
    [VocabularyText("skipped")]
    Skipped = 3,

    /// <summary> The test case did not produce a conclusive result. </summary>
    [VocabularyText("inconclusive")]
    Inconclusive = 4,
}
