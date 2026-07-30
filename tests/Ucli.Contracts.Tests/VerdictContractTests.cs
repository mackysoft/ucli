namespace MackySoft.Ucli.Contracts.Tests;

public sealed class VerdictContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void VerdictLiterals_AreStable ()
    {
        Assert.Equal(
            ["pass", "fail", "incomplete"],
            TextVocabulary.GetTexts<Verdict>());
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(GetAggregationCases))]
    public void Aggregate_UsesVerdictPriorityIndependentlyOfInputOrder (
        Verdict? expected,
        Verdict?[] verdicts)
    {
        Assert.Equal(expected, VerdictAggregation.Aggregate(verdicts));
    }

    public static TheoryData<Verdict?, Verdict?[]> GetAggregationCases ()
    {
        return new TheoryData<Verdict?, Verdict?[]>
        {
            { null, [] },
            { null, [null, null] },
            { Verdict.Pass, [null, Verdict.Pass] },
            { Verdict.Incomplete, [Verdict.Pass, Verdict.Incomplete] },
            { Verdict.Incomplete, [Verdict.Incomplete, Verdict.Pass] },
            { Verdict.Fail, [Verdict.Pass, Verdict.Incomplete, Verdict.Fail] },
            { Verdict.Fail, [Verdict.Fail, Verdict.Pass, Verdict.Incomplete] },
            { Verdict.Fail, [Verdict.Incomplete, Verdict.Fail, Verdict.Pass] },
        };
    }
}
