namespace MackySoft.Ucli.Application.Features.Testing.Run.Common.Contracts;

/// <summary> Represents normalized test-run error kinds. </summary>
[VocabularyDefinition]
internal enum TestRunErrorKind
{
    /// <summary> Indicates invalid user input or contract violations. </summary>
    [VocabularyText("invalidInput")]
    InvalidInput = 0,

    /// <summary> Indicates infrastructure failures such as filesystem or dependency failures. </summary>
    [VocabularyText("infraError")]
    InfraError = 1,

    /// <summary> Indicates tool-level failures produced by Unity execution or conversion tools. </summary>
    [VocabularyText("toolError")]
    ToolError = 2,
}
