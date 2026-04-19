namespace MackySoft.Ucli.Features.Testing.Run;

/// <summary> Defines process exit codes for test-run execution results. </summary>
internal enum TestRunExitCode
{
    /// <summary> Indicates test execution completed with passing tests. </summary>
    Pass = 0,

    /// <summary> Indicates test execution completed with failing tests. </summary>
    Fail = 1,

    /// <summary> Indicates infrastructure failures. </summary>
    InfraError = 2,

    /// <summary> Indicates invalid input failures. </summary>
    InvalidInput = 3,

    /// <summary> Indicates tool-level execution failures. </summary>
    ToolError = 4,
}