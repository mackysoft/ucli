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
            IpcBatchmodeBootstrapArgumentNames.ProjectFingerprint, "fingerprint",
            IpcDaemonBootstrapArgumentNames.SessionPath, "/tmp/session.json",
            IpcEndpointBootstrapArgumentNames.TransportKind, "namedPipe",
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
            IpcOneshotBootstrapArgumentNames.SessionToken, "oneshot-token",
            IpcOneshotBootstrapArgumentNames.ExitDeadlineUtc, "2026-03-09T00:00:00.0000000+00:00",
            IpcEndpointBootstrapArgumentNames.TransportKind, "namedPipe",
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
            IpcBatchmodeBootstrapArgumentNames.ProjectFingerprint, "fingerprint",
            IpcDaemonBootstrapArgumentNames.SessionPath, "/tmp/session.json",
            IpcDaemonBootstrapArgumentNames.SessionIssuedAtUtc, "2026-03-09T00:00:00.0000000+00:00",
            IpcEndpointBootstrapArgumentNames.TransportKind, "namedPipe",
            IpcEndpointBootstrapArgumentNames.Address, "ucli-endpoint",
        };

        var parsed = IpcBatchmodeBootstrapArgumentsCodec.TryParse(args, out var bootstrapArguments, out var error);

        Assert.True(parsed);
        Assert.Equal(IpcBatchmodeBootstrapParseError.None, error);
        var daemonArguments = Assert.IsType<IpcDaemonBootstrapArguments>(bootstrapArguments);
        Assert.Equal("-tmp-repository", daemonArguments.RepositoryRoot);
        Assert.Equal(DateTimeOffset.Parse("2026-03-09T00:00:00.0000000+00:00"), daemonArguments.SessionIssuedAtUtc);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WhenOneshotParentProcessIdExists_ParsesSuccessfully ()
    {
        var args = new[]
        {
            IpcBatchmodeBootstrapArgumentNames.Target, IpcBatchmodeBootstrapTargetValues.Oneshot,
            IpcOneshotBootstrapArgumentNames.ParentProcessId, "123",
            IpcBatchmodeBootstrapArgumentNames.ProjectFingerprint, "project-fingerprint",
            IpcOneshotBootstrapArgumentNames.SessionToken, "oneshot-token",
            IpcOneshotBootstrapArgumentNames.ExitDeadlineUtc, "2026-03-09T00:00:00.0000000+00:00",
            IpcEndpointBootstrapArgumentNames.TransportKind, "namedPipe",
            IpcEndpointBootstrapArgumentNames.Address, "ucli-endpoint",
        };

        var parsed = IpcBatchmodeBootstrapArgumentsCodec.TryParse(args, out var bootstrapArguments, out var error);

        Assert.True(parsed);
        Assert.Equal(IpcBatchmodeBootstrapParseError.None, error);
        var oneshotArguments = Assert.IsType<IpcOneshotBootstrapArguments>(bootstrapArguments);
        Assert.Equal(123, oneshotArguments.ParentProcessId);
        Assert.Equal("project-fingerprint", oneshotArguments.ProjectFingerprint);
        Assert.Equal("oneshot-token", oneshotArguments.SessionToken);
        Assert.Equal(DateTimeOffset.Parse("2026-03-09T00:00:00.0000000+00:00"), oneshotArguments.ExitDeadlineUtc);
        Assert.Equal("namedPipe", oneshotArguments.EndpointTransportKind);
        Assert.Equal("ucli-endpoint", oneshotArguments.EndpointAddress);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WhenOneshotExitDeadlineUtcIsInvalid_ReturnsEmptyRequiredValue ()
    {
        var args = new[]
        {
            IpcBatchmodeBootstrapArgumentNames.Target, IpcBatchmodeBootstrapTargetValues.Oneshot,
            IpcOneshotBootstrapArgumentNames.ParentProcessId, "123",
            IpcBatchmodeBootstrapArgumentNames.ProjectFingerprint, "project-fingerprint",
            IpcOneshotBootstrapArgumentNames.SessionToken, "oneshot-token",
            IpcOneshotBootstrapArgumentNames.ExitDeadlineUtc, "not-a-timestamp",
            IpcEndpointBootstrapArgumentNames.TransportKind, "namedPipe",
            IpcEndpointBootstrapArgumentNames.Address, "ucli-endpoint",
        };

        var parsed = IpcBatchmodeBootstrapArgumentsCodec.TryParse(args, out _, out var error);

        Assert.False(parsed);
        Assert.Equal(IpcBatchmodeBootstrapParseErrorKind.EmptyRequiredValue, error.Kind);
        Assert.Equal("uCLI oneshot bootstrap exit deadline timestamp must be a valid ISO 8601 timestamp with explicit timezone offset.", error.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AppendTokens_ThenTryParse_RoundTripsDaemonValues ()
    {
        IpcBatchmodeBootstrapArguments source = new IpcDaemonBootstrapArguments(
            RepositoryRoot: "/repo/root",
            ProjectFingerprint: "project-fingerprint",
            SessionPath: "/repo/root/.ucli/local/fingerprints/project-fingerprint/session.json",
            SessionIssuedAtUtc: new DateTimeOffset(2026, 03, 09, 0, 0, 0, TimeSpan.Zero),
            EndpointTransportKind: "unixDomainSocket",
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
    public void TryParse_WhenDaemonSessionIssuedAtUtcIsInvalid_ReturnsEmptyRequiredValue ()
    {
        var args = new[]
        {
            IpcBatchmodeBootstrapArgumentNames.Target, IpcBatchmodeBootstrapTargetValues.Daemon,
            IpcDaemonBootstrapArgumentNames.RepositoryRoot, "/repo/root",
            IpcBatchmodeBootstrapArgumentNames.ProjectFingerprint, "fingerprint",
            IpcDaemonBootstrapArgumentNames.SessionPath, "/tmp/session.json",
            IpcDaemonBootstrapArgumentNames.SessionIssuedAtUtc, "not-a-timestamp",
            IpcEndpointBootstrapArgumentNames.TransportKind, "namedPipe",
            IpcEndpointBootstrapArgumentNames.Address, "ucli-endpoint",
        };

        var parsed = IpcBatchmodeBootstrapArgumentsCodec.TryParse(args, out _, out var error);

        Assert.False(parsed);
        Assert.Equal(IpcBatchmodeBootstrapParseErrorKind.EmptyRequiredValue, error.Kind);
        Assert.Equal("uCLI daemon bootstrap session issued-at timestamp must be a valid ISO 8601 timestamp with explicit timezone offset.", error.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AppendTokens_ThenTryParse_RoundTripsOneshotValues ()
    {
        IpcBatchmodeBootstrapArguments source = new IpcOneshotBootstrapArguments(
            456,
            "project-fingerprint",
            "oneshot-token",
            new DateTimeOffset(2026, 03, 09, 0, 0, 0, TimeSpan.Zero),
            "unixDomainSocket",
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
            IpcBatchmodeBootstrapArgumentNames.ProjectFingerprint, "project-fingerprint",
            IpcOneshotBootstrapArgumentNames.SessionToken, "oneshot-token",
            IpcOneshotBootstrapArgumentNames.ExitDeadlineUtc, "2026-03-09T00:00:00.0000000+00:00",
            IpcEndpointBootstrapArgumentNames.TransportKind, "namedPipe",
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
            IpcBatchmodeBootstrapArgumentNames.ProjectFingerprint, "project-fingerprint",
            IpcOneshotBootstrapArgumentNames.SessionToken, "oneshot-token",
            IpcOneshotBootstrapArgumentNames.ExitDeadlineUtc, "2026-03-09T00:00:00.0000000+00:00",
            IpcEndpointBootstrapArgumentNames.TransportKind, "namedPipe",
            IpcEndpointBootstrapArgumentNames.Address, "ucli-endpoint",
        };

        var parsed = IpcBatchmodeBootstrapArgumentsCodec.TryParse(args, out _, out var error);

        Assert.False(parsed);
        Assert.Equal(IpcBatchmodeBootstrapParseErrorKind.EmptyRequiredValue, error.Kind);
        Assert.Equal("uCLI oneshot bootstrap parent process identifier must be a positive integer.", error.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WhenOneshotSessionTokenIsWhitespace_ReturnsEmptyRequiredValue ()
    {
        var args = new[]
        {
            IpcBatchmodeBootstrapArgumentNames.Target, IpcBatchmodeBootstrapTargetValues.Oneshot,
            IpcOneshotBootstrapArgumentNames.ParentProcessId, "123",
            IpcBatchmodeBootstrapArgumentNames.ProjectFingerprint, "project-fingerprint",
            IpcOneshotBootstrapArgumentNames.SessionToken, " ",
            IpcOneshotBootstrapArgumentNames.ExitDeadlineUtc, "2026-03-09T00:00:00.0000000+00:00",
            IpcEndpointBootstrapArgumentNames.TransportKind, "namedPipe",
            IpcEndpointBootstrapArgumentNames.Address, "ucli-endpoint",
        };

        var parsed = IpcBatchmodeBootstrapArgumentsCodec.TryParse(args, out _, out var error);

        Assert.False(parsed);
        Assert.Equal(IpcBatchmodeBootstrapParseErrorKind.EmptyRequiredValue, error.Kind);
        Assert.Equal("uCLI oneshot bootstrap arguments must not be empty.", error.Message);
    }
}
