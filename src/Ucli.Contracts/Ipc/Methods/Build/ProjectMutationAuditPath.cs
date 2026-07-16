using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one canonical project-relative path covered by project mutation auditing. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
public sealed class ProjectMutationAuditPath : UcliStringValue, IComparable<ProjectMutationAuditPath>
{
    private static readonly IReadOnlyList<string> RootDirectoryNameSnapshot = Array.AsReadOnly(new[]
    {
        "Assets",
        "ProjectSettings",
        "Packages",
    });

    /// <summary> Initializes one canonical project mutation audit path. </summary>
    /// <param name="value"> The canonical project-relative path below one audited root directory. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="value" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value" /> is not a normalized descendant of <c>Assets/</c>, <c>ProjectSettings/</c>, or <c>Packages/</c>.
    /// </exception>
    [JsonConstructor]
    public ProjectMutationAuditPath (string value)
        : base(Validate(value))
    {
    }

    /// <summary> Gets the project root directory names covered by project mutation auditing. </summary>
    internal static IReadOnlyList<string> RootDirectoryNames => RootDirectoryNameSnapshot;

    /// <summary> Attempts to parse one already-normalized project mutation audit path. </summary>
    /// <param name="value"> The candidate project-relative path. </param>
    /// <param name="path"> The typed path when parsing succeeds; otherwise <see langword="null" />. </param>
    /// <returns>
    /// <see langword="true" /> when <paramref name="value" /> is a normalized descendant of an audited root;
    /// otherwise <see langword="false" />.
    /// </returns>
    public static bool TryParse (
        string? value,
        [NotNullWhen(true)] out ProjectMutationAuditPath? path)
    {
        path = null;
        if (!IsValid(value))
        {
            return false;
        }

        path = new ProjectMutationAuditPath(value!);
        return true;
    }

    /// <inheritdoc />
    public int CompareTo (ProjectMutationAuditPath? other)
    {
        return other == null
            ? 1
            : string.CompareOrdinal(Value, other.Value);
    }

    private static string Validate (string? value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (!IsValid(value))
        {
            throw new ArgumentException(
                "Project mutation audit path must be a normalized descendant of Assets, ProjectSettings, or Packages.",
                nameof(value));
        }

        return value;
    }

    private static bool IsValid ([NotNullWhen(true)] string? value)
    {
        if (!RelativePathContract.IsNormalized(value))
        {
            return false;
        }

        for (var index = 0; index < RootDirectoryNameSnapshot.Count; index++)
        {
            var rootDirectoryName = RootDirectoryNameSnapshot[index];
            if (value.Length > rootDirectoryName.Length
                && value[rootDirectoryName.Length] == '/'
                && value.StartsWith(rootDirectoryName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
