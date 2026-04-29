using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;

namespace MackySoft.Ucli.Tests;

public sealed class RefreshCliOutputContractTests
{
    private const string UnknownOptionMessage = "Argument '--unknown' is not recognized.";

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Refresh_WithUnknownOption_ReturnsCommandResultInvalidArgumentAsSingleJson ()
    {
        var result = await CliProcessRunner.RunCommand(UcliCommandNames.Refresh, UcliContractConstants.CliOption.Unknown);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Refresh,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, IpcErrorCodes.InvalidArgument);
        Assert.Contains(UnknownOptionMessage, result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Refresh_WithInvalidTimeoutOption_ReturnsCommandResultInvalidArgumentAsSingleJson ()
    {
        using var scope = TestDirectories.CreateTempScope("refresh-cli-output-contract", "invalid-timeout");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var result = await CliProcessRunner.RunCommand(
            UcliCommandNames.Refresh,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath,
            UcliContractConstants.CliOption.Timeout,
            "abc");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Refresh,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, IpcErrorCodes.InvalidArgument);
        AssertRefreshFailurePayload(outputJson.RootElement);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Refresh_WithInvalidModeOption_PreservesRequestPayload ()
    {
        using var scope = TestDirectories.CreateTempScope("refresh-cli-output-contract", "invalid-mode");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var result = await CliProcessRunner.RunCommand(
            UcliCommandNames.Refresh,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath,
            UcliContractConstants.CliOption.Mode,
            "unsupported");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Refresh,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, IpcErrorCodes.InvalidArgument);
        AssertRefreshFailurePayload(outputJson.RootElement);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Refresh_WithShortProjectPathAliasAndInvalidProjectPath_ReturnsCommandResultInvalidArgumentAsSingleJson ()
    {
        using var scope = TestDirectories.CreateTempScope("refresh-cli-output-contract", "invalid-project-path");
        var invalidProjectPath = Path.Combine(scope.FullPath, "NotUnityProject");
        Directory.CreateDirectory(invalidProjectPath);

        var result = await CliProcessRunner.RunCommand(
            UcliCommandNames.Refresh,
            "-p",
            invalidProjectPath);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.DoesNotContain("Argument '-p' is not recognized.", result.StdErr, StringComparison.Ordinal);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Refresh,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, IpcErrorCodes.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Refresh_WithFailFastCamelCaseAlias_IsAcceptedByParser ()
    {
        using var scope = TestDirectories.CreateTempScope("refresh-cli-output-contract", "fail-fast-camel-case");
        var invalidProjectPath = Path.Combine(scope.FullPath, "NotUnityProject");
        Directory.CreateDirectory(invalidProjectPath);

        var result = await CliProcessRunner.RunCommand(
            UcliCommandNames.Refresh,
            UcliContractConstants.CliOption.FailFast,
            UcliContractConstants.CliOption.ProjectPath,
            invalidProjectPath);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.DoesNotContain("Argument '--failFast' is not recognized.", result.StdErr, StringComparison.Ordinal);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Refresh,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, IpcErrorCodes.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Refresh_WithModeDaemonAndPluginMissing_ReturnsCommandResultInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("refresh-cli-output-contract", "plugin-missing");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        WriteRefreshAllowedConfig(scope, unityProjectPath);
        var result = await CliProcessRunner.RunCommand(
            UcliCommandNames.Refresh,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath,
            UcliContractConstants.CliOption.Mode,
            "daemon");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Refresh,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, IpcErrorCodes.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Refresh_WhenOperationPolicyBlocksRefresh_ReturnsCommandResultInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("refresh-cli-output-contract", "operation-policy-blocked");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        WriteRefreshBlockedConfig(scope, unityProjectPath);

        var result = await CliProcessRunner.RunCommand(
            UcliCommandNames.Refresh,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Refresh,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        JsonAssert.For(outputJson.RootElement)
            .HasArrayLength("errors", 1)
            .HasProperty("errors", 0, error => error
                .HasString("code", "OPERATION_NOT_ALLOWED")
                .HasValueKind("message", JsonValueKind.String)
                .HasString("opId", "refresh"));
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

    private static void AssertRefreshFailurePayload (JsonElement rootElement)
    {
        var payload = rootElement.GetProperty("payload");
        var requestId = payload.GetProperty("requestId").GetString();

        Assert.True(Guid.TryParseExact(requestId, "D", out _));
        JsonAssert.For(payload)
            .HasArrayLength("opResults", 0);
    }
}
