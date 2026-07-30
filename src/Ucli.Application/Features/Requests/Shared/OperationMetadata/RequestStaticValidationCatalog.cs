namespace MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

/// <summary> Represents operation metadata availability for static request validation. </summary>
internal sealed record RequestStaticValidationCatalog
{
    private RequestStaticValidationCatalog (
        bool isAvailable,
        IReadOnlyList<UcliOperationDescriptor> operations,
        IReadOnlyDictionary<string, UcliOperationDescriptor> operationsByName,
        IReadOnlyList<UcliOperationAuthorizationDescriptor> authorizationOperations)
    {
        IsAvailable = isAvailable;
        Operations = operations;
        OperationsByName = operationsByName;
        AuthorizationOperations = authorizationOperations;
    }

    /// <summary> Gets the metadata-unavailable catalog used for syntax-only validation. </summary>
    public static RequestStaticValidationCatalog Unavailable { get; }
        = new(
            false,
            Array.Empty<UcliOperationDescriptor>(),
            new Dictionary<string, UcliOperationDescriptor>(0, StringComparer.Ordinal),
            Array.Empty<UcliOperationAuthorizationDescriptor>());

    /// <summary> Gets a value indicating whether metadata-backed validation is available. </summary>
    public bool IsAvailable { get; }

    /// <summary> Gets the registered operation contracts in their catalog order. </summary>
    public IReadOnlyList<UcliOperationDescriptor> Operations { get; }

    /// <summary> Gets the same registered operation contracts keyed by operation name. </summary>
    public IReadOnlyDictionary<string, UcliOperationDescriptor> OperationsByName { get; }

    /// <summary> Gets the operation facts available for authorization. </summary>
    public IReadOnlyList<UcliOperationAuthorizationDescriptor> AuthorizationOperations { get; }

    /// <summary> Creates a metadata-backed validation catalog. </summary>
    /// <param name="operations"> The available operation descriptors. </param>
    /// <returns> The metadata-backed validation catalog. </returns>
    public static RequestStaticValidationCatalog Available (IReadOnlyList<UcliOperationDescriptor> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        var operationSnapshot = operations.ToArray();
        var operationsByName = new Dictionary<string, UcliOperationDescriptor>(
            operationSnapshot.Length,
            StringComparer.Ordinal);
        for (var i = 0; i < operationSnapshot.Length; i++)
        {
            var operation = operationSnapshot[i]
                ?? throw new ArgumentException("Operation catalog must not contain null descriptors.", nameof(operations));
            if (!operationsByName.TryAdd(operation.Name, operation))
            {
                throw new ArgumentException(
                    $"Operation catalog contains duplicate operation name '{operation.Name}'.",
                    nameof(operations));
            }
        }

        var authorizationOperations = operationSnapshot
            .Select(UcliOperationAuthorizationDescriptor.From)
            .ToArray();
        return new RequestStaticValidationCatalog(
            true,
            operationSnapshot,
            operationsByName,
            EditLoweringOnlyOperationAuthorizations.AppendMissingTo(authorizationOperations));
    }
}
