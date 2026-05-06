using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Infrastructure.Project;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Tests;

public sealed class ValidateCliOutputContractTests
{
    private const string GoldenRoot = "tests/Ucli.Tests/GoldenFiles/Json/CliOutput";

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
                CreateGoDescribeEntry("""{"type":"object","required":["path"],"additionalProperties":false,"properties":{"path":{"type":"string"}}}""")));

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
        JsonGoldenFileAssert.Matches(
            Path.Combine(GoldenRoot, "validate", "success.json"),
            result.StdOut,
            CreateGeneratedAtNormalization());
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
                CreateGoDescribeEntry("""{"type":"object","required":["path"],"additionalProperties":false,"properties":{"path":{"type":"string"}}}""")));

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
        JsonGoldenFileAssert.Matches(
            Path.Combine(GoldenRoot, "validate", "static-validation-error.json"),
            result.StdOut,
            CreateGeneratedAtNormalization());
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

    private static IndexOpEntryJsonContract CreateGoDescribeEntry (string argsSchemaJson)
    {
        return new IndexOpEntryJsonContract(
            Name: UcliPrimitiveOperationNames.GoDescribe,
            Kind: "query",
            Policy: "safe",
            ArgsSchemaJson: argsSchemaJson,
            ResultSchemaJson: """{"type":"object"}""")
        {
            Description = "Returns a GameObject description including components and child hierarchy.",
            Inputs = Array.Empty<UcliOperationInputContract>(),
            ResultContract = UcliOperationResultContract.One<GameObjectDescriptionResult>("GameObject description result."),
            Assurance = new UcliOperationAssuranceContract(
                Array.Empty<string>(),
                mayDirty: false,
                mayPersist: false,
                Array.Empty<string>(),
                UcliOperationPlanModeValues.ObservesLiveUnity),
        };
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
        File.WriteAllText(catalogPath, new IndexOpsCatalogJsonContractWriter().Write(contract));
    }

    private static JsonGoldenFileNormalization CreateGeneratedAtNormalization ()
    {
        return JsonGoldenFileNormalization.Create().NormalizeStringProperty(
            "generatedAtUtc",
            "<timestamp>",
            static value => DateTimeOffset.TryParse(value, out _),
            "an ISO-8601 timestamp");
    }

}
