using System.Diagnostics;
using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Cli;

namespace MackySoft.Ucli.Tests;

public sealed class CliOutputContractTests
{
    private const string ConfigFileName = "config.json";

    private const string GitIgnoreFileName = ".gitignore";

    private const string LegacyConfigJson = """{"schemaVersion":999}""";

    private const string LegacyGitIgnoreContent = "legacy/";

    private const string UcliDirectoryName = ".ucli";

    private const string LocalDirectoryName = "local";

    private const string UnknownOptionMessage = "Argument '--unknown' is not recognized.";

    private const string InitProjectPathOptionMessage = "Argument '--projectPath' is not recognized.";

    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(15);

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Status_ReturnsNotImplementedErrorAsSingleJson ()
    {
        var result = await RunToolAsync(UcliCommandNames.Status);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        AssertCommandResultCommon(
            outputJson.RootElement,
            command: UcliCommandNames.Status,
            status: CliProtocol.StatusError,
            exitCode: (int)CliExitCode.ToolError);
        AssertSingleError(
            outputJson.RootElement,
            expectedCode: ErrorCodes.CommandNotImplemented);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(false, "init-success")]
    [InlineData(true, "init-force-success")]
    public async Task Init_ReturnsSuccessJsonContractAsSingleJson (bool force, string scopeName)
    {
        using var scope = TestDirectories.CreateTempScope("cli-output-contract", scopeName);
        var (workingDirectoryPath, _, localDirectoryPath, configPath, gitIgnorePath) = CreateInitTargetPaths(scope, "workspace");
        if (force)
        {
            PrepareLegacyTemplateFiles(scope, configPath, gitIgnorePath);
        }

        var result = await RunInitAsync(force, workingDirectoryPath);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        AssertCommandResultCommon(
            outputJson.RootElement,
            command: UcliCommandNames.Init,
            status: CliProtocol.StatusOk,
            exitCode: (int)CliExitCode.Success);
        AssertNoErrors(outputJson.RootElement);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasValueKind("configPath", JsonValueKind.String)
                .HasValueKind("gitignorePath", JsonValueKind.String));

        var payload = outputJson.RootElement.GetProperty("payload");
        var payloadPropertyCount = 0;
        foreach (var _ in payload.EnumerateObject())
        {
            payloadPropertyCount++;
        }

        Assert.Equal(2, payloadPropertyCount);
        FileSystemAssert.ForPath(payload
            .GetProperty("configPath")
            .GetString()!)
            .IsRooted()
            .HasFileName(ConfigFileName);
        FileSystemAssert.ForFile(payload
            .GetProperty("configPath")
            .GetString()!)
            .Exists();
        FileSystemAssert.ForPath(payload
            .GetProperty("gitignorePath")
            .GetString()!)
            .IsRooted()
            .HasFileName(GitIgnoreFileName);
        FileSystemAssert.ForFile(payload
            .GetProperty("gitignorePath")
            .GetString()!)
            .Exists();

        FileSystemAssert.ForDirectory(localDirectoryPath).Exists();
        FileSystemAssert.ForFile(configPath).Exists();
        FileSystemAssert.ForFile(gitIgnorePath).Exists();
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Init_AlwaysCreatesUnderCurrentWorkingDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("cli-output-contract", "init-cwd");
        var (workingDirectoryPath, ucliDirectoryPath, localDirectoryPath, configPath, gitIgnorePath) = CreateInitTargetPaths(scope, "workspace");

        await RunInitAsync(force: false, workingDirectoryPath);

