namespace MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve.Contracts;

/// <summary> Represents a selector that resolves a Unity asset GUID. </summary>
internal sealed record ResolveAssetGuidSelectorInput : ResolveSelectorInput
{
    /// <summary> Initializes a selector with a validated Unity asset GUID. </summary>
    /// <param name="assetGuid"> The non-empty Unity asset GUID. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="assetGuid" /> is <see cref="Guid.Empty" />. </exception>
    public ResolveAssetGuidSelectorInput (Guid assetGuid)
    {
        if (assetGuid == Guid.Empty)
        {
            throw new ArgumentException("Asset GUID must not be empty.", nameof(assetGuid));
        }

        AssetGuid = assetGuid;
    }

    /// <summary> Gets the validated Unity asset GUID. </summary>
    public Guid AssetGuid { get; }
}
