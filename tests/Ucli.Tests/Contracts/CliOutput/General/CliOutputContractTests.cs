using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Hosting.Cli.Status;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Tests;

[Collection(CurrentDirectoryTestCollection.Name)]
public sealed class CliOutputContractTests
{
    private const string ConfigFileName = "config.json";

    private const string GitIgnoreFileName = ".gitignore";

    private const string LegacyConfigJson = """{"schemaVersion":999}""";

    private const string LegacyGitIgnoreContent = "legacy/";

    private const string UcliDirectoryName = ".ucli";

    private const string LocalDirectoryName = "local";

    private static readonly Lazy<ServiceProvider> SharedUcliServiceProvider = new(UcliServiceProviderTestFactory.CreateCore);

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Status_WithProjectPath_ReturnsStatusJsonContractAsSingleJson ()
    {
        using var scope = TestDirectories.CreateTempScope("cli-output-contract", "status-success");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var ucliDirectoryPath = Path.Combine(unityProjectPath, UcliDirectoryName);
        var result = await RunStatusCommandAsync(projectPath: unityProjectPath);

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        JsonGoldenFileAssert.Matches(CliOutputGoldenFiles.GetPath("status", "success.json"), result.StdOut);
        FileSystemAssert.ForDirectory(ucliDirectoryPath).DoesNotExist();
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Init_ReturnsSuccessJsonContractAsSingleJson ()
    {
        using var scope = TestDirectories.CreateTempScope("cli-output-contract", "init-success");
        var (workingDirectoryPath, _, localDirectoryPath, configPath, gitIgnorePath) = CreateInitTargetPaths(scope, "workspace");

        var result = await RunInitAsync(force: false, workingDirectoryPath);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        var payload = outputJson.RootElement.GetProperty("payload");
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
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("init", "success.json"),
            result.StdOut,
            new JsonGoldenFileNormalization().NormalizePathPrefix(workingDirectoryPath, "<workspace>"));

        FileSystemAssert.ForDirectory(localDirectoryPath).DoesNotExist();
        FileSystemAssert.ForFile(configPath).Exists();
        FileSystemAssert.ForFile(gitIgnorePath).Exists();
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Init_WithForce_OverwritesTemplateFilesWithDefaultConfigAndGitIgnore ()
    {
        using var scope = TestDirectories.CreateTempScope("cli-output-contract", "init-force-template-overwrite");
        var (workingDirectoryPath, _, _, configPath, gitIgnorePath) = CreateInitTargetPaths(scope, "workspace");
        PrepareLegacyTemplateFiles(scope, configPath, gitIgnorePath);

        await RunInitAsync(force: true, workingDirectoryPath);

        AssertDefaultConfigValues(configPath);
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
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Init);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
        if (isConfigFile)
        {
            JsonGoldenFileAssert.Matches(
                CliOutputGoldenFiles.GetPath("init", "existing-config-error.json"),
                result.StdOut,
                new JsonGoldenFileNormalization().NormalizePathPrefix(workingDirectoryPath, "<workspace>"));
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Init_WithProjectPathOption_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        var result = await CliInProcessRunner.RunCommandAsync(
            UcliCommandNames.Init,
            UcliContractConstants.CliOption.ProjectPath,
            "unused");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Init);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
        CommandResultAssert.ReportsUnrecognizedArgument(result.StdErr, UcliContractConstants.CliOption.ProjectPath);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Status_WithUnknownOption_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        var result = await CliInProcessRunner.RunCommandAsync(UcliCommandNames.Status, UcliContractConstants.CliOption.Unknown);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Status);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
        CommandResultAssert.ReportsUnrecognizedArgument(result.StdErr, UcliContractConstants.CliOption.Unknown);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Status_WithInvalidTimeoutOption_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        using var scope = TestDirectories.CreateTempScope("cli-output-contract", "status-invalid-timeout");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var result = await RunStatusCommandAsync(
            projectPath: unityProjectPath,
            timeout: "abc");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Status);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
        Assert.Contains("timeout", outputJson.RootElement.GetProperty("message").GetString(), StringComparison.Ordinal);
        JsonGoldenFileAssert.Matches(CliOutputGoldenFiles.GetPath("status", "invalid-timeout.json"), result.StdOut);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Status_WithModeOption_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        var result = await CliInProcessRunner.RunCommandAsync(
            UcliCommandNames.Status,
            UcliContractConstants.CliOption.Mode,
            "auto");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Status);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
        CommandResultAssert.ReportsUnrecognizedArgument(result.StdErr, UcliContractConstants.CliOption.Mode);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task UnknownCommand_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        var result = await CliInProcessRunner.RunCommandAsync("unknown");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Root);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
    }

    private static Task<CommandExecutionResult> RunInitAsync (
        bool force = false,
        string? workingDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            return force
                ? CliInProcessRunner.RunCommandAsync(UcliCommandNames.Init, UcliContractConstants.CliOption.Force)
                : CliInProcessRunner.RunCommandAsync(UcliCommandNames.Init);
        }

        return force
            ? CliInProcessRunner.RunCommandWithWorkingDirectoryAsync(workingDirectory, UcliCommandNames.Init, UcliContractConstants.CliOption.Force)
            : CliInProcessRunner.RunCommandWithWorkingDirectoryAsync(workingDirectory, UcliCommandNames.Init);
    }

    private static Task<CommandExecutionResult> RunStatusCommandAsync (
        string? projectPath = null,
        string? timeout = null)
    {
        return CommandResultCapture.ExecuteAsync(() =>
            ActivatorUtilities.CreateInstance<StatusCommand>(
                    SharedUcliServiceProvider.Value,
                    CommandResultTestWriter.Create())
                .StatusAsync(
                    projectPath: projectPath,
                    timeout: timeout,
                    cancellationToken: CancellationToken.None));
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
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultReadyMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandReady).GetInt32());
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultCompileMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandCompile).GetInt32());
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultBuildRunMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandBuildRun).GetInt32());
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultStatusMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandStatus).GetInt32());
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultValidateMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandValidate).GetInt32());
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultPlanMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandPlan).GetInt32());
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultCallMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandCall).GetInt32());
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultEvalMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandEval).GetInt32());
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultResolveMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandResolve).GetInt32());
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultQueryMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandQuery).GetInt32());
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultRefreshMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandRefresh).GetInt32());
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultOpsMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandOps).GetInt32());
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultDaemonStartMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandDaemonStart).GetInt32());
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultDaemonStopMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandDaemonStop).GetInt32());
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultDaemonCleanupMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandDaemonCleanup).GetInt32());
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultDaemonStatusMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandDaemonStatus).GetInt32());
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultDaemonListMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandDaemonList).GetInt32());
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultLogsDaemonMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandLogsDaemonRead).GetInt32());
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultLogsUnityMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandLogsUnityRead).GetInt32());
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultLogsUnityClearMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandLogsUnityClear).GetInt32());
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultPlayStatusMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandPlayStatus).GetInt32());
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultPlayEnterMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandPlayEnter).GetInt32());
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultPlayExitMilliseconds, timeoutByCommand.GetProperty(UcliContractConstants.Config.IpcTimeoutCommandPlayExit).GetInt32());
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
