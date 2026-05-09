using System.IO.Compression;
using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Skills.Digests;
using MackySoft.Ucli.Skills.Installation.Validation;
using MackySoft.Ucli.Skills.Manifests;

namespace MackySoft.Ucli.Tests;

public sealed class SkillsCliOutputContractTests
{
    private const string HostUnsupportedCode = "SKILL_HOST_UNSUPPORTED";
    private const string InstallTargetContentDigestMismatchCode = "SKILL_INSTALL_TARGET_CONTENT_DIGEST_MISMATCH";
    private const string InstallTargetHostArtifactDigestMismatchCode = "SKILL_INSTALL_TARGET_HOST_ARTIFACT_DIGEST_MISMATCH";
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
        var result = await CliProcessRunner.RunCommandAsync(UcliCommandNames.Skills);

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
        var result = await CliProcessRunner.RunCommandAsync(UcliCommandNames.Skills, "unknown");

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
    [InlineData(UcliCommandNames.UpdateSubcommand, UcliCommandNames.SkillsUpdate)]
    [InlineData(UcliCommandNames.UninstallSubcommand, UcliCommandNames.SkillsUninstall)]
    [InlineData(UcliCommandNames.DoctorSubcommand, UcliCommandNames.SkillsDoctor)]
    public async Task SkillsSubcommand_WithUnknownOption_ReturnsInvalidArgumentAsSingleJson (
        string subcommand,
        string expectedCommand)
    {
        var result = await CliProcessRunner.RunCommandAsync(UcliCommandNames.Skills, subcommand, UcliContractConstants.CliOption.Unknown);

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
        var result = await CliProcessRunner.RunCommandAsync(UcliCommandNames.Skills, UcliCommandNames.ListSubcommand);

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
                .HasString("projectTargetDirectory", ".claude/skills")
                .HasString("userTargetDirectory", "~/.claude/skills")
                .HasValueKind("reloadGuidance", JsonValueKind.String))
            .HasProperty("supportedHosts", 1, static host => host
                .HasString("host", "copilot")
                .HasString("projectTargetDirectory", ".github/skills")
                .HasString("userTargetDirectory", "~/.copilot/skills")
                .HasValueKind("reloadGuidance", JsonValueKind.String))
            .HasProperty("supportedHosts", 2, static host => host
                .HasString("host", "openai")
                .HasString("projectTargetDirectory", ".agents/skills")
                .HasString("userTargetDirectory", "${CODEX_HOME}/skills or ~/.codex/skills")
                .HasValueKind("reloadGuidance", JsonValueKind.String));

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

        var result = await CliProcessRunner.RunCommandAsync(
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
                .HasString("format", "directory")
                .HasString("outputRoot", outputRoot)
                .HasArrayLength("skills", ExpectedSkillNames.Length)
                .HasInt32("skillCount", ExpectedSkillNames.Length)
                .HasValueKind("reloadGuidance", JsonValueKind.String));

        foreach (var skillName in ExpectedSkillNames)
        {
            Assert.True(File.Exists(Path.Combine(outputRoot, skillName, "SKILL.md")), skillName);
            Assert.True(File.Exists(Path.Combine(outputRoot, skillName, "ucli-skill.json")), skillName);
            Assert.True(File.Exists(Path.Combine(outputRoot, skillName, "agents", "openai.yaml")), skillName);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsExport_WithZipFormat_WritesDeterministicReleaseZip ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "export-openai-zip");
        var firstZip = scope.GetPath("skills-a.zip");
        var secondZip = scope.GetPath("skills-b.zip");

