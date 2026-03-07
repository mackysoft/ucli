using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcDaemonBootstrapArgumentsCodecTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WhenValueIsMissingAndNextTokenIsArgumentName_ReturnsMissingRequiredArguments ()
    {
        var args = new[]
        {
            "-batchmode",
            IpcDaemonBootstrapArgumentNames.RepositoryRoot,
            IpcDaemonBootstrapArgumentNames.ProjectFingerprint, "fingerprint",
            IpcDaemonBootstrapArgumentNames.SessionPath, "/tmp/session.json",
            IpcDaemonBootstrapArgumentNames.EndpointTransportKind, IpcTransportKindValues.NamedPipe,
            IpcDaemonBootstrapArgumentNames.EndpointAddress, "ucli-endpoint",
        };

        var parsed = IpcDaemonBootstrapArgumentsCodec.TryParse(args, out _, out var error);

        Assert.False(parsed);
        Assert.Equal(IpcDaemonBootstrapParseErrorKind.MissingRequiredArguments, error.Kind);
        Assert.Equal("uCLI daemon bootstrap arguments are missing.", error.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WhenValueStartsWithHyphenButIsNotArgumentName_ParsesSuccessfully ()
    {
        var args = new[]
        {
            IpcDaemonBootstrapArgumentNames.RepositoryRoot, "-tmp-repository",
            IpcDaemonBootstrapArgumentNames.ProjectFingerprint, "fingerprint",
            IpcDaemonBootstrapArgumentNames.SessionPath, "/tmp/session.json",
            IpcDaemonBootstrapArgumentNames.EndpointTransportKind, IpcTransportKindValues.NamedPipe,
            IpcDaemonBootstrapArgumentNames.EndpointAddress, "ucli-endpoint",
        };

        var parsed = IpcDaemonBootstrapArgumentsCodec.TryParse(args, out var bootstrapArguments, out var error);

        Assert.True(parsed);
        Assert.Equal(IpcDaemonBootstrapParseError.None, error);
        Assert.Equal("-tmp-repository", bootstrapArguments.RepositoryRoot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AppendTokens_ThenTryParse_RoundTripsValues ()
    {
        var source = new IpcDaemonBootstrapArguments(
            RepositoryRoot: "/repo/root",
            ProjectFingerprint: "project-fingerprint",
            SessionPath: "/repo/root/.ucli/local/fingerprints/project-fingerprint/session.json",
            EndpointTransportKind: IpcTransportKindValues.UnixDomainSocket,
            EndpointAddress: "/tmp/ucli.sock");
        List<string> tokens =
        [
            "-batchmode",
        ];

        IpcDaemonBootstrapArgumentsCodec.AppendTokens(tokens, source);
        var parsed = IpcDaemonBootstrapArgumentsCodec.TryParse(tokens, out var bootstrapArguments, out var error);

        Assert.True(parsed);
        Assert.Equal(IpcDaemonBootstrapParseError.None, error);
        Assert.Equal(source, bootstrapArguments);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WhenRequiredValueIsWhitespace_ReturnsEmptyRequiredValue ()
    {
        var args = new[]
        {
            IpcDaemonBootstrapArgumentNames.RepositoryRoot, " ",
            IpcDaemonBootstrapArgumentNames.ProjectFingerprint, "fingerprint",
            IpcDaemonBootstrapArgumentNames.SessionPath, "/tmp/session.json",
            IpcDaemonBootstrapArgumentNames.EndpointTransportKind, IpcTransportKindValues.NamedPipe,
            IpcDaemonBootstrapArgumentNames.EndpointAddress, "ucli-endpoint",
        };

        var parsed = IpcDaemonBootstrapArgumentsCodec.TryParse(args, out _, out var error);

        Assert.False(parsed);
        Assert.Equal(IpcDaemonBootstrapParseErrorKind.EmptyRequiredValue, error.Kind);
        Assert.Equal("uCLI daemon bootstrap arguments must not be empty.", error.Message);
    }
}