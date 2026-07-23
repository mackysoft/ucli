using System;
using System.Diagnostics.CodeAnalysis;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Binds one IPC endpoint contract to the guarded runtime address required by its transport.
    /// </summary>
    /// <remarks>
    /// Named-pipe addresses remain opaque logical names. Unix-domain-socket addresses are parsed once
    /// as current-platform absolute paths and remain guarded until the native socket boundary.
    /// </remarks>
    internal sealed class UnityIpcEndpointBinding
    {
        private readonly IpcTransportEndpoint runtimeEndpoint;

        private UnityIpcEndpointBinding (IpcTransportEndpoint runtimeEndpoint)
        {
            this.runtimeEndpoint = runtimeEndpoint;
        }

        /// <summary> Gets the transport contract retained for wire serialization and dispatch. </summary>
        public IpcEndpoint Endpoint => runtimeEndpoint.Contract;

        /// <summary> Adapts an already guarded runtime endpoint without reparsing its Unix socket address. </summary>
        /// <param name="endpoint"> The guarded runtime endpoint to retain. </param>
        /// <returns> A Unity binding over the same guarded endpoint. </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="endpoint" /> is <see langword="null" />.
        /// </exception>
        public static UnityIpcEndpointBinding Create (IpcTransportEndpoint endpoint)
        {
            if (endpoint == null)
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            return new UnityIpcEndpointBinding(endpoint);
        }

        /// <summary> Adapts an IPC endpoint contract to its Unity runtime transport address. </summary>
        /// <param name="endpoint"> The endpoint contract to adapt. </param>
        /// <returns>
        /// A binding that retains a guarded absolute path only for a Unix-domain-socket endpoint.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="endpoint" /> is <see langword="null" />.
        /// </exception>
        /// <exception cref="PathValidationException">
        /// Thrown when a Unix-domain-socket address is not an absolute path on the running operating system.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when a Unix-domain-socket address identifies a filesystem root or exceeds the supported
        /// UTF-8 address length.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the endpoint transport kind is unsupported.
        /// </exception>
        public static UnityIpcEndpointBinding Create (IpcEndpoint endpoint)
        {
            if (endpoint == null)
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            return Create(IpcTransportEndpoint.FromContract(endpoint));
        }

        /// <summary> Attempts to get the guarded Unix-domain-socket path carried by this binding. </summary>
        /// <param name="path">
        /// The guarded socket path when the endpoint uses the Unix-domain-socket transport;
        /// otherwise <see langword="null" />.
        /// </param>
        /// <returns>
        /// <see langword="true" /> for a Unix-domain-socket endpoint; otherwise <see langword="false" />.
        /// </returns>
        public bool TryGetUnixDomainSocketPath (
            [NotNullWhen(true)] out AbsolutePath? path)
        {
            path = runtimeEndpoint.UnixSocketPath;
            return path != null;
        }
    }
}
