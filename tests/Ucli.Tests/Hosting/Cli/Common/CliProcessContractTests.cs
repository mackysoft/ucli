using Json.Schema;
using MackySoft.FileSystem;
using MackySoft.Ucli.Hosting.Cli.Schemas;

namespace MackySoft.Ucli.Tests.Cli;

public sealed class CliProcessContractTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task BuildRunParseError_UsesRegisteredEmptyErrorBranch ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Build,
            UcliCommandNames.RunSubcommand,
            "--unknown-option");

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        using var outputJson = JsonAssert.ParseMultilineObject(result.StdOut);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.BuildRun);
        var payload = outputJson.RootElement.GetProperty("payload");
        JsonAssert.For(payload).HasString("payloadKind", "empty");

        var schemaSet = UcliStaticSchemaSetLoader.Load(
            AbsolutePath.Parse(TestRepositoryPaths.GetFullPath("schemas")));
        var artifact = Assert.IsType<UcliStaticSchemaArtifact>(
            schemaSet.Find("cli-output.payload.build.run.error"));
        var schema = global::Json.Schema.JsonSchema.Build(
            artifact.Document,
            new BuildOptions
            {
                SchemaRegistry = new SchemaRegistry
                {
                    Fetch = null!,
                },
            });

        Assert.True(schema.Evaluate(payload).IsValid);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task DaemonVersionOption_ReturnsBuiltAssemblyVersionAndSuccessExitCode ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Daemon,
            "--version");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Matches(@"^\d+\.\d+\.\d+([-\+].*)?$", result.StdOut.Trim());
        Assert.Equal(string.Empty, result.StdErr);
    }
}
