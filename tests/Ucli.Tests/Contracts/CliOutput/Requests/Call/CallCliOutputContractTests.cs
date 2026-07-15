using System.Text.Json;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Tests;

public sealed class CallCliOutputContractTests
{
    private static readonly Lazy<ServiceProvider> SharedCallServiceProvider = new(UcliServiceProviderTestFactory.CreateCore);

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Call_WithUnknownOption_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        var result = await CliInProcessRunner.RunCommandAsync(UcliCommandNames.Call, UcliContractConstants.CliOption.Unknown);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentError(outputJson.RootElement, UcliCommandNames.Call);
        CommandResultAssert.ReportsUnrecognizedArgument(result.StdErr, UcliContractConstants.CliOption.Unknown);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Call_WithNewOptions_IsAcceptedByParser ()
    {
        var result = await CliInProcessRunner.RunCommandWithStandardInputAsync(
            CreateRequestJson(),
            UcliCommandNames.Call,
            UcliContractConstants.CliOption.Timeout,
            "abc",
            UcliContractConstants.CliOption.PlanToken,
            "plan-token-1",
            UcliContractConstants.CliOption.WithPlan,
            UcliContractConstants.CliOption.AllowDangerous,
            UcliContractConstants.CliOption.FailFast);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Call);
        CommandResultAssert.DoesNotReportUnrecognizedArguments(
            result.StdErr,
            UcliContractConstants.CliOption.WithPlan,
            UcliContractConstants.CliOption.AllowDangerous,
            UcliContractConstants.CliOption.PlanToken,
            UcliContractConstants.CliOption.FailFast);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Call_WithReadIndexModeOption_ReturnsParseErrorAsSingleJson ()
    {
        var result = await CliInProcessRunner.RunCommandAsync(
            UcliCommandNames.Call,
            UcliContractConstants.CliOption.ReadIndexMode,
            UcliContractConstants.Config.ReadIndexModeDisabled);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentError(outputJson.RootElement, UcliCommandNames.Call);
        CommandResultAssert.ReportsUnrecognizedArgument(result.StdErr, UcliContractConstants.CliOption.ReadIndexMode);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Call_WhenDaemonIsNotRunning_PreservesDaemonNotRunningCode ()
    {
        using var scope = TestDirectories.CreateTempScope("call-cli-output-contract", "daemon-not-running");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        await UnityProjectTestFactory.WriteUcliUnityPluginMarkerAsync(scope, "UnityProject");

        var result = await RunCallCommandAsync(
            CreateRequestJson(),
            projectPath: unityProjectPath,
            mode: "daemon");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Call,
            ContractLiteralCodec.ToValue(CommandResultStatus.Error),
            (int)CliExitCode.ToolError);
        AssertPayloadHasGeneratedRequestId(outputJson.RootElement);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasArrayLength("opResults", 0))
            .HasProperty("errors", 0, error => error
                .HasString("code", UnityExecutionModeDecisionErrorCodes.DaemonNotRunning.Value));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Call_WithInvalidTimeoutOption_PreservesRequestPayload ()
    {
        using var scope = TestDirectories.CreateTempScope("call-cli-output-contract", "invalid-timeout");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");

        var result = await RunCallCommandAsync(
            CreateRequestJson(),
            projectPath: unityProjectPath,
            timeout: "abc");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Call);
        AssertPayloadHasGeneratedRequestId(outputJson.RootElement);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasArrayLength("opResults", 0));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Call_WithInvalidModeOption_PreservesRequestPayload ()
    {
        using var scope = TestDirectories.CreateTempScope("call-cli-output-contract", "invalid-mode");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");

        var result = await RunCallCommandAsync(
            CreateRequestJson(),
            projectPath: unityProjectPath,
            mode: "unsupported");

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("call", "invalid-mode.json"),
            result.StdOut,
            CliOutputGoldenFiles.NormalizeRequestIds());
    }

    private static Task<CommandExecutionResult> RunCallCommandAsync (
        string requestJson,
        string? projectPath = null,
        string? mode = null,
        string? timeout = null)
    {
        return CommandResultCapture.ExecuteAsync(() =>
            ActivatorUtilities.CreateInstance<CallCommand>(
                    SharedCallServiceProvider.Value,
                    RequestInputReaderStub.Success(requestJson),
                    CommandResultTestWriter.Create())
                .CallAsync(
                    projectPath: projectPath,
                    mode: mode,
                    timeout: timeout,
                    cancellationToken: CancellationToken.None));
    }

    private static string CreateRequestJson ()
    {
        return JsonSerializer.Serialize(new
        {
            steps = Array.Empty<object>(),
        });
    }

    private static void AssertPayloadHasGeneratedRequestId (JsonElement root)
    {
        var requestId = root.GetProperty("payload").GetProperty("requestId").GetString();
        Assert.True(Guid.TryParseExact(requestId, "D", out _));
    }

}
