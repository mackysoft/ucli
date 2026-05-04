using MackySoft.Ucli.Skills.Doctor;

namespace MackySoft.Ucli.Skills.Tests.Doctor;

public sealed class SkillDoctorDiagnosticTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Error_CreatesErrorDiagnostic ()
    {
        var diagnostic = SkillDoctorDiagnostic.Error("SKILL_ERROR", "Broken.", "sample-skill");

        Assert.Equal(SkillDoctorSeverity.Error, diagnostic.Severity);
        Assert.Equal("SKILL_ERROR", diagnostic.Code);
        Assert.Equal("Broken.", diagnostic.Message);
        Assert.Equal("sample-skill", diagnostic.SkillName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Info_CreatesInfoDiagnostic ()
    {
        var diagnostic = SkillDoctorDiagnostic.Info("SKILL_OK", "Healthy.");

        Assert.Equal(SkillDoctorSeverity.Info, diagnostic.Severity);
        Assert.Equal("SKILL_OK", diagnostic.Code);
        Assert.Equal("Healthy.", diagnostic.Message);
        Assert.Null(diagnostic.SkillName);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(null, "message")]
    [InlineData("", "message")]
    [InlineData(" ", "message")]
    [InlineData("SKILL_ERROR", null)]
    [InlineData("SKILL_ERROR", "")]
    [InlineData("SKILL_ERROR", " ")]
    public void Error_Throws_WhenCodeOrMessageIsBlank (
        string? code,
        string? message)
    {
        Assert.ThrowsAny<ArgumentException>(() => SkillDoctorDiagnostic.Error(code!, message!));
    }
}
