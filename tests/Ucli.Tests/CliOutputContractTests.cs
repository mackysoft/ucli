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

    private const string UnityProjectDirectoryName = "UnityProject";

    private const string UnknownOptionMessage = "Argument '--unknown' is not recognized.";

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
        var (unityProjectPath, configPath, gitIgnorePath) = CreateUnityProjectPaths(scope);
        if (force)
        {
            PrepareLegacyTemplateFiles(scope, configPath, gitIgnorePath);
        }

        var result = await RunInitAsync(unityProjectPath, force);

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
                .HasString("projectPath", unityProjectPath)
                .HasValueKind("projectFingerprint", JsonValueKind.String)
                .HasString("configPath", configPath)
                .HasString("gitignorePath", gitIgnorePath));
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(true, "init-config-file")]
    [InlineData(false, "init-gitignore-file")]
    public async Task Init_WithProjectPath_CreatesTemplateFiles (bool isConfigFile, string scopeName)
    {
        using var scope = TestDirectories.CreateTempScope("cli-output-contract", scopeName);
        var (unityProjectPath, _, _) = CreateUnityProjectPaths(scope);
        var templateFilePath = isConfigFile
            ? GetConfigPath(unityProjectPath)
            : GetGitIgnorePath(unityProjectPath);

        await RunInitAsync(unityProjectPath);

        FileSystemAssert.ForFile(templateFilePath).Exists();
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(false, "init-default-config-values")]
    [InlineData(true, "init-force-config-overwrite")]
    public async Task Init_WritesDefaultConfigValues (bool force, string scopeName)
    {
        using var scope = TestDirectories.CreateTempScope("cli-output-contract", scopeName);
        var (unityProjectPath, configPath, _) = CreateUnityProjectPaths(scope);
        if (force)
        {
            PrepareLegacyTemplateFiles(scope, configPath, GetGitIgnorePath(unityProjectPath));
        }

        await RunInitAsync(unityProjectPath, force);

        AssertDefaultConfigValues(configPath);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(false, "init-gitignore-contents")]
    [InlineData(true, "init-force-gitignore-overwrite")]
    public async Task Init_WritesLocalOnlyGitIgnoreContents (bool force, string scopeName)
    {
        using var scope = TestDirectories.CreateTempScope("cli-output-contract", scopeName);
        var (unityProjectPath, _, gitIgnorePath) = CreateUnityProjectPaths(scope);
        if (force)
        {
            PrepareLegacyTemplateFiles(scope, GetConfigPath(unityProjectPath), gitIgnorePath);
        }

        await RunInitAsync(unityProjectPath, force);

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
        var (unityProjectPath, _, _) = CreateUnityProjectPaths(scope);
        var existingTemplateFilePath = isConfigFile
            ? GetConfigPath(unityProjectPath)
            : GetGitIgnorePath(unityProjectPath);
        WriteFileUnderScope(scope, existingTemplateFilePath, existingContent);

        var result = await RunInitAsync(unityProjectPath);

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

    private static async Task<CommandExecutionResult> RunToolAsync (params string[] args)
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

    private static async Task<CommandExecutionResult> RunInitAsync (string unityProjectPath, bool force = false)
    {
        if (force)
        {
            return await RunToolAsync(
                UcliCommandNames.Init,
                UcliContractConstants.CliOption.ProjectPath,
                unityProjectPath,
                UcliContractConstants.CliOption.Force);
        }

        return await RunToolAsync(UcliCommandNames.Init, UcliContractConstants.CliOption.ProjectPath, unityProjectPath);
    }

    private static (string UnityProjectPath, string ConfigPath, string GitIgnorePath) CreateUnityProjectPaths (TestDirectoryScope scope)
    {
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, UnityProjectDirectoryName);
        var configPath = GetConfigPath(unityProjectPath);
        var gitIgnorePath = GetGitIgnorePath(unityProjectPath);
        return (unityProjectPath, configPath, gitIgnorePath);
    }

    private static string GetConfigPath (string unityProjectPath)
    {
        return Path.Combine(unityProjectPath, UcliDirectoryName, ConfigFileName);
    }

    private static string GetGitIgnorePath (string unityProjectPath)
    {
        return Path.Combine(unityProjectPath, UcliDirectoryName, GitIgnoreFileName);
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