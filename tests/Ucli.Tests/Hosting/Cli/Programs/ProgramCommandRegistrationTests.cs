namespace MackySoft.Ucli.Tests;

public sealed class ProgramCommandRegistrationTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task RegisteredProgramLeaves_WithAcceptedArguments_ReachTheirCommandHandlers ()
    {
        using var scope = TestDirectories.CreateTempScope("program-command-registration", "accepted-arguments");
        var invalidProjectPath = scope.CreateDirectory("NotUnityProject");
        var missingProgramPath = Path.Combine(scope.FullPath, "missing-program.json");

        foreach (var testCase in CreateProgramLeafCases(invalidProjectPath, missingProgramPath))
        {
            var result = await CliInProcessRunner.RunCommandAsync(testCase.Arguments);

            using var outputJson = JsonAssert.ParseMultilineObject(result.StdOut);
            Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
            CommandResultAssert.HasInvalidArgumentEnvelope(outputJson.RootElement, testCase.CommandName);
            CommandResultAssert.DoesNotReportUnrecognizedArguments(result.StdErr, testCase.AcceptedOptions);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ProgramInputCommands_HelpAndInvocationExposeKebabCaseProgramPath ()
    {
        using var serviceProvider = UcliServiceProviderTestFactory.CreateCore();

        await ConsoleAppRunner.RunWithRegisteredAppAsync(serviceProvider, async app =>
        {
            foreach (var commandPath in new[] { "program validate", "program plan", "program run" })
            {
                var result = await ConsoleAppHelpRunner.RunHelpAsync(app, commandPath);

                Assert.Equal((int)CliExitCode.Success, result.ExitCode);
                Assert.Contains("--program-path", result.StdOut, StringComparison.Ordinal);
                Assert.Contains("--programPath", result.StdOut, StringComparison.Ordinal);
            }
        });
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ProgramLifecycleAndReconciliationCommands_HelpExposeTheirPublicExecutionOptions ()
    {
        using var serviceProvider = UcliServiceProviderTestFactory.CreateCore();

        await ConsoleAppRunner.RunWithRegisteredAppAsync(serviceProvider, async app =>
        {
            foreach (var commandPath in new[] { "program plan", "program run" })
            {
                var result = await ConsoleAppHelpRunner.RunHelpAsync(app, commandPath);
                Assert.Equal((int)CliExitCode.Success, result.ExitCode);
                Assert.Contains("--fail-fast", result.StdOut, StringComparison.Ordinal);
            }

            foreach (var commandPath in new[] { "program status", "program cancel" })
            {
                var result = await ConsoleAppHelpRunner.RunHelpAsync(app, commandPath);
                Assert.Equal((int)CliExitCode.Success, result.ExitCode);
                Assert.Contains("--timeout", result.StdOut, StringComparison.Ordinal);
            }
        });
    }

    private static ProgramLeafCase[] CreateProgramLeafCases (string invalidProjectPath, string missingProgramPath)
    {
        return
        [
            new(
                UcliCommandNames.ProgramValidate,
                ["program", "validate", "--program-path", missingProgramPath, "--project-path", invalidProjectPath],
                ["--program-path", "--project-path"]),
            new(
                UcliCommandNames.ProgramPlan,
                ["program", "plan", "--program-path", missingProgramPath, "--project-path", invalidProjectPath],
                ["--program-path", "--project-path"]),
            new(
                UcliCommandNames.ProgramRun,
                ["program", "run", "--program-path", missingProgramPath, "--project-path", invalidProjectPath],
                ["--program-path", "--project-path"]),
            new(
                UcliCommandNames.ProgramStatus,
                ["program", "status", "--run-id", Guid.Empty.ToString("D"), "--project-path", invalidProjectPath],
                ["--run-id", "--project-path"]),
            new(
                UcliCommandNames.ProgramCancel,
                ["program", "cancel", "--run-id", Guid.Empty.ToString("D"), "--project-path", invalidProjectPath],
                ["--run-id", "--project-path"]),
            new(
                UcliCommandNames.ProgramPresetsList,
                ["program", "presets", "list", "--project-path", invalidProjectPath],
                ["--project-path"]),
            new(
                UcliCommandNames.ProgramPresetsDescribe,
                ["program", "presets", "describe", "example", "--project-path", invalidProjectPath],
                ["--project-path"]),
        ];
    }

    private sealed record ProgramLeafCase (
        string CommandName,
        string[] Arguments,
        string[] AcceptedOptions);
}
