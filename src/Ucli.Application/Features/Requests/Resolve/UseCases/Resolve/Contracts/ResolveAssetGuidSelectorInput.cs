using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve.Contracts;

/// <summary> Represents a selector that resolves a Unity asset GUID. </summary>
internal sealed record ResolveAssetGuidSelectorInput : ResolveSelectorInput
{
    /// <summary> Initializes a selector with a validated Unity asset GUID. </summary>
    /// <param name="assetGuid"> The non-empty Unity asset GUID. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="assetGuid" /> is <see langword="null" />. </exception>
    public ResolveAssetGuidSelectorInput (UnityAssetGuid assetGuid)
    {
        AssetGuid = assetGuid ?? throw new ArgumentNullException(nameof(assetGuid));
    }

    /// <summary> Gets the validated Unity asset GUID. </summary>
    public UnityAssetGuid AssetGuid { get; }
}
