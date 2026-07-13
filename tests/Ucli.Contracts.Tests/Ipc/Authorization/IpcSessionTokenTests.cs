using MackySoft.Ucli.Contracts.Ipc.Authorization;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Authorization;

public sealed class IpcSessionTokenTests
{
    private const string CanonicalEncodedValue = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    public static TheoryData<string> CanonicalEncodedValues => new()
    {
        CanonicalEncodedValue,
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaag",
        "__________________________________________8",
        "------------------------------------------A",
        "0000000000000000000000000000000000000000000",
    };

    public static TheoryData<string?> InvalidEncodedValues => new()
    {
        null,
        string.Empty,
        " ",
        new string('A', 42),
        new string('A', 44),
        " " + new string('A', 42),
        new string('A', 42) + " ",
        new string('A', 21) + "=" + new string('A', 21),
        new string('A', 21) + "+" + new string('A', 21),
        new string('A', 21) + "/" + new string('A', 21),
        new string('A', 21) + "é" + new string('A', 21),
        new string('A', 42) + "B",
    };

    public static TheoryData<string?, bool> PresentedEncodedValues => new()
    {
        { CanonicalEncodedValue, true },
        { "B" + new string('A', 42), false },
        { new string('A', 21) + "B" + new string('A', 21), false },
        { new string('A', 42) + "E", false },
        { new string('A', 42) + "B", false },
        { new string('A', 42), false },
        { null, false },
    };

    [Fact]
    [Trait("Size", "Small")]
    public void CreateRandom_ReturnsCanonicalEncodedValue ()
    {
        const int SampleCount = 64;

        for (var i = 0; i < SampleCount; i++)
        {
            var token = IpcSessionToken.CreateRandom();
            var encodedValue = token.GetEncodedValue();

            Assert.Equal(43, encodedValue.Length);
            Assert.True(IpcSessionToken.IsValidEncodedValue(encodedValue));
            Assert.True(IpcSessionToken.TryParse(encodedValue, out var parsedToken));
            Assert.Equal(token, parsedToken);
        }
    }

    [Theory]
    [MemberData(nameof(CanonicalEncodedValues))]
    [Trait("Size", "Small")]
    public void TryParse_WithCanonicalEncodedValue_ReturnsToken (string encodedValue)
    {
        var result = IpcSessionToken.TryParse(encodedValue, out var token);

        Assert.True(result);
        Assert.NotNull(token);
        Assert.Equal(encodedValue, token.GetEncodedValue());
    }

    [Theory]
    [MemberData(nameof(InvalidEncodedValues))]
    [Trait("Size", "Small")]
    public void ValidationApis_WithInvalidEncodedValue_RejectIt (string? encodedValue)
    {
        var isValid = IpcSessionToken.IsValidEncodedValue(encodedValue);
        var isParsed = IpcSessionToken.TryParse(encodedValue, out var token);

        Assert.False(isValid);
        Assert.False(isParsed);
        Assert.Null(token);
    }

    [Theory]
    [MemberData(nameof(PresentedEncodedValues))]
    [Trait("Size", "Small")]
    public void Matches_WithPresentedEncodedValue_ReturnsExpectedResult (
        string? presentedEncodedValue,
        bool expectedResult)
    {
        var token = ParseToken(CanonicalEncodedValue);

        var result = token.Matches(presentedEncodedValue);

        Assert.Equal(expectedResult, result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Equality_UsesEncodedValueAndProducesConsistentHashCode ()
    {
        var first = ParseToken(CanonicalEncodedValue);
        var sameValue = ParseToken(CanonicalEncodedValue);
        var differentValue = ParseToken(new string('A', 42) + "E");

        Assert.NotSame(first, sameValue);
        Assert.True(first.Equals(sameValue));
        Assert.True(first.Equals((object)sameValue));
        Assert.True(first == sameValue);
        Assert.False(first != sameValue);
        Assert.Equal(first.GetHashCode(), sameValue.GetHashCode());
        Assert.False(first.Equals(differentValue));
        Assert.True(first != differentValue);
        Assert.False(first.Equals(null));
        Assert.False(first == null);
        Assert.True((IpcSessionToken?)null == null);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesAndEquality_DoNotAllocateComparisonBuffers ()
    {
        const int IterationCount = 10_000;
        var token = ParseToken(CanonicalEncodedValue);
        var sameValue = ParseToken(CanonicalEncodedValue);

        for (var i = 0; i < 100; i++)
        {
            _ = token.Matches(CanonicalEncodedValue);
            _ = token.Equals(sameValue);
        }

        _ = GC.GetAllocatedBytesForCurrentThread();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var matchCount = 0;
        for (var i = 0; i < IterationCount; i++)
        {
            if (token.Matches(CanonicalEncodedValue))
            {
                matchCount++;
            }

            if (token.Equals(sameValue))
            {
                matchCount++;
            }
        }

        var allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(IterationCount * 2, matchCount);
        Assert.Equal(0, allocatedAfter - allocatedBefore);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ToString_DoesNotExposeEncodedValue ()
    {
        var token = ParseToken(CanonicalEncodedValue);

        Assert.Equal("[REDACTED]", token.ToString());
        Assert.Equal("[REDACTED]", $"{token}");
        Assert.DoesNotContain(CanonicalEncodedValue, token.ToString(), StringComparison.Ordinal);
    }

    private static IpcSessionToken ParseToken (string encodedValue)
    {
        Assert.True(IpcSessionToken.TryParse(encodedValue, out var token));
        return token;
    }
}
