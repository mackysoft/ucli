using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Project;
using MackySoft.Ucli.UnityIntegration.Indexing.Core;

namespace MackySoft.Ucli.Tests;

public sealed class OpsCliOutputContractTests
{
    private const string UnknownOptionMessage = "Argument '--unknown' is not recognized.";

    private static readonly string[] FreezeInternalOperationFields =
    [
        "policyDerivation",
        "policyRestriction",
        "exposure",
        "policyReason",
    ];

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Ops_WithoutSubcommand_ReturnsJsonEnvelopeError ()
    {
        var result = await CliProcessRunner.RunCommandAsync(UcliCommandNames.Ops);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.Ops,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Ops_WithUnknownSubcommand_ReturnsJsonEnvelopeError ()
    {
        var result = await CliProcessRunner.RunCommandAsync(UcliCommandNames.Ops, "unknown");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.Ops,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task OpsList_WithUnknownOption_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Ops,
            UcliCommandNames.ListSubcommand,
            UcliContractConstants.CliOption.Unknown);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.OpsList,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
        Assert.Contains(UnknownOptionMessage, outputJson.RootElement.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(UcliContractConstants.CliOption.NameRegex, "[", "nameRegex is invalid")]
    [InlineData(UcliContractConstants.CliOption.Kind, "read", "kind must be one of")]
    [InlineData(UcliContractConstants.CliOption.MaxPolicy, "unsafe", "maxPolicy must be one of")]
    public async Task OpsList_WithInvalidFilterOption_ReturnsInvalidArgumentBeforeProjectResolution (
        string optionName,
        string optionValue,
        string expectedMessage)
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Ops,
            UcliCommandNames.ListSubcommand,
            optionName,
            optionValue);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.OpsList,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
        Assert.Contains(expectedMessage, outputJson.RootElement.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task OpsList_WithFailFastCamelCaseAlias_IsAcceptedByParser ()
    {
        using var scope = TestDirectories.CreateTempScope("ops-cli-output-contract", "list-fail-fast-camel-case");
        var invalidProjectPath = Path.Combine(scope.FullPath, "NotUnityProject");
        Directory.CreateDirectory(invalidProjectPath);

        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Ops,
            UcliCommandNames.ListSubcommand,
            UcliContractConstants.CliOption.FailFast,
            UcliContractConstants.CliOption.ProjectPath,
            invalidProjectPath);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.DoesNotContain("Argument '--failFast' is not recognized.", result.StdErr, StringComparison.Ordinal);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.OpsList,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: ProjectContextErrorCodes.UnityProjectMarkerMissing);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task OpsList_WithPreseededReadIndex_ReturnsJsonEnvelopeSuccess ()
    {
        using var scope = TestDirectories.CreateTempScope("ops-cli-output-contract", "list-success");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        SeedOpsCatalog(
            unityProjectPath,
            [
                CreateDescribedEntry(
                    name: UcliPrimitiveOperationNames.GoDescribe,
                    kind: "query",
                    policy: "safe",
                    argsSchemaJson: """{"type":"object","properties":{"path":{"type":"string"}}}""",
                    resultSchemaJson: """{"type":"object"}"""),
                CreateDescribedEntry(
                    name: UcliPrimitiveOperationNames.SceneSave,
                    kind: "mutation",
                    policy: "advanced",
                    argsSchemaJson: """{"type":"object","properties":{"path":{"type":"string"}}}""",
                    resultSchemaJson: null,
                    describe: UcliOperationDescribeContractBuilder.Create<ScenePathArgs, UcliNoResult>(
                        "Saves a Unity scene asset.",
                        CreateAssurance("mutation", "advanced"))),
            ]);

        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Ops,
            UcliCommandNames.ListSubcommand,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath,
            UcliContractConstants.CliOption.ReadIndexMode,
            UcliContractConstants.Config.ReadIndexModeAllowStale);

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("ops", "list-success.json"),
            result.StdOut,
            CliOutputGoldenFiles.NormalizeGeneratedAtUtc());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task OpsList_WithPreseededReadIndexAndFilters_ReturnsFilteredJsonEnvelopeSuccess ()
    {
        using var scope = TestDirectories.CreateTempScope("ops-cli-output-contract", "list-filtered-success");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        SeedOpsCatalog(
            unityProjectPath,
            [
                CreateDescribedEntry(
                    name: UcliPrimitiveOperationNames.GoDescribe,
                    kind: "query",
                    policy: "safe",
                    argsSchemaJson: """{"type":"object"}""",
                    resultSchemaJson: """{"type":"object"}"""),
                CreateDescribedEntry(
                    name: UcliPrimitiveOperationNames.SceneOpen,
                    kind: "command",
                    policy: "advanced",
                    argsSchemaJson: """{"type":"object"}""",
                    resultSchemaJson: null,
                    describe: UcliOperationDescribeContractBuilder.Create<ScenePathArgs, UcliNoResult>(
                        "Opens a Unity scene asset in the editor.",
                        CreateAssurance("command", "advanced"))),
                CreateDescribedEntry(
                    name: UcliPrimitiveOperationNames.AssetsFind,
                    kind: "command",
                    policy: "dangerous",
                    argsSchemaJson: """{"type":"object"}""",
                    resultSchemaJson: """{"type":"object"}"""),
                CreateDescribedEntry(
                    name: UcliPrimitiveOperationNames.SceneSave,
                    kind: "mutation",
                    policy: "advanced",
                    argsSchemaJson: """{"type":"object"}""",
                    resultSchemaJson: null),
                CreateDescribedEntry(
                    name: "custom.go.describe",
                    kind: "query",
                    policy: "safe",
                    argsSchemaJson: """{"type":"object"}""",
                    resultSchemaJson: """{"type":"object"}"""),
            ]);

        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Ops,
            UcliCommandNames.ListSubcommand,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath,
            UcliContractConstants.CliOption.ReadIndexMode,
            UcliContractConstants.Config.ReadIndexModeAllowStale,
            UcliContractConstants.CliOption.NameRegex,
            "^ucli\\.",
            UcliContractConstants.CliOption.Kind,
            "command",
            UcliContractConstants.CliOption.MaxPolicy,
            "advanced");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("ops", "list-filtered-success.json"),
            result.StdOut,
            CliOutputGoldenFiles.NormalizeGeneratedAtUtc());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task OpsList_WithPreseededReadIndexAndNoMatchingFilter_ReturnsEmptyOperations ()
    {
        using var scope = TestDirectories.CreateTempScope("ops-cli-output-contract", "list-filtered-empty");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        SeedOpsCatalog(
            unityProjectPath,
            [
                CreateDescribedEntry(
                    name: UcliPrimitiveOperationNames.GoDescribe,
                    kind: "query",
                    policy: "safe",
                    argsSchemaJson: """{"type":"object"}""",
                    resultSchemaJson: """{"type":"object"}"""),
            ]);

        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Ops,
            UcliCommandNames.ListSubcommand,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath,
            UcliContractConstants.CliOption.ReadIndexMode,
            UcliContractConstants.Config.ReadIndexModeAllowStale,
            UcliContractConstants.CliOption.NameRegex,
            "^no\\.such\\.operation$");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.OpsList,
            status: "ok",
            exitCode: (int)CliExitCode.Success);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasArrayLength("operations", 0)
                .HasProperty("readIndex", readIndex => readIndex
                    .HasString("source", "index")
                    .HasString("freshness", "probable")));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task OpsDescribe_WithPreseededReadIndex_ReturnsOperationSchema ()
    {
        using var scope = TestDirectories.CreateTempScope("ops-cli-output-contract", "describe-success");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        SeedOpsCatalog(
            unityProjectPath,
            [
                CreateDescribedEntry(
                    name: UcliPrimitiveOperationNames.GoDescribe,
                    kind: "query",
                    policy: "safe",
                    argsSchemaJson: """{"type":"object","properties":{"path":{"type":"string"}}}""",
                    resultSchemaJson: """{"type":"object"}"""),
            ]);

        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Ops,
            UcliCommandNames.DescribeSubcommand,
            UcliPrimitiveOperationNames.GoDescribe,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath,
            UcliContractConstants.CliOption.ReadIndexMode,
            UcliContractConstants.Config.ReadIndexModeAllowStale);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.OpsDescribe,
            status: "ok",
            exitCode: (int)CliExitCode.Success);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasProperty("operation", operation => operation
                    .HasString("name", UcliPrimitiveOperationNames.GoDescribe)
                    .HasString("kind", "query")
                    .HasString("policy", "safe")
                    .HasString("description", "Returns a GameObject description including components and child hierarchy.")
                    .HasProperty("inputs")
                    .HasProperty("resultContract", resultContract => resultContract
                        .HasBoolean("emitted", true)
                        .HasString("resultType", "GameObjectDescriptionResult"))
                    .HasProperty("assurance", assurance => assurance
                        .HasArrayLength("sideEffects", 1)
                        .HasBoolean("mayDirty", false)
                        .HasBoolean("mayPersist", false)
                        .HasString("planMode", "observesLiveUnity"))
                    .HasProperty("argsSchema", argsSchema => argsSchema
                        .HasString("type", "object")
                        .HasProperty("properties", properties => properties
                            .HasProperty("path", path => path
                                .HasString("type", "string"))))
                    .HasProperty("resultSchema", resultSchema => resultSchema
                        .HasString("type", "object")))
                .HasProperty("readIndex", readIndex => readIndex
                    .HasString("source", "index")
                    .HasString("freshness", "probable")));
        var operationElement = outputJson.RootElement.GetProperty("payload").GetProperty("operation");
        Assert.False(operationElement.TryGetProperty("outputs", out _));
        AssertDescribeVariantFields(operationElement);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task OpsDescribe_WithNoResultOperation_ReturnsNullResultSchema ()
    {
        using var scope = TestDirectories.CreateTempScope("ops-cli-output-contract", "describe-no-result");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        SeedOpsCatalog(
            unityProjectPath,
            [
                CreateDescribedEntry(
                    name: UcliPrimitiveOperationNames.SceneOpen,
                    kind: "command",
                    policy: "safe",
                    argsSchemaJson: """{"type":"object","properties":{"path":{"type":"string"}}}""",
                    resultSchemaJson: null,
                    describe: UcliOperationDescribeContractBuilder.Create<ScenePathArgs, UcliNoResult>(
                        "Opens a Unity scene asset in the editor.",
                        CreateAssurance("command", "safe"))),
            ]);

        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Ops,
            UcliCommandNames.DescribeSubcommand,
            UcliPrimitiveOperationNames.SceneOpen,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath,
            UcliContractConstants.CliOption.ReadIndexMode,
            UcliContractConstants.Config.ReadIndexModeAllowStale);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasProperty("operation", operation => operation
                    .HasProperty("resultContract", resultContract => resultContract
                        .HasBoolean("emitted", false)
                        .HasString("resultType", nameof(UcliNoResult)))
                    .IsNull("resultSchema")));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task OpsDescribe_WithQueryOperation_MatchesGolden ()
    {
        using var scope = TestDirectories.CreateTempScope("ops-cli-output-contract", "describe-query-golden");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        SeedOpsCatalog(
            unityProjectPath,
            [
                CreateDescribedEntry(
                    name: UcliPrimitiveOperationNames.Resolve,
                    kind: "query",
                    policy: "safe",
                    argsSchemaJson: """{"type":"object"}""",
                    resultSchemaJson: """{"type":"object"}""",
                    describe: new UcliOperationDescribeContract(
                        "Resolves a Unity object reference.",
                        Array.Empty<UcliOperationInputContract>(),
                        new UcliOperationResultContract(
                            emitted: true,
                            resultType: "QueryResult",
                            description: "Query result."),
                        CreateAssurance("query", "safe"))),
            ]);

        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Ops,
            UcliCommandNames.DescribeSubcommand,
            UcliPrimitiveOperationNames.Resolve,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath,
            UcliContractConstants.CliOption.ReadIndexMode,
            UcliContractConstants.Config.ReadIndexModeAllowStale);

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("ops", "describe-query-success.json"),
            result.StdOut,
            CliOutputGoldenFiles.NormalizeGeneratedAtUtc());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task OpsDescribe_WithNoResultOperation_MatchesGolden ()
    {
        using var scope = TestDirectories.CreateTempScope("ops-cli-output-contract", "describe-no-result-golden");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        SeedOpsCatalog(
            unityProjectPath,
            [
                CreateDescribedEntry(
                    name: UcliPrimitiveOperationNames.SceneOpen,
                    kind: "command",
                    policy: "safe",
                    argsSchemaJson: """{"type":"object"}""",
                    resultSchemaJson: null,
                    describe: new UcliOperationDescribeContract(
                        "Opens a Unity scene asset in the editor.",
                        Array.Empty<UcliOperationInputContract>(),
                        UcliOperationResultContract.NoResult("No operation-specific result is emitted."),
                        CreateAssurance("command", "safe"))),
            ]);

        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Ops,
            UcliCommandNames.DescribeSubcommand,
            UcliPrimitiveOperationNames.SceneOpen,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath,
            UcliContractConstants.CliOption.ReadIndexMode,
            UcliContractConstants.Config.ReadIndexModeAllowStale);

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("ops", "describe-no-result-success.json"),
            result.StdOut,
            CliOutputGoldenFiles.NormalizeGeneratedAtUtc());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task OpsDescribe_WithVariantInputs_MatchesGoldenAndOmitsFreezeInternalFields ()
    {
        const string operationName = "custom.variant.describe";

        using var scope = TestDirectories.CreateTempScope("ops-cli-output-contract", "describe-variant-golden");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        SeedOpsCatalog(
            unityProjectPath,
            [
                CreateDescribedEntry(
                    name: operationName,
                    kind: "query",
                    policy: "safe",
                    argsSchemaJson:
                        """
                        {"type":"object","additionalProperties":false,"required":["target"],"properties":{"target":{"type":"object"},"depth":{"type":["integer","null"]}}}
                        """,
                    resultSchemaJson: """{"type":"object"}""",
                    describe: CreateVariantDescribeContract()),
            ]);

        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Ops,
            UcliCommandNames.DescribeSubcommand,
            operationName,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath,
            UcliContractConstants.CliOption.ReadIndexMode,
            UcliContractConstants.Config.ReadIndexModeAllowStale);

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("ops", "describe-variant-success.json"),
            result.StdOut,
            CliOutputGoldenFiles.NormalizeGeneratedAtUtc());

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        var operation = outputJson.RootElement.GetProperty("payload").GetProperty("operation");
        AssertNoFreezeInternalOperationTopLevelFields(operation);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task OpsList_WhenPublicCatalogIsRead_DoesNotEmitExposureField ()
    {
        using var scope = TestDirectories.CreateTempScope("ops-cli-output-contract", "list-no-exposure-field");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        SeedOpsCatalog(
            unityProjectPath,
            [
                CreateDescribedEntry(
                    name: UcliPrimitiveOperationNames.GoDescribe,
                    kind: "query",
                    policy: "safe",
                    argsSchemaJson: """{"type":"object"}""",
                    resultSchemaJson: """{"type":"object"}"""),
            ]);

        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Ops,
            UcliCommandNames.ListSubcommand,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath,
            UcliContractConstants.CliOption.ReadIndexMode,
            UcliContractConstants.Config.ReadIndexModeAllowStale);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        var operations = outputJson.RootElement.GetProperty("payload").GetProperty("operations").EnumerateArray().ToArray();
        Assert.Single(operations);
        Assert.Equal(UcliPrimitiveOperationNames.GoDescribe, operations[0].GetProperty("name").GetString());
        Assert.False(operations[0].TryGetProperty("exposure", out _));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task OpsDescribe_WhenPublicCatalogIsRead_DoesNotEmitExposureField ()
    {
        using var scope = TestDirectories.CreateTempScope("ops-cli-output-contract", "describe-no-exposure-field");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        SeedOpsCatalog(
            unityProjectPath,
            [
                CreateDescribedEntry(
                    name: UcliPrimitiveOperationNames.GoDescribe,
                    kind: "query",
                    policy: "safe",
                    argsSchemaJson: """{"type":"object"}""",
                    resultSchemaJson: """{"type":"object"}"""),
            ]);

        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Ops,
            UcliCommandNames.DescribeSubcommand,
            UcliPrimitiveOperationNames.GoDescribe,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath,
            UcliContractConstants.CliOption.ReadIndexMode,
            UcliContractConstants.Config.ReadIndexModeAllowStale);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        var operation = outputJson.RootElement.GetProperty("payload").GetProperty("operation");
        Assert.Equal(UcliPrimitiveOperationNames.GoDescribe, operation.GetProperty("name").GetString());
        Assert.False(operation.TryGetProperty("exposure", out _));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task OpsDescribe_WithUnknownOperation_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("ops-cli-output-contract", "describe-unknown-operation");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        SeedOpsCatalog(
            unityProjectPath,
            [
                CreateDescribedEntry(
                    name: UcliPrimitiveOperationNames.GoDescribe,
                    kind: "query",
                    policy: "safe",
                    argsSchemaJson: """{"type":"object"}""",
                    resultSchemaJson: """{"type":"object"}"""),
            ]);

        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Ops,
            UcliCommandNames.DescribeSubcommand,
            "ucli.unknown",
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath,
            UcliContractConstants.CliOption.ReadIndexMode,
            UcliContractConstants.Config.ReadIndexModeAllowStale);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.OpsDescribe,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task OpsList_WhenIndexHitAndTimeoutIsInvalid_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("ops-cli-output-contract", "list-index-hit-invalid-timeout");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        SeedOpsCatalog(
            unityProjectPath,
            [
                CreateDescribedEntry(
                    name: UcliPrimitiveOperationNames.GoDescribe,
                    kind: "query",
                    policy: "safe",
                    argsSchemaJson: """{"type":"object"}""",
                    resultSchemaJson: """{"type":"object"}"""),
            ]);

        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Ops,
            UcliCommandNames.ListSubcommand,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath,
            UcliContractConstants.CliOption.ReadIndexMode,
            UcliContractConstants.Config.ReadIndexModeAllowStale,
            UcliContractConstants.CliOption.Timeout,
            "abc");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.OpsList,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
        Assert.Contains("timeout", outputJson.RootElement.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task OpsList_WhenIndexHitAndModeIsInvalid_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("ops-cli-output-contract", "list-index-hit-invalid-mode");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        SeedOpsCatalog(
            unityProjectPath,
            [
                CreateDescribedEntry(
                    name: UcliPrimitiveOperationNames.GoDescribe,
                    kind: "query",
                    policy: "safe",
                    argsSchemaJson: """{"type":"object"}""",
                    resultSchemaJson: """{"type":"object"}"""),
            ]);

        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Ops,
            UcliCommandNames.ListSubcommand,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath,
            UcliContractConstants.CliOption.ReadIndexMode,
            UcliContractConstants.Config.ReadIndexModeAllowStale,
            UcliContractConstants.CliOption.Mode,
            "unsupported");

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        JsonGoldenFileAssert.Matches(CliOutputGoldenFiles.GetPath("ops", "list-invalid-mode.json"), result.StdOut);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task OpsList_WhenReadIndexDisabledAndTimeoutIsInvalid_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("ops-cli-output-contract", "list-disabled-invalid-timeout");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");

        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Ops,
            UcliCommandNames.ListSubcommand,
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
            command: UcliCommandNames.OpsList,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
        Assert.Contains("timeout", outputJson.RootElement.GetProperty("message").GetString(), StringComparison.Ordinal);
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

    private static IndexOpEntryJsonContract CreateDescribedEntry (
        string name,
        string kind,
        string policy,
        string argsSchemaJson,
        string? resultSchemaJson = null,
        UcliOperationDescribeContract? describe = null)
    {
        var useDefaultDescribe = describe == null;
        describe ??= CreateGoDescribeContract();
        return new IndexOpEntryJsonContract(
            name,
            kind,
            policy,
            argsSchemaJson,
            resultSchemaJson)
        {
            Description = describe.Description,
            Inputs = describe.Inputs,
            ResultContract = describe.ResultContract,
            Assurance = useDefaultDescribe ? CreateAssurance(kind, policy) : describe.Assurance,
            CodeContract = describe.CodeContract,
        };
    }

    private static void AssertDescribeVariantFields (JsonElement operationElement)
    {
        var targetInput = Assert.Single(
            operationElement.GetProperty("inputs").EnumerateArray(),
            candidate => string.Equals(candidate.GetProperty("name").GetString(), "target", StringComparison.Ordinal));
        var globalObjectIdVariant = Assert.Single(
            targetInput.GetProperty("variants").EnumerateArray(),
            candidate => string.Equals(candidate.GetProperty("name").GetString(), "byGlobalObjectId", StringComparison.Ordinal));
        Assert.False(globalObjectIdVariant.TryGetProperty("argsPaths", out _));
        Assert.False(globalObjectIdVariant.TryGetProperty("constraints", out _));

        var field = Assert.Single(globalObjectIdVariant.GetProperty("fields").EnumerateArray());
        Assert.Equal("globalObjectId", field.GetProperty("name").GetString());
        Assert.Equal("$.target.globalObjectId", field.GetProperty("argsPath").GetString());
        Assert.Equal("Resolved Unity GlobalObjectId.", field.GetProperty("description").GetString());

        var constraint = Assert.Single(field.GetProperty("constraints").EnumerateArray());
        Assert.Equal("globalObjectId", constraint.GetProperty("kind").GetString());

        var sceneHierarchyVariant = Assert.Single(
            targetInput.GetProperty("variants").EnumerateArray(),
            candidate => string.Equals(candidate.GetProperty("name").GetString(), "bySceneHierarchyPath", StringComparison.Ordinal));
        Assert.False(sceneHierarchyVariant.TryGetProperty("argsPaths", out _));
        Assert.False(sceneHierarchyVariant.TryGetProperty("constraints", out _));

        var sceneField = Assert.Single(
            sceneHierarchyVariant.GetProperty("fields").EnumerateArray(),
            candidate => string.Equals(candidate.GetProperty("name").GetString(), "scene", StringComparison.Ordinal));
        Assert.Equal("$.target.scene", sceneField.GetProperty("argsPath").GetString());
        Assert.Equal("Scene asset path for a hierarchy selector.", sceneField.GetProperty("description").GetString());
        var sceneConstraint = Assert.Single(
            sceneField.GetProperty("constraints").EnumerateArray(),
            constraint => string.Equals(constraint.GetProperty("kind").GetString(), "assetExists", StringComparison.Ordinal));
        Assert.Equal("assetExists", sceneConstraint.GetProperty("kind").GetString());
        Assert.Equal("scene", sceneConstraint.GetProperty("assetKind").GetString());

        var hierarchyPathField = Assert.Single(
            sceneHierarchyVariant.GetProperty("fields").EnumerateArray(),
            candidate => string.Equals(candidate.GetProperty("name").GetString(), "hierarchyPath", StringComparison.Ordinal));
        Assert.Equal("$.target.hierarchyPath", hierarchyPathField.GetProperty("argsPath").GetString());
        Assert.Equal("Unity hierarchy path inside the selected scene or prefab.", hierarchyPathField.GetProperty("description").GetString());
        var hierarchyPathConstraint = Assert.Single(
            hierarchyPathField.GetProperty("constraints").EnumerateArray(),
            constraint => string.Equals(constraint.GetProperty("kind").GetString(), "hierarchyPath", StringComparison.Ordinal));
        Assert.Equal("hierarchyPath", hierarchyPathConstraint.GetProperty("kind").GetString());
    }

    private static UcliOperationDescribeContract CreateGoDescribeContract ()
    {
        return UcliOperationDescribeContractBuilder.Create<GoDescribeArgs, GameObjectDescriptionResult>(
            "Returns a GameObject description including components and child hierarchy.",
            new UcliOperationAssuranceContract(
                sideEffects: new[] { UcliOperationSideEffect.ObservesUnityState },
                touchedKinds: Array.Empty<string>(),
                planMode: UcliOperationPlanMode.ObservesLiveUnity,
                planSemantics: "Validate arguments and observe Unity state without applying mutation.",
                callSemantics: "Read Unity state without applying mutation.",
                touchedContract: "Returns no touched resources.",
                readPostconditionContract: "Does not stale read surfaces by itself.",
                failureSemantics: "Failure means the observation was not fully produced.",
                dangerousNotes: Array.Empty<string>()));
    }

    private static UcliOperationDescribeContract CreateVariantDescribeContract ()
    {
        return new UcliOperationDescribeContract(
            "Returns a GameObject description including components and child hierarchy.",
            [
                new UcliOperationInputContract(
                    name: "target",
                    valueType: "object",
                    description: "Target GameObject reference.",
                    constraints:
                    [
                        new UcliOperationInputConstraintContract("referenceResolvable")
                        {
                            TargetKind = "gameObject",
                        },
                    ],
                    argsPath: "$.target",
                    variants:
                    [
                        new UcliOperationInputVariantContract(
                            name: "byGlobalObjectId",
                            description: "Use resolved Unity GlobalObjectId.",
                            fields:
                            [
                                new UcliOperationInputVariantFieldContract(
                                    name: "globalObjectId",
                                    argsPath: "$.target.globalObjectId",
                                    description: "Resolved Unity GlobalObjectId.",
                                    constraints:
                                    [
                                        new UcliOperationInputConstraintContract("globalObjectId"),
                                    ]),
                            ]),
                        new UcliOperationInputVariantContract(
                            name: "bySceneHierarchyPath",
                            description: "Use Scene asset path for a hierarchy selector and Unity hierarchy path inside the selected scene or prefab.",
                            fields:
                            [
                                new UcliOperationInputVariantFieldContract(
                                    name: "scene",
                                    argsPath: "$.target.scene",
                                    description: "Scene asset path for a hierarchy selector.",
                                    constraints:
                                    [
                                        new UcliOperationInputConstraintContract("assetExists")
                                        {
                                            AssetKind = "scene",
                                        },
                                    ]),
                                new UcliOperationInputVariantFieldContract(
                                    name: "hierarchyPath",
                                    argsPath: "$.target.hierarchyPath",
                                    description: "Unity hierarchy path inside the selected scene or prefab.",
                                    constraints:
                                    [
                                        new UcliOperationInputConstraintContract("hierarchyPath"),
                                    ]),
                            ]),
                    ]),
                new UcliOperationInputContract(
                    name: "depth",
                    valueType: "integer",
                    description: "Maximum child hierarchy depth to include; null means unbounded.",
                    constraints:
                    [
                        new UcliOperationInputConstraintContract("range")
                        {
                            Min = 0,
                        },
                    ]),
            ],
            new UcliOperationResultContract(
                emitted: true,
                resultType: "GameObjectDescriptionResult",
                description: "GameObject describe operation result."),
            CreateAssurance("query", "safe"));
    }

    private static UcliOperationAssuranceContract CreateAssurance (
        string kind,
        string policy)
    {
        var isMutation = string.Equals(kind, "mutation", StringComparison.Ordinal);
        var isAdvancedCommand = string.Equals(kind, "command", StringComparison.Ordinal)
            && string.Equals(policy, "advanced", StringComparison.Ordinal);
        var isDangerousPolicy = string.Equals(policy, "dangerous", StringComparison.Ordinal);
        var isRiskyPolicy = !string.Equals(policy, "safe", StringComparison.Ordinal);
        return new UcliOperationAssuranceContract(
            sideEffects: isDangerousPolicy
                ? [UcliOperationSideEffect.ExternalProcess]
                : isMutation ? [UcliOperationSideEffect.SceneSave]
                : isAdvancedCommand ? [UcliOperationSideEffect.EditorStateChange]
                : [UcliOperationSideEffect.ObservesUnityState],
            touchedKinds: isMutation ? [UcliTouchedResourceKindNames.Scene] : Array.Empty<string>(),
            planMode: UcliOperationPlanMode.ObservesLiveUnity,
            planSemantics: "Validate arguments and observe Unity state without applying mutation.",
            callSemantics: isMutation ? "Persist save-relevant Unity state." : "Read Unity state without applying mutation.",
            touchedContract: isMutation ? "Reports the saved scene resource." : "Returns no touched resources.",
            readPostconditionContract: isMutation ? "Saved scene read surfaces may be stale after a successful call." : "Does not stale read surfaces by itself.",
            failureSemantics: isMutation ? "Save failure may leave partial or indeterminate scene file changes." : "Failure means the observation was not fully produced.",
            dangerousNotes: isRiskyPolicy ? ["Fixture operation has policy-specific risk metadata for contract validation."] : Array.Empty<string>());
    }

    private static void AssertNoFreezeInternalOperationTopLevelFields (JsonElement operation)
    {
        foreach (var property in operation.EnumerateObject())
        {
            Assert.DoesNotContain(property.Name, FreezeInternalOperationFields);
        }
    }
}
