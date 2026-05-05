using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Persistence;
using MackySoft.Ucli.Infrastructure.Index;

namespace MackySoft.Ucli.Features.OperationCatalog.Catalog.Persistence;

/// <summary> Adapts infrastructure read-index input fingerprints to ops-catalog application ports. </summary>
internal sealed class InfrastructureOpsCatalogInputFingerprintCalculator : IOpsCatalogInputFingerprintCalculator
{
    private readonly IIndexInputFingerprintCalculator indexInputFingerprintCalculator;

    /// <summary> Initializes a new instance of the <see cref="InfrastructureOpsCatalogInputFingerprintCalculator" /> class. </summary>
    /// <param name="indexInputFingerprintCalculator"> The infrastructure fingerprint calculator dependency. </param>
    public InfrastructureOpsCatalogInputFingerprintCalculator (IIndexInputFingerprintCalculator indexInputFingerprintCalculator)
    {
        this.indexInputFingerprintCalculator = indexInputFingerprintCalculator ?? throw new ArgumentNullException(nameof(indexInputFingerprintCalculator));
    }

    /// <inheritdoc />
    public async ValueTask<OpsCatalogCoreInputHashSnapshot?> TryComputeCore (
        string projectRootPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = await indexInputFingerprintCalculator.TryComputeCore(
                projectRootPath,
                cancellationToken)
            .ConfigureAwait(false);
        return snapshot == null
            ? null
            : new OpsCatalogCoreInputHashSnapshot(
                ScriptAssembliesHash: snapshot.ScriptAssembliesHash,
                PackagesManifestHash: snapshot.PackagesManifestHash,
                PackagesLockHash: snapshot.PackagesLockHash,
                AssemblyDefinitionHash: snapshot.AssemblyDefinitionHash,
                CombinedHash: snapshot.CombinedHash);
    }

    /// <inheritdoc />
    public async ValueTask<OpsCatalogInputHashSnapshot?> TryCompute (
        string projectRootPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = await indexInputFingerprintCalculator.TryCompute(
                projectRootPath,
                cancellationToken)
            .ConfigureAwait(false);
        return snapshot == null
            ? null
            : new OpsCatalogInputHashSnapshot(
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
