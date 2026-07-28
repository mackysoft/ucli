namespace MackySoft.Ucli.Application.Features.Testing.Run.Common.Contracts;

/// <summary> Represents normalized test-run result kinds. </summary>
[VocabularyDefinition]
internal enum TestRunResultKind
{
    /// <summary> Indicates that all tests passed. </summary>
    [VocabularyText("pass")]
    Pass = 0,

    /// <summary> Indicates that one or more tests failed. </summary>
    [VocabularyText("fail")]
    Fail = 1,
}
