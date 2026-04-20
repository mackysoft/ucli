namespace MackySoft.Ucli.Features.Testing.Run.Common.Contracts;

/// <summary> Represents normalized test-run error kinds. </summary>
internal enum TestRunErrorKind
{
    /// <summary> Indicates invalid user input or contract violations. </summary>
    InvalidInput = 0,

    /// <summary> Indicates infrastructure failures such as filesystem or dependency failures. </summary>
    InfraError = 1,

    /// <summary> Indicates tool-level failures produced by Unity execution or conversion tools. </summary>
    ToolError = 2,
}