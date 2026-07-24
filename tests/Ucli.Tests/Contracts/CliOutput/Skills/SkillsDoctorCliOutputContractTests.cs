namespace MackySoft.Ucli.Tests;

[Collection(CurrentDirectoryTestCollection.Name)]
public sealed class SkillsDoctorCliOutputContractTests
{
    private const string InstallTargetContentDigestMismatchCode = "SKILL_INSTALL_TARGET_CONTENT_DIGEST_MISMATCH";
    private const string InstallTargetHostArtifactDigestMismatchCode = "SKILL_INSTALL_TARGET_HOST_ARTIFACT_DIGEST_MISMATCH";
    private const string InstallTargetUnmanagedCode = "SKILL_INSTALL_TARGET_UNMANAGED";

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsDoctor_WithInstalledTarget_ReturnsHealthy ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "doctor-healthy");
        var repoRoot = scope.CreateDirectory("repo");
        await SkillsCliOutputContractTestSupport.InstallSelectedProjectSkillAsync(repoRoot);

        var doctor = await SkillsCliOutputContractTestSupport.RunOpenAiDoctorAsync(
            repoRoot,
            skill: [SkillsCliOutputContractTestSupport.SelectedSingleSkillName]);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(doctor.StdOut);
        Assert.Equal((int)CliExitCode.Success, doctor.ExitCode);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.SkillsDoctor);
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
        var installed = await SkillsCliOutputContractTestSupport.InstallSelectedProjectSkillAsync(repoRoot);
        await File.AppendAllTextAsync(installed.SkillMarkdownPath, "\nDrifted content.\n");

        var doctor = await SkillsCliOutputContractTestSupport.RunOpenAiDoctorAsync(
            repoRoot,
            skill: [SkillsCliOutputContractTestSupport.SelectedSingleSkillName]);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(doctor.StdOut);
        Assert.Equal((int)CliExitCode.ToolError, doctor.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.SkillsDoctor,
            status: TextVocabulary.GetText(CommandResultStatus.Error),
            exitCode: (int)CliExitCode.ToolError);
        CommandResultAssert.HasSingleError(outputJson.RootElement, InstallTargetContentDigestMismatchCode);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasBoolean("isHealthy", false)
                .HasProperty("diagnostics", 0, static diagnostic => diagnostic
                    .HasString("severity", "error")
                    .HasString("code", InstallTargetContentDigestMismatchCode)
                    .HasString("skillName", SkillsCliOutputContractTestSupport.SelectedSingleSkillName)));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsDoctor_WithOpenAiMetadataDrift_ReturnsHostArtifactDiagnostic ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "doctor-openai-metadata-drift");
        var repoRoot = scope.CreateDirectory("repo");
        var installed = await SkillsCliOutputContractTestSupport.InstallSelectedProjectSkillAsync(repoRoot);
        await File.AppendAllTextAsync(installed.GetPath("agents/openai.yaml"), "\n# Drifted metadata.\n");

        var doctor = await SkillsCliOutputContractTestSupport.RunOpenAiDoctorAsync(
            repoRoot,
            skill: [SkillsCliOutputContractTestSupport.SelectedSingleSkillName]);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(doctor.StdOut);
        Assert.Equal((int)CliExitCode.ToolError, doctor.ExitCode);
        CommandResultAssert.HasSingleError(outputJson.RootElement, InstallTargetHostArtifactDigestMismatchCode);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasProperty("diagnostics", 0, static diagnostic => diagnostic
                    .HasString("severity", "error")
                    .HasString("code", InstallTargetHostArtifactDigestMismatchCode)
                    .HasString("skillName", SkillsCliOutputContractTestSupport.SelectedSingleSkillName)));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsDoctor_WithMissingTarget_ReturnsUnhealthyDiagnostics ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "doctor-missing-target");
        var repoRoot = scope.CreateDirectory("repo");

        var result = await SkillsCliOutputContractTestSupport.RunOpenAiDoctorAsync(
            repoRoot,
            skill: [SkillsCliOutputContractTestSupport.SelectedSingleSkillName]);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.SkillsDoctor,
            status: TextVocabulary.GetText(CommandResultStatus.Error),
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
}
