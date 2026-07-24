using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Bootstrap;

public sealed class IpcEndpointTests
{
    public static TheoryData<IpcTransportKind, string> InvalidCommonAddresses => new()
    {
        { IpcTransportKind.NamedPipe, "pipe\0name" },
        { IpcTransportKind.NamedPipe, "pipe\nname" },
        { IpcTransportKind.UnixDomainSocket, "/tmp/pipe\0name" },
        { IpcTransportKind.UnixDomainSocket, "/tmp/pipe\nname" },
    };

    public static TheoryData<string> InvalidNamedPipeAddresses => new()
    {
        string.Empty,
        " ",
        "anonymous",
        "ANONYMOUS",
        "directory/pipe",
        "directory\\pipe",
        new string('a', IpcTransportConstraints.NamedPipeAddressMaxCharacters + 1),
    };

    public static TheoryData<string> UnixDomainSocketWireAddresses => new()
    {
        string.Empty,
        " ",
        "tmp/ucli.sock",
        "/tmp/ucli.sock/",
        "//tmp/ucli.sock",
        "/tmp//ucli.sock",
        "/tmp/./ucli.sock",
        "/tmp/../ucli.sock",
        CreateUnixDomainSocketAddress(IpcTransportConstraints.UnixDomainSocketPathMaxBytes + 1),
    };

    [Theory]
    [InlineData(IpcTransportKind.NamedPipe)]
    [InlineData(IpcTransportKind.UnixDomainSocket)]
    [Trait("Size", "Small")]
    public void Constructor_WithNullAddress_ThrowsArgumentNullException (IpcTransportKind transportKind)
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new IpcEndpoint(transportKind, null!));

        Assert.Equal("address", exception.ParamName);
    }

    [Theory]
    [MemberData(nameof(InvalidCommonAddresses))]
    [Trait("Size", "Small")]
    public void Constructor_WithInvalidCommonAddress_ThrowsArgumentException (
        IpcTransportKind transportKind,
        string address)
    {
        var exception = Assert.Throws<ArgumentException>(() => new IpcEndpoint(transportKind, address));

        Assert.Equal("address", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithUnpairedSurrogate_ThrowsArgumentException ()
    {
        var invalidCharacters = new[]
        {
            new string('\uD800', 1),
            new string('\uDC00', 1),
        };
        foreach (var invalidCharacter in invalidCharacters)
        {
            Assert.Throws<ArgumentException>(() => new IpcEndpoint(
                IpcTransportKind.NamedPipe,
                "pipe" + invalidCharacter + "name"));
            Assert.Throws<ArgumentException>(() => new IpcEndpoint(
                IpcTransportKind.UnixDomainSocket,
                "/tmp/pipe" + invalidCharacter + "name"));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithUnsupportedTransportKind_ThrowsArgumentOutOfRangeException ()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new IpcEndpoint(
            (IpcTransportKind)int.MaxValue,
            "ucli-endpoint"));

        Assert.Equal("transportKind", exception.ParamName);
    }

    [Theory]
    [MemberData(nameof(InvalidNamedPipeAddresses))]
    [Trait("Size", "Small")]
    public void Constructor_WithInvalidNamedPipeAddress_ThrowsArgumentException (string address)
    {
        var exception = Assert.Throws<ArgumentException>(() => new IpcEndpoint(
            IpcTransportKind.NamedPipe,
            address));

        Assert.Equal("address", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithSupportedNamedPipeAddress_PreservesAddress ()
    {
        const string Address = "Ucli Pipe.Name-1_2";

        var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, Address);

        Assert.Equal(IpcTransportKind.NamedPipe, endpoint.TransportKind);
        Assert.Equal(Address, endpoint.Address);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithMaximumNamedPipeAddressLength_PreservesAddress ()
    {
        var address = new string('a', IpcTransportConstraints.NamedPipeAddressMaxCharacters);

        var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, address);

        Assert.Equal(address, endpoint.Address);
    }

    [Theory]
    [MemberData(nameof(UnixDomainSocketWireAddresses))]
    [Trait("Size", "Small")]
    public void Constructor_WithUnixDomainSocketWireText_PreservesAddressForRuntimeAdapter (
        string address)
    {
        var endpoint = new IpcEndpoint(IpcTransportKind.UnixDomainSocket, address);

        Assert.Equal(IpcTransportKind.UnixDomainSocket, endpoint.TransportKind);
        Assert.Equal(address, endpoint.Address);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Equality_UsesTransportKindAndAddress ()
    {
        var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-endpoint");
        var sameValue = new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-endpoint");
        var differentTransport = new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/ucli-endpoint");
        var differentAddress = new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-other-endpoint");

        Assert.Equal(endpoint, sameValue);
        Assert.Equal(endpoint.GetHashCode(), sameValue.GetHashCode());
        Assert.NotEqual(endpoint, differentTransport);
        Assert.NotEqual(endpoint, differentAddress);
    }

    private static string CreateUnixDomainSocketAddress (int utf8ByteLength)
    {
        return "/" + new string('a', utf8ByteLength - 1);
    }
}
