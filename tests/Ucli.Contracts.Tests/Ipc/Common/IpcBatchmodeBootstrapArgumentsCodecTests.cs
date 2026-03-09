using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcBatchmodeBootstrapArgumentsCodecTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WhenTargetIsMissing_ReturnsMissingTarget ()
    {
        var parsed = IpcBatchmodeBootstrapArgumentsCodec.TryParse(
            ["-batchmode"],
            out _,
            out var error);

        Assert.False(parsed);
        Assert.Equal(IpcBatchmodeBootstrapParseErrorKind.MissingTarget, error.Kind);
        Assert.Equal("uCLI batchmode bootstrap target is missing.", error.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WhenTargetIsInvalid_ReturnsInvalidTarget ()
    {
        var parsed = IpcBatchmodeBootstrapArgumentsCodec.TryParse(
            [
                IpcBatchmodeBootstrapArgumentNames.Target, "unsupported",
            ],
            out _,
            out var error);

        Assert.False(parsed);
        Assert.Equal(IpcBatchmodeBootstrapParseErrorKind.InvalidTarget, error.Kind);
        Assert.Equal("uCLI batchmode bootstrap target is invalid. Actual: unsupported", error.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WhenDaemonValueIsMissingAndNextTokenIsArgumentName_ReturnsMissingRequiredArguments ()
    {
        var args = new[]
        {
            IpcBatchmodeBootstrapArgumentNames.Target, IpcBatchmodeBootstrapTargetValues.Daemon,
            IpcDaemonBootstrapArgumentNames.RepositoryRoot,
            IpcDaemonBootstrapArgumentNames.ProjectFingerprint, "fingerprint",
            IpcDaemonBootstrapArgumentNames.SessionPath, "/tmp/session.json",
            IpcEndpointBootstrapArgumentNames.TransportKind, IpcTransportKindValues.NamedPipe,
            IpcEndpointBootstrapArgumentNames.Address, "ucli-endpoint",
        };

        var parsed = IpcBatchmodeBootstrapArgumentsCodec.TryParse(args, out _, out var error);

        Assert.False(parsed);
        Assert.Equal(IpcBatchmodeBootstrapParseErrorKind.MissingRequiredArguments, error.Kind);
        Assert.Equal("uCLI daemon bootstrap arguments are missing.", error.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WhenOneshotValueIsMissingAndNextTokenIsArgumentName_ReturnsMissingRequiredArguments ()
    {
        var args = new[]
        {
            IpcBatchmodeBootstrapArgumentNames.Target, IpcBatchmodeBootstrapTargetValues.Oneshot,
            IpcOneshotBootstrapArgumentNames.ParentProcessId,
            IpcEndpointBootstrapArgumentNames.TransportKind, IpcTransportKindValues.NamedPipe,
            IpcEndpointBootstrapArgumentNames.Address, "ucli-endpoint",
            IpcBatchmodeBootstrapArgumentNames.Target,
        };

        var parsed = IpcBatchmodeBootstrapArgumentsCodec.TryParse(args, out _, out var error);

        Assert.False(parsed);
        Assert.Equal(IpcBatchmodeBootstrapParseErrorKind.MissingRequiredArguments, error.Kind);
        Assert.Equal("uCLI oneshot bootstrap arguments are missing.", error.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WhenDaemonValueStartsWithHyphenButIsNotArgumentName_ParsesSuccessfully ()
    {
        var args = new[]
        {
            IpcBatchmodeBootstrapArgumentNames.Target, IpcBatchmodeBootstrapTargetValues.Daemon,
            IpcDaemonBootstrapArgumentNames.RepositoryRoot, "-tmp-repository",
            IpcDaemonBootstrapArgumentNames.ProjectFingerprint, "fingerprint",
            IpcDaemonBootstrapArgumentNames.SessionPath, "/tmp/session.json",
            IpcEndpointBootstrapArgumentNames.TransportKind, IpcTransportKindValues.NamedPipe,
            IpcEndpointBootstrapArgumentNames.Address, "ucli-endpoint",
        };

        var parsed = IpcBatchmodeBootstrapArgumentsCodec.TryParse(args, out var bootstrapArguments, out var error);

        Assert.True(parsed);
        Assert.Equal(IpcBatchmodeBootstrapParseError.None, error);
        var daemonArguments = Assert.IsType<IpcDaemonBootstrapArguments>(bootstrapArguments);
        Assert.Equal("-tmp-repository", daemonArguments.RepositoryRoot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WhenOneshotParentProcessIdExists_ParsesSuccessfully ()
    {
        var args = new[]
        {
            IpcBatchmodeBootstrapArgumentNames.Target, IpcBatchmodeBootstrapTargetValues.Oneshot,
            IpcOneshotBootstrapArgumentNames.ParentProcessId, "123",
            IpcEndpointBootstrapArgumentNames.TransportKind, IpcTransportKindValues.NamedPipe,
            IpcEndpointBootstrapArgumentNames.Address, "ucli-endpoint",
        };

        var parsed = IpcBatchmodeBootstrapArgumentsCodec.TryParse(args, out var bootstrapArguments, out var error);

        Assert.True(parsed);
        Assert.Equal(IpcBatchmodeBootstrapParseError.None, error);
        var oneshotArguments = Assert.IsType<IpcOneshotBootstrapArguments>(bootstrapArguments);
        Assert.Equal(123, oneshotArguments.ParentProcessId);
        Assert.Equal(IpcTransportKindValues.NamedPipe, oneshotArguments.EndpointTransportKind);
        Assert.Equal("ucli-endpoint", oneshotArguments.EndpointAddress);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AppendTokens_ThenTryParse_RoundTripsDaemonValues ()
    {
        IpcBatchmodeBootstrapArguments source = new IpcDaemonBootstrapArguments(
            RepositoryRoot: "/repo/root",
            ProjectFingerprint: "project-fingerprint",
            SessionPath: "/repo/root/.ucli/local/fingerprints/project-fingerprint/session.json",
            EndpointTransportKind: IpcTransportKindValues.UnixDomainSocket,
            EndpointAddress: "/tmp/ucli.sock");
        List<string> tokens =
        [
            "-batchmode",
        ];

        IpcBatchmodeBootstrapArgumentsCodec.AppendTokens(tokens, source);
        var parsed = IpcBatchmodeBootstrapArgumentsCodec.TryParse(tokens, out var bootstrapArguments, out var error);

        Assert.True(parsed);
        Assert.Equal(IpcBatchmodeBootstrapParseError.None, error);
        Assert.Equal(source, bootstrapArguments);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AppendTokens_ThenTryParse_RoundTripsOneshotValues ()
    {
        IpcBatchmodeBootstrapArguments source = new IpcOneshotBootstrapArguments(
            456,
            IpcTransportKindValues.UnixDomainSocket,
            "/tmp/ucli.sock");
        List<string> tokens =
        [
            "-batchmode",
        ];

        IpcBatchmodeBootstrapArgumentsCodec.AppendTokens(tokens, source);
        var parsed = IpcBatchmodeBootstrapArgumentsCodec.TryParse(tokens, out var bootstrapArguments, out var error);

        Assert.True(parsed);
        Assert.Equal(IpcBatchmodeBootstrapParseError.None, error);
        Assert.Equal(source, bootstrapArguments);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WhenOneshotRequiredValueIsWhitespace_ReturnsEmptyRequiredValue ()
    {
        var args = new[]
        {
            IpcBatchmodeBootstrapArgumentNames.Target, IpcBatchmodeBootstrapTargetValues.Oneshot,
            IpcOneshotBootstrapArgumentNames.ParentProcessId, " ",
            IpcEndpointBootstrapArgumentNames.TransportKind, IpcTransportKindValues.NamedPipe,
            IpcEndpointBootstrapArgumentNames.Address, "ucli-endpoint",
        };

        var parsed = IpcBatchmodeBootstrapArgumentsCodec.TryParse(args, out _, out var error);

        Assert.False(parsed);
        Assert.Equal(IpcBatchmodeBootstrapParseErrorKind.EmptyRequiredValue, error.Kind);
        Assert.Equal("uCLI oneshot bootstrap arguments must not be empty.", error.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WhenOneshotParentProcessIdIsInvalid_ReturnsEmptyRequiredValue ()
    {
        var args = new[]
        {
            IpcBatchmodeBootstrapArgumentNames.Target, IpcBatchmodeBootstrapTargetValues.Oneshot,
            IpcOneshotBootstrapArgumentNames.ParentProcessId, "0",
            IpcEndpointBootstrapArgumentNames.TransportKind, IpcTransportKindValues.NamedPipe,
            IpcEndpointBootstrapArgumentNames.Address, "ucli-endpoint",
        };

        var parsed = IpcBatchmodeBootstrapArgumentsCodec.TryParse(args, out _, out var error);

        Assert.False(parsed);
        Assert.Equal(IpcBatchmodeBootstrapParseErrorKind.EmptyRequiredValue, error.Kind);
        Assert.Equal("uCLI oneshot bootstrap parent process identifier must be a positive integer.", error.Message);
    }
}
