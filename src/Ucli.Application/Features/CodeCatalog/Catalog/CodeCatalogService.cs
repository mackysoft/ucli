using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;

/// <summary> Implements code catalog listing, description lookup, and unknown-code fallback semantics. </summary>
internal sealed class CodeCatalogService : ICodeCatalogService
{
    private static readonly HashSet<UcliCommand> KnownCommandSet = new(UcliPublicCommandCatalog.KnownCommands);

    private static readonly IReadOnlyList<CodeCatalogDescriptor> EmptyDescriptors = Array.Empty<CodeCatalogDescriptor>();

    private static readonly IReadOnlyList<UcliCommand> EmptyCommands = Array.Empty<UcliCommand>();

    private static readonly IReadOnlyList<string> EmptyStrings = Array.Empty<string>();

    private static readonly IReadOnlyList<UcliCode> EmptyCodeValues = Array.Empty<UcliCode>();

    private readonly ICodeCatalog catalog;

    /// <summary> Initializes a new instance of the <see cref="CodeCatalogService" /> class. </summary>
    /// <param name="catalog"> The validated catalog used for known-code lookup. Must not be <see langword="null" />. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="catalog" /> is <see langword="null" />. </exception>
    public CodeCatalogService (ICodeCatalog catalog)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    /// <inheritdoc />
    public CodeCatalogListResult List (CodeCatalogListInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        CodeCatalogKind? kindFilter = null;
        if (input.Kind is not null && string.IsNullOrWhiteSpace(input.Kind))
        {
            return CodeCatalogListResult.Failure(
                ExecutionError.InvalidArgument(
                    "kind must not be empty.",
                    UcliCoreErrorCodes.InvalidArgument));
        }

        if (input.Kind is not null)
        {
            if (!TextVocabulary.TryGetValue<CodeCatalogKind>(input.Kind, out var parsedKind)
                || parsedKind == CodeCatalogKind.Unknown)
            {
                return CodeCatalogListResult.Success(EmptyDescriptors);
            }

            kindFilter = parsedKind;
        }

        UcliCommand? commandFilter = null;
        if (input.Command is not null)
        {
            if (!UcliCommand.TryCreate(input.Command, out var command))
            {
                return CodeCatalogListResult.Failure(
                    ExecutionError.InvalidArgument(
                        "command must be a valid command identifier.",
                        UcliCoreErrorCodes.InvalidArgument));
            }

            commandFilter = command;
        }

        if (commandFilter is not null && !KnownCommandSet.Contains(commandFilter))
        {
            return CodeCatalogListResult.Success(EmptyDescriptors);
        }

        var descriptors = new List<CodeCatalogDescriptor>();
        foreach (var descriptor in catalog.Descriptors)
        {
            if (kindFilter.HasValue && descriptor.Kind != kindFilter.Value)
            {
                continue;
            }

            if (commandFilter is not null && !MatchesCommandFilter(descriptor.AppliesTo, commandFilter))
            {
                continue;
            }

            descriptors.Add(descriptor);
        }

        return CodeCatalogListResult.Success(descriptors);
    }

    /// <inheritdoc />
    public CodeCatalogDescribeResult Describe (
        CodeCatalogCodeReference reference,
        bool requireKnown)
    {
        ArgumentNullException.ThrowIfNull(reference);

        if (catalog.TryFind(reference.Code, out var descriptor))
        {
            if (reference.ExpectedKind.HasValue && reference.ExpectedKind.Value != descriptor.Kind)
            {
                var actualKind = TextVocabulary.GetText(descriptor.Kind);
                var expectedKind = TextVocabulary.GetText(reference.ExpectedKind.Value);
                return CodeCatalogDescribeResult.Failure(
                    ExecutionError.InvalidArgument(
                        $"Code '{reference.Code}' has kind '{actualKind}', not '{expectedKind}'.",
                        UcliCoreErrorCodes.InvalidArgument));
            }

            return CodeCatalogDescribeResult.Success(descriptor, known: true);
        }

        if (requireKnown)
        {
            return CodeCatalogDescribeResult.Failure(
                ExecutionError.InvalidArgument(
                    $"Code '{reference.Code}' is not known to this uCLI client.",
                    UcliCoreErrorCodes.InvalidArgument));
        }

        return CodeCatalogDescribeResult.Success(CreateUnknownDescriptor(reference.Code), known: false);
    }

    private static CodeCatalogDescriptor CreateUnknownDescriptor (UcliCode code)
    {
        return new CodeCatalogDescriptor(
            Code: code,
            Kind: CodeCatalogKind.Unknown,
            Category: TextVocabulary.GetText(CodeCatalogKind.Unknown),
            Summary: "This code is not known to this uCLI client.",
            Meaning: null,
            AppearsIn: EmptyStrings,
            AppliesTo: EmptyCommands,
            CoverageImpact: null,
            VerdictSemantics: null,
            ExecutionSemantics: null,
            Inspect: EmptyStrings,
            RelatedCodes: EmptyCodeValues);
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
