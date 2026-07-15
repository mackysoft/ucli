using System.Text.Json;

namespace MackySoft.Ucli.Tests;

public sealed class EvalCliOutputContractTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Eval_WithUnknownOption_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        var result = await CliInProcessRunner.RunCommandAsync(UcliCommandNames.Eval, UcliContractConstants.CliOption.Unknown);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentError(outputJson.RootElement, UcliCommandNames.Eval);
        CommandResultAssert.ReportsUnrecognizedArgument(result.StdErr, UcliContractConstants.CliOption.Unknown);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Eval_WithSupportedOptions_IsAcceptedByParser ()
    {
        using var scope = TestDirectories.CreateTempScope("eval-cli-output-contract", "supported-options-parser");
        var invalidProjectPath = Path.Combine(scope.FullPath, "NotUnityProject");
        Directory.CreateDirectory(invalidProjectPath);

        var result = await CliInProcessRunner.RunCommandAsync(
            UcliCommandNames.Eval,
            UcliContractConstants.CliOption.ProjectPath,
            invalidProjectPath,
            UcliContractConstants.CliOption.Mode,
            "unsupported",
            UcliContractConstants.CliOption.Timeout,
            "1234",
            UcliContractConstants.CliOption.AllowDangerous,
            UcliContractConstants.CliOption.AllowPlayMode,
            UcliContractConstants.CliOption.FailFast,
            UcliContractConstants.CliOption.Source,
            "return 1;");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Eval);
        CommandResultAssert.DoesNotReportUnrecognizedArguments(
            result.StdErr,
            UcliContractConstants.CliOption.AllowDangerous,
            UcliContractConstants.CliOption.AllowPlayMode,
            UcliContractConstants.CliOption.FailFast,
            UcliContractConstants.CliOption.Source);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Eval_WithFileOption_IsAcceptedByParser ()
    {
        using var scope = TestDirectories.CreateTempScope("eval-cli-output-contract", "file-option-parser");
        var sourcePath = Path.Combine(scope.FullPath, "eval.cs");
        await File.WriteAllTextAsync(sourcePath, "return 1;");

        var result = await CliInProcessRunner.RunCommandAsync(
            UcliCommandNames.Eval,
            UcliContractConstants.CliOption.Mode,
            "unsupported",
            UcliContractConstants.CliOption.File,
            sourcePath);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Eval);
        CommandResultAssert.DoesNotReportUnrecognizedArguments(result.StdErr, UcliContractConstants.CliOption.File);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Eval_WithCallOnlyOrUnsupportedOptions_ReturnsParseErrorAsSingleJson ()
    {
        foreach (var option in GetCallOnlyOrUnsupportedOptions())
        {
            var result = await CliInProcessRunner.RunCommandAsync(
                UcliCommandNames.Eval,
                option,
                UcliContractConstants.CliOption.Source,
                "return 1;");

            using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
            Assert.True(
                result.ExitCode == (int)CliExitCode.InvalidArgument,
                $"{option} must be rejected by eval command parsing.");
            CommandResultAssert.HasInvalidArgumentError(outputJson.RootElement, UcliCommandNames.Eval);
            CommandResultAssert.ReportsUnrecognizedArgument(result.StdErr, option);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Eval_WithRedirectedStandardInputAndInvalidMode_IsAcceptedByParser ()
    {
        var result = await CliInProcessRunner.RunCommandWithStandardInputAsync(
            "return 1;",
            UcliCommandNames.Eval,
            UcliContractConstants.CliOption.Mode,
            "unsupported");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Eval);
        Assert.Equal(JsonValueKind.Object, outputJson.RootElement.GetProperty("payload").ValueKind);
    }

    private static string[] GetCallOnlyOrUnsupportedOptions ()
    {
        return
        [
            "--plan",
            "--withPlan",
            "--planToken",
            "--kind",
            "--raw",
        ];
    }
}
