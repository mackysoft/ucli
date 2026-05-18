using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Infrastructure.Project;
using MackySoft.Ucli.UnityIntegration.Indexing.Core;

namespace MackySoft.Ucli.Tests;

public sealed class ValidateCliOutputContractTests
{
    private const string UnknownOptionMessage = "Argument '--unknown' is not recognized.";

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Validate_WithUnknownOption_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
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
            [
                CreateGoDescribeEntry("""{"type":"object","required":["path"],"additionalProperties":false,"properties":{"path":{"type":"string"}}}"""),
            ]);

        var result = await CliProcessRunner.RunCommandWithStandardInputAsync(
            requestJson,
            UcliCommandNames.Validate,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath,
            UcliContractConstants.CliOption.ReadIndexMode,
            UcliContractConstants.Config.ReadIndexModeAllowStale);

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
        SeedOpsCatalog(
            unityProjectPath,
            [
                CreateGoDescribeEntry("""{"type":"object","required":["path"],"additionalProperties":false,"properties":{"path":{"type":"string"}}}"""),
            ]);

        var result = await CliProcessRunner.RunCommandWithStandardInputAsync(
            requestJson,
            UcliCommandNames.Validate,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath,
            UcliContractConstants.CliOption.ReadIndexMode,
            UcliContractConstants.Config.ReadIndexModeAllowStale);

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

        var result = await CliProcessRunner.RunCommandWithWorkingDirectoryAndStandardInputAsync(
            scope.FullPath,
            requestJson,
            UcliCommandNames.Validate,
            UcliContractConstants.CliOption.ReadIndexMode,
            UcliContractConstants.Config.ReadIndexModeDisabled);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.Validate,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
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

        var result = await CliProcessRunner.RunCommandWithWorkingDirectoryAndStandardInputAsync(
            unityProjectPath,
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
                sideEffects: Array.Empty<string>(),
                touchedKinds: Array.Empty<string>(),
                planMode: UcliOperationPlanModeValues.ObservesLiveUnity,
                planSemantics: "Validate arguments and observe Unity state without applying mutation.",
                callSemantics: "Read Unity state without applying mutation.",
                touchedContract: "Returns no touched resources.",
                readPostconditionContract: "Does not stale read surfaces by itself.",
                failureSemantics: "Failure means the observation was not fully produced.",
                dangerousNotes: Array.Empty<string>()),
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
        IReadOnlyList<IndexOpEntryJsonContract> operations)
    {
        var fingerprint = UnityProjectFingerprintCalculator.Create(unityProjectPath, unityProjectPath);
        var writer = new FileReadIndexArtifactWriter(
            new IndexOpsCatalogJsonContractWriter(),
            new IndexOpsDescribeJsonContractWriter(),
            new IndexAssetSearchLookupJsonContractWriter(),
            new IndexGuidPathLookupJsonContractWriter(),
            new IndexSceneTreeLiteLookupJsonContractWriter(),
            new IndexInputsManifestJsonContractWriter());
        writer.WriteOpsCatalogAsync(
                unityProjectPath,
                fingerprint,
                DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                operations,
                "source-hash",
                manifestInputSnapshot: null,
                CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();
    }

}
