using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Project;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

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
                    new IndexOpEntryJsonContract(
                        Name: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
                        Kind: "query",
                        Policy: "safe",
                        ArgsSchemaJson: """{"type":"object","properties":{"path":{"type":"string"}}}"""),
                    new IndexOpEntryJsonContract(
                        Name: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneSave,
                        Kind: "mutation",
                        Policy: "advanced",
                        ArgsSchemaJson: """{"type":"object","properties":{"path":{"type":"string"}}}"""),
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
                    .HasString("name", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe)
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
                    new IndexOpEntryJsonContract(
                        Name: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
                        Kind: "query",
                        Policy: "safe",
                        ArgsSchemaJson: """{"type":"object","properties":{"path":{"type":"string"}}}"""),
                ]));

        var result = await CliProcessRunner.RunCommand(
            UcliCommandNames.Ops,
            UcliCommandNames.DescribeSubcommand,
            MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
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
                    .HasString("name", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe)
                    .HasString("kind", "query")
                    .HasString("policy", "safe")
                    .HasProperty("argsSchema", argsSchema => argsSchema
                        .HasString("type", "object")
                        .HasProperty("properties", properties => properties
                            .HasProperty("path", path => path
                                .HasString("type", "string")))))
                .HasProperty("readIndex", readIndex => readIndex
                    .HasString("source", "index")
                    .HasString("freshness", "probable")));
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
                    new IndexOpEntryJsonContract(
                        Name: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
                        Kind: "query",
                        Policy: "safe",
                        ArgsSchemaJson: """{"type":"object"}"""),
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
                    new IndexOpEntryJsonContract(
                        Name: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
                        Kind: "query",
                        Policy: "safe",
                        ArgsSchemaJson: """{"type":"object"}"""),
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
                    new IndexOpEntryJsonContract(
                        Name: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
                        Kind: "query",
                        Policy: "safe",
                        ArgsSchemaJson: """{"type":"object"}"""),
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
}