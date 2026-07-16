using System.IO.Compression;
using System.Text.Json;

namespace MackySoft.Ucli.Tests;

[Collection(CurrentDirectoryTestCollection.Name)]
public sealed class SkillsExportCliOutputContractTests
{
    private const string SkillInputInvalidCode = "SKILL_INPUT_INVALID";
    private const string InvalidArgumentCode = "INVALID_ARGUMENT";

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsExport_WithOpenAiHost_WritesMaterializedPackages ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "export-openai");
        var outputRoot = scope.GetPath("exported");

        var result = await RunSkillsExportCommandAsync(outputRoot);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.SkillsExport);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("host", "openai")
                .HasArrayLength("tiers", 1)
                .HasArrayLength("skillNames", 0)
                .HasString("format", "directory")
                .HasString("outputRoot", outputRoot)
                .HasArrayLength("skills", SkillsCliOutputContractTestSupport.ExpectedSkillNames.Length)
                .HasInt32("skillCount", SkillsCliOutputContractTestSupport.ExpectedSkillNames.Length)
                .HasValueKind("reloadGuidance", JsonValueKind.String));

        foreach (var skillName in SkillsCliOutputContractTestSupport.ExpectedSkillNames)
        {
            Assert.True(File.Exists(Path.Combine(outputRoot, skillName, "SKILL.md")), skillName);
            Assert.True(File.Exists(Path.Combine(outputRoot, skillName, "agent-skill.json")), skillName);
            Assert.True(File.Exists(Path.Combine(outputRoot, skillName, "agents", "openai.yaml")), skillName);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsExport_WithSkillNameOnly_WritesMatchingPackage ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "export-openai-skill");
        var outputRoot = scope.GetPath("exported");

        var result = await RunSkillsExportCommandAsync(
            outputRoot,
            skill: [SkillsCliOutputContractTestSupport.SelectedSingleSkillName]);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.SkillsExport);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasArrayLength("tiers", 3)
                .HasArrayLength("skillNames", 1)
                .HasArrayLength("skills", 1)
                .HasInt32("skillCount", 1));
        var payload = outputJson.RootElement.GetProperty("payload");
        Assert.Equal([SkillsCliOutputContractTestSupport.SelectedSingleSkillName], payload
            .GetProperty("skillNames")
            .EnumerateArray()
            .Select(static skillName => skillName.GetString() ?? string.Empty)
            .ToArray());
        Assert.Equal([SkillsCliOutputContractTestSupport.SelectedSingleSkillName], payload
            .GetProperty("skills")
            .EnumerateArray()
            .Select(static skillName => skillName.GetString() ?? string.Empty)
            .ToArray());
        Assert.True(File.Exists(Path.Combine(outputRoot, SkillsCliOutputContractTestSupport.SelectedSingleSkillName, "SKILL.md")));
        Assert.False(Directory.Exists(Path.Combine(outputRoot, SkillsCliOutputContractTestSupport.ExpectedSkillNames[0])));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsExport_WithTierAndMismatchedSkillName_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "export-skill-tier-mismatch");

        var result = await RunSkillsExportCommandAsync(
            scope.GetPath("exported"),
            tier: ["advanced"],
            skill: [SkillsCliOutputContractTestSupport.SelectedSingleSkillName]);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.SkillsExport);
        CommandResultAssert.HasSingleError(outputJson.RootElement, SkillInputInvalidCode);
        Assert.Contains("does not match selected tiers", outputJson.RootElement.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsExport_WithZipFormat_WritesDeterministicReleaseZip ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "export-openai-zip");
        var firstZip = scope.GetPath("skills-a.zip");
        var secondZip = scope.GetPath("skills-b.zip");

        var first = await RunSkillsExportCommandAsync(firstZip, format: "zip");
        var second = await RunSkillsExportCommandAsync(secondZip, format: "zip");

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
        Assert.Contains($"{SkillsCliOutputContractTestSupport.ExpectedSkillNames[0]}/SKILL.md", entryNames);
        Assert.Contains($"{SkillsCliOutputContractTestSupport.ExpectedSkillNames[0]}/agents/openai.yaml", entryNames);
        Assert.DoesNotContain(entryNames, static entry => entry.EndsWith("/", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsExport_WithoutHost_ReturnsInvalidArgumentAsSingleJson ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "missing-host-export");

        var result = await RunSkillsExportCommandAsync(
            scope.GetPath("exported"),
            host: null);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.SkillsExport);
        CommandResultAssert.HasSingleError(outputJson.RootElement, InvalidArgumentCode);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsExport_WithoutOutput_ReturnsInvalidArgument ()
    {
        var result = await RunSkillsExportCommandAsync(outputRoot: null);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.SkillsExport);
        CommandResultAssert.HasSingleError(outputJson.RootElement, InvalidArgumentCode);
    }

    private static Task<CommandExecutionResult> RunSkillsExportCommandAsync (
        string? outputRoot,
        string? host = "openai",
        string format = "directory",
        string[]? tier = null,
        string[]? skill = null)
    {
        return SkillsCliOutputContractTestSupport.SharedRunner.ExportAsync(new SkillsCommandTestRunner.Options
        {
            Host = host,
            Output = outputRoot,
            Format = format,
            Tier = tier ?? (skill is null ? ["basic"] : null),
            Skill = skill,
        });
    }
}
