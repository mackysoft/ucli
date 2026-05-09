using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.ErrorCatalog.Catalog;

/// <summary> Implements error-code catalog listing, description lookup, and unknown-code fallback semantics. </summary>
internal sealed class ErrorCodeCatalogService : IErrorCodeCatalogService
{
    private static readonly HashSet<UcliCommand> KnownCommandSet = new(UcliPublicCommandCatalog.KnownCommands);

    private static readonly IReadOnlyList<UcliErrorCodeDescriptor> EmptyDescriptors = Array.Empty<UcliErrorCodeDescriptor>();

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

    /// <summary> Initializes a new instance of the <see cref="ErrorCodeCatalogService" /> class. </summary>
    /// <param name="catalog"> The validated catalog used for known-code lookup. Must not be <see langword="null" />. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="catalog" /> is <see langword="null" />. </exception>
    public ErrorCodeCatalogService (IErrorCodeCatalog catalog)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    /// <inheritdoc />
    public ErrorCodeCatalogListResult List (ErrorCodeCatalogListInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var categoryFilter = input.Category;
        if (categoryFilter is not null && string.IsNullOrWhiteSpace(categoryFilter))
        {
            return ErrorCodeCatalogListResult.Failure(
                ExecutionError.InvalidArgument(
                    "category must not be empty.",
                    UcliCoreErrorCodes.InvalidArgument));
        }

        UcliCommand? commandFilter = null;
        if (input.Command is not null)
        {
            if (!UcliCommand.TryCreate(input.Command, out var command))
            {
                return ErrorCodeCatalogListResult.Failure(
                    ExecutionError.InvalidArgument(
                        "command must be a valid command identifier.",
                        UcliCoreErrorCodes.InvalidArgument));
            }

            commandFilter = command;
        }

        if (commandFilter.HasValue && !KnownCommandSet.Contains(commandFilter.Value))
        {
            return ErrorCodeCatalogListResult.Success(EmptyDescriptors);
        }

        var descriptors = new List<UcliErrorCodeDescriptor>();
        foreach (var descriptor in catalog.Descriptors)
        {
            if (categoryFilter is not null
                && !string.Equals(descriptor.Category, categoryFilter, StringComparison.Ordinal))
            {
                continue;
            }

            if (commandFilter.HasValue && !MatchesCommandFilter(descriptor.AppliesTo, commandFilter.Value))
            {
                continue;
            }

            descriptors.Add(descriptor);
        }

        return ErrorCodeCatalogListResult.Success(descriptors);
    }

    /// <inheritdoc />
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
            Inspect: ["status", "errors[].code", "errors[].opId", "errors[].message"],
            NextActions: UnknownNextActions,
            RelatedCodes: EmptyCodes);
    }

    private static bool MatchesCommandFilter (
        IReadOnlyList<UcliCommand> appliesTo,
        UcliCommand commandFilter)
    {
        for (var i = 0; i < appliesTo.Count; i++)
        {
            if (IsSameOrRelatedCommand(appliesTo[i].Name, commandFilter.Name))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSameOrRelatedCommand (
        string appliesTo,
        string commandFilter)
    {
        return string.Equals(appliesTo, commandFilter, StringComparison.Ordinal)
            || IsDotSegmentChild(appliesTo, commandFilter)
            || IsDotSegmentChild(commandFilter, appliesTo);
    }

    private static bool IsDotSegmentChild (
        string candidate,
        string parent)
    {
        return candidate.Length > parent.Length
            && candidate[parent.Length] == '.'
            && candidate.StartsWith(parent, StringComparison.Ordinal);
    }
}
