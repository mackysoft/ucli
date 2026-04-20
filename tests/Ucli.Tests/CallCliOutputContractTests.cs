using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Hosting.Cli;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;

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
        CommandResultAssert.HasSingleError(outputJson.RootElement, IpcErrorCodes.InvalidArgument);
        Assert.Contains(UnknownOptionMessage, result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Call_WithoutSuppliedRequestInput_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        var result = await CliProcessRunner.RunCommand(UcliCommandNames.Call);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Call,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, IpcErrorCodes.InvalidArgument);
        Assert.Contains(
            "Request JSON from standard input must not be empty.",
            outputJson.RootElement.GetProperty("message").GetString(),
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Call_WithRequestPathAndStandardInput_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        using var scope = TestDirectories.CreateTempScope("call-cli-output-contract", "ambiguous-input");
        var requestPath = Path.Combine(scope.FullPath, "request.json");
        await scope.WriteFileAsync("request.json", CreateRequestJson());

        var result = await CliProcessRunner.RunCommandWithWorkingDirectoryAndStandardInput(
            scope.FullPath,
            CreateRequestJson(),
            UcliCommandNames.Call,
            UcliContractConstants.CliOption.RequestPath,
            requestPath);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Call,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, IpcErrorCodes.InvalidArgument);
        Assert.Contains(
            "Request input source is ambiguous. Specify either --requestPath or redirected standard input.",
            outputJson.RootElement.GetProperty("message").GetString(),
            StringComparison.Ordinal);
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
        CommandResultAssert.HasSingleError(outputJson.RootElement, IpcErrorCodes.InvalidArgument);
        Assert.Contains("Argument '--readIndexMode' is not recognized.", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Call_WhenDaemonIsNotRunning_PreservesDaemonNotRunningCode ()
    {
        using var scope = TestDirectories.CreateTempScope("call-cli-output-contract", "daemon-not-running");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        await WriteUnityPluginMarker(scope, "UnityProject");

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
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("requestId", "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62")
                .HasArrayLength("opResults", 0))
            .HasProperty("errors", 0, error => error
                .HasString("code", UnityExecutionModeDecisionErrorCodes.DaemonNotRunning));
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
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("requestId", "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62")
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

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Call,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("requestId", "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62")
                .HasArrayLength("opResults", 0));
    }

    private static string CreateRequestJson ()
    {
        return JsonSerializer.Serialize(new
        {
            protocolVersion = IpcProtocol.CurrentVersion,
            requestId = "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
            steps = Array.Empty<object>(),
        });
    }

    private static Task WriteUnityPluginMarker (
        TestDirectoryScope scope,
        string unityProjectDirectoryName)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(unityProjectDirectoryName);

        return scope.WriteFileAsync(
            Path.Combine(
                unityProjectDirectoryName,
                "Assets",
                "MackySoft",
                "MackySoft.Ucli.Unity",
                "ucli-plugin.json"),
            """
            {
              "pluginId": "com.mackysoft.ucli.unity",
              "protocolVersion": 1
            }
            """);
    }
}