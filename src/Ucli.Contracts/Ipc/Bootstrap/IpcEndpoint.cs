namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents an immutable IPC transport endpoint whose address satisfies the selected transport contract. </summary>
public sealed record IpcEndpoint
{
    /// <summary> Initializes an IPC endpoint after validating its transport-specific address. </summary>
    /// <param name="transportKind"> The supported transport used to connect or bind the endpoint. </param>
    /// <param name="address"> The transport-specific address. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="address" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="address" /> is empty, contains invalid text, or violates the selected transport's address constraints.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="transportKind" /> is unsupported. </exception>
    public IpcEndpoint (
        IpcTransportKind transportKind,
        string address)
    {
        if (transportKind is not IpcTransportKind.NamedPipe and not IpcTransportKind.UnixDomainSocket)
        {
            throw new ArgumentOutOfRangeException(
                nameof(transportKind),
                transportKind,
                "IPC transport kind is unsupported.");
        }

        if (address == null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("IPC endpoint address must not be empty or whitespace.", nameof(address));
        }

        ValidateAddressText(address);
        switch (transportKind)
        {
            case IpcTransportKind.NamedPipe:
                ValidateNamedPipeAddress(address);
                break;

            case IpcTransportKind.UnixDomainSocket:
                ValidateUnixDomainSocketAddress(address);
                break;
        }

        TransportKind = transportKind;
        Address = address;
    }

    /// <summary> Gets the transport used to connect or bind this endpoint. </summary>
    public IpcTransportKind TransportKind { get; }

    /// <summary> Gets the validated transport-specific address. </summary>
    public string Address { get; }

    private static void ValidateAddressText (string address)
    {
        for (var index = 0; index < address.Length; index++)
        {
            var character = address[index];
            if (char.IsControl(character))
            {
                throw new ArgumentException(
                    "IPC endpoint address must not contain control characters.",
                    nameof(address));
            }

            if (char.IsHighSurrogate(character))
            {
                if (index + 1 >= address.Length || !char.IsLowSurrogate(address[index + 1]))
                {
                    throw new ArgumentException(
                        "IPC endpoint address must contain well-formed Unicode text.",
                        nameof(address));
                }

                index++;
                continue;
            }

            if (char.IsLowSurrogate(character))
            {
                throw new ArgumentException(
                    "IPC endpoint address must contain well-formed Unicode text.",
                    nameof(address));
            }
        }
    }

    private static void ValidateNamedPipeAddress (string address)
    {
        if (address.Length > IpcTransportConstraints.NamedPipeAddressMaxCharacters)
        {
            throw new ArgumentException(
                $"Named pipe address must not exceed {IpcTransportConstraints.NamedPipeAddressMaxCharacters} characters.",
                nameof(address));
        }

        if (string.Equals(address, "anonymous", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Named pipe address 'anonymous' is reserved.", nameof(address));
        }

        if (address.IndexOf('/') >= 0 || address.IndexOf('\\') >= 0)
        {
            throw new ArgumentException(
                "Named pipe address must be a pipe name without directory separators.",
                nameof(address));
        }
    }

    private static void ValidateUnixDomainSocketAddress (string address)
    {
        if (address[0] != '/')
        {
            throw new ArgumentException(
                "Unix domain socket address must be an absolute path beginning with '/'.",
                nameof(address));
        }

        if (address[address.Length - 1] == '/')
        {
            throw new ArgumentException(
                "Unix domain socket address must end with a socket file name.",
                nameof(address));
        }

        if (address.IndexOf("//", StringComparison.Ordinal) >= 0)
        {
            throw new ArgumentException(
                "Unix domain socket address must not contain consecutive path separators.",
                nameof(address));
        }

        ValidateUnixDomainSocketPathSegments(address);
        var addressByteCount = System.Text.Encoding.UTF8.GetByteCount(address);
        if (addressByteCount > IpcTransportConstraints.UnixDomainSocketPathMaxBytes)
        {
            throw new ArgumentException(
                "Unix domain socket address exceeds the supported UTF-8 byte length. " +
                $"AddressBytes={addressByteCount}, MaxBytes={IpcTransportConstraints.UnixDomainSocketPathMaxBytes}.",
                nameof(address));
        }
    }

    private static void ValidateUnixDomainSocketPathSegments (string address)
    {
        var segmentStart = 1;
        while (segmentStart < address.Length)
        {
            var segmentEnd = address.IndexOf('/', segmentStart);
            if (segmentEnd < 0)
            {
                segmentEnd = address.Length;
            }

            var segmentLength = segmentEnd - segmentStart;
            if ((segmentLength == 1 && address[segmentStart] == '.')
                || (segmentLength == 2
                    && address[segmentStart] == '.'
                    && address[segmentStart + 1] == '.'))
            {
                throw new ArgumentException(
                    "Unix domain socket address must not contain '.' or '..' path segments.",
                    nameof(address));
            }

            segmentStart = segmentEnd + 1;
        }
    }
}
