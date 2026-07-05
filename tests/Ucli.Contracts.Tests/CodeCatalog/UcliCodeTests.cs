namespace MackySoft.Ucli.Contracts.Tests.CodeCatalog;

public sealed class UcliCodeTests
{
    private static readonly string[] ValidCodeValues =
    [
        "IPC_TIMEOUT",
        "UNITY_COMPILE_NO_ERRORS",
        "A1_B2",
        "SOME.FUTURE_CODE",
    ];

    private static readonly string?[] InvalidCodeValues =
    [
        null,
        string.Empty,
        " ",
        "lowercase_code",
        "Code",
        "CODE-WITH-HYPHEN",
        "1_CODE",
        ".CODE",
        "CODE.",
        "A..B",
        "ERROR.2ND_PHASE",
        new string('A', UcliCode.MaximumLength + 1),
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidValue_WithValidCode_ReturnsTrue ()
    {
        foreach (var value in ValidCodeValues)
        {
            Assert.True(UcliCode.IsValidValue(value));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ValidCode_ExposesRawValueForStringConversionAndComparison ()
    {
        var code = new UcliCode("UNITY_READY_EXECUTION");

        Assert.Equal("UNITY_READY_EXECUTION", code.Value);
        Assert.Equal("UNITY_READY_EXECUTION", code.ToString());
        Assert.True(code.EqualsValue("UNITY_READY_EXECUTION"));
        string rawValue = code;
        Assert.Equal("UNITY_READY_EXECUTION", rawValue);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UnknownValidCode_ExposesRawValueForForwardCompatibleComparison ()
    {
        UcliCode code = new("FUTURE_DAEMON_FAILURE");

        Assert.Equal("FUTURE_DAEMON_FAILURE", code.Value);
        Assert.Equal("FUTURE_DAEMON_FAILURE", code.ToString());
        string rawValue = code;
        Assert.Equal("FUTURE_DAEMON_FAILURE", rawValue);
        Assert.Equal(new UcliCode("FUTURE_DAEMON_FAILURE"), code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryCreate_WithValidValue_ReturnsCode ()
    {
        var result = UcliCode.TryCreate("UNITY_READY_MUTATION", out var code);

        Assert.True(result);
        Assert.Equal("UNITY_READY_MUTATION", code.Value);
        Assert.True(code.IsValid);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void InvalidCodeValues_AreRejectedByAllConstructionPaths ()
    {
        foreach (var value in InvalidCodeValues)
        {
            Assert.False(UcliCode.IsValidValue(value));

            var result = UcliCode.TryCreate(value, out var code);
            Assert.False(result);
            Assert.False(code.IsValid);

            Assert.ThrowsAny<ArgumentException>(() => new UcliCode(value!));
        }
    }
}