        var first = await CliProcessRunner.RunCommandWithTimeoutAsync(
            TimeSpan.FromSeconds(45),
            UcliCommandNames.Skills,
            UcliCommandNames.ExportSubcommand,
            "--host",
            "openai",
            "--format",
            "zip",
            "--output",
            firstZip);
        var second = await CliProcessRunner.RunCommandWithTimeoutAsync(
            TimeSpan.FromSeconds(45),
            UcliCommandNames.Skills,
            UcliCommandNames.ExportSubcommand,
            "--host",
            "openai",
            "--format",
            "zip",
            "--output",
            secondZip);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(first.StdOut);
        Assert.Equal((int)CliExitCode.Success, first.ExitCode);
        Assert.Equal((int)CliExitCode.Success, second.ExitCode);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", static payload => payload
                .HasString("format", "zip")
                .HasValueKind("reloadGuidance", JsonValueKind.String));

        Assert.Equal(await File.ReadAllBytesAsync(firstZip), await File.ReadAllBytesAsync(secondZip));
        using var archive = ZipFile.OpenRead(firstZip);
        var entryNames = archive.Entries.Select(static entry => entry.FullName).ToArray();
        Assert.Equal(entryNames.Order(StringComparer.Ordinal).ToArray(), entryNames);
        Assert.Contains($"{ExpectedSkillNames[0]}/SKILL.md", entryNames);
        Assert.Contains($"{ExpectedSkillNames[0]}/agents/openai.yaml", entryNames);
        Assert.DoesNotContain(entryNames, static entry => entry.EndsWith("/", StringComparison.Ordinal));
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(UcliCommandNames.ExportSubcommand, UcliCommandNames.SkillsExport)]
    [InlineData(UcliCommandNames.InstallSubcommand, UcliCommandNames.SkillsInstall)]
    [InlineData(UcliCommandNames.UpdateSubcommand, UcliCommandNames.SkillsUpdate)]
    [InlineData(UcliCommandNames.UninstallSubcommand, UcliCommandNames.SkillsUninstall)]
    [InlineData(UcliCommandNames.DoctorSubcommand, UcliCommandNames.SkillsDoctor)]
    public async Task SkillsSubcommand_WithoutHost_ReturnsInvalidArgument (
        string subcommand,
        string expectedCommand)
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", $"missing-host-{subcommand}");
        var repoRoot = scope.CreateDirectory("repo");
        var args = CreateRequiredHostScenarioArgs(subcommand, repoRoot, scope.GetPath("exported"));

        var result = await CliProcessRunner.RunCommandAsync(args);

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
        var result = await CliProcessRunner.RunCommandAsync(
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
    [InlineData(UcliCommandNames.UpdateSubcommand, UcliCommandNames.SkillsUpdate)]
    [InlineData(UcliCommandNames.UninstallSubcommand, UcliCommandNames.SkillsUninstall)]
    [InlineData(UcliCommandNames.DoctorSubcommand, UcliCommandNames.SkillsDoctor)]
    public async Task SkillsScopedSubcommand_WithoutScope_ReturnsInvalidArgument (
        string subcommand,
        string expectedCommand)
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", $"missing-scope-{subcommand}");
        var repoRoot = scope.CreateDirectory("repo");

        var result = await RunScopedCommandAsync(subcommand, repoRoot, host: "openai", scope: null, targetDir: null);

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
    [InlineData(UcliCommandNames.UpdateSubcommand, UcliCommandNames.SkillsUpdate)]
    [InlineData(UcliCommandNames.UninstallSubcommand, UcliCommandNames.SkillsUninstall)]
    [InlineData(UcliCommandNames.DoctorSubcommand, UcliCommandNames.SkillsDoctor)]
    public async Task SkillsScopedSubcommand_WithInvalidScope_ReturnsInvalidArgument (
        string subcommand,
        string expectedCommand)
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", $"invalid-scope-{subcommand}");
        var repoRoot = scope.CreateDirectory("repo");

        var result = await RunScopedCommandAsync(subcommand, repoRoot, host: "openai", scope: "global", targetDir: null);

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
    [InlineData(UcliCommandNames.UpdateSubcommand, UcliCommandNames.SkillsUpdate)]
    [InlineData(UcliCommandNames.UninstallSubcommand, UcliCommandNames.SkillsUninstall)]
    [InlineData(UcliCommandNames.DoctorSubcommand, UcliCommandNames.SkillsDoctor)]
    public async Task SkillsScopedSubcommand_WithUserScopeAndRepoRoot_ReturnsInvalidArgument (
        string subcommand,
        string expectedCommand)
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", $"user-repo-root-{subcommand}");
        var repoRoot = scope.CreateDirectory("repo");

        var result = await RunScopedCommandAsync(subcommand, repoRoot, host: "openai", scope: "user", targetDir: null);

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
    [InlineData(UcliCommandNames.UpdateSubcommand, UcliCommandNames.SkillsUpdate)]
    [InlineData(UcliCommandNames.UninstallSubcommand, UcliCommandNames.SkillsUninstall)]
    [InlineData(UcliCommandNames.DoctorSubcommand, UcliCommandNames.SkillsDoctor)]
    public async Task SkillsSubcommand_WithUnsupportedHost_ReturnsHostUnsupported (
        string subcommand,
        string expectedCommand)
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", $"unsupported-host-{subcommand}");
        using var outsideScope = TestDirectories.CreateTempScope("skills-cli-output-contract", $"unsupported-host-outside-{subcommand}");
        var repoRoot = scope.CreateDirectory("repo");
        var args = CreateUnsupportedHostScenarioArgs(subcommand, repoRoot, outsideScope.FullPath, scope.GetPath("exported"));

        var result = await CliProcessRunner.RunCommandAsync(args);

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

        var created = await RunOpenAiInstallAsync(repoRoot);
        var noOp = await RunOpenAiInstallAsync(repoRoot);

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
    public async Task SkillsUserScope_WithoutTargetDir_UsesOpenAiCodexHomeDefault ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "user-scope-codex-home-default");
        var codexHome = scope.GetPath("codex-home");

        var install = await CliProcessRunner.RunCommandWithEnvironmentAsync(
            new Dictionary<string, string?> { ["CODEX_HOME"] = codexHome },
            UcliCommandNames.Skills,
            UcliCommandNames.InstallSubcommand,
            "--host",
            "openai",
            "--scope",
            "user");

        using var installJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(install.StdOut);
        Assert.Equal((int)CliExitCode.Success, install.ExitCode);
        JsonAssert.For(installJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("scope", "user")
                .IsNull("repositoryRoot")
                .HasInt32("createdCount", ExpectedSkillNames.Length)
                .HasValueKind("reloadGuidance", JsonValueKind.String));
        var targetRoot = installJson.RootElement.GetProperty("payload").GetProperty("targetRoot").GetString()!;
        Assert.True(Path.IsPathFullyQualified(targetRoot), targetRoot);
        Assert.EndsWith(Path.Combine("codex-home", "skills"), targetRoot, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsUserScope_WithExplicitTarget_InstallsUpdatesDoctorsAndUninstallsWithoutRepoRoot ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "user-scope-explicit-target");
        var targetRoot = scope.GetPath("user-skills");

        var install = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Skills,
            UcliCommandNames.InstallSubcommand,
            "--host",
            "openai",
            "--scope",
            "user",
            "--targetDir",
            targetRoot);
        var update = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Skills,
            UcliCommandNames.UpdateSubcommand,
            "--host",
            "openai",
            "--scope",
            "user",
            "--targetDir",
            targetRoot);
        var doctor = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Skills,
            UcliCommandNames.DoctorSubcommand,
            "--host",
            "openai",
            "--scope",
            "user",
            "--targetDir",
            targetRoot);
        var uninstall = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Skills,
            UcliCommandNames.UninstallSubcommand,
            "--host",
            "openai",
            "--scope",
            "user",
            "--targetDir",
            targetRoot);

        using var installJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(install.StdOut);
        Assert.Equal((int)CliExitCode.Success, install.ExitCode);
        JsonAssert.For(installJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("scope", "user")
                .IsNull("repositoryRoot")
                .HasValueKind("targetRoot", JsonValueKind.String)
                .HasInt32("createdCount", ExpectedSkillNames.Length)
                .HasValueKind("reloadGuidance", JsonValueKind.String));
        var resolvedTargetRoot = installJson.RootElement.GetProperty("payload").GetProperty("targetRoot").GetString()!;
        Assert.True(Path.IsPathFullyQualified(resolvedTargetRoot), resolvedTargetRoot);
        Assert.EndsWith(Path.DirectorySeparatorChar + Path.GetFileName(targetRoot), resolvedTargetRoot, StringComparison.Ordinal);

        using var updateJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(update.StdOut);
        Assert.Equal((int)CliExitCode.Success, update.ExitCode);
        JsonAssert.For(updateJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("scope", "user")
                .IsNull("repositoryRoot")
                .HasInt32("createdCount", 0)
                .HasInt32("noOpCount", ExpectedSkillNames.Length)
                .HasValueKind("reloadGuidance", JsonValueKind.String));

        using var doctorJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(doctor.StdOut);
        Assert.Equal((int)CliExitCode.Success, doctor.ExitCode);
        JsonAssert.For(doctorJson.RootElement)
            .HasProperty("payload", static payload => payload
                .HasString("scope", "user")
                .IsNull("repositoryRoot")
                .HasBoolean("isHealthy", true)
                .HasValueKind("reloadGuidance", JsonValueKind.String));

        using var uninstallJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(uninstall.StdOut);
        Assert.Equal((int)CliExitCode.Success, uninstall.ExitCode);
        JsonAssert.For(uninstallJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("scope", "user")
                .IsNull("repositoryRoot")
                .HasInt32("deletedCount", ExpectedSkillNames.Length)
                .HasValueKind("reloadGuidance", JsonValueKind.String));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsInstall_WithDryRunAndPrintDiff_ReturnsPlanWithoutWriting ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "install-dry-run-diff");
        var repoRoot = scope.CreateDirectory("repo");

        var result = await RunOpenAiInstallAsync(repoRoot, dryRun: true, printDiff: true);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.SkillsInstall,
            status: "ok",
            exitCode: (int)CliExitCode.Success);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasBoolean("dryRun", true)
                .HasBoolean("force", false)
                .HasBoolean("printDiff", true)
                .HasInt32("createdCount", ExpectedSkillNames.Length)
                .HasInt32("updatedCount", 0)
                .HasInt32("noOpCount", 0)
                .HasInt32("blockedCount", 0)
                .HasProperty("actions", 0, static action => action
                    .HasString("skillName", ExpectedSkillNames[0])
                    .HasString("action", "created")
                    .IsNull("blockedReason")
                    .HasArrayLength("diffs", 1)
                    .HasProperty("diffs", 0, static diff => diff
                        .HasProperty("files", 0, static file => file
                            .HasString("relativePath", "SKILL.md")
                            .HasString("changeKind", "added")
                            .IsNull("beforeContent")
                            .HasValueKind("afterContent", JsonValueKind.String)))));

        var targetRoot = outputJson.RootElement.GetProperty("payload").GetProperty("targetRoot").GetString()!;
        foreach (var skillName in ExpectedSkillNames)
        {
            Assert.False(Directory.Exists(Path.Combine(targetRoot, skillName)), skillName);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsInstall_WithSharedTargetRootAcrossHosts_ReturnsHostConflict ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "install-host-conflict");
        var repoRoot = scope.CreateDirectory("repo");
        var claude = await RunInstallAsync(repoRoot, host: "claude", targetDir: "shared-skills");
        Assert.Equal((int)CliExitCode.Success, claude.ExitCode);

        var openAi = await RunInstallAsync(repoRoot, host: "openai", targetDir: "shared-skills");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(openAi.StdOut);
        Assert.Equal((int)CliExitCode.ToolError, openAi.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.SkillsInstall,
            status: "error",
            exitCode: (int)CliExitCode.ToolError);
        CommandResultAssert.HasSingleError(outputJson.RootElement, InstallTargetHostConflictCode);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsInstall_WithForceAndPrintDiff_OverwritesLocalModification ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "install-force-local-modification");
        var repoRoot = scope.CreateDirectory("repo");
        var install = await RunOpenAiInstallAsync(repoRoot);
        Assert.Equal((int)CliExitCode.Success, install.ExitCode);
        using var installJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(install.StdOut);
        var targetRoot = installJson.RootElement.GetProperty("payload").GetProperty("targetRoot").GetString()!;
        var skillPath = Path.Combine(targetRoot, ExpectedSkillNames[0], "SKILL.md");
        var localPath = Path.Combine(targetRoot, ExpectedSkillNames[0], "local.md");
        await File.AppendAllTextAsync(skillPath, "\nInjected instruction.\n");
        await File.WriteAllTextAsync(localPath, "# Local\n");

        var result = await RunOpenAiInstallAsync(repoRoot, force: true, printDiff: true);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasBoolean("force", true)
                .HasBoolean("printDiff", true)
                .HasInt32("updatedCount", 1)
                .HasInt32("blockedCount", 0)
                .HasProperty("actions", 0, static action => action
                    .HasString("skillName", ExpectedSkillNames[0])
                    .HasString("action", "updated")
                    .IsNull("blockedReason")
                    .HasArrayLength("diffs", 1)));
        var action = FindAction(outputJson.RootElement, ExpectedSkillNames[0]);
        var modified = FindDiffFile(action, "SKILL.md");
        Assert.Equal("modified", modified.GetProperty("changeKind").GetString());
        Assert.Contains("Injected instruction.", modified.GetProperty("beforeContent").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Injected instruction.", modified.GetProperty("afterContent").GetString(), StringComparison.Ordinal);
        var deleted = FindDiffFile(action, "local.md");
        Assert.Equal("deleted", deleted.GetProperty("changeKind").GetString());
        Assert.Equal("# Local\n", deleted.GetProperty("beforeContent").GetString());
        Assert.Equal(JsonValueKind.Null, deleted.GetProperty("afterContent").ValueKind);
        Assert.DoesNotContain("Injected instruction.", await File.ReadAllTextAsync(skillPath), StringComparison.Ordinal);
        Assert.False(File.Exists(localPath));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsUpdate_WithProjectScope_CreatesThenNoOps ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "update-openai");
        var repoRoot = scope.CreateDirectory("repo");

        var created = await RunOpenAiUpdateAsync(repoRoot);
        var noOp = await RunOpenAiUpdateAsync(repoRoot);

        using var createdJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(created.StdOut);
        Assert.Equal((int)CliExitCode.Success, created.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            createdJson.RootElement,
            command: UcliCommandNames.SkillsUpdate,
            status: "ok",
            exitCode: (int)CliExitCode.Success);
        JsonAssert.For(createdJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("host", "openai")
                .HasString("scope", "project")
                .HasString("repositoryRoot", repoRoot)
                .HasValueKind("targetRoot", JsonValueKind.String)
                .HasValueKind("reloadGuidance", JsonValueKind.String)
                .HasArrayLength("actions", ExpectedSkillNames.Length)
                .HasInt32("createdCount", ExpectedSkillNames.Length)
                .HasInt32("updatedCount", 0)
                .HasInt32("noOpCount", 0)
                .HasProperty("actions", 0, static action => action
                    .HasString("skillName", ExpectedSkillNames[0])
                    .HasString("action", "created")));

        using var noOpJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(noOp.StdOut);
        Assert.Equal((int)CliExitCode.Success, noOp.ExitCode);
        JsonAssert.For(noOpJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasInt32("createdCount", 0)
                .HasInt32("updatedCount", 0)
                .HasInt32("noOpCount", ExpectedSkillNames.Length)
                .HasValueKind("reloadGuidance", JsonValueKind.String)
                .HasProperty("actions", 0, static action => action
                    .HasString("action", "noOp")));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsUpdate_WithDryRunAndPrintDiff_ReturnsCreatedPlanWithoutWriting ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "update-dry-run-diff");
        var repoRoot = scope.CreateDirectory("repo");

        var result = await RunOpenAiUpdateAsync(repoRoot, dryRun: true, printDiff: true);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasBoolean("dryRun", true)
                .HasBoolean("force", false)
                .HasBoolean("printDiff", true)
                .HasInt32("createdCount", ExpectedSkillNames.Length)
                .HasInt32("updatedCount", 0)
                .HasInt32("blockedCount", 0)
                .HasProperty("actions", 0, static action => action
                    .HasString("skillName", ExpectedSkillNames[0])
                    .HasString("action", "created")
                    .IsNull("blockedReason")
                    .HasArrayLength("diffs", 1)));

        var targetRoot = outputJson.RootElement.GetProperty("payload").GetProperty("targetRoot").GetString()!;
        foreach (var skillName in ExpectedSkillNames)
        {
            Assert.False(Directory.Exists(Path.Combine(targetRoot, skillName)), skillName);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsUpdate_WithCleanOutdatedTarget_ReturnsUpdatedAction ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "update-outdated");
        var repoRoot = scope.CreateDirectory("repo");
        var install = await RunOpenAiInstallAsync(repoRoot);
        Assert.Equal((int)CliExitCode.Success, install.ExitCode);
        using var installJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(install.StdOut);
        var targetRoot = installJson.RootElement.GetProperty("payload").GetProperty("targetRoot").GetString()!;
        await RewriteInstalledSkillBodyAndManifestAsOlderAsync(targetRoot, ExpectedSkillNames[0]);

        var updated = await RunOpenAiUpdateAsync(repoRoot);

        using var updatedJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(updated.StdOut);
        Assert.Equal((int)CliExitCode.Success, updated.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            updatedJson.RootElement,
            command: UcliCommandNames.SkillsUpdate,
            status: "ok",
            exitCode: (int)CliExitCode.Success);
        JsonAssert.For(updatedJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasInt32("createdCount", 0)
                .HasInt32("updatedCount", 1)
                .HasInt32("noOpCount", ExpectedSkillNames.Length - 1)
                .HasProperty("actions", 0, static action => action
                    .HasString("skillName", ExpectedSkillNames[0])
                    .HasString("action", "updated")));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsUpdate_WithDryRunLocalModification_ReturnsBlockedPlanWithoutWriting ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "update-dry-run-local-modification");
        var repoRoot = scope.CreateDirectory("repo");
        var install = await RunOpenAiInstallAsync(repoRoot);
        Assert.Equal((int)CliExitCode.Success, install.ExitCode);
        using var installJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(install.StdOut);
        var targetRoot = installJson.RootElement.GetProperty("payload").GetProperty("targetRoot").GetString()!;
        var skillPath = Path.Combine(targetRoot, ExpectedSkillNames[0], "SKILL.md");
        await File.AppendAllTextAsync(skillPath, "\nInjected instruction.\n");
        var modifiedSkill = await File.ReadAllTextAsync(skillPath);

        var result = await RunOpenAiUpdateAsync(repoRoot, dryRun: true, printDiff: true);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasBoolean("dryRun", true)
                .HasBoolean("force", false)
                .HasBoolean("printDiff", true)
                .HasInt32("updatedCount", 0)
                .HasInt32("blockedCount", 1)
                .HasProperty("actions", 0, static action => action
                    .HasString("skillName", ExpectedSkillNames[0])
                    .HasString("action", "blockedLocalModification")
                    .HasString("blockedReason", "localModificationRequiresForce")
                    .HasArrayLength("diffs", 1)));
        Assert.Equal(modifiedSkill, await File.ReadAllTextAsync(skillPath));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsUpdate_WithForceAndPrintDiff_OverwritesLocalModification ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "update-force-local-modification");
        var repoRoot = scope.CreateDirectory("repo");
        var install = await RunOpenAiInstallAsync(repoRoot);
        Assert.Equal((int)CliExitCode.Success, install.ExitCode);
        using var installJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(install.StdOut);
        var targetRoot = installJson.RootElement.GetProperty("payload").GetProperty("targetRoot").GetString()!;
        var skillPath = Path.Combine(targetRoot, ExpectedSkillNames[0], "SKILL.md");
        await File.AppendAllTextAsync(skillPath, "\nInjected instruction.\n");

        var result = await RunOpenAiUpdateAsync(repoRoot, force: true, printDiff: true);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasBoolean("dryRun", false)
                .HasBoolean("force", true)
                .HasBoolean("printDiff", true)
                .HasInt32("updatedCount", 1)
                .HasInt32("blockedCount", 0)
                .HasProperty("actions", 0, static action => action
                    .HasString("skillName", ExpectedSkillNames[0])
                    .HasString("action", "updated")
                    .IsNull("blockedReason")
                    .HasArrayLength("diffs", 1)));
        var action = FindAction(outputJson.RootElement, ExpectedSkillNames[0]);
        var modified = FindDiffFile(action, "SKILL.md");
        Assert.Equal("modified", modified.GetProperty("changeKind").GetString());
        Assert.Contains("Injected instruction.", modified.GetProperty("beforeContent").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Injected instruction.", modified.GetProperty("afterContent").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Injected instruction.", await File.ReadAllTextAsync(skillPath), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsUninstall_WithProjectScope_DeletesThenNoOps ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "uninstall-openai");
        var repoRoot = scope.CreateDirectory("repo");
        var install = await RunOpenAiInstallAsync(repoRoot);
        Assert.Equal((int)CliExitCode.Success, install.ExitCode);

        var deleted = await RunOpenAiUninstallAsync(repoRoot);
        var noOp = await RunOpenAiUninstallAsync(repoRoot);

        using var deletedJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(deleted.StdOut);
        Assert.Equal((int)CliExitCode.Success, deleted.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            deletedJson.RootElement,
            command: UcliCommandNames.SkillsUninstall,
            status: "ok",
            exitCode: (int)CliExitCode.Success);
        JsonAssert.For(deletedJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("host", "openai")
                .HasString("scope", "project")
                .HasString("repositoryRoot", repoRoot)
                .HasArrayLength("actions", ExpectedSkillNames.Length)
                .HasInt32("deletedCount", ExpectedSkillNames.Length)
                .HasInt32("noOpCount", 0)
                .HasInt32("skippedUnmanagedCount", 0)
                .HasProperty("actions", 0, static action => action
                    .HasString("skillName", ExpectedSkillNames[0])
                    .HasString("action", "deleted")));

        using var noOpJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(noOp.StdOut);
        Assert.Equal((int)CliExitCode.Success, noOp.ExitCode);
        JsonAssert.For(noOpJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasInt32("deletedCount", 0)
                .HasInt32("noOpCount", ExpectedSkillNames.Length)
                .HasInt32("skippedUnmanagedCount", 0)
                .HasProperty("actions", 0, static action => action
                    .HasString("action", "noOp")));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsUninstall_WithUnmanagedOfficialDirectory_SkipsWithoutDeleting ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "uninstall-unmanaged");
        var repoRoot = scope.CreateDirectory("repo");
        var unmanagedPath = Path.Combine(repoRoot, ".agents", "skills", ExpectedSkillNames[0], "SKILL.md");
        Directory.CreateDirectory(Path.GetDirectoryName(unmanagedPath)!);
        await File.WriteAllTextAsync(unmanagedPath, "# Existing\n");

        var result = await RunOpenAiUninstallAsync(repoRoot);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasInt32("deletedCount", 0)
                .HasInt32("noOpCount", ExpectedSkillNames.Length - 1)
                .HasInt32("skippedUnmanagedCount", 1)
                .HasProperty("actions", 0, static action => action
                    .HasString("skillName", ExpectedSkillNames[0])
                    .HasString("action", "skippedUnmanaged")));
        Assert.True(File.Exists(unmanagedPath));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsUninstall_WithDryRunLocalModification_ReturnsBlockedPlanWithoutDeleting ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "uninstall-dry-run-local-modification");
        var repoRoot = scope.CreateDirectory("repo");
        var install = await RunOpenAiInstallAsync(repoRoot);
        Assert.Equal((int)CliExitCode.Success, install.ExitCode);
        using var installJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(install.StdOut);
        var targetRoot = installJson.RootElement.GetProperty("payload").GetProperty("targetRoot").GetString()!;
        var skillDirectory = Path.Combine(targetRoot, ExpectedSkillNames[0]);
        await File.AppendAllTextAsync(Path.Combine(skillDirectory, "SKILL.md"), "\nInjected instruction.\n");

        var result = await RunOpenAiUninstallAsync(repoRoot, dryRun: true);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasBoolean("dryRun", true)
                .HasBoolean("force", false)
                .HasInt32("deletedCount", ExpectedSkillNames.Length - 1)
                .HasInt32("blockedCount", 1)
                .HasProperty("actions", 0, static action => action
                    .HasString("skillName", ExpectedSkillNames[0])
                    .HasString("action", "blockedLocalModification")
                    .HasString("blockedReason", "localModificationRequiresForce")
                    .HasArrayLength("diffs", 0)));
        Assert.True(Directory.Exists(skillDirectory));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsUninstall_DoesNotModifyOtherHostTarget ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "uninstall-other-host");
        var repoRoot = scope.CreateDirectory("repo");
        var openAi = await RunOpenAiInstallAsync(repoRoot);
        var claude = await RunInstallAsync(repoRoot, host: "claude", targetDir: null);
        Assert.Equal((int)CliExitCode.Success, openAi.ExitCode);
        Assert.Equal((int)CliExitCode.Success, claude.ExitCode);

        var result = await RunOpenAiUninstallAsync(repoRoot);

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        using var openAiJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(openAi.StdOut);
        using var claudeJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(claude.StdOut);
        var openAiTargetRoot = openAiJson.RootElement.GetProperty("payload").GetProperty("targetRoot").GetString()!;
        var claudeTargetRoot = claudeJson.RootElement.GetProperty("payload").GetProperty("targetRoot").GetString()!;
        foreach (var skillName in ExpectedSkillNames)
        {
            Assert.False(Directory.Exists(Path.Combine(openAiTargetRoot, skillName)), skillName);
            Assert.True(Directory.Exists(Path.Combine(claudeTargetRoot, skillName)), skillName);
        }
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(UcliCommandNames.InstallSubcommand, UcliCommandNames.SkillsInstall)]
    [InlineData(UcliCommandNames.UpdateSubcommand, UcliCommandNames.SkillsUpdate)]
    [InlineData(UcliCommandNames.UninstallSubcommand, UcliCommandNames.SkillsUninstall)]
    [InlineData(UcliCommandNames.DoctorSubcommand, UcliCommandNames.SkillsDoctor)]
    public async Task SkillsScopedSubcommand_WithTargetDirOutsideRepositoryRoot_ReturnsPathUnsafe (
        string subcommand,
        string expectedCommand)
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", $"outside-target-{subcommand}");
        using var outsideScope = TestDirectories.CreateTempScope("skills-cli-output-contract", $"outside-target-root-{subcommand}");
        var repoRoot = scope.CreateDirectory("repo");

        var result = await RunScopedCommandAsync(subcommand, repoRoot, host: "openai", scope: "project", targetDir: outsideScope.FullPath);

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
        var install = await RunOpenAiInstallAsync(repoRoot);
        Assert.Equal((int)CliExitCode.Success, install.ExitCode);

        var doctor = await RunOpenAiDoctorAsync(repoRoot);

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
        var install = await RunOpenAiInstallAsync(repoRoot);
        Assert.Equal((int)CliExitCode.Success, install.ExitCode);

        using (var installJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(install.StdOut))
        {
            var targetRoot = installJson.RootElement.GetProperty("payload").GetProperty("targetRoot").GetString()!;
            await File.AppendAllTextAsync(Path.Combine(targetRoot, ExpectedSkillNames[0], "SKILL.md"), "\nDrifted content.\n");
        }

        var doctor = await RunOpenAiDoctorAsync(repoRoot);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(doctor.StdOut);
        Assert.Equal((int)CliExitCode.ToolError, doctor.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.SkillsDoctor,
            status: "error",
            exitCode: (int)CliExitCode.ToolError);
        CommandResultAssert.HasSingleError(outputJson.RootElement, InstallTargetContentDigestMismatchCode);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasBoolean("isHealthy", false)
                .HasProperty("diagnostics", 0, static diagnostic => diagnostic
                    .HasString("severity", "error")
                    .HasString("code", InstallTargetContentDigestMismatchCode)
                    .HasString("skillName", ExpectedSkillNames[0])));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsDoctor_WithOpenAiMetadataDrift_ReturnsHostArtifactDiagnostic ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "doctor-openai-metadata-drift");
        var repoRoot = scope.CreateDirectory("repo");
        var install = await RunOpenAiInstallAsync(repoRoot);
        Assert.Equal((int)CliExitCode.Success, install.ExitCode);

        using (var installJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(install.StdOut))
        {
            var targetRoot = installJson.RootElement.GetProperty("payload").GetProperty("targetRoot").GetString()!;
            await File.AppendAllTextAsync(Path.Combine(targetRoot, ExpectedSkillNames[0], "agents", "openai.yaml"), "\n# Drifted metadata.\n");
        }

        var doctor = await RunOpenAiDoctorAsync(repoRoot);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(doctor.StdOut);
        Assert.Equal((int)CliExitCode.ToolError, doctor.ExitCode);
        CommandResultAssert.HasSingleError(outputJson.RootElement, InstallTargetHostArtifactDigestMismatchCode);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasProperty("diagnostics", 0, static diagnostic => diagnostic
                    .HasString("severity", "error")
                    .HasString("code", InstallTargetHostArtifactDigestMismatchCode)
                    .HasString("skillName", ExpectedSkillNames[0])));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsDoctor_WithMissingTarget_ReturnsUnhealthyDiagnostics ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "doctor-missing-target");
        var repoRoot = scope.CreateDirectory("repo");

        var result = await RunOpenAiDoctorAsync(repoRoot);

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

    private static Task<CommandExecutionResult> RunOpenAiInstallAsync (
        string repoRoot,
        bool dryRun = false,
        bool force = false,
        bool printDiff = false)
    {
        return RunInstallAsync(repoRoot, "openai", targetDir: null, dryRun, force, printDiff);
    }

    private static async Task RewriteInstalledSkillBodyAndManifestAsOlderAsync (
        string targetRoot,
        string skillName)
    {
        var skillDirectory = Path.Combine(targetRoot, skillName);
        var skillPath = Path.Combine(skillDirectory, "SKILL.md");
        var manifestPath = Path.Combine(skillDirectory, "ucli-skill.json");
        var skillText = NormalizeToLf(await File.ReadAllTextAsync(skillPath));
        Assert.True(SkillHostMaterializationInspector.TryExtractFrontmatter(skillText, out var frontmatter));
        var body = skillText[frontmatter.Length..];
        if (body.StartsWith('\n'))
        {
            body = body[1..];
        }

        var olderBody = body + "\nSynthetic previous version.\n";
        await File.WriteAllTextAsync(skillPath, frontmatter + "\n" + olderBody);

        var digestInputs = new List<SkillDigestInputFile>
        {
            new("SKILL.md", olderBody),
        };
        var referencesRoot = Path.Combine(skillDirectory, "references");
        if (Directory.Exists(referencesRoot))
        {
            foreach (var referencePath in Directory.EnumerateFiles(referencesRoot, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                var relativePath = Path.GetRelativePath(skillDirectory, referencePath).Replace(Path.DirectorySeparatorChar, '/');
                var content = NormalizeToLf(await File.ReadAllTextAsync(referencePath));
                digestInputs.Add(new SkillDigestInputFile(relativePath, content));
            }
        }

        var serializer = new SkillManifestJsonSerializer();
        var manifestResult = serializer.TryDeserialize(await File.ReadAllTextAsync(manifestPath));
        Assert.True(manifestResult.IsSuccess, manifestResult.Failure?.Message);
        var manifest = manifestResult.Value! with
        {
            ContentDigest = new SkillDigestCalculator().ComputeDigest(digestInputs),
        };
        await File.WriteAllTextAsync(manifestPath, serializer.Serialize(manifest));
    }

    private static JsonElement FindAction (
        JsonElement root,
        string skillName)
    {
        return root.GetProperty("payload")
            .GetProperty("actions")
            .EnumerateArray()
            .Single(action => string.Equals(action.GetProperty("skillName").GetString(), skillName, StringComparison.Ordinal));
    }

    private static JsonElement FindDiffFile (
        JsonElement action,
        string relativePath)
    {
        return action.GetProperty("diffs")[0]
            .GetProperty("files")
            .EnumerateArray()
            .Single(file => string.Equals(file.GetProperty("relativePath").GetString(), relativePath, StringComparison.Ordinal));
    }

    private static Task<CommandExecutionResult> RunOpenAiUpdateAsync (
        string repoRoot,
        bool dryRun = false,
        bool force = false,
        bool printDiff = false)
    {
        return RunScopedCommandAsync(UcliCommandNames.UpdateSubcommand, repoRoot, "openai", "project", targetDir: null, dryRun, force, printDiff);
    }

    private static Task<CommandExecutionResult> RunOpenAiUninstallAsync (
        string repoRoot,
        bool dryRun = false,
        bool force = false)
    {
        return RunScopedCommandAsync(UcliCommandNames.UninstallSubcommand, repoRoot, "openai", "project", targetDir: null, dryRun, force, printDiff: false);
    }

    private static Task<CommandExecutionResult> RunInstallAsync (
        string repoRoot,
        string host,
        string? targetDir,
        bool dryRun = false,
        bool force = false,
        bool printDiff = false)
    {
        return RunScopedCommandAsync(UcliCommandNames.InstallSubcommand, repoRoot, host, "project", targetDir, dryRun, force, printDiff);
    }

    private static Task<CommandExecutionResult> RunScopedCommandAsync (
        string subcommand,
        string repoRoot,
        string? host,
        string? scope,
        string? targetDir,
        bool dryRun = false,
        bool force = false,
        bool printDiff = false)
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

        if (dryRun)
        {
            args.Add("--dryRun");
        }

        if (force)
        {
            args.Add("--force");
        }

        if (printDiff)
        {
            args.Add("--printDiff");
        }

        return CliProcessRunner.RunCommandAsync(args.ToArray());
    }

    private static Task<CommandExecutionResult> RunOpenAiDoctorAsync (string repoRoot)
    {
        return RunScopedCommandAsync(UcliCommandNames.DoctorSubcommand, repoRoot, "openai", "project", targetDir: null);
    }

    private static string NormalizeToLf (string text)
    {
        return text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
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
            UcliCommandNames.UpdateSubcommand => [UcliCommandNames.Skills, subcommand, "--scope", "project", "--repoRoot", repoRoot],
            UcliCommandNames.UninstallSubcommand => [UcliCommandNames.Skills, subcommand, "--scope", "project", "--repoRoot", repoRoot],
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
            UcliCommandNames.UpdateSubcommand => [UcliCommandNames.Skills, subcommand, "--host", "generic", "--scope", "project", "--repoRoot", repoRoot, "--targetDir", targetDir],
            UcliCommandNames.UninstallSubcommand => [UcliCommandNames.Skills, subcommand, "--host", "generic", "--scope", "project", "--repoRoot", repoRoot, "--targetDir", targetDir],
            UcliCommandNames.DoctorSubcommand => [UcliCommandNames.Skills, subcommand, "--host", "generic", "--scope", "project", "--repoRoot", repoRoot, "--targetDir", targetDir],
            _ => throw new ArgumentOutOfRangeException(nameof(subcommand), subcommand, "Unsupported skills subcommand."),
        };
    }
}
