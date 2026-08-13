namespace MackySoft.Ucli.Tests;

public sealed class UcliRunProgramSkillContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void GeneratedSkill_UsesThePublicProgramCommandSet ()
    {
        var skillText = ReadGeneratedSkill();

        Assert.Contains("ucli program presets list", skillText, StringComparison.Ordinal);
        Assert.Contains("ucli program presets describe <presetId>", skillText, StringComparison.Ordinal);
        Assert.Contains("ucli program validate", skillText, StringComparison.Ordinal);
        Assert.Contains("ucli program plan", skillText, StringComparison.Ordinal);
        Assert.Contains("ucli program run", skillText, StringComparison.Ordinal);
        Assert.Contains("ucli program status --runId <runId>", skillText, StringComparison.Ordinal);
        Assert.Contains("ucli program cancel --runId <runId>", skillText, StringComparison.Ordinal);
        Assert.DoesNotContain("ucli eval", skillText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ucli verify", skillText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GeneratedSkill_PreservesPublicRunRecoveryAndEvidenceBoundaries ()
    {
        var skillText = ReadGeneratedSkill();

        Assert.Contains("program.run.started", skillText, StringComparison.Ordinal);
        Assert.Contains("Do not start a second Program Run", skillText, StringComparison.Ordinal);
        Assert.Contains("state` and `verdict` independently", skillText, StringComparison.Ordinal);
        Assert.Contains("terminal", skillText, StringComparison.Ordinal);
        Assert.Contains("applicationState", skillText, StringComparison.Ordinal);
        Assert.Contains("terminal.recordRef", skillText, StringComparison.Ordinal);
        Assert.Contains("steps[].resultRef", skillText, StringComparison.Ordinal);
    }

    private static string ReadGeneratedSkill () =>
        File.ReadAllText(TestRepositoryPaths.GetFullPath("skills", "generated", "ucli-run-program", "SKILL.md"));
}
