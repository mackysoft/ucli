using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Infrastructure.Project;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Tests;

public sealed class ValidateCliOutputContractTests
{
    private const string UnknownOptionMessage = "Argument '--unknown' is not recognized.";

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Validate_WithUnknownOption_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        var result = await CliProcessRunner.RunCommand(
            UcliCommandNames.Validate,
            UcliContractConstants.CliOption.Unknown);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.Validate,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
        Assert.Contains(UnknownOptionMessage, outputJson.RootElement.GetProperty("message").GetString(), StringComparison.Ordinal);
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
        SeedOpsCatalog(
            unityProjectPath,
            CreateOpsCatalog(
                new IndexOpEntryJsonContract(
                    Name: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
                    Kind: "query",
                    Policy: "safe",
                    ArgsSchemaJson: """{"type":"object","required":["path"],"additionalProperties":false,"properties":{"path":{"type":"string"}}}""")));

        var result = await CliProcessRunner.RunCommandWithStandardInput(
            requestJson,
            UcliCommandNames.Validate,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath,
            UcliContractConstants.CliOption.ReadIndexMode,
            UcliContractConstants.Config.ReadIndexModeAllowStale);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.Validate,
            status: "ok",
            exitCode: (int)CliExitCode.Success);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasProperty("readIndex", readIndex => readIndex
                    .HasBoolean("used", true)
                    .HasBoolean("hit", true)
                    .HasString("source", "index")
                    .HasString("freshness", "probable")
                    .IsNull("fallbackReason")));
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
        SeedOpsCatalog(
            unityProjectPath,
            CreateOpsCatalog(
                new IndexOpEntryJsonContract(
                    Name: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
                    Kind: "query",
                    Policy: "safe",
                    ArgsSchemaJson: """{"type":"object","required":["path"],"additionalProperties":false,"properties":{"path":{"type":"string"}}}""")));

        var result = await CliProcessRunner.RunCommandWithStandardInput(
            requestJson,
            UcliCommandNames.Validate,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath,
            UcliContractConstants.CliOption.ReadIndexMode,
            UcliContractConstants.Config.ReadIndexModeAllowStale);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.Validate,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasProperty("readIndex", readIndex => readIndex
                    .HasBoolean("used", true)
                    .HasBoolean("hit", true)
                    .HasString("source", "index")))
            .HasProperty("errors", errors => errors
                .HasArrayLength(1)
                .HasIndex(0, error => error
                    .HasString("code", "OPERATION_ARGS_INVALID")));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Validate_WithExplicitDisabledModeOutsideUnityProject_ReturnsSyntaxOnlySuccess ()
    {
        using var scope = TestDirectories.CreateTempScope("validate-cli-output-contract", "disabled-syntax-only");
        var requestJson = """
            {
              "steps": []
            }
            """;

        var result = await CliProcessRunner.RunCommandWithWorkingDirectoryAndStandardInput(
            scope.FullPath,
            requestJson,
            UcliCommandNames.Validate,
            UcliContractConstants.CliOption.ReadIndexMode,
            UcliContractConstants.Config.ReadIndexModeDisabled);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.Validate,
            status: "ok",
            exitCode: (int)CliExitCode.Success);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasProperty("readIndex", readIndex => readIndex
                    .HasBoolean("used", false)
                    .HasBoolean("hit", false)
                    .HasString("source", "index")
                    .HasString("freshness", "probable")
                    .HasString("fallbackReason", "readIndex disabled by mode.")));
    }

    private static IndexOpsCatalogJsonContract CreateOpsCatalog (params IndexOpEntryJsonContract[] entries)
    {
        return new IndexOpsCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Entries: entries);
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

    private static void SeedOpsCatalog (
        string unityProjectPath,
        IndexOpsCatalogJsonContract contract)
    {
        var fingerprint = UnityProjectFingerprintCalculator.Create(unityProjectPath, unityProjectPath);
        var catalogPath = UcliStoragePathResolver.ResolveOpsCatalogPath(unityProjectPath, fingerprint);
        var directoryPath = Path.GetDirectoryName(catalogPath)
            ?? throw new InvalidOperationException($"Directory path could not be resolved: {catalogPath}");
        Directory.CreateDirectory(directoryPath);
        File.WriteAllText(catalogPath, IndexOpsCatalogJsonContractSerializer.Serialize(contract));
    }

}
