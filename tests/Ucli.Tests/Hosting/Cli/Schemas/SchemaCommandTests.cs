using System.Text.Json.Nodes;
using MackySoft.Ucli.Hosting.Cli.Schemas;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests.Hosting.Cli.Schemas;

public sealed class SchemaCommandTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task List_WithInstalledSchemaSet_ReturnsCompleteManifest ()
    {
        using var scope = TestDirectories.CreateTempScope("schema-command", "list");
        var schemaRoot = StaticSchemaSetTestSupport.CopyRepositorySchemaSet(scope);
        var expectedManifest = StaticSchemaSetTestSupport.ReadManifest(schemaRoot);
        var command = CreateListCommand(schemaRoot);

        var result = await CommandResultCapture.ExecuteSynchronousCommandAsync(
            () => command.List(CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasSuccessEnvelope(outputJson.RootElement, UcliCommandNames.SchemaList);
        var actualManifest = JsonNode.Parse(outputJson.RootElement.GetProperty("payload").GetRawText());
        Assert.True(JsonNode.DeepEquals(expectedManifest, actualManifest));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Get_WithExactLogicalName_ReturnsEntryMetadataAndSchemaDocument ()
    {
        using var scope = TestDirectories.CreateTempScope("schema-command", "get");
        var schemaRoot = StaticSchemaSetTestSupport.CopyRepositorySchemaSet(scope);
        var manifestEntry = StaticSchemaSetTestSupport.GetFirstEntry(
            StaticSchemaSetTestSupport.ReadManifest(schemaRoot));
        var logicalName = manifestEntry["name"]!.GetValue<string>();
        var schemaPath = manifestEntry["path"]!.GetValue<string>();
        var expectedDocument = JsonNode.Parse(File.ReadAllText(Path.Combine(schemaRoot, schemaPath)));
        var command = CreateGetCommand(schemaRoot);

        var result = await CommandResultCapture.ExecuteSynchronousCommandAsync(
            () => command.Get(logicalName, CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasSuccessEnvelope(outputJson.RootElement, UcliCommandNames.SchemaGet);
        var payload = outputJson.RootElement.GetProperty("payload");
        Assert.Equal(logicalName, payload.GetProperty("name").GetString());
        Assert.Equal(manifestEntry["$id"]!.GetValue<string>(), payload.GetProperty("$id").GetString());
        Assert.Equal(
            manifestEntry["status"]?.GetValue<string>(),
            payload.GetProperty("status").GetString());
        var actualDocument = JsonNode.Parse(payload.GetProperty("document").GetRawText());
        Assert.True(JsonNode.DeepEquals(expectedDocument, actualDocument));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Get_WithDifferentLogicalNameCasing_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("schema-command", "get-unknown");
        var schemaRoot = StaticSchemaSetTestSupport.CopyRepositorySchemaSet(scope);
        var manifestEntry = StaticSchemaSetTestSupport.GetFirstEntry(
            StaticSchemaSetTestSupport.ReadManifest(schemaRoot));
        var unknownName = manifestEntry["name"]!.GetValue<string>().ToUpperInvariant();
        var command = CreateGetCommand(schemaRoot);

        var result = await CommandResultCapture.ExecuteSynchronousCommandAsync(
            () => command.Get(unknownName, CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentOutput(result.StdOut, UcliCommandNames.SchemaGet);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Size", "Medium")]
    public async Task Export_ToNewOrEmptyDirectory_CopiesManifestAndEverySchemaByteForByte (
        bool createEmptyDestination)
    {
        using var scope = TestDirectories.CreateTempScope(
            "schema-command",
            createEmptyDestination ? "export-empty" : "export-new");
        var schemaRoot = StaticSchemaSetTestSupport.CopyRepositorySchemaSet(scope);
        var destination = scope.GetPath("exported");
        if (createEmptyDestination)
        {
            Directory.CreateDirectory(destination);
        }

        var command = CreateExportCommand(schemaRoot);

        var result = await CommandResultCapture.ExecuteSynchronousCommandAsync(
            () => command.Export(destination, CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasSuccessEnvelope(outputJson.RootElement, UcliCommandNames.SchemaExport);
        var payload = outputJson.RootElement.GetProperty("payload");
        Assert.Equal(Path.GetFullPath(destination), payload.GetProperty("outputPath").GetString());
        AssertExportMatchesInstalledSet(schemaRoot, destination, payload.GetProperty("fileCount").GetInt32());
    }

    [Theory]
    [InlineData("file")]
    [InlineData("non-empty-directory")]
    [Trait("Size", "Medium")]
    public async Task Export_ToOccupiedDestination_ReturnsInvalidArgumentWithoutReplacingContent (
        string destinationKind)
    {
        using var scope = TestDirectories.CreateTempScope("schema-command", "export-occupied");
        var schemaRoot = StaticSchemaSetTestSupport.CopyRepositorySchemaSet(scope);
        var destination = scope.GetPath("occupied");
        var preservedPath = destination;
        if (string.Equals(destinationKind, "file", StringComparison.Ordinal))
        {
            File.WriteAllText(destination, "preserve");
        }
        else
        {
            Directory.CreateDirectory(destination);
            preservedPath = Path.Combine(destination, "preserve.txt");
            File.WriteAllText(preservedPath, "preserve");
        }

        var command = CreateExportCommand(schemaRoot);

        var result = await CommandResultCapture.ExecuteSynchronousCommandAsync(
            () => command.Export(destination, CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentOutput(result.StdOut, UcliCommandNames.SchemaExport);
        Assert.Equal("preserve", File.ReadAllText(preservedPath));
    }

    private static SchemaListCommand CreateListCommand (string schemaRoot)
    {
        return new SchemaListCommand(
            new SchemaSetTestProvider(schemaRoot),
            CommandResultTestWriter.Create());
    }

    private static SchemaGetCommand CreateGetCommand (string schemaRoot)
    {
        return new SchemaGetCommand(
            new SchemaSetTestProvider(schemaRoot),
            CommandResultTestWriter.Create());
    }

    private static SchemaExportCommand CreateExportCommand (string schemaRoot)
    {
        return new SchemaExportCommand(
            new SchemaSetTestProvider(schemaRoot),
            CommandResultTestWriter.Create());
    }

    private static void AssertExportMatchesInstalledSet (
        string installedRoot,
        string exportedRoot,
        int reportedFileCount)
    {
        var manifest = StaticSchemaSetTestSupport.ReadManifest(installedRoot);
        var relativePaths = manifest["schemas"]!
            .AsArray()
            .Select(static entry => entry!["path"]!.GetValue<string>())
            .Prepend("schema-manifest.json")
            .ToArray();

        Assert.Equal(relativePaths.Length, reportedFileCount);
        Assert.Equal(
            relativePaths.Length,
            Directory.EnumerateFiles(exportedRoot, "*", SearchOption.AllDirectories).Count());
        foreach (var relativePath in relativePaths)
        {
            Assert.Equal(
                File.ReadAllBytes(Path.Combine(installedRoot, relativePath)),
                File.ReadAllBytes(Path.Combine(exportedRoot, relativePath)));
        }
    }
}
