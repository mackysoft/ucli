namespace MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

/// <summary> Represents operation metadata availability for static request validation. </summary>
/// <param name="IsAvailable"> Whether metadata-backed validation is available. </param>
/// <param name="Operations"> The registered operation contracts available for argument validation. </param>
/// <param name="AuthorizationOperations"> The operation facts available for authorization. </param>
internal sealed record RequestStaticValidationCatalog (
    bool IsAvailable,
    IReadOnlyList<UcliOperationDescriptor> Operations,
    IReadOnlyList<UcliOperationAuthorizationDescriptor> AuthorizationOperations)
{
    /// <summary> Gets the metadata-unavailable catalog used for syntax-only validation. </summary>
    public static RequestStaticValidationCatalog Unavailable { get; }
        = new(
            false,
            Array.Empty<UcliOperationDescriptor>(),
            Array.Empty<UcliOperationAuthorizationDescriptor>());

    /// <summary> Creates a metadata-backed validation catalog. </summary>
    /// <param name="operations"> The available operation descriptors. </param>
    /// <returns> The metadata-backed validation catalog. </returns>
    public static RequestStaticValidationCatalog Available (IReadOnlyList<UcliOperationDescriptor> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        var authorizationOperations = operations
            .Select(UcliOperationAuthorizationDescriptor.From)
            .ToArray();
        return new RequestStaticValidationCatalog(
            true,
            operations,
            EditLoweringOnlyOperationAuthorizations.AppendMissingTo(authorizationOperations));
    }
}
