namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorIpcMethodTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void GetLiterals_ReturnsCanonicalSupervisorMethods ()
    {
        Assert.Equal(
            [
                "supervisor.ensureRunning",
                "supervisor.ping",
                "supervisor.stopProject",
            ],
            ContractLiteralCodec
                .GetLiterals<SupervisorIpcMethod>()
                .Order(StringComparer.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ContractLiteralCodec_WhenMethodIsCanonical_RoundTrips ()
    {
        var cases = new (SupervisorIpcMethod Method, string Literal)[]
        {
            (SupervisorIpcMethod.Ping, "supervisor.ping"),
            (SupervisorIpcMethod.EnsureRunning, "supervisor.ensureRunning"),
            (SupervisorIpcMethod.StopProject, "supervisor.stopProject"),
        };

        foreach (var (method, literal) in cases)
        {
            Assert.Equal(literal, ContractLiteralCodec.ToValue(method));
            Assert.True(ContractLiteralCodec.TryParse(literal, out SupervisorIpcMethod parsedMethod));
            Assert.Equal(method, parsedMethod);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("unknown")]
    [InlineData("SUPERVISOR.PING")]
    [InlineData(" supervisor.ping")]
    [InlineData("supervisor.ping ")]
    [Trait("Size", "Small")]
    public void TryParse_WhenLiteralIsNotCanonical_ReturnsFalse (string? literal)
    {
        var parsed = ContractLiteralCodec.TryParse<SupervisorIpcMethod>(literal, out _);

        Assert.False(parsed);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ToValue_WhenMethodIsUnspecified_ThrowsArgumentOutOfRangeException ()
    {
        var method = default(SupervisorIpcMethod);
        Assert.Equal(SupervisorIpcMethod.Unspecified, method);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ContractLiteralCodec.ToValue(method));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ToValue_WhenMethodIsUndefined_ThrowsArgumentOutOfRangeException ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ContractLiteralCodec.ToValue((SupervisorIpcMethod)999));
    }
}