        FileSystemAssert.ForDirectory(ucliDirectoryPath).Exists();
        FileSystemAssert.ForDirectory(localDirectoryPath).Exists();
        FileSystemAssert.ForFile(configPath).Exists();
        FileSystemAssert.ForFile(gitIgnorePath).Exists();
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(false, "init-default-config-values")]
    [InlineData(true, "init-force-config-overwrite")]
    public async Task Init_WritesDefaultConfigValues (bool force, string scopeName)
    {
        using var scope = TestDirectories.CreateTempScope("cli-output-contract", scopeName);
        var (workingDirectoryPath, _, _, configPath, gitIgnorePath) = CreateInitTargetPaths(scope, "workspace");
        if (force)
        {
            PrepareLegacyTemplateFiles(scope, configPath, gitIgnorePath);
        }

        await RunInitAsync(force, workingDirectoryPath);

        AssertDefaultConfigValues(configPath);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(false, "init-gitignore-contents")]
    [InlineData(true, "init-force-gitignore-overwrite")]
    public async Task Init_WritesLocalOnlyGitIgnoreContents (bool force, string scopeName)
    {
        using var scope = TestDirectories.CreateTempScope("cli-output-contract", scopeName);
        var (workingDirectoryPath, _, _, configPath, gitIgnorePath) = CreateInitTargetPaths(scope, "workspace");
        if (force)
        {
            PrepareLegacyTemplateFiles(scope, configPath, gitIgnorePath);
        }

        await RunInitAsync(force, workingDirectoryPath);

        Assert.Equal(UcliContractConstants.LocalDirectoryIgnoreEntry + Environment.NewLine, File.ReadAllText(gitIgnorePath));
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(true, LegacyConfigJson, "init-existing-config")]
    [InlineData(false, LegacyGitIgnoreContent, "init-existing-gitignore")]
    public async Task Init_WithoutForce_WhenTemplateFileExists_ReturnsInvalidArgumentErrorAsSingleJson (
        bool isConfigFile,
        string existingContent,
        string scopeName)
    {
        using var scope = TestDirectories.CreateTempScope("cli-output-contract", scopeName);
        var (workingDirectoryPath, _, _, configPath, gitIgnorePath) = CreateInitTargetPaths(scope, "workspace");
        var existingTemplateFilePath = isConfigFile
            ? configPath
            : gitIgnorePath;
        WriteFileUnderScope(scope, existingTemplateFilePath, existingContent);

        var result = await RunInitAsync(force: false, workingDirectoryPath);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        AssertCommandResultCommon(
            outputJson.RootElement,
            command: UcliCommandNames.Init,
            status: CliProtocol.StatusError,
            exitCode: (int)CliExitCode.InvalidArgument);
        AssertSingleError(
            outputJson.RootElement,
            expectedCode: ErrorCodes.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Init_WithProjectPathOption_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        using var scope = TestDirectories.CreateTempScope("cli-output-contract", "init-project-path-option");
        var (workingDirectoryPath, _, _, _, _) = CreateInitTargetPaths(scope, "workspace");

        var result = await RunToolWithWorkingDirectoryAsync(
            workingDirectoryPath,
            UcliCommandNames.Init,
            UcliContractConstants.CliOption.ProjectPath,
            workingDirectoryPath);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        AssertCommandResultCommon(
            outputJson.RootElement,
            command: UcliCommandNames.Init,
            status: CliProtocol.StatusError,
            exitCode: (int)CliExitCode.InvalidArgument);
        AssertSingleError(
            outputJson.RootElement,
            expectedCode: ErrorCodes.InvalidArgument);
        Assert.Contains(InitProjectPathOptionMessage, result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Status_WithUnknownOption_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        var result = await RunToolAsync(UcliCommandNames.Status, UcliContractConstants.CliOption.Unknown);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        AssertCommandResultCommon(
            outputJson.RootElement,
            command: UcliCommandNames.Status,
            status: CliProtocol.StatusError,
            exitCode: (int)CliExitCode.InvalidArgument);
        AssertSingleError(
            outputJson.RootElement,
            expectedCode: ErrorCodes.InvalidArgument);
        Assert.Contains(UnknownOptionMessage, result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task UnknownCommand_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        var result = await RunToolAsync("unknown");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        AssertCommandResultCommon(
            outputJson.RootElement,
            command: CliProtocol.RootCommand,
            status: CliProtocol.StatusError,
            exitCode: (int)CliExitCode.InvalidArgument);
        AssertSingleError(
            outputJson.RootElement,
            expectedCode: ErrorCodes.InvalidArgument);
    }

    private static void AssertCommandResultCommon (
        JsonElement root,
        string command,
        string status,
        int exitCode)
    {
        JsonAssert.For(root)
            .HasInt32("protocolVersion", CliProtocol.CurrentVersion)
            .HasString("command", command)
            .HasString("status", status)
            .HasInt32("exitCode", exitCode)
            .HasValueKind("message", JsonValueKind.String)
            .HasValueKind("payload", JsonValueKind.Object)
            .HasValueKind("errors", JsonValueKind.Array);
    }

    private static void AssertNoErrors (JsonElement root)
    {
        JsonAssert.For(root)
            .HasArrayLength("errors", 0);
    }

    private static void AssertSingleError (JsonElement root, string expectedCode)
    {
        JsonAssert.For(root)
            .HasArrayLength("errors", 1)
            .HasProperty("errors", 0, error => error
                .HasString("code", expectedCode)
                .HasValueKind("message", JsonValueKind.String)
                .IsNull("opId"));
    }

    private static Task<CommandExecutionResult> RunToolAsync (params string[] args)
    {
        return RunToolCoreAsync(args, null);
    }

    private static Task<CommandExecutionResult> RunToolWithWorkingDirectoryAsync (
        string workingDirectory,
        params string[] args)
    {
        return RunToolCoreAsync(args, workingDirectory);
    }

    private static async Task<CommandExecutionResult> RunToolCoreAsync (
        string[] args,
        string? workingDirectory)
    {
        // NOTE:
        // This test validates the process-level CLI contract (stdout JSON, stderr, exit code).
        // Resolving command instances directly would bypass Program and parser error paths.
        var toolPath = typeof(CommandResult).Assembly.Location;
        Assert.True(File.Exists(toolPath), $"CLI assembly was not found: {toolPath}");

        using var process = new Process();
        var startInfo = process.StartInfo;
        startInfo.FileName = "dotnet";
        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.CreateNoWindow = true;
        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        startInfo.ArgumentList.Add(toolPath);
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        var started = process.Start();
        Assert.True(started, "Failed to start ucli process.");

        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();

        using var timeoutCts = new CancellationTokenSource(ProcessTimeout);
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw new TimeoutException($"ucli process timed out after {ProcessTimeout.TotalSeconds} seconds.");
        }

        return new CommandExecutionResult(
            ExitCode: process.ExitCode,
            StdOut: await stdOutTask,
            StdErr: await stdErrTask);
    }

    private static async Task<CommandExecutionResult> RunInitAsync (
        bool force = false,
        string? workingDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            return force
                ? await RunToolAsync(UcliCommandNames.Init, UcliContractConstants.CliOption.Force)
                : await RunToolAsync(UcliCommandNames.Init);
        }

        return force
            ? await RunToolWithWorkingDirectoryAsync(workingDirectory, UcliCommandNames.Init, UcliContractConstants.CliOption.Force)
            : await RunToolWithWorkingDirectoryAsync(workingDirectory, UcliCommandNames.Init);
    }

    private static (string WorkingDirectoryPath, string UcliDirectoryPath, string LocalDirectoryPath, string ConfigPath, string GitIgnorePath) CreateInitTargetPaths (
        TestDirectoryScope scope,
        string targetDirectoryName)
    {
        var workingDirectoryPath = scope.CreateDirectory(targetDirectoryName);
        var ucliDirectoryPath = GetUcliDirectoryPath(workingDirectoryPath);
        var localDirectoryPath = GetLocalDirectoryPath(workingDirectoryPath);
        var configPath = GetConfigPath(workingDirectoryPath);
        var gitIgnorePath = GetGitIgnorePath(workingDirectoryPath);
        return (workingDirectoryPath, ucliDirectoryPath, localDirectoryPath, configPath, gitIgnorePath);
    }

    private static string GetUcliDirectoryPath (string workingDirectoryPath)
    {
        return Path.Combine(workingDirectoryPath, UcliDirectoryName);
    }

    private static string GetLocalDirectoryPath (string workingDirectoryPath)
    {
        return Path.Combine(GetUcliDirectoryPath(workingDirectoryPath), LocalDirectoryName);
    }

    private static string GetConfigPath (string workingDirectoryPath)
    {
        return Path.Combine(GetUcliDirectoryPath(workingDirectoryPath), ConfigFileName);
    }

    private static string GetGitIgnorePath (string workingDirectoryPath)
    {
        return Path.Combine(GetUcliDirectoryPath(workingDirectoryPath), GitIgnoreFileName);
    }

    private static void AssertDefaultConfigValues (string configPath)
    {
        using var configJson = JsonDocument.Parse(File.ReadAllText(configPath));
        JsonAssert.For(configJson.RootElement)
            .HasInt32("schemaVersion", UcliContractConstants.Config.SchemaVersion)
            .HasString("operationPolicy", UcliContractConstants.Config.OperationPolicySafe)
            .HasString("planTokenMode", UcliContractConstants.Config.PlanTokenModeOptional)
            .HasArrayLength("operationAllowlist", 1)
            .HasProperty("operationAllowlist", 0, static allowlistValue => allowlistValue
                .HasString(UcliContractConstants.Config.DefaultOperationAllowlistPattern));
    }

    private static void PrepareLegacyTemplateFiles (
        TestDirectoryScope scope,
        string configPath,
        string gitIgnorePath)
    {
        WriteFileUnderScope(scope, configPath, LegacyConfigJson);
        WriteFileUnderScope(scope, gitIgnorePath, LegacyGitIgnoreContent);
    }

    private static void WriteFileUnderScope (
        TestDirectoryScope scope,
        string absolutePath,
        string contents)
    {
        var relativePath = Path.GetRelativePath(scope.FullPath, absolutePath);
        scope.WriteFile(relativePath, contents);
    }

    private readonly record struct CommandExecutionResult (
        int ExitCode,
        string StdOut,
        string StdErr);
}