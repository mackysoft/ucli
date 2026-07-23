using System.Text.Json;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Tests;

public sealed class RefreshCliOutputContractTests
{
    private static readonly Lazy<ServiceProvider> SharedRefreshServiceProvider = new(UcliServiceProviderTestFactory.CreateCore);

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Refresh_WithUnknownOption_ReturnsCommandResultInvalidArgumentAsSingleJson ()
    {
        var result = await CliInProcessRunner.RunCommandAsync(UcliCommandNames.Refresh, UcliContractConstants.CliOption.Unknown);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentError(outputJson.RootElement, UcliCommandNames.Refresh);
        CommandResultAssert.ReportsUnrecognizedArgument(result.StdErr, UcliContractConstants.CliOption.Unknown);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Refresh_WithShortProjectPathAliasAndInvalidProjectPath_ReturnsCommandResultInvalidArgumentAsSingleJson ()
    {
        using var scope = TestDirectories.CreateTempScope("refresh-cli-output-contract", "invalid-project-path");
        var invalidProjectPath = Path.Combine(scope.FullPath, "NotUnityProject");
        Directory.CreateDirectory(invalidProjectPath);

        var result = await CliInProcessRunner.RunCommandAsync(
            UcliCommandNames.Refresh,
            "-p",
            invalidProjectPath);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.DoesNotReportUnrecognizedArguments(result.StdErr, "-p");
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Refresh);
        CommandResultAssert.HasSingleError(outputJson.RootElement, ProjectContextErrorCodes.UnityProjectMarkerMissing);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Refresh_WithFailFastCamelCaseAlias_IsAcceptedByParser ()
    {
        var result = await CliInProcessRunner.RunCommandAsync(
            UcliCommandNames.Refresh,
            UcliContractConstants.CliOption.FailFast,
            UcliContractConstants.CliOption.Mode,
            "unsupported");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.DoesNotReportUnrecognizedArguments(result.StdErr, UcliContractConstants.CliOption.FailFast);
        CommandResultAssert.HasInvalidArgumentError(outputJson.RootElement, UcliCommandNames.Refresh);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Refresh_WithModeDaemonAndDaemonSessionMissing_ReturnsDaemonNotRunning ()
    {
        using var scope = TestDirectories.CreateTempScope("refresh-cli-output-contract", "daemon-session-missing");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        WriteRefreshAllowedConfig(scope, unityProjectPath);

        var result = await RunRefreshCommandAsync(
            projectPath: unityProjectPath,
            mode: "daemon");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Refresh,
            TextVocabulary.GetText(CommandResultStatus.Error),
            (int)CliExitCode.ToolError);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UnityExecutionModeDecisionErrorCodes.DaemonNotRunning);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Refresh_WhenOperationPolicyBlocksRefresh_ReturnsCommandResultInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("refresh-cli-output-contract", "operation-policy-blocked");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        WriteRefreshBlockedConfig(scope, unityProjectPath);

        var result = await RunRefreshCommandAsync(
            projectPath: unityProjectPath);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Refresh);
        JsonAssert.For(outputJson.RootElement)
            .HasArrayLength("errors", 1)
            .HasProperty("errors", 0, error => error
                .HasString("code", "OPERATION_NOT_ALLOWED")
                .HasValueKind("message", JsonValueKind.String)
                .HasString("opId", "refresh"));
        var message = outputJson.RootElement.GetProperty("errors")[0].GetProperty("message").GetString();
        Assert.Contains("ucli.project.refresh", message, StringComparison.Ordinal);
        Assert.Contains("advanced", message, StringComparison.Ordinal);
        Assert.Contains("safe", message, StringComparison.Ordinal);
        Assert.Contains(".ucli/config.json", message, StringComparison.Ordinal);
        Assert.DoesNotContain("AssetDatabase", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ucli status", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ucli ready", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ucli query", message, StringComparison.OrdinalIgnoreCase);
    }

    private static Task<CommandExecutionResult> RunRefreshCommandAsync (
        string? projectPath = null,
        string? mode = null)
    {
        return CommandResultCapture.ExecuteAsync(() =>
            ActivatorUtilities.CreateInstance<RefreshCommand>(
                    SharedRefreshServiceProvider.Value,
                    CommandResultTestWriter.Create())
                .RefreshAsync(
                    projectPath: projectPath,
                    mode: mode,
                    cancellationToken: CancellationToken.None));
    }

    private static void WriteRefreshAllowedConfig (
        TestDirectoryScope scope,
        string unityProjectPath)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(unityProjectPath);

        WriteConfigJson(
            scope,
            unityProjectPath,
            UcliContractConstants.Config.OperationPolicyDangerous);
    }

    private static void WriteRefreshBlockedConfig (
        TestDirectoryScope scope,
        string unityProjectPath)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(unityProjectPath);

        WriteConfigJson(
            scope,
            unityProjectPath,
            UcliContractConstants.Config.OperationPolicySafe);
    }

    private static void WriteConfigJson (
        TestDirectoryScope scope,
        string unityProjectPath,
        string operationPolicy)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(unityProjectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationPolicy);

        var configPath = Path.Combine(unityProjectPath, ".ucli", "config.json");
        var relativeConfigPath = Path.GetRelativePath(scope.FullPath, configPath);
        var configJson = JsonSerializer.Serialize(new
        {
            schemaVersion = UcliContractConstants.Config.SchemaVersion,
            operationPolicy,
            planTokenMode = UcliContractConstants.Config.PlanTokenModeOptional,
            readIndexDefaultMode = UcliContractConstants.Config.ReadIndexModeRequireFresh,
            operationAllowlist = new[]
            {
                UcliContractConstants.Config.DefaultOperationAllowlistPattern,
            },
        });

        scope.WriteFile(relativeConfigPath, configJson);
    }
}
