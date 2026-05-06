using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.UnityIntegration.Project.Plugin;

namespace MackySoft.Ucli.Tests;

public sealed class PlanCliOutputContractTests
{
    private const string UnknownOptionMessage = "Argument '--unknown' is not recognized.";

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Plan_WithUnknownOption_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        var result = await CliProcessRunner.RunCommand(UcliCommandNames.Plan, UcliContractConstants.CliOption.Unknown);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Plan,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, IpcErrorCodes.InvalidArgument);
        Assert.Contains(UnknownOptionMessage, result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Plan_WhenRequestStepsPropertyIsMissing_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        var result = await CliProcessRunner.RunCommandWithStandardInput(
            """{}""",
            UcliCommandNames.Plan);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Plan,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, IpcErrorCodes.InvalidArgument);
        Assert.Contains(
            "Request property 'steps' is required.",
            outputJson.RootElement.GetProperty("message").GetString(),
            StringComparison.Ordinal);
        Assert.False(outputJson.RootElement.GetProperty("payload").EnumerateObject().MoveNext());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Plan_WithInvalidReadIndexMode_ReturnsInvalidArgumentErrorWithoutPayload ()
    {
        var result = await CliProcessRunner.RunCommand(
            UcliCommandNames.Plan,
            UcliContractConstants.CliOption.ReadIndexMode,
            "unsupported");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Plan,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, IpcErrorCodes.InvalidArgument);
        Assert.False(outputJson.RootElement.GetProperty("payload").EnumerateObject().MoveNext());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Plan_WithFailFastCamelCaseAlias_IsAcceptedByParser ()
    {
        using var scope = TestDirectories.CreateTempScope("plan-cli-output-contract", "fail-fast-camel-case");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");

        var result = await CliProcessRunner.RunCommandWithStandardInput(
            CreateRequestJson(),
            UcliCommandNames.Plan,
            UcliContractConstants.CliOption.FailFast,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath,
            UcliContractConstants.CliOption.ReadIndexMode,
            UcliContractConstants.Config.ReadIndexModeDisabled,
            UcliContractConstants.CliOption.Mode,
            "unsupported");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.DoesNotContain("Argument '--failFast' is not recognized.", result.StdErr, StringComparison.Ordinal);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Plan,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, IpcErrorCodes.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Plan_WithInvalidTimeoutOption_ReturnsInvalidArgumentAndPreservesPreflightPayload ()
    {
        using var scope = TestDirectories.CreateTempScope("plan-cli-output-contract", "invalid-timeout");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");

        var result = await CliProcessRunner.RunCommandWithStandardInput(
            CreateRequestJson(),
            UcliCommandNames.Plan,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath,
            UcliContractConstants.CliOption.ReadIndexMode,
            UcliContractConstants.Config.ReadIndexModeDisabled,
            UcliContractConstants.CliOption.Timeout,
            "abc");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Plan,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, IpcErrorCodes.InvalidArgument);
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

        var result = await CliProcessRunner.RunCommandWithStandardInput(
            CreateRequestJson(),
            UcliCommandNames.Plan,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath,
            UcliContractConstants.CliOption.ReadIndexMode,
            UcliContractConstants.Config.ReadIndexModeDisabled,
            UcliContractConstants.CliOption.Mode,
            "unsupported");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Plan,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, IpcErrorCodes.InvalidArgument);
        AssertPayloadHasGeneratedRequestId(outputJson.RootElement);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasArrayLength("opResults", 0)
                .HasProperty("readIndex", readIndex => readIndex
                    .HasBoolean("used", false)
                    .HasBoolean("hit", false)
                    .HasString("fallbackReason", "readIndex disabled by mode.")));
        Assert.False(outputJson.RootElement.GetProperty("payload").TryGetProperty("planToken", out _));
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("plan", "invalid-mode.json"),
            result.StdOut,
            new JsonGoldenFileNormalization().NormalizeRequestIds());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Plan_WhenDaemonIsNotRunning_ReturnsToolErrorAndPreservesPreflightPayload ()
    {
        using var scope = TestDirectories.CreateTempScope("plan-cli-output-contract", "daemon-not-running");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        await WriteUnityPluginMarker(scope, "UnityProject");

        var result = await CliProcessRunner.RunCommandWithStandardInput(
            CreateRequestJson(),
            UcliCommandNames.Plan,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath,
            UcliContractConstants.CliOption.ReadIndexMode,
            UcliContractConstants.Config.ReadIndexModeDisabled,
            UcliContractConstants.CliOption.Mode,
            "daemon");

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
                UnityUcliPluginLocator.MarkerFileName),
            """
            {
              "pluginId": "com.mackysoft.ucli.unity",
              "protocolVersion": 1
            }
            """);
    }
}
