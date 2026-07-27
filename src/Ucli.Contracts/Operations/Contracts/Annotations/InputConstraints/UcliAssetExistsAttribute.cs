namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Requires an existing Unity asset of a specified kind. </summary>
public sealed class UcliAssetExistsAttribute : UcliOperationInputConstraintAnnotationAttribute
{
    /// <summary> Initializes a new instance of the <see cref="UcliAssetExistsAttribute" /> class. </summary>
    /// <param name="assetKind"> The required Unity asset kind. </param>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="assetKind" /> is undefined. </exception>
    public UcliAssetExistsAttribute (UcliOperationAssetKind assetKind)
    {
        if (!TextVocabulary.IsDefined(assetKind))
        {
            throw new ArgumentOutOfRangeException(nameof(assetKind), assetKind, "Asset kind must be defined by the operation contract.");
        }

        AssetKind = assetKind;
    }

    /// <summary> Gets the required Unity asset kind. </summary>
    public UcliOperationAssetKind AssetKind { get; }
}
