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

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Status_ReturnsNotImplementedErrorAsSingleJson ()
    {
        var result = await CliProcessRunner.RunCommand(UcliCommandNames.Status);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.Status,
            status: CliProtocol.StatusError,
            exitCode: (int)CliExitCode.ToolError);
        CommandResultAssert.HasSingleError(
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

        var result = await RunInit(force, workingDirectoryPath);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.Init,
            status: CliProtocol.StatusOk,
            exitCode: (int)CliExitCode.Success);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);
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

        await RunInit(force: false, workingDirectoryPath);

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

        await RunInit(force, workingDirectoryPath);

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

        await RunInit(force, workingDirectoryPath);

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

        var result = await RunInit(force: false, workingDirectoryPath);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.Init,
            status: CliProtocol.StatusError,
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: ErrorCodes.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Init_WithProjectPathOption_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        using var scope = TestDirectories.CreateTempScope("cli-output-contract", "init-project-path-option");
        var (workingDirectoryPath, _, _, _, _) = CreateInitTargetPaths(scope, "workspace");

        var result = await CliProcessRunner.RunCommandWithWorkingDirectory(
            workingDirectoryPath,
            UcliCommandNames.Init,
            UcliContractConstants.CliOption.ProjectPath,
            workingDirectoryPath);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.Init,
            status: CliProtocol.StatusError,
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: ErrorCodes.InvalidArgument);
        Assert.Contains(InitProjectPathOptionMessage, result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Status_WithUnknownOption_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        var result = await CliProcessRunner.RunCommand(UcliCommandNames.Status, UcliContractConstants.CliOption.Unknown);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.Status,
            status: CliProtocol.StatusError,
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: ErrorCodes.InvalidArgument);
        Assert.Contains(UnknownOptionMessage, result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task UnknownCommand_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        var result = await CliProcessRunner.RunCommand("unknown");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: CliProtocol.RootCommand,
            status: CliProtocol.StatusError,
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: ErrorCodes.InvalidArgument);
    }

    private static async Task<CommandExecutionResult> RunInit (
        bool force = false,
        string? workingDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            return force
                ? await CliProcessRunner.RunCommand(UcliCommandNames.Init, UcliContractConstants.CliOption.Force)
                : await CliProcessRunner.RunCommand(UcliCommandNames.Init);
        }

        return force
            ? await CliProcessRunner.RunCommandWithWorkingDirectory(workingDirectory, UcliCommandNames.Init, UcliContractConstants.CliOption.Force)
            : await CliProcessRunner.RunCommandWithWorkingDirectory(workingDirectory, UcliCommandNames.Init);
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
            .HasString("readIndexDefaultMode", UcliContractConstants.Config.ReadIndexModeRequireFresh)
            .HasInt32("ipcDefaultTimeoutMilliseconds", UcliContractConstants.Config.IpcDefaultTimeoutMilliseconds)
            .HasProperty("ipcTimeoutMillisecondsByCommand", timeoutByCommand => timeoutByCommand
                .IsNull(UcliContractConstants.Config.IpcTimeoutCommandStatus)
                .IsNull(UcliContractConstants.Config.IpcTimeoutCommandValidate)
                .IsNull(UcliContractConstants.Config.IpcTimeoutCommandPlan)
                .IsNull(UcliContractConstants.Config.IpcTimeoutCommandCall)
                .IsNull(UcliContractConstants.Config.IpcTimeoutCommandResolve)
                .IsNull(UcliContractConstants.Config.IpcTimeoutCommandQuery)
                .IsNull(UcliContractConstants.Config.IpcTimeoutCommandRefresh)
                .IsNull(UcliContractConstants.Config.IpcTimeoutCommandOps)
                .IsNull(UcliContractConstants.Config.IpcTimeoutCommandDaemon))
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
}