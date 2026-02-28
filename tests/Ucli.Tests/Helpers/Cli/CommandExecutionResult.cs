namespace MackySoft.Tests;

internal readonly record struct CommandExecutionResult (
    int ExitCode,
    string StdOut,
    string StdErr);