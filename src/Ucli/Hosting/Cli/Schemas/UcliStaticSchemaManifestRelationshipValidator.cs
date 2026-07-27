using MackySoft.Ucli.Contracts.Schemas;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Hosting.Cli.Schemas;

/// <summary> Validates ordering and references between static schema manifest entries. </summary>
internal static class UcliStaticSchemaManifestRelationshipValidator
{
    public static void EnsureEntryOrder (IReadOnlyList<UcliStaticSchemaEntry> entries)
    {
        for (var index = 1; index < entries.Count; index++)
        {
            var previous = entries[index - 1]?.Name;
            var current = entries[index]?.Name;
            if (previous == null
                || current == null
                || UnicodeCodePointComparer.Instance.Compare(previous, current) >= 0)
            {
                throw new InvalidDataException("Static schema manifest entries must be uniquely ordered by logical name.");
            }
        }
    }

    public static void EnsureEntryCollections (UcliStaticSchemaEntry entry)
    {
        EnsureOrderedUnique(entry.StaticDependencies!, entry.Name!, "staticDependencies");
        EnsureOrderedUnique(entry.DynamicValidationSources!, entry.Name!, "dynamicValidationSources");
        EnsureUsages(entry.Usages!, entry.Name!);
    }

    public static void EnsureDependenciesResolve (
        IReadOnlyList<UcliStaticSchemaEntry> entries,
        ISet<string> names)
    {
        foreach (var entry in entries)
        {
            foreach (var dependency in entry.StaticDependencies!)
            {
                if (!names.Contains(dependency)
                    || string.Equals(dependency, entry.Name, StringComparison.Ordinal))
                {
                    throw new InvalidDataException($"Static schema entry '{entry.Name}' has an invalid static dependency '{dependency}'.");
                }
            }
        }
    }

    private static void EnsureOrderedUnique (
        IReadOnlyList<string> values,
        string entryName,
        string propertyName)
    {
        string? previous = null;
        foreach (var current in values)
        {
            if (string.IsNullOrWhiteSpace(current)
                || (previous != null && UnicodeCodePointComparer.Instance.Compare(previous, current) >= 0))
            {
                throw new InvalidDataException($"Static schema entry '{entryName}' has invalid {propertyName} ordering.");
            }

            previous = current;
        }
    }

    private static void EnsureUsages (
        IReadOnlyList<UcliStaticSchemaUsage> usages,
        string entryName)
    {
        string? previous = null;
        foreach (var usage in usages)
        {
            if (usage == null || string.IsNullOrWhiteSpace(usage.Command))
            {
                throw new InvalidDataException($"Static schema entry '{entryName}' contains an incomplete usage.");
            }

            var key = usage.Command + "\0" + TextVocabulary.GetText(usage.Delivery) + "\0" + usage.Locator;
            if (previous != null && UnicodeCodePointComparer.Instance.Compare(previous, key) >= 0)
            {
                throw new InvalidDataException($"Static schema entry '{entryName}' has invalid usages ordering.");
            }

            previous = key;
        }
    }
}
