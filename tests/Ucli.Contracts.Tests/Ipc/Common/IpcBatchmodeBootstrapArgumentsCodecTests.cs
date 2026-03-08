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
            IpcDaemonBootstrapArgumentNames.EndpointTransportKind, IpcTransportKindValues.NamedPipe,
            IpcDaemonBootstrapArgumentNames.EndpointAddress, "ucli-endpoint",
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
            IpcOneshotBootstrapArgumentNames.RequestPath, "/tmp/request.json",
            IpcOneshotBootstrapArgumentNames.ResponsePath,
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
            IpcDaemonBootstrapArgumentNames.EndpointTransportKind, IpcTransportKindValues.NamedPipe,
            IpcDaemonBootstrapArgumentNames.EndpointAddress, "ucli-endpoint",
        };

        var parsed = IpcBatchmodeBootstrapArgumentsCodec.TryParse(args, out var bootstrapArguments, out var error);

        Assert.True(parsed);
        Assert.Equal(IpcBatchmodeBootstrapParseError.None, error);
        var daemonArguments = Assert.IsType<IpcDaemonBootstrapArguments>(bootstrapArguments);
        Assert.Equal("-tmp-repository", daemonArguments.RepositoryRoot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WhenOneshotValueStartsWithHyphenButIsNotArgumentName_ParsesSuccessfully ()
    {
        var args = new[]
        {
            IpcBatchmodeBootstrapArgumentNames.Target, IpcBatchmodeBootstrapTargetValues.Oneshot,
            IpcOneshotBootstrapArgumentNames.RequestPath, "-tmp-request.json",
            IpcOneshotBootstrapArgumentNames.ResponsePath, "-tmp-response.json",
        };

        var parsed = IpcBatchmodeBootstrapArgumentsCodec.TryParse(args, out var bootstrapArguments, out var error);

        Assert.True(parsed);
        Assert.Equal(IpcBatchmodeBootstrapParseError.None, error);
        var oneshotArguments = Assert.IsType<IpcOneshotBootstrapArguments>(bootstrapArguments);
        Assert.Equal("-tmp-request.json", oneshotArguments.RequestPath);
        Assert.Equal("-tmp-response.json", oneshotArguments.ResponsePath);
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
            "/tmp/ucli-request.json",
            "/tmp/ucli-response.json");
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
            IpcOneshotBootstrapArgumentNames.RequestPath, " ",
            IpcOneshotBootstrapArgumentNames.ResponsePath, "/tmp/response.json",
        };

        var parsed = IpcBatchmodeBootstrapArgumentsCodec.TryParse(args, out _, out var error);

        Assert.False(parsed);
        Assert.Equal(IpcBatchmodeBootstrapParseErrorKind.EmptyRequiredValue, error.Kind);
        Assert.Equal("uCLI oneshot bootstrap arguments must not be empty.", error.Message);
    }
}