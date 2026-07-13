using System;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Project;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Project;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Validates batchmode bootstrap endpoint declarations against the current Unity project identity. </summary>
    internal static class UnityBatchmodeBootstrapEndpointValidator
    {
        /// <summary> Resolves the only endpoint owned by the current project and rejects a different bootstrap declaration. </summary>
        /// <param name="bootstrapArguments"> The parsed batchmode bootstrap arguments. </param>
        /// <returns> The internally derived endpoint after all declared identity and endpoint values match. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="bootstrapArguments" /> is <see langword="null" />. </exception>
        /// <exception cref="InvalidOperationException"> Thrown when the bootstrap identity or endpoint differs from the current project. </exception>
        public static IpcEndpoint ResolveValidatedEndpoint (IpcBatchmodeBootstrapArguments bootstrapArguments)
        {
            if (bootstrapArguments == null)
            {
                throw new ArgumentNullException(nameof(bootstrapArguments));
            }

            var projectRoot = UnityProjectPathResolver.ResolveProjectRootPath();
            var storageRoot = UcliStoragePathResolver.ResolveStorageRoot(projectRoot);
            var expectedProjectFingerprint = UnityProjectFingerprintCalculator.Create(storageRoot, projectRoot);

            ProjectFingerprint declaredProjectFingerprint;
            string declaredTransportKind;
            string declaredAddress;
            switch (bootstrapArguments)
            {
                case IpcDaemonBootstrapArguments daemonArguments:
                    if (!PathIdentity.IsSamePath(daemonArguments.RepositoryRoot, storageRoot))
                    {
                        throw new InvalidOperationException(
                            $"Daemon bootstrap storage root does not match the current Unity project. Expected={storageRoot}, Actual={daemonArguments.RepositoryRoot}");
                    }

                    declaredProjectFingerprint = daemonArguments.ProjectFingerprint;
                    declaredTransportKind = daemonArguments.EndpointTransportKind;
                    declaredAddress = daemonArguments.EndpointAddress;
                    break;

                case IpcOneshotBootstrapArguments oneshotArguments:
                    declaredProjectFingerprint = oneshotArguments.ProjectFingerprint;
                    declaredTransportKind = oneshotArguments.EndpointTransportKind;
                    declaredAddress = oneshotArguments.EndpointAddress;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(bootstrapArguments),
                        bootstrapArguments,
                        "Batchmode bootstrap argument type is unsupported.");
            }

            if (!expectedProjectFingerprint.Equals(declaredProjectFingerprint))
            {
                throw new InvalidOperationException(
                    $"Batchmode bootstrap project fingerprint does not match the current Unity project. Expected={expectedProjectFingerprint}, Actual={declaredProjectFingerprint}");
            }

            var expectedEndpoint = UcliIpcEndpointResolver.ResolveDaemonEndpoint(
                storageRoot,
                expectedProjectFingerprint);
            if (!ContractLiteralCodec.Matches(declaredTransportKind, expectedEndpoint.TransportKind)
                || !string.Equals(declaredAddress, expectedEndpoint.Address, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Batchmode bootstrap endpoint does not match the endpoint owned by the current Unity project. " +
                    $"ExpectedTransport={ContractLiteralCodec.ToValue(expectedEndpoint.TransportKind)}, " +
                    $"ExpectedAddress={expectedEndpoint.Address}, " +
                    $"ActualTransport={declaredTransportKind}, ActualAddress={declaredAddress}");
            }

            return expectedEndpoint;
        }
    }
}
