using System;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Infrastructure.Project;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Project;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Validates batchmode bootstrap endpoint declarations against the current Unity project identity. </summary>
    internal static class UnityBatchmodeBootstrapEndpointValidator
    {
        /// <summary> Resolves the current project's daemon endpoint and validates one daemon bootstrap declaration. </summary>
        internal static UnityIpcEndpointBinding ResolveValidatedDaemonEndpoint (UnityDaemonBootstrapContext bootstrapContext)
        {
            if (bootstrapContext == null)
            {
                throw new ArgumentNullException(nameof(bootstrapContext));
            }

            ResolveCurrentProject(out var storageRoot, out var projectFingerprint, out var endpoint);
            if (!bootstrapContext.RepositoryRoot.IsSameAs(storageRoot))
            {
                throw new InvalidOperationException(
                    $"Daemon bootstrap storage root does not match the current Unity project. Expected={storageRoot.Value}, Actual={bootstrapContext.RepositoryRoot.Value}");
            }

            ValidateDeclaredIdentity(
                projectFingerprint,
                endpoint,
                bootstrapContext.ProjectFingerprint,
                bootstrapContext.EndpointBinding.Endpoint);
            return bootstrapContext.EndpointBinding;
        }

        /// <summary> Resolves the current project's oneshot endpoint and validates one persisted bootstrap generation. </summary>
        internal static UnityIpcEndpointBinding ResolveValidatedOneshotEndpoint (IpcOneshotBootstrapEnvelope bootstrapEnvelope)
        {
            if (bootstrapEnvelope == null)
            {
                throw new ArgumentNullException(nameof(bootstrapEnvelope));
            }

            ResolveCurrentProject(out _, out var projectFingerprint, out var endpoint);
            ValidateDeclaredIdentity(
                projectFingerprint,
                endpoint,
                bootstrapEnvelope.ProjectFingerprint,
                bootstrapEnvelope.Endpoint);
            return UnityIpcEndpointBinding.Create(bootstrapEnvelope.Endpoint);
        }

        private static void ResolveCurrentProject (
            out AbsolutePath storageRoot,
            out ProjectFingerprint projectFingerprint,
            out IpcEndpoint endpoint)
        {
            var projectRoot = UnityProjectPathResolver.ResolveProjectRootPath();
            storageRoot = UcliStoragePathResolver.ResolveStorageRoot(projectRoot);
            projectFingerprint = UnityProjectFingerprintCalculator.Create(storageRoot, projectRoot);
            endpoint = UcliIpcEndpointResolver.ResolveDaemonEndpoint(storageRoot, projectFingerprint).Contract;
        }

        private static void ValidateDeclaredIdentity (
            ProjectFingerprint expectedProjectFingerprint,
            IpcEndpoint expectedEndpoint,
            ProjectFingerprint declaredProjectFingerprint,
            IpcEndpoint declaredEndpoint)
        {
            if (!expectedProjectFingerprint.Equals(declaredProjectFingerprint))
            {
                throw new InvalidOperationException(
                    $"Batchmode bootstrap project fingerprint does not match the current Unity project. Expected={expectedProjectFingerprint}, Actual={declaredProjectFingerprint}");
            }

            if (declaredEndpoint != expectedEndpoint)
            {
                throw new InvalidOperationException(
                    "Batchmode bootstrap endpoint does not match the endpoint owned by the current Unity project. " +
                    $"ExpectedTransport={ContractLiteralCodec.ToValue(expectedEndpoint.TransportKind)}, " +
                    $"ExpectedAddress={expectedEndpoint.Address}, " +
                    $"ActualTransport={ContractLiteralCodec.ToValue(declaredEndpoint.TransportKind)}, ActualAddress={declaredEndpoint.Address}");
            }
        }
    }
}
