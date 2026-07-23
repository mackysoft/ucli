namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents an immutable IPC transport endpoint at a wire boundary. </summary>
/// <remarks>
/// A Named Pipe address is a validated logical name. A Unix-domain-socket address retains transport-safe
/// text only; the runtime adapter must convert it to a guarded current-platform filesystem path before use.
/// </remarks>
public sealed record IpcEndpoint
{
    /// <summary> Initializes an IPC endpoint after validating its transport wire constraints. </summary>
    /// <param name="transportKind"> The supported transport used to connect or bind the endpoint. </param>
    /// <param name="address"> The transport-specific address. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="address" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="address" /> contains invalid transport text or violates the selected transport's non-filesystem constraints.
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

        ValidateAddressText(address);
        switch (transportKind)
        {
            case IpcTransportKind.NamedPipe:
                ValidateNamedPipeAddress(address);
                break;

            case IpcTransportKind.UnixDomainSocket:
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
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("Named pipe address must not be empty or whitespace.", nameof(address));
        }

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
}
