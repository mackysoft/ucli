using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Tests;

public sealed class PlanCliOutputContractTests
{
    private static readonly Lazy<ServiceProvider> SharedPlanServiceProvider = new(UcliServiceProviderTestFactory.CreateCore);

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Plan_WithUnknownOption_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        var result = await CliInProcessRunner.RunCommandAsync(UcliCommandNames.Plan, UcliContractConstants.CliOption.Unknown);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentError(outputJson.RootElement, UcliCommandNames.Plan);
        CommandResultAssert.ReportsUnrecognizedArgument(result.StdErr, UcliContractConstants.CliOption.Unknown);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Plan_WhenRequestStepsPropertyIsMissing_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        var result = await RunPlanCommandAsync("""{}""");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentError(outputJson.RootElement, UcliCommandNames.Plan);
        Assert.Contains(
            "Request property 'steps' is required.",
            outputJson.RootElement.GetProperty("message").GetString(),
            StringComparison.Ordinal);
        Assert.False(outputJson.RootElement.GetProperty("payload").EnumerateObject().MoveNext());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Plan_WithFailFastCamelCaseAlias_IsAcceptedByParser ()
    {
        var result = await CliInProcessRunner.RunCommandAsync(
            UcliCommandNames.Plan,
            UcliContractConstants.CliOption.FailFast,
            UcliContractConstants.CliOption.ReadIndexMode,
            "unsupported");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.DoesNotReportUnrecognizedArguments(result.StdErr, UcliContractConstants.CliOption.FailFast);
        CommandResultAssert.HasInvalidArgumentError(outputJson.RootElement, UcliCommandNames.Plan);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Plan_WithInvalidTimeoutOption_ReturnsInvalidArgumentAndPreservesPreflightPayload ()
    {
        using var scope = TestDirectories.CreateTempScope("plan-cli-output-contract", "invalid-timeout");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");

        var result = await RunPlanCommandAsync(
            CreateRequestJson(),
            projectPath: unityProjectPath,
            readIndexMode: UcliContractConstants.Config.ReadIndexModeDisabled,
            timeout: "abc");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentError(outputJson.RootElement, UcliCommandNames.Plan);
        AssertPayloadHasGeneratedRequestId(outputJson.RootElement);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasArrayLength("opResults", 0)
                .HasProperty("readIndex", readIndex => readIndex
                    .HasBoolean("used", false)
                    .HasBoolean("hit", false)
                    .HasString("fallbackReason", "readIndex disabled by mode.")));
        Assert.False(outputJson.RootElement.GetProperty("payload").TryGetProperty("planToken", out _));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Plan_WithInvalidModeOption_ReturnsInvalidArgumentAndPreservesPreflightPayload ()
    {
        using var scope = TestDirectories.CreateTempScope("plan-cli-output-contract", "invalid-mode");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");

        var result = await RunPlanCommandAsync(
            CreateRequestJson(),
            projectPath: unityProjectPath,
            readIndexMode: UcliContractConstants.Config.ReadIndexModeDisabled,
            mode: "unsupported");

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("plan", "invalid-mode.json"),
            result.StdOut,
            CliOutputGoldenFiles.NormalizeRequestIds());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Plan_WhenDaemonIsNotRunning_ReturnsToolErrorAndPreservesPreflightPayload ()
    {
        using var scope = TestDirectories.CreateTempScope("plan-cli-output-contract", "daemon-not-running");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        await UnityProjectTestFactory.WriteUcliUnityPluginMarkerAsync(scope, "UnityProject");

        var result = await RunPlanCommandAsync(
            CreateRequestJson(),
            projectPath: unityProjectPath,
            readIndexMode: UcliContractConstants.Config.ReadIndexModeDisabled,
            mode: "daemon");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Plan,
            IpcProtocol.StatusError,
            (int)CliExitCode.ToolError);
        AssertPayloadHasGeneratedRequestId(outputJson.RootElement);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasArrayLength("opResults", 0)
                .HasProperty("readIndex", readIndex => readIndex
                    .HasBoolean("used", false)
                    .HasBoolean("hit", false)
                    .HasString("fallbackReason", "readIndex disabled by mode.")))
            .HasProperty("errors", 0, error => error
                .HasString("code", "DAEMON_NOT_RUNNING"));
        Assert.False(outputJson.RootElement.GetProperty("payload").TryGetProperty("planToken", out _));
    }

    private static Task<CommandExecutionResult> RunPlanCommandAsync (
        string requestJson,
        string? projectPath = null,
        string? mode = null,
        string? timeout = null,
        string? readIndexMode = null)
    {
        return CommandResultCapture.ExecuteAsync(() =>
            ActivatorUtilities.CreateInstance<PlanCommand>(
                    SharedPlanServiceProvider.Value,
                    RequestInputReaderStub.Success(requestJson),
                    CommandResultTestWriter.Create())
                .PlanAsync(
                    projectPath: projectPath,
                    mode: mode,
                    timeout: timeout,
                    readIndexMode: readIndexMode,
                    cancellationToken: CancellationToken.None));
    }

    private static string CreateRequestJson ()
    {
        return """
            {
              "steps": []
            }
            """;
    }

    private static void AssertPayloadHasGeneratedRequestId (JsonElement root)
    {
        var requestId = root.GetProperty("payload").GetProperty("requestId").GetString();
        Assert.True(Guid.TryParseExact(requestId, "D", out _));
    }

}
