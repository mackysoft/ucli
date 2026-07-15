namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Contains artifact read results resolved from one immutable read-index generation. </summary>
internal sealed class ReadIndexGenerationArtifacts
{
    /// <summary> Initializes one complete generation read result. </summary>
    public ReadIndexGenerationArtifacts (
        ReadIndexArtifactReadResult<OpsCatalogDescriptorSnapshot> opsCatalog,
        ReadIndexArtifactReadResult<AssetSearchLookupSnapshot> assetSearchLookup,
        ReadIndexArtifactReadResult<GuidPathLookupSnapshot> guidPathLookup,
        ReadIndexArtifactReadResult<ReadIndexInputsManifestSnapshot> inputsManifest)
    {
        OpsCatalog = opsCatalog ?? throw new ArgumentNullException(nameof(opsCatalog));
        AssetSearchLookup = assetSearchLookup ?? throw new ArgumentNullException(nameof(assetSearchLookup));
        GuidPathLookup = guidPathLookup ?? throw new ArgumentNullException(nameof(guidPathLookup));
        InputsManifest = inputsManifest ?? throw new ArgumentNullException(nameof(inputsManifest));
    }

    /// <summary> Gets the operation catalog read result. </summary>
    public ReadIndexArtifactReadResult<OpsCatalogDescriptorSnapshot> OpsCatalog { get; }

    /// <summary> Gets the asset-search lookup read result. </summary>
    public ReadIndexArtifactReadResult<AssetSearchLookupSnapshot> AssetSearchLookup { get; }

    /// <summary> Gets the GUID-path lookup read result. </summary>
    public ReadIndexArtifactReadResult<GuidPathLookupSnapshot> GuidPathLookup { get; }

    /// <summary> Gets the inputs manifest read result. </summary>
    public ReadIndexArtifactReadResult<ReadIndexInputsManifestSnapshot> InputsManifest { get; }
}
