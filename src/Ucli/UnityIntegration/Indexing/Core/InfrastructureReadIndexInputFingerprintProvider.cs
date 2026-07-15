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
    public async ValueTask<ReadIndexCoreInputHashSnapshot?> TryComputeCoreAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        var snapshot = await inputFingerprintCalculator.TryComputeCoreAsync(unityProject.UnityProjectRoot, cancellationToken).ConfigureAwait(false);
        return snapshot == null
            ? null
            : new ReadIndexCoreInputHashSnapshot(
                snapshot.ScriptAssembliesHash,
                snapshot.PackagesManifestHash,
                snapshot.PackagesLockHash,
                snapshot.AssemblyDefinitionHash,
                snapshot.CombinedHash);
    }

    /// <inheritdoc />
    public async ValueTask<ReadIndexInputHashSnapshot?> TryComputeAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        var snapshot = await inputFingerprintCalculator.TryComputeAsync(unityProject.UnityProjectRoot, cancellationToken).ConfigureAwait(false);
        return snapshot == null
            ? null
            : new ReadIndexInputHashSnapshot(
                snapshot.ScriptAssembliesHash,
                snapshot.PackagesManifestHash,
                snapshot.PackagesLockHash,
                snapshot.AssemblyDefinitionHash,
                snapshot.AssetsContentHash,
                snapshot.AssetSearchHash,
                snapshot.GuidPathHash,
                snapshot.CombinedHash);
    }
}
