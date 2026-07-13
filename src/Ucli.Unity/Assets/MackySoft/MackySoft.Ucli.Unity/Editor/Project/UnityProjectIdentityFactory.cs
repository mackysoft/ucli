using System;
using System.IO;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Project;
using MackySoft.Ucli.Infrastructure.Storage;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Project
{
    /// <summary> Creates the immutable project identity bound to the current Unity host session. </summary>
    internal static class UnityProjectIdentityFactory
    {
        /// <summary> Creates the current project identity after validating the expected fingerprint. </summary>
        public static IpcProjectIdentity Create (string expectedProjectFingerprint)
        {
            if (string.IsNullOrWhiteSpace(expectedProjectFingerprint))
            {
                throw new ArgumentException(
                    "Expected project fingerprint must not be empty.",
                    nameof(expectedProjectFingerprint));
            }

            var projectPath = Path.GetFullPath(UnityProjectPathResolver.ResolveProjectRootPath());
            var storageRoot = UcliStoragePathResolver.ResolveStorageRoot(projectPath);
            var actualProjectFingerprint = UnityProjectFingerprintCalculator.Create(storageRoot, projectPath);
            if (!string.Equals(
                    expectedProjectFingerprint,
                    actualProjectFingerprint,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Expected project fingerprint does not match the current Unity project. Expected={expectedProjectFingerprint}, Actual={actualProjectFingerprint}.",
                    nameof(expectedProjectFingerprint));
            }

            var unityVersion = string.IsNullOrWhiteSpace(Application.unityVersion)
                ? "unknown"
                : Application.unityVersion;
            return new IpcProjectIdentity(
                ProjectPath: projectPath,
                ProjectFingerprint: actualProjectFingerprint,
                UnityVersion: unityVersion);
        }
    }
}
