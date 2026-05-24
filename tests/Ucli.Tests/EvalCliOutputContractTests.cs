using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests;

public sealed class EvalCliOutputContractTests
{
    private const string UnknownOptionMessage = "is not recognized.";

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Eval_WithUnknownOption_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        var result = await CliProcessRunner.RunCommandAsync(UcliCommandNames.Eval, UcliContractConstants.CliOption.Unknown);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Eval,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
        Assert.Contains(UnknownOptionMessage, result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Eval_WithSupportedOptions_IsAcceptedByParser ()
    {
        using var scope = TestDirectories.CreateTempScope("eval-cli-output-contract", "supported-options-parser");
        var invalidProjectPath = Path.Combine(scope.FullPath, "NotUnityProject");
        Directory.CreateDirectory(invalidProjectPath);

        var result = await CliProcessRunner.RunCommandAsync(
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
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Eval,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        Assert.DoesNotContain("Argument '--allowDangerous' is not recognized.", result.StdErr, StringComparison.Ordinal);
        Assert.DoesNotContain("Argument '--allowPlayMode' is not recognized.", result.StdErr, StringComparison.Ordinal);
        Assert.DoesNotContain("Argument '--failFast' is not recognized.", result.StdErr, StringComparison.Ordinal);
        Assert.DoesNotContain("Argument '--source' is not recognized.", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Eval_WithFileOption_IsAcceptedByParser ()
    {
        using var scope = TestDirectories.CreateTempScope("eval-cli-output-contract", "file-option-parser");
        var sourcePath = Path.Combine(scope.FullPath, "eval.cs");
        await File.WriteAllTextAsync(sourcePath, "return 1;");

        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Eval,
            UcliContractConstants.CliOption.Mode,
            "unsupported",
            UcliContractConstants.CliOption.File,
            sourcePath);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Eval,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        Assert.DoesNotContain("Argument '--file' is not recognized.", result.StdErr, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData("--plan")]
    [InlineData("--withPlan")]
    [InlineData("--planToken")]
    [InlineData("--kind")]
    [InlineData("--raw")]
    public async Task Eval_WithCallOnlyOrUnsupportedOptions_ReturnsParseErrorAsSingleJson (string option)
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Eval,
            option,
            UcliContractConstants.CliOption.Source,
            "return 1;");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Eval,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
        Assert.Contains($"Argument '{option}' is not recognized.", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Eval_WithRedirectedStandardInputAndInvalidMode_IsAcceptedByParser ()
    {
        var result = await CliProcessRunner.RunCommandWithStandardInputAsync(
            "return 1;",
            UcliCommandNames.Eval,
            UcliContractConstants.CliOption.Mode,
            "unsupported");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Eval,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        Assert.Equal(JsonValueKind.Object, outputJson.RootElement.GetProperty("payload").ValueKind);
    }
}
