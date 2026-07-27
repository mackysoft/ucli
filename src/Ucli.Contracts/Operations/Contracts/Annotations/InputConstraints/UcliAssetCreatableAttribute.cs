namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Requires a Unity asset path at which an asset of a specified kind can be created. </summary>
public sealed class UcliAssetCreatableAttribute : UcliOperationInputConstraintAnnotationAttribute
{
    /// <summary> Initializes a new instance of the <see cref="UcliAssetCreatableAttribute" /> class. </summary>
    /// <param name="assetKind"> The Unity asset kind to create. </param>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="assetKind" /> is undefined. </exception>
    public UcliAssetCreatableAttribute (UcliOperationAssetKind assetKind)
    {
        if (!TextVocabulary.IsDefined(assetKind))
        {
            throw new ArgumentOutOfRangeException(nameof(assetKind), assetKind, "Asset kind must be defined by the operation contract.");
        }

        AssetKind = assetKind;
    }

    /// <summary> Gets the Unity asset kind to create. </summary>
    public UcliOperationAssetKind AssetKind { get; }
}
