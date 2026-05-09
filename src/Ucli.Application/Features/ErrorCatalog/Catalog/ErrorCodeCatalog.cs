namespace MackySoft.Ucli.Application.Features.ErrorCatalog.Catalog;

internal sealed class ErrorCodeCatalog : IErrorCodeCatalog
{
    private readonly IReadOnlyDictionary<UcliErrorCode, UcliErrorCodeDescriptor> descriptorsByCode;

    public ErrorCodeCatalog (IEnumerable<IErrorCodeCatalogContributor> contributors)
    {
        ArgumentNullException.ThrowIfNull(contributors);

        var descriptors = CollectDescriptors(contributors);
        Descriptors = descriptors
            .OrderBy(static descriptor => descriptor.Code.Value, StringComparer.Ordinal)
            .ToArray();
        descriptorsByCode = Descriptors.ToDictionary(static descriptor => descriptor.Code);
    }

    public IReadOnlyList<UcliErrorCodeDescriptor> Descriptors { get; }

    public bool TryFind (
        UcliErrorCode code,
        out UcliErrorCodeDescriptor descriptor)
    {
        return descriptorsByCode.TryGetValue(code, out descriptor!);
    }

    private static List<UcliErrorCodeDescriptor> CollectDescriptors (IEnumerable<IErrorCodeCatalogContributor> contributors)
    {
        var descriptors = new List<UcliErrorCodeDescriptor>();
        var seenCodes = new HashSet<UcliErrorCode>();

        foreach (var contributor in contributors)
        {
            if (contributor is null)
            {
                throw new InvalidOperationException("Error code catalog contributor must not be null.");
            }

            var contributedDescriptors = contributor.GetDescriptors();
            if (contributedDescriptors is null)
            {
                throw new InvalidOperationException($"Error code catalog contributor '{contributor.GetType().FullName}' returned null descriptors.");
            }

            for (var i = 0; i < contributedDescriptors.Count; i++)
            {
                var descriptor = contributedDescriptors[i];
                ValidateDescriptorCore(descriptor, contributor);

                if (!seenCodes.Add(descriptor.Code))
                {
                    throw new InvalidOperationException($"Error code catalog contains duplicate code '{descriptor.Code.Value}'.");
                }

                descriptors.Add(descriptor);
            }
        }

        ValidateRelatedCodes(descriptors);
        return descriptors;
    }

    private static void ValidateDescriptorCore (
        UcliErrorCodeDescriptor descriptor,
        IErrorCodeCatalogContributor contributor)
    {
        if (descriptor is null)
        {
            throw new InvalidOperationException($"Error code catalog contributor '{contributor.GetType().FullName}' returned a null descriptor.");
        }

        if (!descriptor.Code.IsValid)
        {
            throw new InvalidOperationException("Error code catalog descriptor code must not be empty.");
        }

        ValidateRequiredString(descriptor.Code, descriptor.Category, nameof(descriptor.Category));
        ValidateRequiredString(descriptor.Code, descriptor.Summary, nameof(descriptor.Summary));
        ValidateRequiredString(descriptor.Code, descriptor.Meaning, nameof(descriptor.Meaning));

        if (descriptor.AppliesTo is null)
        {
            throw new InvalidOperationException($"Error code catalog descriptor '{descriptor.Code.Value}' has null appliesTo.");
        }

        if (descriptor.PossiblePhases is null)
        {
            throw new InvalidOperationException($"Error code catalog descriptor '{descriptor.Code.Value}' has null possiblePhases.");
        }

        if (descriptor.ExecutionSemantics is null)
        {
            throw new InvalidOperationException($"Error code catalog descriptor '{descriptor.Code.Value}' has null executionSemantics.");
        }

        ValidateRequiredString(
            descriptor.Code,
            descriptor.ExecutionSemantics.SafeToRetry,
            nameof(descriptor.ExecutionSemantics.SafeToRetry));

        if (!UcliErrorRetryClassValues.IsKnown(descriptor.ExecutionSemantics.SafeToRetry))
        {
            throw new InvalidOperationException($"Error code catalog descriptor '{descriptor.Code.Value}' has unknown safeToRetry '{descriptor.ExecutionSemantics.SafeToRetry}'.");
        }

        if (descriptor.Inspect is null)
        {
            throw new InvalidOperationException($"Error code catalog descriptor '{descriptor.Code.Value}' has null inspect.");
        }

        if (descriptor.NextActions is null)
        {
            throw new InvalidOperationException($"Error code catalog descriptor '{descriptor.Code.Value}' has null nextActions.");
        }

        for (var i = 0; i < descriptor.NextActions.Count; i++)
        {
            var nextAction = descriptor.NextActions[i];
            if (nextAction is null)
            {
                throw new InvalidOperationException($"Error code catalog descriptor '{descriptor.Code.Value}' has a null next action.");
            }

            ValidateRequiredString(descriptor.Code, nextAction.Action, nameof(nextAction.Action));
        }

        if (descriptor.RelatedCodes is null)
        {
            throw new InvalidOperationException($"Error code catalog descriptor '{descriptor.Code.Value}' has null relatedCodes.");
        }
    }

    private static void ValidateRequiredString (
        UcliErrorCode code,
        string? value,
        string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Error code catalog descriptor '{code.Value}' has empty {propertyName}.");
        }
    }

    private static void ValidateRelatedCodes (IReadOnlyList<UcliErrorCodeDescriptor> descriptors)
    {
        var knownCodes = descriptors.Select(static descriptor => descriptor.Code).ToHashSet();
        for (var i = 0; i < descriptors.Count; i++)
        {
            var descriptor = descriptors[i];
            for (var j = 0; j < descriptor.RelatedCodes.Count; j++)
            {
                var relatedCode = descriptor.RelatedCodes[j];
                if (relatedCode == descriptor.Code)
                {
                    throw new InvalidOperationException($"Error code catalog descriptor '{descriptor.Code.Value}' must not reference itself.");
                }

                if (!knownCodes.Contains(relatedCode))
                {
                    throw new InvalidOperationException($"Error code catalog descriptor '{descriptor.Code.Value}' references unknown related code '{relatedCode.Value}'.");
                }
            }
        }
    }
}
