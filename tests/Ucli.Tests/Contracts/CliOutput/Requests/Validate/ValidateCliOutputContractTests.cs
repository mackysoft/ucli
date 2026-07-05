using MackySoft.Tests;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Tests;

[Collection(CurrentDirectoryTestCollection.Name)]
public sealed class ValidateCliOutputContractTests
{
    private static readonly Lazy<ServiceProvider> SharedValidateServiceProvider = new(UcliServiceProviderTestFactory.CreateCore);

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Validate_WithUnknownOption_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        var result = await CliInProcessRunner.RunCommandAsync(
            UcliCommandNames.Validate,
            UcliContractConstants.CliOption.Unknown);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Validate);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
        CommandResultAssert.ReportsUnrecognizedArgument(
            outputJson.RootElement.GetProperty("message").GetString(),
            UcliContractConstants.CliOption.Unknown);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Validate_WithPreseededReadIndex_ReturnsJsonEnvelopeSuccess ()
    {
        using var scope = TestDirectories.CreateTempScope("validate-cli-output-contract", "success");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var requestJson = CreateRequestJson(
            operationName: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
            argsJson: """{"path":"Root"}""");
        ReadIndexCatalogTestSeeder.SeedOpsCatalog(
            unityProjectPath,
            [
                ReadIndexOperationTestFactory.CreateGoDescribeEntry("""{"type":"object","required":["path"],"additionalProperties":false,"properties":{"path":{"type":"string"}}}"""),
            ]);

        var result = await RunValidateCommandAsync(
            requestJson,
            projectPath: unityProjectPath,
            readIndexMode: UcliContractConstants.Config.ReadIndexModeAllowStale);

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("validate", "success.json"),
            result.StdOut,
            CliOutputGoldenFiles.NormalizeGeneratedAtUtc());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Validate_WhenStaticValidationFails_ReturnsErrorsAndReadIndexPayload ()
    {
        using var scope = TestDirectories.CreateTempScope("validate-cli-output-contract", "validation-failure");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var requestJson = CreateRequestJson(
            operationName: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
            argsJson: """{}""");
        ReadIndexCatalogTestSeeder.SeedOpsCatalog(
            unityProjectPath,
            [
                ReadIndexOperationTestFactory.CreateGoDescribeEntry("""{"type":"object","required":["path"],"additionalProperties":false,"properties":{"path":{"type":"string"}}}"""),
            ]);

        var result = await RunValidateCommandAsync(
            requestJson,
            projectPath: unityProjectPath,
            readIndexMode: UcliContractConstants.Config.ReadIndexModeAllowStale);

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("validate", "static-validation-error.json"),
            result.StdOut,
            CliOutputGoldenFiles.NormalizeGeneratedAtUtc());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Validate_WithExplicitDisabledModeOutsideUnityProject_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("validate-cli-output-contract", "disabled-syntax-only");
        var requestJson = """
            {
              "steps": []
            }
            """;
        using var currentDirectoryScope = new CurrentDirectoryScope(scope.FullPath);

        var result = await RunValidateCommandAsync(
            requestJson,
            readIndexMode: UcliContractConstants.Config.ReadIndexModeDisabled);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Validate);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "UNITY_PROJECT_MARKER_MISSING");
        Assert.False(outputJson.RootElement.GetProperty("payload").TryGetProperty("project", out _));
        Assert.False(outputJson.RootElement.GetProperty("payload").TryGetProperty("readIndex", out _));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Validate_WithExplicitDisabledModeInsideUnityProject_ReturnsProjectAndDisabledReadIndex ()
    {
        using var scope = TestDirectories.CreateTempScope("validate-cli-output-contract", "disabled-project");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var requestJson = """
            {
              "steps": []
            }
            """;
        using var currentDirectoryScope = new CurrentDirectoryScope(unityProjectPath);

        var result = await RunValidateCommandAsync(
            requestJson,
            readIndexMode: UcliContractConstants.Config.ReadIndexModeDisabled);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Validate);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);
        var projectPath = outputJson.RootElement
            .GetProperty("payload")
            .GetProperty("project")
            .GetProperty("projectPath")
            .GetString();
        Assert.False(string.IsNullOrWhiteSpace(projectPath));
        Assert.True(Path.IsPathFullyQualified(projectPath));
        Assert.Equal("UnityProject", Path.GetFileName(projectPath));
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasProperty("project", project => project
                    .HasString("unityVersion", "6000.1.4f1"))
                .HasProperty("readIndex", readIndex => readIndex
                    .HasBoolean("used", false)
                    .HasBoolean("hit", false)
                    .HasString("source", "index")
                    .HasString("freshness", "probable")
                    .HasString("fallbackReason", "readIndex disabled by mode.")));
    }

    private static Task<CommandExecutionResult> RunValidateCommandAsync (
        string requestJson,
        string? projectPath = null,
        string? timeout = null,
        string? readIndexMode = null)
    {
        return CommandResultCapture.ExecuteAsync(() =>
            ActivatorUtilities.CreateInstance<ValidateCommand>(
                    SharedValidateServiceProvider.Value,
                    RequestInputReaderStub.Success(requestJson),
                    CommandResultTestWriter.Create())
                .ValidateAsync(
                    projectPath: projectPath,
                    timeout: timeout,
                    readIndexMode: readIndexMode,
                    cancellationToken: CancellationToken.None));
    }

    private static string CreateRequestJson (
        string operationName,
        string argsJson)
    {
        return $$"""
            {
              "steps": [
                {
                  "kind": "op",
                  "id": "step-1",
                  "op": "{{operationName}}",
                  "args": {{argsJson}}
                }
              ]
            }
            """;
    }

}
