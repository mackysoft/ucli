using MackySoft.Ucli.Contracts.Testing;

namespace MackySoft.Ucli.Contracts.Tests.Testing;

public sealed class TestRunPlatformCodecTests
{
    private static readonly TestRunPlatformParseCase[] TestRunPlatformParseCases =
    [
        new("editmode", ExpectedResult: true, TestRunPlatformKind.EditMode, ExpectedPlayerLiteral: null),
        new(" PLAYMODE ", ExpectedResult: true, TestRunPlatformKind.PlayMode, ExpectedPlayerLiteral: null),
        new("Android", ExpectedResult: true, TestRunPlatformKind.Player, ExpectedPlayerLiteral: "Android"),
        new("", ExpectedResult: false, ExpectedKind: null, ExpectedPlayerLiteral: null),
        new(" ", ExpectedResult: false, ExpectedKind: null, ExpectedPlayerLiteral: null),
        new(null, ExpectedResult: false, ExpectedKind: null, ExpectedPlayerLiteral: null),
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void TestRunPlatformCodec_HasStableStringValues ()
    {
        Assert.Equal("editmode", TestRunPlatformCodec.EditMode);
        Assert.Equal("playmode", TestRunPlatformCodec.PlayMode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TestRunPlatformCodec_HasStableUnityStringValues ()
    {
        Assert.Equal("editmode", TestRunPlatformCodec.UnityEditMode);
        Assert.Equal("playmode", TestRunPlatformCodec.UnityPlayMode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TestRunPlatformKind_HasStableEnumValues ()
    {
        Assert.Equal(0, (int)TestRunPlatformKind.EditMode);
        Assert.Equal(1, (int)TestRunPlatformKind.PlayMode);
        Assert.Equal(2, (int)TestRunPlatformKind.Player);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TestRunPlatformCodec_ToValue_ReturnsExpectedLiterals ()
    {
        Assert.Equal("editmode", TestRunPlatformCodec.ToValue(TestRunPlatform.EditMode));
        Assert.Equal("playmode", TestRunPlatformCodec.ToValue(TestRunPlatform.PlayMode));
        Assert.Equal("Android", TestRunPlatformCodec.ToValue(TestRunPlatform.Player("Android")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TestRunPlatformCodec_ToUnityValue_ReturnsExpectedLiterals ()
    {
        Assert.Equal("editmode", TestRunPlatformCodec.ToUnityValue(TestRunPlatform.EditMode));
        Assert.Equal("playmode", TestRunPlatformCodec.ToUnityValue(TestRunPlatform.PlayMode));
        Assert.Equal("Android", TestRunPlatformCodec.ToUnityValue(TestRunPlatform.Player("Android")));
    }

    [Theory]
    [InlineData(" Android ")]
    [InlineData("Android\n")]
    [Trait("Size", "Small")]
    public void Player_WhenBuildTargetLiteralIsNotCanonical_RejectsInvalidValue (string value)
    {
        var exception = Assert.Throws<ArgumentException>(() => TestRunPlatform.Player(value));

        Assert.Equal("playerBuildTargetLiteral", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TestRunPlatformCodec_TryParse_ReturnsExpectedResult ()
    {
        foreach (var testCase in TestRunPlatformParseCases)
        {
            var result = TestRunPlatformCodec.TryParse(testCase.Value, out var testPlatform);

            Assert.Equal(testCase.ExpectedResult, result);
            if (testCase.ExpectedKind.HasValue)
            {
                Assert.Equal(testCase.ExpectedKind.Value, testPlatform.Kind);
                Assert.Equal(testCase.ExpectedPlayerLiteral, testPlatform.PlayerBuildTargetLiteral);
            }
        }
    }

    private sealed record TestRunPlatformParseCase (
        string? Value,
        bool ExpectedResult,
        TestRunPlatformKind? ExpectedKind,
        string? ExpectedPlayerLiteral);
}
