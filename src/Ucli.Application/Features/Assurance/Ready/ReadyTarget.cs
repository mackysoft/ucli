
namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Defines readiness targets supported by the <c>ready</c> command. </summary>
[VocabularyDefinition]
internal enum ReadyTarget
{
    /// <summary> Verifies that request execution can be dispatched. </summary>
    [VocabularyText("execution")]
    Execution = 1,

    /// <summary> Verifies that mutation requests can be dispatched. </summary>
    [VocabularyText("mutation")]
    Mutation = 2,

    /// <summary> Verifies that Unity test execution can be dispatched. </summary>
    [VocabularyText("test")]
    Test = 3,

    /// <summary> Verifies that project-wide read-index artifacts satisfy the selected mode. </summary>
    [VocabularyText("readIndex")]
    ReadIndex = 4,
}
