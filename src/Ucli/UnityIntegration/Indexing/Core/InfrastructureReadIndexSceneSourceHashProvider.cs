using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Infrastructure.Index;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Core;

/// <summary> Adapts infrastructure scene source hashing to application read-index policy. </summary>
internal sealed class InfrastructureReadIndexSceneSourceHashProvider : IReadIndexSceneSourceHashProvider
{
    private readonly ISceneTreeLiteSourceHashCalculator sourceHashCalculator;

    /// <summary> Initializes a new instance of the <see cref="InfrastructureReadIndexSceneSourceHashProvider" /> class. </summary>
    public InfrastructureReadIndexSceneSourceHashProvider (ISceneTreeLiteSourceHashCalculator sourceHashCalculator)
    {
        this.sourceHashCalculator = sourceHashCalculator ?? throw new ArgumentNullException(nameof(sourceHashCalculator));
    }

    /// <inheritdoc />
    public ValueTask<Sha256Digest?> TryComputeAsync (
        SceneTreeLiteSourcePaths sourcePaths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);
        return sourceHashCalculator.TryComputeAsync(
            sourcePaths.SceneFilePath,
            sourcePaths.MetaFilePath,
            cancellationToken);
    }
}
