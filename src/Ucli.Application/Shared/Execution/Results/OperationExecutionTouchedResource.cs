
namespace MackySoft.Ucli.Application.Shared.Execution.Results;

/// <summary> Represents one resource touched by operation execution. </summary>
/// <param name="Kind"> The touched resource kind. </param>
/// <param name="Path"> The project-relative resource path. </param>
/// <param name="AssetGuid"> The optional non-empty asset GUID. </param>
internal sealed record OperationExecutionTouchedResource
{
    public OperationExecutionTouchedResource (
        UcliTouchedResourceKind Kind,
        string Path,
        Guid? AssetGuid)
    {
        if (!TextVocabulary.IsDefined(Kind))
        {
            throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Touched resource kind must be defined.");
        }
        if (!RelativePathContract.IsNormalized(Path))
        {
            throw new ArgumentException("Touched resource path must be a normalized project-relative path.", nameof(Path));
        }
        if (AssetGuid == Guid.Empty)
        {
            throw new ArgumentException("Touched resource asset GUID must not be empty.", nameof(AssetGuid));
        }

        this.Kind = Kind;
        this.Path = Path;
        this.AssetGuid = AssetGuid;
    }

    public UcliTouchedResourceKind Kind { get; }

    public string Path { get; }

    public Guid? AssetGuid { get; }
}
