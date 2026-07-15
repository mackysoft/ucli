using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Bootstrap;

public sealed class IpcGuiBootstrapArgumentsCodecTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void AppendTokens_AppendsGuiBootstrapArgumentPairs ()
    {
        var tokens = new List<string>
        {
            "-projectPath",
            "/repo/UnityProject",
        };

        IpcGuiBootstrapArgumentsCodec.AppendTokens(tokens, new IpcGuiBootstrapArguments(
            OwnerProcessId: 123,
            CanShutdownProcess: true));

        Assert.Equal(
            [
                "-projectPath",
                "/repo/UnityProject",
                IpcGuiBootstrapArgumentNames.Target,
                "daemon",
                IpcGuiBootstrapArgumentNames.OwnerProcessId,
                "123",
                IpcGuiBootstrapArgumentNames.CanShutdownProcess,
                "true",
            ],
            tokens);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WhenGuiBootstrapArgumentsExist_ReturnsPayload ()
    {
        var args = new[]
        {
            "Unity",
            IpcGuiBootstrapArgumentNames.Target,
            "daemon",
            IpcGuiBootstrapArgumentNames.OwnerProcessId,
            "123",
            IpcGuiBootstrapArgumentNames.CanShutdownProcess,
            "true",
        };

        var parsed = IpcGuiBootstrapArgumentsCodec.TryParse(args, out var bootstrapArguments, out var error);

        Assert.True(parsed);
        Assert.Equal(IpcGuiBootstrapParseError.None, error);
        Assert.Equal(123, bootstrapArguments.OwnerProcessId);
        Assert.True(bootstrapArguments.CanShutdownProcess);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("True", true)]
    [InlineData("False", false)]
    public void TryParse_WhenCanShutdownProcessIsBoolean_ParsesValue (
        string rawValue,
        bool expectedValue)
    {
        var args = new[]
        {
            "Unity",
            IpcGuiBootstrapArgumentNames.Target,
            "daemon",
            IpcGuiBootstrapArgumentNames.OwnerProcessId,
            "456",
            IpcGuiBootstrapArgumentNames.CanShutdownProcess,
            rawValue,
        };

        var parsed = IpcGuiBootstrapArgumentsCodec.TryParse(args, out var bootstrapArguments, out var error);

        Assert.True(parsed);
        Assert.Equal(IpcGuiBootstrapParseError.None, error);
        Assert.Equal(expectedValue, bootstrapArguments.CanShutdownProcess);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WhenTargetIsMissing_ReturnsMissingTarget ()
    {
        var parsed = IpcGuiBootstrapArgumentsCodec.TryParse(
            ["Unity"],
            out _,
            out var error);

        Assert.False(parsed);
        Assert.Equal(IpcGuiBootstrapParseErrorKind.MissingTarget, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WhenTargetIsInvalid_ReturnsInvalidTarget ()
    {
        var args = new[]
        {
            "Unity",
            IpcGuiBootstrapArgumentNames.Target,
            "unsupported",
            IpcGuiBootstrapArgumentNames.OwnerProcessId,
            "123",
            IpcGuiBootstrapArgumentNames.CanShutdownProcess,
            "true",
        };

        var parsed = IpcGuiBootstrapArgumentsCodec.TryParse(args, out _, out var error);

        Assert.False(parsed);
        Assert.Equal(IpcGuiBootstrapParseErrorKind.InvalidTarget, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WhenTargetMarkerHasNoValue_ReturnsInvalidTarget ()
    {
        var args = new[]
        {
            "Unity",
            IpcGuiBootstrapArgumentNames.Target,
            IpcGuiBootstrapArgumentNames.OwnerProcessId,
            "123",
            IpcGuiBootstrapArgumentNames.CanShutdownProcess,
            "true",
        };

        var parsed = IpcGuiBootstrapArgumentsCodec.TryParse(args, out _, out var error);

        Assert.False(parsed);
        Assert.Equal(IpcGuiBootstrapParseErrorKind.InvalidTarget, error.Kind);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("not-a-number")]
    public void TryParse_WhenOwnerProcessIdIsInvalid_ReturnsInvalidRequiredValue (string rawValue)
    {
        var args = new[]
        {
            "Unity",
            IpcGuiBootstrapArgumentNames.Target,
            "daemon",
            IpcGuiBootstrapArgumentNames.OwnerProcessId,
            rawValue,
            IpcGuiBootstrapArgumentNames.CanShutdownProcess,
            "true",
        };

        var parsed = IpcGuiBootstrapArgumentsCodec.TryParse(args, out _, out var error);

        Assert.False(parsed);
        Assert.Equal(IpcGuiBootstrapParseErrorKind.InvalidRequiredValue, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WhenCanShutdownProcessIsInvalid_ReturnsInvalidRequiredValue ()
    {
        var args = new[]
        {
            "Unity",
            IpcGuiBootstrapArgumentNames.Target,
            "daemon",
            IpcGuiBootstrapArgumentNames.OwnerProcessId,
            "123",
            IpcGuiBootstrapArgumentNames.CanShutdownProcess,
            "yes",
        };

        var parsed = IpcGuiBootstrapArgumentsCodec.TryParse(args, out _, out var error);

        Assert.False(parsed);
        Assert.Equal(IpcGuiBootstrapParseErrorKind.InvalidRequiredValue, error.Kind);
    }

}
