using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;
using MackySoft.Ucli.Infrastructure.Index;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Core;

/// <summary> Adapts infrastructure input fingerprint calculation to application read-index policy. </summary>
internal sealed class InfrastructureReadIndexInputFingerprintProvider : IReadIndexInputFingerprintProvider
{
    private readonly IIndexInputFingerprintCalculator inputFingerprintCalculator;

    /// <summary> Initializes a new instance of the <see cref="InfrastructureReadIndexInputFingerprintProvider" /> class. </summary>
    public InfrastructureReadIndexInputFingerprintProvider (IIndexInputFingerprintCalculator inputFingerprintCalculator)
    {
        this.inputFingerprintCalculator = inputFingerprintCalculator ?? throw new ArgumentNullException(nameof(inputFingerprintCalculator));
    }

    /// <inheritdoc />
    public async ValueTask<ReadIndexCoreInputHashSnapshot?> TryComputeCore (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        var snapshot = await inputFingerprintCalculator.TryComputeCore(unityProject.UnityProjectRoot, cancellationToken).ConfigureAwait(false);
        return snapshot == null
            ? null
            : new ReadIndexCoreInputHashSnapshot(
                ScriptAssembliesHash: snapshot.ScriptAssembliesHash,
                PackagesManifestHash: snapshot.PackagesManifestHash,
                PackagesLockHash: snapshot.PackagesLockHash,
                AssemblyDefinitionHash: snapshot.AssemblyDefinitionHash,
                CombinedHash: snapshot.CombinedHash);
    }

    /// <inheritdoc />
    public async ValueTask<ReadIndexInputHashSnapshot?> TryCompute (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        var snapshot = await inputFingerprintCalculator.TryCompute(unityProject.UnityProjectRoot, cancellationToken).ConfigureAwait(false);
        return snapshot == null
            ? null
            : new ReadIndexInputHashSnapshot(
                ScriptAssembliesHash: snapshot.ScriptAssembliesHash,
                PackagesManifestHash: snapshot.PackagesManifestHash,
                PackagesLockHash: snapshot.PackagesLockHash,
                AssemblyDefinitionHash: snapshot.AssemblyDefinitionHash,
                AssetsContentHash: snapshot.AssetsContentHash,
                AssetSearchHash: snapshot.AssetSearchHash,
                GuidPathHash: snapshot.GuidPathHash,
                CombinedHash: snapshot.CombinedHash);
    }
}
