namespace MackySoft.Ucli.Application.Shared.Execution;

/// <summary> Defines process exit codes requested by application use cases before CLI projection. </summary>
internal enum ApplicationExitCode
{
    /// <summary> Indicates successful command completion. </summary>
    Success = 0,

    /// <summary> Indicates completed test execution with failing tests. </summary>
    TestFailure = 1,

    /// <summary> Indicates infrastructure failures for test execution. </summary>
    InfrastructureError = 2,

    /// <summary> Indicates invalid input or argument validation failures. </summary>
    InvalidArgument = 3,

    /// <summary> Indicates tool-level execution failures. </summary>
    ToolError = 4,
}
