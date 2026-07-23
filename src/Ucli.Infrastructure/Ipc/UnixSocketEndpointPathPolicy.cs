using System.Text;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Infrastructure.Ipc;

/// <summary> Adapts Unix-domain-socket wire text to the guarded path required by the local runtime. </summary>
internal static class UnixSocketEndpointPathPolicy
{
    /// <summary>
    /// Parses one wire address exactly once, then applies Unix socket constraints to the guarded value.
    /// </summary>
    /// <exception cref="PathValidationException">
    /// Thrown when <paramref name="address" /> is not an absolute path on the running operating system.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the guarded path identifies a filesystem root or exceeds the supported address length.
    /// </exception>
    public static AbsolutePath Parse (string address)
    {
        var path = AbsolutePath.Parse(address);
        EnsureSupported(path);
        return path;
    }

    /// <summary> Applies Unix socket constraints without revalidating a guarded absolute path. </summary>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="path" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the guarded path identifies a filesystem root or exceeds the supported address length.
    /// </exception>
    public static void EnsureSupported (AbsolutePath path)
    {
        if (path is null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        if (!path.TryGetParent(out _))
        {
            throw new ArgumentException(
                "Unix domain socket path must identify a node below a filesystem root.",
                nameof(path));
        }

        var addressByteCount = Encoding.UTF8.GetByteCount(path.Value);
        if (addressByteCount > IpcTransportConstraints.UnixDomainSocketPathMaxBytes)
        {
            throw new ArgumentException(
                "Unix domain socket path exceeds the supported UTF-8 byte length. " +
                $"AddressBytes={addressByteCount}, MaxBytes={IpcTransportConstraints.UnixDomainSocketPathMaxBytes}.",
                nameof(path));
        }
    }
}
