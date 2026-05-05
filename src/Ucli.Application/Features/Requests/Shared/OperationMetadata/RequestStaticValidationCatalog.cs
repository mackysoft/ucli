namespace MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

/// <summary> Represents operation metadata availability for static request validation. </summary>
/// <param name="IsAvailable"> Whether metadata-backed validation is available. </param>
/// <param name="Operations"> The available operation descriptors. </param>
internal sealed record RequestStaticValidationCatalog (
    bool IsAvailable,
    IReadOnlyList<UcliOperationDescriptor> Operations)
{
    /// <summary> Gets the metadata-unavailable catalog used for syntax-only validation. </summary>
    public static RequestStaticValidationCatalog Unavailable { get; }
        = new(false, Array.Empty<UcliOperationDescriptor>());

    /// <summary> Creates a metadata-backed validation catalog. </summary>
    /// <param name="operations"> The available operation descriptors. </param>
    /// <returns> The metadata-backed validation catalog. </returns>
    public static RequestStaticValidationCatalog Available (IReadOnlyList<UcliOperationDescriptor> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        return new RequestStaticValidationCatalog(true, operations);
    }
}
