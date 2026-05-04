using MackySoft.Ucli.Skills.Digests;

namespace MackySoft.Ucli.Skills.Tests.Digests;

public sealed class SkillDigestCalculatorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ComputeDigest_IsIndependentOfInputOrder ()
    {
        var calculator = new SkillDigestCalculator();

        var first = calculator.ComputeDigest(
        [
            new SkillDigestInputFile("b.md", "second\n"),
            new SkillDigestInputFile("a.md", "first\n"),
        ]);
        var second = calculator.ComputeDigest(
        [
            new SkillDigestInputFile("a.md", "first\n"),
            new SkillDigestInputFile("b.md", "second\n"),
        ]);

        Assert.Equal(first, second);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ComputeDigest_NormalizesLineEndings ()
    {
        var calculator = new SkillDigestCalculator();

        var lf = calculator.ComputeDigest([new SkillDigestInputFile("SKILL.md", "line1\nline2\n")]);
        var crlf = calculator.ComputeDigest([new SkillDigestInputFile("SKILL.md", "line1\r\nline2\r\n")]);

        Assert.Equal(lf, crlf);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ComputeDigest_SeparatesPathAndContentWithNullByte ()
    {
        var calculator = new SkillDigestCalculator();

        var first = calculator.ComputeDigest([new SkillDigestInputFile("ab", "c")]);
        var second = calculator.ComputeDigest([new SkillDigestInputFile("a", "bc")]);

        Assert.NotEqual(first, second);
    }
}
