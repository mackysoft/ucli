using System.Text;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Infrastructure.Cryptography;

namespace MackySoft.Ucli.Infrastructure.Project;

/// <summary> Calculates deterministic Unity-project fingerprints from storage and project paths. </summary>
public static class UnityProjectFingerprintCalculator
{
    /// <summary> Creates one deterministic SHA-256 fingerprint from guarded storage and project roots. </summary>
    /// <param name="storageRoot"> The normalized absolute storage root path. </param>
    /// <param name="unityProjectRoot"> The normalized absolute Unity project root path. </param>
    /// <returns> The canonical project fingerprint. </returns>
    public static ProjectFingerprint Create (
        AbsolutePath storageRoot,
        AbsolutePath unityProjectRoot)
    {
        if (storageRoot is null)
        {
            throw new ArgumentNullException(nameof(storageRoot));
        }

        if (unityProjectRoot is null)
        {
            throw new ArgumentNullException(nameof(unityProjectRoot));
        }

        var projectPathFragment = BuildProjectPathFragment(
            storageRoot,
            unityProjectRoot);
        var storageRootIdentity = DeterministicPathText.ForIdentity(storageRoot);
        var fingerprintInput = $"{storageRootIdentity}\n{projectPathFragment}";
        var normalizedBytes = Encoding.UTF8.GetBytes(fingerprintInput);

        return new ProjectFingerprint(Sha256LowerHex.Compute(normalizedBytes));
    }

    /// <summary> Builds a stable project-path fragment used for fingerprint input. </summary>
    /// <param name="storageRoot"> The normalized storage-root path. </param>
    /// <param name="unityProjectRoot"> The normalized Unity project root path. </param>
    /// <returns> The relative fragment when possible; otherwise the absolute Unity project root path. </returns>
    private static string BuildProjectPathFragment (
        AbsolutePath storageRoot,
        AbsolutePath unityProjectRoot)
    {
        if (storageRoot.IsSameOrAncestorOf(unityProjectRoot))
        {
            var containedProjectRoot = ContainedPath.Create(storageRoot, unityProjectRoot);
            return DeterministicPathText.ForIdentity(containedProjectRoot.RelativePath);
        }

        // NOTE:
        // Unity project path is expected to be equal to or under storage root.
        // Keep deterministic behavior even for unexpected directory layouts.
        return DeterministicPathText.ForIdentity(unityProjectRoot);
    }
}
