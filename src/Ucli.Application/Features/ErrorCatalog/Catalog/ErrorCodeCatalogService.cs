using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.ErrorCatalog.Catalog;

internal sealed class ErrorCodeCatalogService : IErrorCodeCatalogService
{
    private static readonly IReadOnlyList<UcliCommand> EmptyCommands = Array.Empty<UcliCommand>();

    private static readonly IReadOnlyList<string> EmptyStrings = Array.Empty<string>();

    private static readonly IReadOnlyList<UcliErrorCode> EmptyCodes = Array.Empty<UcliErrorCode>();

    private static readonly IReadOnlyList<UcliErrorNextActionDescriptor> UnknownNextActions =
    [
        new UcliErrorNextActionDescriptor(
            When: null,
            Action: "Treat as generic failure. Inspect the full result and update uCLI if this code came from a newer server."),
    ];

    private readonly IErrorCodeCatalog catalog;

    public ErrorCodeCatalogService (IErrorCodeCatalog catalog)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public ErrorCodeCatalogDescribeResult Describe (
        UcliErrorCode code,
        bool requireKnown)
    {
        if (!code.IsValid)
        {
            return ErrorCodeCatalogDescribeResult.Failure(
                ExecutionError.InvalidArgument(
                    "Error code must not be empty.",
                    UcliCoreErrorCodes.InvalidArgument));
        }

        if (catalog.TryFind(code, out var descriptor))
        {
            return ErrorCodeCatalogDescribeResult.Success(descriptor, known: true);
        }

        if (requireKnown)
        {
            return ErrorCodeCatalogDescribeResult.Failure(
                ExecutionError.InvalidArgument(
                    $"Error code '{code.Value}' is not known to this uCLI client.",
                    UcliCoreErrorCodes.InvalidArgument));
        }

        return ErrorCodeCatalogDescribeResult.Success(CreateUnknownDescriptor(code), known: false);
    }

    private static UcliErrorCodeDescriptor CreateUnknownDescriptor (UcliErrorCode code)
    {
        return new UcliErrorCodeDescriptor(
            Code: code,
            Category: "unknown",
            Summary: "This code is not known to this uCLI client.",
            Meaning: "The code is valid as an open error-code value, but the current catalog cannot provide code-specific semantics.",
            AppliesTo: EmptyCommands,
            PossiblePhases: EmptyStrings,
            ExecutionSemantics: new UcliErrorExecutionSemantics(
                ImpliesNotApplied: null,
                MayBeIndeterminate: true,
                SafeToRetry: UcliErrorRetryClassValues.Unknown),
            Inspect: ["status", "payload", "errors", "logs daemon", "logs unity"],
            NextActions: UnknownNextActions,
            RelatedCodes: EmptyCodes);
    }
}
