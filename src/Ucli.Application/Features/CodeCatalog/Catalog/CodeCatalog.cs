namespace MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;

/// <summary> Aggregates contributor descriptor sets into one validated, deterministic code catalog. </summary>
internal sealed class CodeCatalog : ICodeCatalog
{
    private static readonly HashSet<UcliCommand> KnownCommandSet = new(UcliPublicCommandCatalog.KnownCommands);

    private readonly IReadOnlyDictionary<string, CodeCatalogDescriptor> descriptorsByCode;

    /// <summary> Initializes a new instance of the <see cref="CodeCatalog" /> class. </summary>
    /// <param name="contributors"> The contributor set to aggregate. The sequence must not be <see langword="null" /> and must not contain <see langword="null" /> entries. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="contributors" /> is <see langword="null" />. </exception>
    /// <exception cref="InvalidOperationException"> Thrown when a contributor returns invalid descriptors, duplicate codes, invalid command ids, or related codes outside the aggregate. </exception>
    public CodeCatalog (IEnumerable<ICodeCatalogContributor> contributors)
    {
        ArgumentNullException.ThrowIfNull(contributors);

        var descriptors = CollectDescriptors(contributors);
        Descriptors = descriptors
            .OrderBy(static descriptor => descriptor.Code, StringComparer.Ordinal)
            .ToArray();
        descriptorsByCode = Descriptors.ToDictionary(
            static descriptor => descriptor.Code,
            static descriptor => descriptor,
            StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public IReadOnlyList<CodeCatalogDescriptor> Descriptors { get; }

    /// <inheritdoc />
    public bool TryFind (
        string? code,
        out CodeCatalogDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            descriptor = null!;
            return false;
        }

        return descriptorsByCode.TryGetValue(code, out descriptor!);
    }

    private static List<CodeCatalogDescriptor> CollectDescriptors (IEnumerable<ICodeCatalogContributor> contributors)
    {
        var descriptors = new List<CodeCatalogDescriptor>();
        var seenCodes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var contributor in contributors)
        {
            if (contributor is null)
            {
                throw new InvalidOperationException("Code catalog contributor must not be null.");
            }

            var contributedDescriptors = contributor.GetDescriptors();
            if (contributedDescriptors is null)
            {
                throw new InvalidOperationException($"Code catalog contributor '{contributor.GetType().FullName}' returned null descriptors.");
            }

            for (var i = 0; i < contributedDescriptors.Count; i++)
            {
                var descriptor = contributedDescriptors[i];
                ValidateDescriptorCore(descriptor, contributor);

                if (!seenCodes.Add(descriptor.Code))
                {
                    throw new InvalidOperationException($"Code catalog contains duplicate code '{descriptor.Code}'.");
                }

                descriptors.Add(descriptor);
            }
        }

        ValidateRelatedCodes(descriptors);
        return descriptors;
    }

    private static void ValidateDescriptorCore (
        CodeCatalogDescriptor descriptor,
        ICodeCatalogContributor contributor)
    {
        if (descriptor is null)
        {
            throw new InvalidOperationException($"Code catalog contributor '{contributor.GetType().FullName}' returned a null descriptor.");
        }

        ValidateRequiredString(descriptor.Code, descriptor.Code, nameof(descriptor.Code));
        if (!CodeCatalogKindValues.IsSupported(descriptor.Kind))
        {
            throw new InvalidOperationException($"Code catalog descriptor '{descriptor.Code}' has unsupported kind '{descriptor.Kind}'.");
        }

        ValidateRequiredString(descriptor.Code, descriptor.Category, nameof(descriptor.Category));
        ValidateRequiredString(descriptor.Code, descriptor.Summary, nameof(descriptor.Summary));

        if (descriptor.AppearsIn is null)
        {
            throw new InvalidOperationException($"Code catalog descriptor '{descriptor.Code}' has null appearsIn.");
        }

        if (descriptor.AppearsIn.Count == 0)
        {
            throw new InvalidOperationException($"Code catalog descriptor '{descriptor.Code}' must declare at least one appearsIn item.");
        }

        ValidateStringList(descriptor.Code, descriptor.AppearsIn, nameof(descriptor.AppearsIn));

        if (descriptor.AppliesTo is null)
        {
            throw new InvalidOperationException($"Code catalog descriptor '{descriptor.Code}' has null appliesTo.");
        }

        ValidateCommandList(descriptor.Code, descriptor.AppliesTo, nameof(descriptor.AppliesTo));

        if (descriptor.Inspect is null)
        {
            throw new InvalidOperationException($"Code catalog descriptor '{descriptor.Code}' has null inspect.");
        }

        ValidateStringList(descriptor.Code, descriptor.Inspect, nameof(descriptor.Inspect));

        if (descriptor.RelatedCodes is null)
        {
            throw new InvalidOperationException($"Code catalog descriptor '{descriptor.Code}' has null relatedCodes.");
        }
    }

    private static void ValidateCommandList (
        string code,
        IReadOnlyList<UcliCommand> commands,
        string propertyName)
    {
        var seenCommands = new HashSet<UcliCommand>();
        for (var i = 0; i < commands.Count; i++)
        {
            if (!commands[i].IsValid)
            {
                throw new InvalidOperationException($"Code catalog descriptor '{code}' has invalid {propertyName} item at index {i}.");
            }

            if (!KnownCommandSet.Contains(commands[i]))
            {
                throw new InvalidOperationException($"Code catalog descriptor '{code}' references unknown public command '{commands[i].Name}'.");
            }

            if (!seenCommands.Add(commands[i]))
            {
                throw new InvalidOperationException($"Code catalog descriptor '{code}' contains duplicate {propertyName} item '{commands[i].Name}'.");
            }
        }
    }

    private static void ValidateRequiredString (
        string code,
        string? value,
        string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Code catalog descriptor '{code}' has empty {propertyName}.");
        }
    }

    private static void ValidateStringList (
        string code,
        IReadOnlyList<string> values,
        string propertyName)
    {
        for (var i = 0; i < values.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(values[i]))
            {
                throw new InvalidOperationException($"Code catalog descriptor '{code}' has empty {propertyName} item at index {i}.");
            }
        }
    }

    private static void ValidateRelatedCodes (IReadOnlyList<CodeCatalogDescriptor> descriptors)
    {
        var knownCodes = descriptors
            .Select(static descriptor => descriptor.Code)
            .ToHashSet(StringComparer.Ordinal);
        for (var i = 0; i < descriptors.Count; i++)
        {
            var descriptor = descriptors[i];
            for (var j = 0; j < descriptor.RelatedCodes.Count; j++)
            {
                var relatedCode = descriptor.RelatedCodes[j];
                if (string.Equals(relatedCode, descriptor.Code, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Code catalog descriptor '{descriptor.Code}' must not reference itself.");
                }

                if (!knownCodes.Contains(relatedCode))
                {
                    throw new InvalidOperationException($"Code catalog descriptor '{descriptor.Code}' references unknown related code '{relatedCode}'.");
                }
            }
        }
    }
}
