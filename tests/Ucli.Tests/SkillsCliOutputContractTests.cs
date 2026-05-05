using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;

namespace MackySoft.Ucli.Tests;

public sealed class SkillsCliOutputContractTests
{
    private const string HostUnsupportedCode = "SKILL_HOST_UNSUPPORTED";
    private const string InstallTargetDigestMismatchCode = "SKILL_INSTALL_TARGET_DIGEST_MISMATCH";
    private const string InstallTargetHostConflictCode = "SKILL_INSTALL_TARGET_HOST_CONFLICT";
    private const string InstallTargetUnmanagedCode = "SKILL_INSTALL_TARGET_UNMANAGED";
    private const string InvalidArgumentCode = "INVALID_ARGUMENT";
    private const string PathUnsafeCode = "SKILL_PATH_UNSAFE";
    private const string UnknownOptionMessage = "Argument '--unknown' is not recognized.";

    private static readonly string[] ExpectedSkillNames =
    [
        "ucli-plan-apply",
        "ucli-read-project",
        "ucli-troubleshoot",
        "ucli-verify-changes",
    ];

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Skills_WithoutSubcommand_ReturnsJsonEnvelopeError ()
    {
        var result = await CliProcessRunner.RunCommand(UcliCommandNames.Skills);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.Skills,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, InvalidArgumentCode);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Skills_WithUnknownSubcommand_ReturnsJsonEnvelopeError ()
    {
        var result = await CliProcessRunner.RunCommand(UcliCommandNames.Skills, "unknown");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.Skills,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, InvalidArgumentCode);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(UcliCommandNames.ListSubcommand, UcliCommandNames.SkillsList)]
    [InlineData(UcliCommandNames.ExportSubcommand, UcliCommandNames.SkillsExport)]
    [InlineData(UcliCommandNames.InstallSubcommand, UcliCommandNames.SkillsInstall)]
    [InlineData(UcliCommandNames.DoctorSubcommand, UcliCommandNames.SkillsDoctor)]
    public async Task SkillsSubcommand_WithUnknownOption_ReturnsInvalidArgumentAsSingleJson (
        string subcommand,
        string expectedCommand)
    {
        var result = await CliProcessRunner.RunCommand(UcliCommandNames.Skills, subcommand, UcliContractConstants.CliOption.Unknown);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: expectedCommand,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, InvalidArgumentCode);
        Assert.Contains(UnknownOptionMessage, outputJson.RootElement.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsList_ReturnsOfficialSkillsAndSupportedHosts ()
    {
        var result = await CliProcessRunner.RunCommand(UcliCommandNames.Skills, UcliCommandNames.ListSubcommand);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.SkillsList,
            status: "ok",
            exitCode: (int)CliExitCode.Success);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);

        var payload = outputJson.RootElement.GetProperty("payload");
        JsonAssert.For(payload)
            .HasArrayLength("skills", ExpectedSkillNames.Length)
            .HasArrayLength("supportedHosts", 3)
            .HasProperty("skills", 0, static skill => skill
                .HasString("skillName", ExpectedSkillNames[0])
                .HasValueKind("displayName", JsonValueKind.String)
                .HasValueKind("description", JsonValueKind.String)
                .HasValueKind("contentDigest", JsonValueKind.String)
                .HasArrayLength("hostArtifacts", 3))
            .HasProperty("supportedHosts", 0, static host => host
                .HasString("host", "claude")
                .HasString("projectTargetDirectory", ".claude/skills"))
            .HasProperty("supportedHosts", 1, static host => host
                .HasString("host", "copilot")
                .HasString("projectTargetDirectory", ".github/skills"))
            .HasProperty("supportedHosts", 2, static host => host
                .HasString("host", "openai")
                .HasString("projectTargetDirectory", ".agents/skills"));

        Assert.Equal(ExpectedSkillNames, payload
            .GetProperty("skills")
            .EnumerateArray()
            .Select(static skill => skill.GetProperty("skillName").GetString())
            .ToArray());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsExport_WithOpenAiHost_WritesMaterializedPackages ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "export-openai");
        var outputRoot = scope.GetPath("exported");

        var result = await CliProcessRunner.RunCommand(
            UcliCommandNames.Skills,
            UcliCommandNames.ExportSubcommand,
            "--host",
            "openai",
            "--output",
            outputRoot);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.SkillsExport,
            status: "ok",
            exitCode: (int)CliExitCode.Success);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("host", "openai")
                .HasString("outputRoot", outputRoot)
                .HasArrayLength("skills", ExpectedSkillNames.Length)
                .HasInt32("skillCount", ExpectedSkillNames.Length));

        foreach (var skillName in ExpectedSkillNames)
        {
            Assert.True(File.Exists(Path.Combine(outputRoot, skillName, "SKILL.md")), skillName);
            Assert.True(File.Exists(Path.Combine(outputRoot, skillName, "ucli-skill.json")), skillName);
            Assert.True(File.Exists(Path.Combine(outputRoot, skillName, "agents", "openai.yaml")), skillName);
        }
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(UcliCommandNames.ExportSubcommand, UcliCommandNames.SkillsExport)]
    [InlineData(UcliCommandNames.InstallSubcommand, UcliCommandNames.SkillsInstall)]
    [InlineData(UcliCommandNames.DoctorSubcommand, UcliCommandNames.SkillsDoctor)]
    public async Task SkillsSubcommand_WithoutHost_ReturnsInvalidArgument (
        string subcommand,
        string expectedCommand)
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", $"missing-host-{subcommand}");
        var repoRoot = scope.CreateDirectory("repo");
        var args = CreateRequiredHostScenarioArgs(subcommand, repoRoot, scope.GetPath("exported"));

        var result = await CliProcessRunner.RunCommand(args);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: expectedCommand,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, InvalidArgumentCode);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsExport_WithoutOutput_ReturnsInvalidArgument ()
    {
        var result = await CliProcessRunner.RunCommand(
            UcliCommandNames.Skills,
            UcliCommandNames.ExportSubcommand,
            "--host",
            "openai");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.SkillsExport,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, InvalidArgumentCode);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(UcliCommandNames.InstallSubcommand, UcliCommandNames.SkillsInstall)]
    [InlineData(UcliCommandNames.DoctorSubcommand, UcliCommandNames.SkillsDoctor)]
    public async Task SkillsScopedSubcommand_WithoutScope_ReturnsInvalidArgument (
        string subcommand,
        string expectedCommand)
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", $"missing-scope-{subcommand}");
        var repoRoot = scope.CreateDirectory("repo");

        var result = await RunScopedCommand(subcommand, repoRoot, host: "openai", scope: null, targetDir: null);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: expectedCommand,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, InvalidArgumentCode);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(UcliCommandNames.InstallSubcommand, UcliCommandNames.SkillsInstall)]
    [InlineData(UcliCommandNames.DoctorSubcommand, UcliCommandNames.SkillsDoctor)]
    public async Task SkillsScopedSubcommand_WithInvalidScope_ReturnsInvalidArgument (
        string subcommand,
        string expectedCommand)
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", $"invalid-scope-{subcommand}");
        var repoRoot = scope.CreateDirectory("repo");

        var result = await RunScopedCommand(subcommand, repoRoot, host: "openai", scope: "user", targetDir: null);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: expectedCommand,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, InvalidArgumentCode);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(UcliCommandNames.ExportSubcommand, UcliCommandNames.SkillsExport)]
    [InlineData(UcliCommandNames.InstallSubcommand, UcliCommandNames.SkillsInstall)]
    [InlineData(UcliCommandNames.DoctorSubcommand, UcliCommandNames.SkillsDoctor)]
    public async Task SkillsSubcommand_WithUnsupportedHost_ReturnsHostUnsupported (
        string subcommand,
        string expectedCommand)
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", $"unsupported-host-{subcommand}");
        using var outsideScope = TestDirectories.CreateTempScope("skills-cli-output-contract", $"unsupported-host-outside-{subcommand}");
        var repoRoot = scope.CreateDirectory("repo");
        var args = CreateUnsupportedHostScenarioArgs(subcommand, repoRoot, outsideScope.FullPath, scope.GetPath("exported"));

        var result = await CliProcessRunner.RunCommand(args);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: expectedCommand,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, HostUnsupportedCode);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsInstall_WithProjectScope_CreatesThenNoOps ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "install-openai");
        var repoRoot = scope.CreateDirectory("repo");

        var created = await RunOpenAiInstall(repoRoot);
        var noOp = await RunOpenAiInstall(repoRoot);

        using var createdJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(created.StdOut);
        Assert.Equal((int)CliExitCode.Success, created.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            createdJson.RootElement,
            command: UcliCommandNames.SkillsInstall,
            status: "ok",
            exitCode: (int)CliExitCode.Success);
        JsonAssert.For(createdJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("host", "openai")
                .HasString("scope", "project")
                .HasString("repositoryRoot", repoRoot)
                .HasValueKind("targetRoot", JsonValueKind.String)
                .HasArrayLength("actions", ExpectedSkillNames.Length)
                .HasInt32("createdCount", ExpectedSkillNames.Length)
                .HasInt32("noOpCount", 0)
                .HasProperty("actions", 0, static action => action
                    .HasString("skillName", ExpectedSkillNames[0])
                    .HasString("action", "created")));

        using var noOpJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(noOp.StdOut);
        Assert.Equal((int)CliExitCode.Success, noOp.ExitCode);
        JsonAssert.For(noOpJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasInt32("createdCount", 0)
                .HasInt32("noOpCount", ExpectedSkillNames.Length)
                .HasProperty("actions", 0, static action => action
                    .HasString("action", "noOp")));

        var targetRoot = createdJson.RootElement.GetProperty("payload").GetProperty("targetRoot").GetString()!;
        Assert.EndsWith(Path.Combine(".agents", "skills"), targetRoot, StringComparison.Ordinal);
        foreach (var skillName in ExpectedSkillNames)
        {
            Assert.True(File.Exists(Path.Combine(targetRoot, skillName, "SKILL.md")), skillName);
            Assert.True(File.Exists(Path.Combine(targetRoot, skillName, "ucli-skill.json")), skillName);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsInstall_WithSharedTargetRootAcrossHosts_ReturnsHostConflict ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "install-host-conflict");
        var repoRoot = scope.CreateDirectory("repo");
        var claude = await RunInstall(repoRoot, host: "claude", targetDir: "shared-skills");
        Assert.Equal((int)CliExitCode.Success, claude.ExitCode);

        var openAi = await RunInstall(repoRoot, host: "openai", targetDir: "shared-skills");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(openAi.StdOut);
        Assert.Equal((int)CliExitCode.ToolError, openAi.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.SkillsInstall,
            status: "error",
            exitCode: (int)CliExitCode.ToolError);
        CommandResultAssert.HasSingleError(outputJson.RootElement, InstallTargetHostConflictCode);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(UcliCommandNames.InstallSubcommand, UcliCommandNames.SkillsInstall)]
    [InlineData(UcliCommandNames.DoctorSubcommand, UcliCommandNames.SkillsDoctor)]
    public async Task SkillsScopedSubcommand_WithTargetDirOutsideRepositoryRoot_ReturnsPathUnsafe (
        string subcommand,
        string expectedCommand)
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", $"outside-target-{subcommand}");
        using var outsideScope = TestDirectories.CreateTempScope("skills-cli-output-contract", $"outside-target-root-{subcommand}");
        var repoRoot = scope.CreateDirectory("repo");

        var result = await RunScopedCommand(subcommand, repoRoot, host: "openai", scope: "project", targetDir: outsideScope.FullPath);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: expectedCommand,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, PathUnsafeCode);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsDoctor_WithInstalledTarget_ReturnsHealthy ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "doctor-healthy");
        var repoRoot = scope.CreateDirectory("repo");
        var install = await RunOpenAiInstall(repoRoot);
        Assert.Equal((int)CliExitCode.Success, install.ExitCode);

        var doctor = await RunOpenAiDoctor(repoRoot);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(doctor.StdOut);
        Assert.Equal((int)CliExitCode.Success, doctor.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.SkillsDoctor,
            status: "ok",
            exitCode: (int)CliExitCode.Success);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("host", "openai")
                .HasString("scope", "project")
                .HasString("repositoryRoot", repoRoot)
                .HasBoolean("isHealthy", true)
                .HasArrayLength("diagnostics", 1)
                .HasProperty("diagnostics", 0, static diagnostic => diagnostic
                    .HasString("severity", "info")));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsDoctor_WithDriftedTarget_ReturnsUnhealthyDiagnostics ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "doctor-drift");
        var repoRoot = scope.CreateDirectory("repo");
        var install = await RunOpenAiInstall(repoRoot);
        Assert.Equal((int)CliExitCode.Success, install.ExitCode);

        using (var installJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(install.StdOut))
        {
            var targetRoot = installJson.RootElement.GetProperty("payload").GetProperty("targetRoot").GetString()!;
            await File.AppendAllTextAsync(Path.Combine(targetRoot, ExpectedSkillNames[0], "SKILL.md"), "\nDrifted content.\n");
        }

        var doctor = await RunOpenAiDoctor(repoRoot);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(doctor.StdOut);
        Assert.Equal((int)CliExitCode.ToolError, doctor.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.SkillsDoctor,
            status: "error",
            exitCode: (int)CliExitCode.ToolError);
        CommandResultAssert.HasSingleError(outputJson.RootElement, InstallTargetDigestMismatchCode);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasBoolean("isHealthy", false)
                .HasProperty("diagnostics", 0, static diagnostic => diagnostic
                    .HasString("severity", "error")
                    .HasString("code", InstallTargetDigestMismatchCode)
                    .HasString("skillName", ExpectedSkillNames[0])));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsDoctor_WithMissingTarget_ReturnsUnhealthyDiagnostics ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "doctor-missing-target");
        var repoRoot = scope.CreateDirectory("repo");

        var result = await RunOpenAiDoctor(repoRoot);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.SkillsDoctor,
            status: "error",
            exitCode: (int)CliExitCode.ToolError);
        CommandResultAssert.HasSingleError(outputJson.RootElement, InstallTargetUnmanagedCode);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasBoolean("isHealthy", false)
                .HasArrayLength("diagnostics", 1)
                .HasProperty("diagnostics", 0, static diagnostic => diagnostic
                    .HasString("severity", "error")
                    .HasString("code", InstallTargetUnmanagedCode)));
    }

    private static Task<CommandExecutionResult> RunOpenAiInstall (string repoRoot)
    {
        return RunInstall(repoRoot, "openai", targetDir: null);
    }

    private static Task<CommandExecutionResult> RunInstall (
        string repoRoot,
        string host,
        string? targetDir)
    {
        return RunScopedCommand(UcliCommandNames.InstallSubcommand, repoRoot, host, "project", targetDir);
    }

    private static Task<CommandExecutionResult> RunScopedCommand (
        string subcommand,
        string repoRoot,
        string? host,
        string? scope,
        string? targetDir)
    {
        var args = new List<string>
        {
            UcliCommandNames.Skills,
            subcommand,
        };
        if (host is not null)
        {
            args.Add("--host");
            args.Add(host);
        }

        if (scope is not null)
        {
            args.Add("--scope");
            args.Add(scope);
        }

        args.AddRange([
            "--repoRoot",
            repoRoot,
        ]);

        if (targetDir is not null)
        {
            args.Add("--targetDir");
            args.Add(targetDir);
        }

        return CliProcessRunner.RunCommand(args.ToArray());
    }

    private static Task<CommandExecutionResult> RunOpenAiDoctor (string repoRoot)
    {
        return RunScopedCommand(UcliCommandNames.DoctorSubcommand, repoRoot, "openai", "project", targetDir: null);
    }

    private static string[] CreateRequiredHostScenarioArgs (
        string subcommand,
        string repoRoot,
        string outputRoot)
    {
        return subcommand switch
        {
            UcliCommandNames.ExportSubcommand => [UcliCommandNames.Skills, subcommand, "--output", outputRoot],
            UcliCommandNames.InstallSubcommand => [UcliCommandNames.Skills, subcommand, "--scope", "project", "--repoRoot", repoRoot],
            UcliCommandNames.DoctorSubcommand => [UcliCommandNames.Skills, subcommand, "--scope", "project", "--repoRoot", repoRoot],
            _ => throw new ArgumentOutOfRangeException(nameof(subcommand), subcommand, "Unsupported skills subcommand."),
        };
    }

    private static string[] CreateUnsupportedHostScenarioArgs (
        string subcommand,
        string repoRoot,
        string targetDir,
        string outputRoot)
    {
        return subcommand switch
        {
            UcliCommandNames.ExportSubcommand => [UcliCommandNames.Skills, subcommand, "--host", "generic", "--output", outputRoot],
            UcliCommandNames.InstallSubcommand => [UcliCommandNames.Skills, subcommand, "--host", "generic", "--scope", "project", "--repoRoot", repoRoot, "--targetDir", targetDir],
            UcliCommandNames.DoctorSubcommand => [UcliCommandNames.Skills, subcommand, "--host", "generic", "--scope", "project", "--repoRoot", repoRoot, "--targetDir", targetDir],
            _ => throw new ArgumentOutOfRangeException(nameof(subcommand), subcommand, "Unsupported skills subcommand."),
        };
    }
}
