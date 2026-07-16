namespace MackySoft.Ucli.Tests;

public sealed class QueryCommandParserTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task AssetsFind_WithCamelCaseAliases_IsAcceptedByParser ()
    {
        using var scope = TestDirectories.CreateTempScope("query-command", "assets-find-aliases");
        var invalidProjectPath = scope.GetPath("MissingUnityProject");

        var result = await CliInProcessRunner.RunCommandAsync(
            UcliCommandNames.Query,
            UcliCommandNames.AssetsSubcommand,
            UcliCommandNames.FindSubcommand,
            "--type",
            "UnityEngine.Material, UnityEngine.CoreModule",
            UcliContractConstants.CliOption.ProjectPath,
            invalidProjectPath,
            UcliContractConstants.CliOption.FailFast);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.DoesNotReportUnrecognizedArguments(
            result.StdErr,
            UcliContractConstants.CliOption.ProjectPath,
            UcliContractConstants.CliOption.FailFast);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.QueryAssetsFind);
        CommandResultAssert.HasSingleError(outputJson.RootElement, ProjectContextErrorCodes.ProjectPathNotFound);
    }
}
