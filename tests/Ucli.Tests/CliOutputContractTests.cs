using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Hosting.Cli;

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

    private const string StatusModeOptionMessage = "Argument '--mode' is not recognized.";

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Status_WithProjectPath_ReturnsStatusJsonContractAsSingleJson ()
    {
        using var scope = TestDirectories.CreateTempScope("cli-output-contract", "status-success");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var ucliDirectoryPath = Path.Combine(unityProjectPath, UcliDirectoryName);
        var result = await CliProcessRunner.RunCommand(
            UcliCommandNames.Status,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.Status,
            status: "ok",
            exitCode: (int)CliExitCode.Success);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);

        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("daemonStatus", "notRunning")
                .HasString("unityVersion", "6000.1.4f1")
                .IsNull("serverVersion")
                .IsNull("lifecycleState")
                .IsNull("blockingReason")
                .IsNull("compileState")
                .IsNull("compileGeneration")
                .IsNull("domainReloadGeneration")
                .HasBoolean("canAcceptExecutionRequests", false)
                .IsNull("runtime")
                .HasInt32("timeoutMilliseconds", UcliContractConstants.Config.IpcTimeoutDefaultStatusMilliseconds));
        FileSystemAssert.ForDirectory(ucliDirectoryPath).DoesNotExist();
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
            status: "ok",
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

        FileSystemAssert.ForDirectory(localDirectoryPath).DoesNotExist();
        FileSystemAssert.ForFile(configPath).Exists();
        FileSystemAssert.ForFile(gitIgnorePath).Exists();
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Init_AlwaysCreatesConfigTemplateUnderCurrentWorkingDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("cli-output-contract", "init-cwd");
        var (workingDirectoryPath, ucliDirectoryPath, localDirectoryPath, configPath, gitIgnorePath) = CreateInitTargetPaths(scope, "workspace");

        await RunInit(force: false, workingDirectoryPath);

        FileSystemAssert.ForDirectory(ucliDirectoryPath).Exists();
        FileSystemAssert.ForDirectory(localDirectoryPath).DoesNotExist();
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
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
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
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
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
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
        Assert.Contains(UnknownOptionMessage, result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Status_WithInvalidTimeoutOption_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        using var scope = TestDirectories.CreateTempScope("cli-output-contract", "status-invalid-timeout");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var result = await CliProcessRunner.RunCommand(
            UcliCommandNames.Status,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath,
            UcliContractConstants.CliOption.Timeout,
            "abc");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.Status,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
        Assert.Contains("timeout", outputJson.RootElement.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Status_WithModeOption_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        var result = await CliProcessRunner.RunCommand(
            UcliCommandNames.Status,
            UcliContractConstants.CliOption.Mode,
            "auto");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.Status,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
        Assert.Contains(StatusModeOptionMessage, result.StdErr, StringComparison.Ordinal);
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
            command: UcliCommandNames.Root,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
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
            .HasArrayLength("operationAllowlist", 1)
            .HasProperty("operationAllowlist", 0, static allowlistValue => allowlistValue
                .HasString(UcliContractConstants.Config.DefaultOperationAllowlistPattern));

        var timeoutByCommand = configJson.RootElement.GetProperty("ipcTimeoutMillisecondsByCommand");
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultTestMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandTest).GetInt32());
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultStatusMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandStatus).GetInt32());
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultValidateMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandValidate).GetInt32());
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultPlanMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandPlan).GetInt32());
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultCallMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandCall).GetInt32());
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultResolveMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandResolve).GetInt32());
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultQueryMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandQuery).GetInt32());
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultRefreshMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandRefresh).GetInt32());
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultOpsMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandOps).GetInt32());
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultDaemonStartMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandDaemonStart).GetInt32());
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultDaemonStopMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandDaemonStop).GetInt32());
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultDaemonCleanupMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandDaemonCleanup).GetInt32());
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultDaemonStatusMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandDaemonStatus).GetInt32());
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultDaemonListMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandDaemonList).GetInt32());
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultLogsDaemonMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandLogsDaemon).GetInt32());
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultLogsUnityMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandLogsUnity).GetInt32());
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