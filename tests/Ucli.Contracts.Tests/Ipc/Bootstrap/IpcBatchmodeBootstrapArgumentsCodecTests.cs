using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcBatchmodeBootstrapArgumentsCodecTests
{
    private const string ProjectFingerprintText = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static readonly Guid SessionGenerationId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenDaemonSessionGenerationIdIsEmpty_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new IpcDaemonBootstrapArguments(
            RepositoryRoot: "/repo/root",
            ProjectFingerprint: new ProjectFingerprint(ProjectFingerprintText),
            SessionPath: "/repo/root/.ucli/session.json",
            SessionGenerationId: Guid.Empty,
            SessionIssuedAtUtc: new DateTimeOffset(2026, 03, 09, 0, 0, 0, TimeSpan.Zero),
            Endpoint: new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-endpoint")));

        Assert.Equal("SessionGenerationId", exception.ParamName);
    }

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
            IpcBatchmodeBootstrapArgumentNames.Target, "daemon",
            IpcDaemonBootstrapArgumentNames.RepositoryRoot,
            IpcBatchmodeBootstrapArgumentNames.ProjectFingerprint, ProjectFingerprintText,
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
            IpcBatchmodeBootstrapArgumentNames.Target, "oneshot",
            IpcOneshotBootstrapArgumentNames.BootstrapId,
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
            IpcBatchmodeBootstrapArgumentNames.Target, "daemon",
            IpcDaemonBootstrapArgumentNames.RepositoryRoot, "-tmp-repository",
            IpcBatchmodeBootstrapArgumentNames.ProjectFingerprint, ProjectFingerprintText,
            IpcDaemonBootstrapArgumentNames.SessionPath, "/tmp/session.json",
            IpcDaemonBootstrapArgumentNames.SessionGenerationId, SessionGenerationId.ToString("D"),
            IpcDaemonBootstrapArgumentNames.SessionIssuedAtUtc, "2026-03-09T00:00:00.0000000+00:00",
            IpcEndpointBootstrapArgumentNames.TransportKind, "namedPipe",
            IpcEndpointBootstrapArgumentNames.Address, "ucli-endpoint",
        };

        var parsed = IpcBatchmodeBootstrapArgumentsCodec.TryParse(args, out var bootstrapArguments, out var error);

        Assert.True(parsed);
        Assert.Equal(IpcBatchmodeBootstrapParseError.None, error);
        var daemonArguments = Assert.IsType<IpcDaemonBootstrapArguments>(bootstrapArguments);
        Assert.Equal("-tmp-repository", daemonArguments.RepositoryRoot);
        Assert.Equal(SessionGenerationId, daemonArguments.SessionGenerationId);
        Assert.Equal(DateTimeOffset.Parse("2026-03-09T00:00:00.0000000+00:00"), daemonArguments.SessionIssuedAtUtc);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WhenOneshotBootstrapIdExists_ParsesSuccessfully ()
    {
        var bootstrapId = Guid.NewGuid();
        var args = new[]
        {
            IpcBatchmodeBootstrapArgumentNames.Target, "oneshot",
            IpcOneshotBootstrapArgumentNames.BootstrapId, bootstrapId.ToString("D"),
        };

        var parsed = IpcBatchmodeBootstrapArgumentsCodec.TryParse(args, out var bootstrapArguments, out var error);

        Assert.True(parsed);
        Assert.Equal(IpcBatchmodeBootstrapParseError.None, error);
        var oneshotArguments = Assert.IsType<IpcOneshotBootstrapArguments>(bootstrapArguments);
        Assert.Equal(bootstrapId, oneshotArguments.BootstrapId);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("0123456789abcdef0123456789abcdef")]
    [Trait("Size", "Small")]
    public void TryParse_WhenOneshotBootstrapIdIsInvalid_ReturnsInvalidBootstrapId (string bootstrapId)
    {
        var args = new[]
        {
            IpcBatchmodeBootstrapArgumentNames.Target, "oneshot",
            IpcOneshotBootstrapArgumentNames.BootstrapId, bootstrapId,
        };

        var parsed = IpcBatchmodeBootstrapArgumentsCodec.TryParse(args, out _, out var error);

        Assert.False(parsed);
        Assert.Equal(IpcBatchmodeBootstrapParseErrorKind.InvalidBootstrapId, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AppendTokens_ThenTryParse_RoundTripsDaemonValues ()
    {
        IpcBatchmodeBootstrapArguments source = new IpcDaemonBootstrapArguments(
            RepositoryRoot: "/repo/root",
            ProjectFingerprint: new ProjectFingerprint(ProjectFingerprintText),
            SessionPath: $"/repo/root/.ucli/local/fingerprints/{ProjectFingerprintText}/session.json",
            SessionGenerationId: SessionGenerationId,
            SessionIssuedAtUtc: new DateTimeOffset(2026, 03, 09, 0, 0, 0, TimeSpan.Zero),
            Endpoint: new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli.sock"));
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

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("0123456789abcdef0123456789abcdef")]
    [Trait("Size", "Small")]
    public void TryParse_WhenDaemonSessionGenerationIdIsInvalid_ReturnsInvalidSessionGenerationId (
        string sessionGenerationId)
    {
        var args = new[]
        {
            IpcBatchmodeBootstrapArgumentNames.Target, "daemon",
            IpcDaemonBootstrapArgumentNames.RepositoryRoot, "/repo/root",
            IpcBatchmodeBootstrapArgumentNames.ProjectFingerprint, ProjectFingerprintText,
            IpcDaemonBootstrapArgumentNames.SessionPath, "/tmp/session.json",
            IpcDaemonBootstrapArgumentNames.SessionGenerationId, sessionGenerationId,
            IpcDaemonBootstrapArgumentNames.SessionIssuedAtUtc, "2026-03-09T00:00:00.0000000+00:00",
            IpcEndpointBootstrapArgumentNames.TransportKind, "namedPipe",
            IpcEndpointBootstrapArgumentNames.Address, "ucli-endpoint",
        };

        var parsed = IpcBatchmodeBootstrapArgumentsCodec.TryParse(args, out _, out var error);

        Assert.False(parsed);
        Assert.Equal(IpcBatchmodeBootstrapParseErrorKind.InvalidSessionGenerationId, error.Kind);
        Assert.Equal("uCLI daemon bootstrap session generation identifier must be a non-empty GUID in D format.", error.Message);
    }

    [Theory]
    [InlineData("not-a-timestamp")]
    [InlineData("2026-03-09T09:00:00.0000000+09:00")]
    [Trait("Size", "Small")]
    public void TryParse_WhenDaemonSessionIssuedAtUtcIsInvalid_ReturnsEmptyRequiredValue (string sessionIssuedAtUtc)
    {
        var args = new[]
        {
            IpcBatchmodeBootstrapArgumentNames.Target, "daemon",
            IpcDaemonBootstrapArgumentNames.RepositoryRoot, "/repo/root",
            IpcBatchmodeBootstrapArgumentNames.ProjectFingerprint, ProjectFingerprintText,
            IpcDaemonBootstrapArgumentNames.SessionPath, "/tmp/session.json",
            IpcDaemonBootstrapArgumentNames.SessionGenerationId, SessionGenerationId.ToString("D"),
            IpcDaemonBootstrapArgumentNames.SessionIssuedAtUtc, sessionIssuedAtUtc,
            IpcEndpointBootstrapArgumentNames.TransportKind, "namedPipe",
            IpcEndpointBootstrapArgumentNames.Address, "ucli-endpoint",
        };

        var parsed = IpcBatchmodeBootstrapArgumentsCodec.TryParse(args, out _, out var error);

        Assert.False(parsed);
        Assert.Equal(IpcBatchmodeBootstrapParseErrorKind.EmptyRequiredValue, error.Kind);
        Assert.Equal("uCLI daemon bootstrap session issued-at timestamp must be a valid ISO 8601 UTC timestamp.", error.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WhenDaemonProjectFingerprintIsInvalid_ReturnsInvalidProjectFingerprint ()
    {
        var args = new[]
        {
            IpcBatchmodeBootstrapArgumentNames.Target, "daemon",
            IpcDaemonBootstrapArgumentNames.RepositoryRoot, "/repo/root",
            IpcBatchmodeBootstrapArgumentNames.ProjectFingerprint, "not-a-project-fingerprint",
            IpcDaemonBootstrapArgumentNames.SessionPath, "/tmp/session.json",
            IpcDaemonBootstrapArgumentNames.SessionGenerationId, SessionGenerationId.ToString("D"),
            IpcDaemonBootstrapArgumentNames.SessionIssuedAtUtc, "2026-03-09T00:00:00.0000000+00:00",
            IpcEndpointBootstrapArgumentNames.TransportKind, "namedPipe",
            IpcEndpointBootstrapArgumentNames.Address, "ucli-endpoint",
        };

        var parsed = IpcBatchmodeBootstrapArgumentsCodec.TryParse(args, out _, out var error);

        Assert.False(parsed);
        Assert.Equal(IpcBatchmodeBootstrapParseErrorKind.InvalidProjectFingerprint, error.Kind);
        Assert.Equal("uCLI batchmode bootstrap project fingerprint must be exactly 64 lowercase hexadecimal SHA-256 characters.", error.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WhenEndpointTransportKindIsUnsupported_ReturnsInvalidEndpointTransportKind ()
    {
        var args = new[]
        {
            IpcBatchmodeBootstrapArgumentNames.Target, "daemon",
            IpcDaemonBootstrapArgumentNames.RepositoryRoot, "/repo/root",
            IpcBatchmodeBootstrapArgumentNames.ProjectFingerprint, ProjectFingerprintText,
            IpcDaemonBootstrapArgumentNames.SessionPath, "/tmp/session.json",
            IpcDaemonBootstrapArgumentNames.SessionGenerationId, SessionGenerationId.ToString("D"),
            IpcDaemonBootstrapArgumentNames.SessionIssuedAtUtc, "2026-03-09T00:00:00.0000000+00:00",
            IpcEndpointBootstrapArgumentNames.TransportKind, "unsupported",
            IpcEndpointBootstrapArgumentNames.Address, "ucli-endpoint",
        };

        var parsed = IpcBatchmodeBootstrapArgumentsCodec.TryParse(args, out _, out var error);

        Assert.False(parsed);
        Assert.Equal(IpcBatchmodeBootstrapParseErrorKind.InvalidEndpointTransportKind, error.Kind);
    }

    [Theory]
    [InlineData("namedPipe", "directory/pipe")]
    [InlineData("unixDomainSocket", "relative.sock")]
    [InlineData("unixDomainSocket", "/tmp/../ucli.sock")]
    [Trait("Size", "Small")]
    public void TryParse_WhenEndpointAddressViolatesTransportContract_ReturnsInvalidEndpointAddress (
        string transportKind,
        string endpointAddress)
    {
        var args = new[]
        {
            IpcBatchmodeBootstrapArgumentNames.Target, "daemon",
            IpcDaemonBootstrapArgumentNames.RepositoryRoot, "/repo/root",
            IpcBatchmodeBootstrapArgumentNames.ProjectFingerprint, ProjectFingerprintText,
            IpcDaemonBootstrapArgumentNames.SessionPath, "/tmp/session.json",
            IpcDaemonBootstrapArgumentNames.SessionGenerationId, SessionGenerationId.ToString("D"),
            IpcDaemonBootstrapArgumentNames.SessionIssuedAtUtc, "2026-03-09T00:00:00.0000000+00:00",
            IpcEndpointBootstrapArgumentNames.TransportKind, transportKind,
            IpcEndpointBootstrapArgumentNames.Address, endpointAddress,
        };

        var parsed = IpcBatchmodeBootstrapArgumentsCodec.TryParse(args, out _, out var error);

        Assert.False(parsed);
        Assert.Equal(IpcBatchmodeBootstrapParseErrorKind.InvalidEndpointAddress, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AppendTokens_ThenTryParse_RoundTripsOneshotValues ()
    {
        IpcBatchmodeBootstrapArguments source = new IpcOneshotBootstrapArguments(Guid.NewGuid());
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
            IpcBatchmodeBootstrapArgumentNames.Target, "oneshot",
            IpcOneshotBootstrapArgumentNames.BootstrapId, " ",
        };

        var parsed = IpcBatchmodeBootstrapArgumentsCodec.TryParse(args, out _, out var error);

        Assert.False(parsed);
        Assert.Equal(IpcBatchmodeBootstrapParseErrorKind.EmptyRequiredValue, error.Kind);
        Assert.Equal("uCLI oneshot bootstrap arguments must not be empty.", error.Message);
    }
}
