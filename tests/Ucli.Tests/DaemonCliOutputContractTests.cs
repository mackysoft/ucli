using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Infrastructure.Project;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Tests;

public sealed class DaemonCliOutputContractTests
{
    private const string UnknownOptionMessage = "Argument '--unknown' is not recognized.";

    private const int DaemonListContractTestTimeoutMilliseconds = 15000;

    private static readonly TimeSpan ProcessExitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Status_WithProjectPath_ReturnsNotRunningJsonContractAsSingleJson ()
    {
        using var scope = TestDirectories.CreateTempScope("cli-output-contract", "daemon-status-success");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");

        var result = await CliProcessRunner.RunCommandAsync(
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
                .IsNull("serverVersion")
                .IsNull("editorMode")
                .IsNull("lifecycleState")
                .IsNull("blockingReason")
                .IsNull("compileState")
                .IsNull("compileGeneration")
                .IsNull("domainReloadGeneration")
                .HasBoolean("canAcceptExecutionRequests", false)
                .HasInt32("timeoutMilliseconds", UcliContractConstants.Config.IpcTimeoutDefaultDaemonStatusMilliseconds)
                .IsNull("session")
                .IsNull("diagnosis"));
        Assert.False(outputJson.RootElement.GetProperty("payload").TryGetProperty("runtime", out _));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Stop_WithProjectPath_WhenNotRunning_ReturnsSuccessJsonContractAsSingleJson ()
    {
        using var scope = TestDirectories.CreateTempScope("cli-output-contract", "daemon-stop-success");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");

        var result = await CliProcessRunner.RunCommandAsync(
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
    public async Task Cleanup_WithProjectPath_WhenNoDaemonSessionExists_ReturnsSkippedJsonContractAsSingleJson ()
    {
        using var scope = TestDirectories.CreateTempScope("cli-output-contract", "daemon-cleanup-success");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");

        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Daemon,
            UcliCommandNames.CleanupSubcommand,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.DaemonCleanup,
            status: "ok",
            exitCode: (int)CliExitCode.Success);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);

        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("cleanupStatus", "skipped")
                .HasString("skipReason", "uncertainReachability")
                .HasInt32("timeoutMilliseconds", UcliContractConstants.Config.IpcTimeoutDefaultDaemonCleanupMilliseconds));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Cleanup_WithProjectPath_WhenUnsafeInvalidSessionExists_ReturnsSkippedJsonContractAsSingleJson ()
    {
        using var scope = TestDirectories.CreateTempScope("cli-output-contract", "daemon-cleanup-skipped");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        WriteUnsafeInvalidSession(unityProjectPath);

        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Daemon,
            UcliCommandNames.CleanupSubcommand,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.DaemonCleanup,
            status: "ok",
            exitCode: (int)CliExitCode.Success);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);

        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("cleanupStatus", "skipped")
                .HasString("skipReason", "unsafeInvalidSession")
                .HasInt32("timeoutMilliseconds", UcliContractConstants.Config.IpcTimeoutDefaultDaemonCleanupMilliseconds));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Start_WithUnknownOption_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
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
    public async Task Start_WithInvalidEditorMode_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Daemon,
            UcliCommandNames.StartSubcommand,
            UcliContractConstants.CliOption.EditorMode,
            "unsupported");

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
        Assert.Contains("editorMode must be one of", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Start_WithEditorModeBatchmode_WhenUnityPluginMarkerIsMissing_DoesNotRejectEditorModeOption ()
    {
        using var scope = TestDirectories.CreateTempScope("cli-output-contract", "daemon-start-batchmode-option");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");

        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Daemon,
            UcliCommandNames.StartSubcommand,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath,
            UcliContractConstants.CliOption.EditorMode,
            "batchmode");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.DaemonStart,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
        Assert.Contains("Unity project does not contain the uCLI Unity plugin", result.StdOut, StringComparison.Ordinal);
        Assert.DoesNotContain("editorMode must be one of", result.StdOut, StringComparison.Ordinal);
        Assert.DoesNotContain("Argument '--editorMode' is not recognized.", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Start_WithEditorModeGui_WhenUnityPluginMarkerExists_AttemptsGuiLaunchAndReturnsSingleJson ()
    {
        using var scope = TestDirectories.CreateTempScope("cli-output-contract", "daemon-start-gui-option");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(
            scope,
            "UnityProject",
            "m_EditorVersion: 0.0.0-ucli-test-missing");
        await UnityProjectTestFactory.WriteUcliUnityPluginMarkerAsync(scope, "UnityProject");

        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Daemon,
            UcliCommandNames.StartSubcommand,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath,
            UcliContractConstants.CliOption.Timeout,
            "30000",
            UcliContractConstants.CliOption.EditorMode,
            "gui");
        await WaitForSupervisorIdleExitAsync(scope, unityProjectPath);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.DaemonStart,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
        Assert.Contains("Unity Editor is not installed for unityVersion", result.StdOut, StringComparison.Ordinal);
        Assert.DoesNotContain("daemon start --editorMode gui is not implemented", result.StdOut, StringComparison.Ordinal);
        Assert.DoesNotContain("editorMode must be one of", result.StdOut, StringComparison.Ordinal);
        Assert.DoesNotContain("Argument '--editorMode' is not recognized.", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Stop_WithUnknownOption_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
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
    public async Task Cleanup_WithUnknownOption_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Daemon,
            UcliCommandNames.CleanupSubcommand,
            UcliContractConstants.CliOption.Unknown);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.DaemonCleanup,
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
        var result = await CliProcessRunner.RunCommandAsync(
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
        await InitializeGitRepositoryAsync(scope);
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");

        var result = await CliProcessRunner.RunCommandWithWorkingDirectoryAsync(
            scope.FullPath,
            UcliCommandNames.Daemon,
            UcliCommandNames.ListSubcommand,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath,
            UcliContractConstants.CliOption.Timeout,
            DaemonListContractTestTimeoutMilliseconds.ToString(CultureInfo.InvariantCulture));

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
                .HasInt32("timeoutMilliseconds", DaemonListContractTestTimeoutMilliseconds)
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
        var result = await CliProcessRunner.RunCommandAsync(
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
        var result = await CliProcessRunner.RunCommandAsync(UcliCommandNames.Daemon);

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
        var result = await CliProcessRunner.RunCommandAsync(
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
        var result = await CliProcessRunner.RunCommandAsync(
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
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Daemon,
            "--help");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("daemon start", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("daemon cleanup", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Daemon_WithVersionOption_ReturnsVersionOutputAndSuccessExitCode ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Daemon,
            "--version");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Matches(@"^\d+\.\d+\.\d+([-\+].*)?$", result.StdOut.Trim());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task DaemonStart_WithHelpOutput_IncludesShortProjectPathOption ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Daemon,
            UcliCommandNames.StartSubcommand,
            "--help");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("-p, --projectPath", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task DaemonStart_WithHelpOutput_IncludesEditorModeOption ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Daemon,
            UcliCommandNames.StartSubcommand,
            "--help");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("--editorMode", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task DaemonStop_WithHelpOutput_IncludesShortProjectPathOption ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
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
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Daemon,
            UcliCommandNames.Status,
            "--help");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("-p, --projectPath", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task DaemonCleanup_WithHelpOutput_IncludesShortProjectPathOption ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Daemon,
            UcliCommandNames.CleanupSubcommand,
            "--help");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("-p, --projectPath", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task DaemonList_WithHelpOutput_IncludesShortProjectPathOption ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Daemon,
            UcliCommandNames.ListSubcommand,
            "--help");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("-p, --projectPath", result.StdOut, StringComparison.Ordinal);
    }

    private static Task InitializeGitRepositoryAsync (TestDirectoryScope scope)
    {
        return RunGitAsync(scope.FullPath, "init");
    }

    private static async Task RunGitAsync (
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
        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        await TestProcessAwaiter.WaitForExitAsync(process, "Git process", ProcessExitTimeout);
        var standardOutput = await TestAwaiter.WaitAsync(standardOutputTask, "Git stdout read", ProcessExitTimeout);
        var standardError = await TestAwaiter.WaitAsync(standardErrorTask, "Git stderr read", ProcessExitTimeout);

        Assert.True(
            process.ExitCode == 0,
            $"Git command failed. Args={string.Join(' ', arguments)} stdout={standardOutput} stderr={standardError}");
    }

    private static async Task WaitForSupervisorIdleExitAsync (
        TestDirectoryScope scope,
        string unityProjectPath)
    {
        var storageRoot = UcliStoragePathResolver.ResolveStorageRoot(Path.GetFullPath(unityProjectPath));
        var manifestPath = UcliStoragePathResolver.ResolveSupervisorManifestPath(storageRoot);
        var deadline = TimeProvider.System.GetUtcNow().AddSeconds(15);

        while (File.Exists(manifestPath) && TimeProvider.System.GetUtcNow() < deadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        if (File.Exists(manifestPath))
        {
            // NOTE: The GUI-not-implemented path intentionally reaches the supervisor in #250.
            // Preserve the scope so a Windows working-directory lock does not hide the real timeout failure.
            scope.Preserve();
            Assert.Fail($"Supervisor did not exit after becoming idle. Manifest={manifestPath}");
        }
    }

    private static void WriteUnsafeInvalidSession (string unityProjectPath)
    {
        var normalizedProjectPath = Path.GetFullPath(unityProjectPath);
        var storageRoot = UcliStoragePathResolver.ResolveStorageRoot(normalizedProjectPath);
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(storageRoot, normalizedProjectPath);
        var sessionPath = UcliStoragePathResolver.ResolveSessionPath(storageRoot, projectFingerprint);
        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath)!);

        using var currentProcess = Process.GetCurrentProcess();
        var issuedAtUtc = new DateTimeOffset(currentProcess.StartTime.ToUniversalTime()).AddSeconds(1);
        var json = JsonSerializer.Serialize(
            new
            {
                schemaVersion = DaemonSession.CurrentSchemaVersion,
                sessionToken = "session-token",
                projectFingerprint,
                issuedAtUtc,
                editorMode = DaemonEditorModeValues.Batchmode,
                ownerKind = DaemonSessionOwnerKindValues.Cli,
                canShutdownProcess = true,
                endpointTransportKind = "namedPipe",
                endpointAddress = "ucli-cleanup-test",
                processId = Environment.ProcessId,
                ownerProcessId = (int?)null,
            }) + Environment.NewLine;
        File.WriteAllText(sessionPath, json);
    }
}
