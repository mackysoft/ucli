namespace MackySoft.Tests;

internal readonly record struct TestProcessResult (
    int ExitCode,
    string StdOut,
    string StdErr);
