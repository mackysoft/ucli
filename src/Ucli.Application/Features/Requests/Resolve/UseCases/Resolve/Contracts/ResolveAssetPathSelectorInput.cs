using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve.Contracts;

/// <summary> Represents a selector that resolves an asset path. </summary>
internal sealed record ResolveAssetPathSelectorInput : ResolveSelectorInput
{
    /// <summary> Initializes a selector with a validated Unity asset path. </summary>
    /// <param name="assetPath"> The path below <c>Assets/</c>. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="assetPath" /> is <see langword="null" />. </exception>
    public ResolveAssetPathSelectorInput (UnityAssetPath assetPath)
    {
        AssetPath = assetPath ?? throw new ArgumentNullException(nameof(assetPath));
    }

    /// <summary> Gets the validated Unity asset path. </summary>
    public UnityAssetPath AssetPath { get; }
}
