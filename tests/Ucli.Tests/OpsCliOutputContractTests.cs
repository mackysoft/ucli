using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Infrastructure.Project;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Tests;

public sealed class OpsCliOutputContractTests
{
    private const string UnknownOptionMessage = "Argument '--unknown' is not recognized.";

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Ops_WithoutSubcommand_ReturnsJsonEnvelopeError ()
    {
        var result = await CliProcessRunner.RunCommand(UcliCommandNames.Ops);

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
        var result = await CliProcessRunner.RunCommand(UcliCommandNames.Ops, "unknown");

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
        var result = await CliProcessRunner.RunCommand(
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

    [Fact]
    [Trait("Size", "Medium")]
    public async Task OpsList_WithFailFastCamelCaseAlias_IsAcceptedByParser ()
    {
        using var scope = TestDirectories.CreateTempScope("ops-cli-output-contract", "list-fail-fast-camel-case");
        var invalidProjectPath = Path.Combine(scope.FullPath, "NotUnityProject");
        Directory.CreateDirectory(invalidProjectPath);

        var result = await CliProcessRunner.RunCommand(
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
            expectedCode: "INVALID_ARGUMENT");
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task OpsList_WithPreseededReadIndex_ReturnsJsonEnvelopeSuccess ()
    {
        using var scope = TestDirectories.CreateTempScope("ops-cli-output-contract", "list-success");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        SeedOpsCatalog(
            unityProjectPath,
            new IndexOpsCatalogJsonContract(
                SchemaVersion: 1,
                GeneratedAtUtc: DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                SourceInputsHash: "source-hash",
                Entries:
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
                            new UcliOperationAssuranceContract(
                                Array.Empty<UcliOperationSideEffect>(),
                                mayDirty: false,
                                mayPersist: true,
                                Array.Empty<string>(),
                                UcliOperationPlanMode.ObservesLiveUnity))),
                ]));

        var result = await CliProcessRunner.RunCommand(
            UcliCommandNames.Ops,
            UcliCommandNames.ListSubcommand,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath,
            UcliContractConstants.CliOption.ReadIndexMode,
            UcliContractConstants.Config.ReadIndexModeAllowStale);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.OpsList,
            status: "ok",
            exitCode: (int)CliExitCode.Success);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasArrayLength("operations", 2)
                .HasProperty("operations", 0, operation => operation
                    .HasString("name", UcliPrimitiveOperationNames.GoDescribe)
                    .HasString("kind", "query")
                    .HasString("policy", "safe"))
                .HasProperty("readIndex", readIndex => readIndex
                    .HasBoolean("used", true)
                    .HasBoolean("hit", true)
                    .HasString("source", "index")
                    .HasString("freshness", "probable")
                    .IsNull("fallbackReason")));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task OpsDescribe_WithPreseededReadIndex_ReturnsOperationSchema ()
    {
        using var scope = TestDirectories.CreateTempScope("ops-cli-output-contract", "describe-success");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        SeedOpsCatalog(
            unityProjectPath,
            new IndexOpsCatalogJsonContract(
                SchemaVersion: 1,
                GeneratedAtUtc: DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                SourceInputsHash: "source-hash",
                Entries:
                [
                    CreateDescribedEntry(
                        name: UcliPrimitiveOperationNames.GoDescribe,
                        kind: "query",
                        policy: "safe",
                        argsSchemaJson: """{"type":"object","properties":{"path":{"type":"string"}}}""",
                        resultSchemaJson: """{"type":"object"}"""),
                ]));

        var result = await CliProcessRunner.RunCommand(
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
                        .HasArrayLength("sideEffects", 0)
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
            new IndexOpsCatalogJsonContract(
                SchemaVersion: 1,
                GeneratedAtUtc: DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                SourceInputsHash: "source-hash",
                Entries:
                [
                    CreateDescribedEntry(
                        name: UcliPrimitiveOperationNames.SceneOpen,
                        kind: "command",
                        policy: "safe",
                        argsSchemaJson: """{"type":"object","properties":{"path":{"type":"string"}}}""",
                        resultSchemaJson: null,
                        describe: UcliOperationDescribeContractBuilder.Create<ScenePathArgs, UcliNoResult>(
                            "Opens a Unity scene asset in the editor.",
                            new UcliOperationAssuranceContract(
                                Array.Empty<UcliOperationSideEffect>(),
                                mayDirty: false,
                                mayPersist: false,
                                Array.Empty<string>(),
                                UcliOperationPlanMode.ObservesLiveUnity))),
                ]));

        var result = await CliProcessRunner.RunCommand(
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
    public async Task OpsDescribe_WithUnknownOperation_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("ops-cli-output-contract", "describe-unknown-operation");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        SeedOpsCatalog(
            unityProjectPath,
            new IndexOpsCatalogJsonContract(
                SchemaVersion: 1,
                GeneratedAtUtc: DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                SourceInputsHash: "source-hash",
                Entries:
                [
                    CreateDescribedEntry(
                        name: UcliPrimitiveOperationNames.GoDescribe,
                        kind: "query",
                        policy: "safe",
                        argsSchemaJson: """{"type":"object"}""",
                        resultSchemaJson: """{"type":"object"}"""),
                ]));

        var result = await CliProcessRunner.RunCommand(
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
            new IndexOpsCatalogJsonContract(
                SchemaVersion: 1,
                GeneratedAtUtc: DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                SourceInputsHash: "source-hash",
                Entries:
                [
                    CreateDescribedEntry(
                        name: UcliPrimitiveOperationNames.GoDescribe,
                        kind: "query",
                        policy: "safe",
                        argsSchemaJson: """{"type":"object"}""",
                        resultSchemaJson: """{"type":"object"}"""),
                ]));

        var result = await CliProcessRunner.RunCommand(
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
            new IndexOpsCatalogJsonContract(
                SchemaVersion: 1,
                GeneratedAtUtc: DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                SourceInputsHash: "source-hash",
                Entries:
                [
                    CreateDescribedEntry(
                        name: UcliPrimitiveOperationNames.GoDescribe,
                        kind: "query",
                        policy: "safe",
                        argsSchemaJson: """{"type":"object"}""",
                        resultSchemaJson: """{"type":"object"}"""),
                ]));

        var result = await CliProcessRunner.RunCommand(
            UcliCommandNames.Ops,
            UcliCommandNames.ListSubcommand,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath,
            UcliContractConstants.CliOption.ReadIndexMode,
            UcliContractConstants.Config.ReadIndexModeAllowStale,
            UcliContractConstants.CliOption.Mode,
            "unsupported");

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
        Assert.Contains("Mode must be auto, daemon, or oneshot.", outputJson.RootElement.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task OpsList_WhenReadIndexDisabledAndTimeoutIsInvalid_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("ops-cli-output-contract", "list-disabled-invalid-timeout");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");

        var result = await CliProcessRunner.RunCommand(
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
        IndexOpsCatalogJsonContract contract)
    {
        var fingerprint = UnityProjectFingerprintCalculator.Create(unityProjectPath, unityProjectPath);
        var catalogPath = UcliStoragePathResolver.ResolveOpsCatalogPath(unityProjectPath, fingerprint);
        var directoryPath = Path.GetDirectoryName(catalogPath)
            ?? throw new InvalidOperationException($"Directory path could not be resolved: {catalogPath}");
        Directory.CreateDirectory(directoryPath);
        File.WriteAllText(catalogPath, IndexOpsCatalogJsonContractSerializer.Serialize(contract));
    }

    private static IndexOpEntryJsonContract CreateDescribedEntry (
        string name,
        string kind,
        string policy,
        string argsSchemaJson,
        string? resultSchemaJson = null,
        UcliOperationDescribeContract? describe = null)
    {
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
            Assurance = describe.Assurance,
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
        Assert.Equal(UcliOperationInputConstraintKindValues.GlobalObjectId, constraint.GetProperty("kind").GetString());
    }

    private static UcliOperationDescribeContract CreateGoDescribeContract ()
    {
        return UcliOperationDescribeContractBuilder.Create<GoDescribeArgs, GameObjectDescriptionResult>(
            "Returns a GameObject description including components and child hierarchy.",
            new UcliOperationAssuranceContract(
                Array.Empty<UcliOperationSideEffect>(),
                mayDirty: false,
                mayPersist: false,
                Array.Empty<string>(),
                UcliOperationPlanMode.ObservesLiveUnity));
    }
}
