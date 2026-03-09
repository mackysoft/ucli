using System.Diagnostics;
using MackySoft.Tests;
using MackySoft.Ucli.Cli;

namespace MackySoft.Ucli.Tests;

public sealed class DaemonCliOutputContractTests
{
    private const string UnknownOptionMessage = "Argument '--unknown' is not recognized.";

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Status_WithProjectPath_ReturnsNotRunningJsonContractAsSingleJson ()
    {
        using var scope = TestDirectories.CreateTempScope("cli-output-contract", "daemon-status-success");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");

        var result = await CliProcessRunner.RunCommand(
            UcliCommandNames.Daemon,
            UcliCommandNames.Status,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.DaemonStatus,
            status: "ok",
            exitCode: (int)CliExitCode.Success);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);

        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("daemonStatus", "notRunning")
                .HasInt32("timeoutMilliseconds", UcliContractConstants.Config.IpcTimeoutDefaultDaemonStatusMilliseconds)
                .IsNull("session")
                .IsNull("diagnosis"));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Stop_WithProjectPath_WhenNotRunning_ReturnsSuccessJsonContractAsSingleJson ()
    {
        using var scope = TestDirectories.CreateTempScope("cli-output-contract", "daemon-stop-success");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");

        var result = await CliProcessRunner.RunCommand(
            UcliCommandNames.Daemon,
            UcliCommandNames.StopSubcommand,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.DaemonStop,
            status: "ok",
            exitCode: (int)CliExitCode.Success);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);

        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("stopStatus", "notRunning")
                .HasString("daemonStatus", "notRunning")
                .HasInt32("timeoutMilliseconds", UcliContractConstants.Config.IpcTimeoutDefaultDaemonStopMilliseconds)
                .IsNull("session"));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Start_WithUnknownOption_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        var result = await CliProcessRunner.RunCommand(
            UcliCommandNames.Daemon,
            UcliCommandNames.StartSubcommand,
            UcliContractConstants.CliOption.Unknown);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.DaemonStart,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
        Assert.Contains(UnknownOptionMessage, result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Stop_WithUnknownOption_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        var result = await CliProcessRunner.RunCommand(
            UcliCommandNames.Daemon,
            UcliCommandNames.StopSubcommand,
            UcliContractConstants.CliOption.Unknown);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.DaemonStop,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
        Assert.Contains(UnknownOptionMessage, result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Status_WithUnknownOption_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        var result = await CliProcessRunner.RunCommand(
            UcliCommandNames.Daemon,
            UcliCommandNames.Status,
            UcliContractConstants.CliOption.Unknown);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.DaemonStatus,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
        Assert.Contains(UnknownOptionMessage, result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task List_WithProjectPath_WhenNoDaemonSessionExists_ReturnsSuccessJsonContractAsSingleJson ()
    {
        using var scope = TestDirectories.CreateTempScope("cli-output-contract", "daemon-list-success");
        InitializeGitRepository(scope);
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");

        var result = await CliProcessRunner.RunCommand(
            UcliCommandNames.Daemon,
            UcliCommandNames.ListSubcommand,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.DaemonList,
            status: "ok",
            exitCode: (int)CliExitCode.Success);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);

        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasInt32("timeoutMilliseconds", UcliContractConstants.Config.IpcTimeoutDefaultDaemonListMilliseconds)
                .HasString("projectRelativePath", "UnityProject")
                .HasBoolean("isComplete", true)
                .IsNull("completionReason")
                .HasInt32("remainingWorktreeCount", 0)
                .HasArrayLength("items", 0));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task List_WithUnknownOption_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        var result = await CliProcessRunner.RunCommand(
            UcliCommandNames.Daemon,
            UcliCommandNames.ListSubcommand,
            UcliContractConstants.CliOption.Unknown);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.DaemonList,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
        Assert.Contains(UnknownOptionMessage, result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Daemon_WithoutSubcommand_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        var result = await CliProcessRunner.RunCommand(UcliCommandNames.Daemon);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.Daemon,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Daemon_WithUnknownOption_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        var result = await CliProcessRunner.RunCommand(
            UcliCommandNames.Daemon,
            UcliContractConstants.CliOption.Unknown);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.Daemon,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Daemon_WithUnknownSubcommand_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        var result = await CliProcessRunner.RunCommand(
            UcliCommandNames.Daemon,
            "foo");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.Daemon,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Daemon_WithHelpOption_ReturnsHelpOutputAndSuccessExitCode ()
    {
        var result = await CliProcessRunner.RunCommand(
            UcliCommandNames.Daemon,
            "--help");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("daemon start", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Daemon_WithVersionOption_ReturnsVersionOutputAndSuccessExitCode ()
    {
        var result = await CliProcessRunner.RunCommand(
            UcliCommandNames.Daemon,
            "--version");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Matches(@"^\d+\.\d+\.\d+([-\+].*)?$", result.StdOut.Trim());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task DaemonStart_WithHelpOutput_IncludesShortProjectPathOption ()
    {
        var result = await CliProcessRunner.RunCommand(
            UcliCommandNames.Daemon,
            UcliCommandNames.StartSubcommand,
            "--help");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("-p, --projectPath", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task DaemonStop_WithHelpOutput_IncludesShortProjectPathOption ()
    {
        var result = await CliProcessRunner.RunCommand(
            UcliCommandNames.Daemon,
            UcliCommandNames.StopSubcommand,
            "--help");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("-p, --projectPath", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task DaemonStatus_WithHelpOutput_IncludesShortProjectPathOption ()
    {
        var result = await CliProcessRunner.RunCommand(
            UcliCommandNames.Daemon,
            UcliCommandNames.Status,
            "--help");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("-p, --projectPath", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task DaemonList_WithHelpOutput_IncludesShortProjectPathOption ()
    {
        var result = await CliProcessRunner.RunCommand(
            UcliCommandNames.Daemon,
            UcliCommandNames.ListSubcommand,
            "--help");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("-p, --projectPath", result.StdOut, StringComparison.Ordinal);
    }

    private static void InitializeGitRepository (TestDirectoryScope scope)
    {
        RunGit(scope.FullPath, "init");
        RunGit(scope.FullPath, "config", "user.email", "ucli-tests@example.com");
        RunGit(scope.FullPath, "config", "user.name", "ucli-tests");
        RunGit(scope.FullPath, "commit", "--allow-empty", "-m", "initial");
    }

    private static void RunGit (
        string workingDirectory,
        params string[] arguments)
    {
        using var process = new Process();
        var startInfo = process.StartInfo;
        startInfo.FileName = "git";
        startInfo.WorkingDirectory = workingDirectory;
        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.CreateNoWindow = true;
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        Assert.True(process.Start(), "Failed to start git process.");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(
            process.ExitCode == 0,
            $"Git command failed. Args={string.Join(' ', arguments)} stdout={standardOutput} stderr={standardError}");
    }
}