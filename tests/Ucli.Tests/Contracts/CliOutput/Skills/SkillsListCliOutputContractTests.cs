using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Tests.Schemas;

namespace MackySoft.Ucli.Tests;

[Collection(CurrentDirectoryTestCollection.Name)]
public sealed class SkillsListCliOutputContractTests
{
    private const string SkillInputInvalidCode = "SKILL_INPUT_INVALID";
    private const string InvalidArgumentCode = "INVALID_ARGUMENT";

    [Fact]
    [Trait("Size", "Small")]
    public async Task SkillsList_ReturnsOfficialSkillsAndSupportedHosts ()
    {
        var result = await SkillsCliOutputContractTestSupport.SharedRunner.ListAsync(tier: ["basic"]);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.SkillsList);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);

        var payload = outputJson.RootElement.GetProperty("payload");
        JsonAssert.For(payload)
            .HasArrayLength("tiers", 1)
            .HasArrayLength("skillNames", 0)
            .HasArrayLength("availableTiers", 3)
            .HasArrayLength("skills", SkillsCliOutputContractTestSupport.ExpectedSkillNames.Length)
            .HasArrayLength("supportedHosts", 3)
            .HasProperty("availableTiers", 0, static tier => tier
                .HasString("tier", "basic")
                .HasInt32("skillCount", SkillsCliOutputContractTestSupport.ExpectedSkillNames.Length))
            .HasProperty("availableTiers", 1, static tier => tier
                .HasString("tier", "advanced")
                .HasInt32("skillCount", 0))
            .HasProperty("availableTiers", 2, static tier => tier
                .HasString("tier", "developer")
                .HasInt32("skillCount", 0))
            .HasProperty("skills", 0, static skill => skill
                .HasString("skillName", SkillsCliOutputContractTestSupport.ExpectedSkillNames[0])
                .HasValueKind("displayName", JsonValueKind.String)
                .HasValueKind("description", JsonValueKind.String)
                .HasArrayLength("dependencies", 0)
                .HasString("tier", "basic")
                .HasString("catalogId", "com.mackysoft.ucli")
                .HasInt32("skillBundleVersion", 1)
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

        Assert.Equal(["basic"], payload
            .GetProperty("tiers")
            .EnumerateArray()
            .Select(static tier => tier.GetString() ?? string.Empty)
            .ToArray());
        Assert.Equal(SkillsCliOutputContractTestSupport.ExpectedSkillNames, payload
            .GetProperty("skills")
            .EnumerateArray()
            .Select(static skill => skill.GetProperty("skillName").GetString())
            .ToArray());
        SkillsListPayloadSchemaTestSupport.AssertPayloadMatchesSchema(outputJson.RootElement);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("advanced")]
    [InlineData("developer")]
    public async Task SkillsList_WithEmptyTier_ReturnsEmptySkillList (string tier)
    {
        var result = await SkillsCliOutputContractTestSupport.SharedRunner.ListAsync(tier: [tier]);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.SkillsList);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasArrayLength("tiers", 1)
                .HasArrayLength("skillNames", 0)
                .HasArrayLength("availableTiers", 3)
                .HasArrayLength("skills", 0));
        Assert.Equal(tier, outputJson.RootElement.GetProperty("payload").GetProperty("tiers")[0].GetString());
        Assert.Equal(["basic", "advanced", "developer"], ReadPayloadStringArray(outputJson.RootElement, "availableTiers", "tier"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SkillsList_WithMultipleTiers_ReturnsMatchingSkills ()
    {
        var result = await SkillsCliOutputContractTestSupport.SharedRunner.ListAsync(tier: ["basic", "advanced"]);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.SkillsList);

        var payload = outputJson.RootElement.GetProperty("payload");
        Assert.Equal(["basic", "advanced"], payload
            .GetProperty("tiers")
            .EnumerateArray()
            .Select(static tier => tier.GetString() ?? string.Empty)
            .ToArray());
        Assert.Empty(payload.GetProperty("skillNames").EnumerateArray());
        Assert.Equal(["basic", "advanced", "developer"], ReadPayloadStringArray(outputJson.RootElement, "availableTiers", "tier"));
        Assert.Equal(SkillsCliOutputContractTestSupport.ExpectedSkillNames, payload
            .GetProperty("skills")
            .EnumerateArray()
            .Select(static skill => skill.GetProperty("skillName").GetString())
            .ToArray());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SkillsList_WithoutTier_ReturnsAllDefinedTiersAndMatchingSkills ()
    {
        var result = await SkillsCliOutputContractTestSupport.SharedRunner.ListAsync();

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.SkillsList);

        var payload = outputJson.RootElement.GetProperty("payload");
        Assert.Equal(["basic", "advanced", "developer"], payload
            .GetProperty("tiers")
            .EnumerateArray()
            .Select(static tier => tier.GetString() ?? string.Empty)
            .ToArray());
        Assert.Empty(payload.GetProperty("skillNames").EnumerateArray());
        Assert.Equal(["basic", "advanced", "developer"], ReadPayloadStringArray(outputJson.RootElement, "availableTiers", "tier"));
        Assert.Equal(SkillsCliOutputContractTestSupport.ExpectedSkillNames, payload
            .GetProperty("skills")
            .EnumerateArray()
            .Select(static skill => skill.GetProperty("skillName").GetString())
            .ToArray());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SkillsList_WithSkillName_ReturnsExactSkill ()
    {
        var result = await SkillsCliOutputContractTestSupport.SharedRunner.ListAsync(skill: [SkillsCliOutputContractTestSupport.SelectedSingleSkillName]);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.SkillsList);

        var payload = outputJson.RootElement.GetProperty("payload");
        Assert.Equal(["basic", "advanced", "developer"], payload
            .GetProperty("tiers")
            .EnumerateArray()
            .Select(static tier => tier.GetString() ?? string.Empty)
            .ToArray());
        Assert.Equal([SkillsCliOutputContractTestSupport.SelectedSingleSkillName], payload
            .GetProperty("skillNames")
            .EnumerateArray()
            .Select(static skillName => skillName.GetString() ?? string.Empty)
            .ToArray());
        Assert.Equal([SkillsCliOutputContractTestSupport.SelectedSingleSkillName], payload
            .GetProperty("skills")
            .EnumerateArray()
            .Select(static skill => skill.GetProperty("skillName").GetString() ?? string.Empty)
            .ToArray());
        SkillsListPayloadSchemaTestSupport.AssertPayloadMatchesSchema(outputJson.RootElement);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SkillsList_WithTierAndMismatchedSkillName_ReturnsInvalidArgument ()
    {
        var result = await SkillsCliOutputContractTestSupport.SharedRunner.ListAsync(
            tier: ["advanced"],
            skill: [SkillsCliOutputContractTestSupport.SelectedSingleSkillName]);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.SkillsList);
        CommandResultAssert.HasSingleError(outputJson.RootElement, SkillInputInvalidCode);
        Assert.Contains("does not match selected tiers", outputJson.RootElement.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("unknown")]
    [InlineData("Basic")]
    [InlineData("advanced ")]
    public async Task SkillsList_WithInvalidTier_ReturnsInvalidArgument (string tier)
    {
        var result = await SkillsCliOutputContractTestSupport.SharedRunner.ListAsync(tier: [tier]);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.SkillsList);
        CommandResultAssert.HasSingleError(outputJson.RootElement, InvalidArgumentCode);
        Assert.Contains("Unsupported SKILL tier:", outputJson.RootElement.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    private static string[] ReadPayloadStringArray (
        JsonElement root,
        string arrayName,
        string propertyName)
    {
        return root
            .GetProperty("payload")
            .GetProperty(arrayName)
            .EnumerateArray()
            .Select(value => value.GetProperty(propertyName).GetString() ?? string.Empty)
            .ToArray();
    }
}
