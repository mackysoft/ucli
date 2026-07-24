using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Hosting.Cli.Assurance;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.CompileCommandTestData;

namespace MackySoft.Ucli.Tests;

public sealed class CompileCommandGoldenOutputTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("text")]
    [InlineData("json")]
    [Trait("Size", "Medium")]
    public async Task Compile_WithDefaultOrSupportedFormat_WritesOnlyFinalCommandResult (string? format)
    {
        var service = new RecordingCompileService((_, _, _) => ValueTask.FromResult(CompileExecutionResult.Success(CreateOutput())));
        var command = new CompileCommand(service, CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);

        var result = await CommandResultCapture.ExecuteWithErrorAsync(() => command.CompileAsync(
            format: format,
            cancellationToken: CancellationToken.None));

        CompileCommandAssert.SucceededWithOnlyFinalOutputAndGolden(
            result,
            service,
            CreateGoldenNormalization());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Compile_WithCompileErrorOutput_ReturnsOkEnvelopeWithFailureExitCodeAndMatchesGolden ()
    {
        var service = new RecordingCompileService((_, _, _) => ValueTask.FromResult(CompileExecutionResult.Success(CreateOutput(errorCount: 1))));
        var command = new CompileCommand(service, CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);

        var result = await CommandResultCapture.ExecuteAsync(() => command.CompileAsync(
            cancellationToken: CancellationToken.None));

        Assert.Equal(1, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Compile,
            TextVocabulary.GetText(CommandResultStatus.Ok),
            1);
        Assert.Equal(
            TextVocabulary.GetText(AssuranceVerdict.Fail),
            outputJson.RootElement.GetProperty("payload").GetProperty("verdict").GetString());

        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("compile", "compile-error.json"),
            result.StdOut,
            CreateGoldenNormalization());
    }
}
