using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;

namespace MackySoft.Ucli.Tests;

public sealed class CallCliOutputContractTests
{
    private const string UnknownOptionMessage = "Argument '--unknown' is not recognized.";

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Call_WithUnknownOption_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        var result = await CliProcessRunner.RunCommand(UcliCommandNames.Call, UcliContractConstants.CliOption.Unknown);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Call,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
        Assert.Contains(UnknownOptionMessage, result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Call_WithNewOptions_IsAcceptedByParser ()
    {
        using var scope = TestDirectories.CreateTempScope("call-cli-output-contract", "new-options-parser");
        var invalidProjectPath = Path.Combine(scope.FullPath, "NotUnityProject");
        Directory.CreateDirectory(invalidProjectPath);

        var result = await CliProcessRunner.RunCommandWithStandardInput(
            CreateRequestJson(),
            UcliCommandNames.Call,
            UcliContractConstants.CliOption.ProjectPath,
            invalidProjectPath,
            UcliContractConstants.CliOption.Mode,
            "unsupported",
            UcliContractConstants.CliOption.Timeout,
            "abc",
            UcliContractConstants.CliOption.PlanToken,
            "plan-token-1",
            UcliContractConstants.CliOption.WithPlan,
            UcliContractConstants.CliOption.AllowDangerous,
            UcliContractConstants.CliOption.FailFast);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Call,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        Assert.DoesNotContain("Argument '--withPlan' is not recognized.", result.StdErr, StringComparison.Ordinal);
        Assert.DoesNotContain("Argument '--allowDangerous' is not recognized.", result.StdErr, StringComparison.Ordinal);
        Assert.DoesNotContain("Argument '--planToken' is not recognized.", result.StdErr, StringComparison.Ordinal);
        Assert.DoesNotContain("Argument '--failFast' is not recognized.", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Call_WithReadIndexModeOption_ReturnsParseErrorAsSingleJson ()
    {
        var result = await CliProcessRunner.RunCommand(
            UcliCommandNames.Call,
            UcliContractConstants.CliOption.ReadIndexMode,
            UcliContractConstants.Config.ReadIndexModeDisabled);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Call,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
        Assert.Contains("Argument '--readIndexMode' is not recognized.", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Call_WhenDaemonIsNotRunning_PreservesDaemonNotRunningCode ()
    {
        using var scope = TestDirectories.CreateTempScope("call-cli-output-contract", "daemon-not-running");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        await UnityProjectTestFactory.WriteUcliUnityPluginMarker(scope, "UnityProject");

        var result = await CliProcessRunner.RunCommandWithStandardInput(
            CreateRequestJson(),
            UcliCommandNames.Call,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath,
            UcliContractConstants.CliOption.Mode,
            "daemon");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Call,
            IpcProtocol.StatusError,
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

        var result = await CliProcessRunner.RunCommandWithStandardInput(
            CreateRequestJson(),
            UcliCommandNames.Call,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath,
            UcliContractConstants.CliOption.Timeout,
            "abc");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Call,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
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

        var result = await CliProcessRunner.RunCommandWithStandardInput(
            CreateRequestJson(),
            UcliCommandNames.Call,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath,
            UcliContractConstants.CliOption.Mode,
            "unsupported");

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("call", "invalid-mode.json"),
            result.StdOut,
            CliOutputGoldenFiles.NormalizeRequestIds());
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
