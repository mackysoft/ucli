namespace MackySoft.Tests;

using System.Diagnostics;
using System.Globalization;

internal static class TestProcessInvocations
{
    public static Process StartProcess (TestProcessInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);

        var startInfo = invocation.CreateStartInfo();
        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("Test process could not be started.");
    }

    public static Process StartLongRunningProcess ()
    {
        return StartProcess(CreateLongRunning());
    }

    public static TestProcessInvocation CreateLongRunning ()
    {
        return OperatingSystem.IsWindows()
            ? new TestProcessInvocation(
                "powershell",
                ["-NoProfile", "-Command", "Start-Sleep -Seconds 30"])
            : new TestProcessInvocation(
                "/bin/sh",
                ["-c", "sleep 30"]);
    }

    public static TestProcessInvocation CreateStandardOutput (string output)
    {
        ArgumentNullException.ThrowIfNull(output);

        return OperatingSystem.IsWindows()
            ? new TestProcessInvocation(
                "powershell",
                ["-NoProfile", "-Command", "Write-Output " + PowerShellSingleQuote(output)])
            : CreateUnixShellInvocation("printf '%s\\n' " + ShellSingleQuote(output));
    }

    public static TestProcessInvocation CreateUnixExitedParentWithInheritedOutputHandle (TimeSpan childLifetime)
    {
        ThrowIfWindowsShellUnavailable();
        ArgumentOutOfRangeException.ThrowIfLessThan(childLifetime, TimeSpan.Zero);

        return CreateUnixShellInvocation(
            "sleep " + childLifetime.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture) + " & exit 0");
    }

    public static TestProcessInvocation CreateUnixTermSignalIgnoredLoop ()
    {
        ThrowIfWindowsShellUnavailable();

        return CreateUnixShellInvocation("trap '' TERM; while :; do sleep 0.05; done");
    }

    public static TestProcessInvocation CreateUnixTermSignalMarkerLoop (string markerPath)
    {
        ThrowIfWindowsShellUnavailable();
        ArgumentException.ThrowIfNullOrWhiteSpace(markerPath);

        return CreateUnixShellInvocation(
            $"trap 'printf term > {TestShellPaths.QuoteBashArgument(markerPath)}; exit 0' TERM; while :; do sleep 0.01; done");
    }

    public static TestProcessInvocation CreateUnixReadyTermSignalMarkerLoop (
        string readyPath,
        string markerPath)
    {
        ThrowIfWindowsShellUnavailable();
        ArgumentException.ThrowIfNullOrWhiteSpace(readyPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(markerPath);

        return CreateUnixShellInvocation(
            $"trap 'printf term > {TestShellPaths.QuoteBashArgument(markerPath)}; exit 0' TERM; printf ready > {TestShellPaths.QuoteBashArgument(readyPath)}; while :; do sleep 0.01; done");
    }

    public static TestProcessInvocation CreateUnixReadyTermSignalMarkerPassiveExit (
        string readyPath,
        string markerPath)
    {
        ThrowIfWindowsShellUnavailable();
        ArgumentException.ThrowIfNullOrWhiteSpace(readyPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(markerPath);

        return CreateUnixShellInvocation(
            $"trap 'printf term > {TestShellPaths.QuoteBashArgument(markerPath)}; exit 0' TERM; printf ready > {TestShellPaths.QuoteBashArgument(readyPath)}; sleep 0.2; exit 0");
    }

    public static TestProcessInvocation CreateNonZeroExit (int exitCode = 42)
    {
        if (exitCode == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(exitCode), "The exit code must be non-zero.");
        }

        var exitCodeText = exitCode.ToString(CultureInfo.InvariantCulture);
        return OperatingSystem.IsWindows()
            ? new TestProcessInvocation(
                "cmd",
                ["/c", "exit /b " + exitCodeText])
            : new TestProcessInvocation(
                "/bin/sh",
                ["-c", "exit " + exitCodeText]);
    }

    private static TestProcessInvocation CreateUnixShellInvocation (string script)
    {
        return new TestProcessInvocation(
            "/bin/sh",
            ["-c", script]);
    }

    private static void ThrowIfWindowsShellUnavailable ()
    {
        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("This test process invocation requires a Unix shell.");
        }
    }

    private static string ShellSingleQuote (string value)
    {
        return TestShellPaths.QuoteBashArgument(value);
    }

    private static string PowerShellSingleQuote (string value)
    {
        return "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
    }
}
