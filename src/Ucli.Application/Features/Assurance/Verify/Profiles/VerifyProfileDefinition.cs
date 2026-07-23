
namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Profiles;

/// <summary> Represents one resolved verify profile. </summary>
internal sealed record VerifyProfileDefinition
{
    public VerifyProfileDefinition (
        VerifyProfileSource Source,
        string Name,
        string? RepositoryRelativePath,
        IReadOnlyList<VerifyProfileStep> Steps)
    {
        if (!TextVocabulary.IsDefined(Source))
        {
            throw new ArgumentOutOfRangeException(nameof(Source), Source, "Unsupported verify profile source.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        if (!string.Equals(Name, Name.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("Profile name must not contain outer whitespace.", nameof(Name));
        }
        ArgumentNullException.ThrowIfNull(Steps);
        if (Steps.Any(static step => step is null))
        {
            throw new ArgumentException("Profile steps must not contain null.", nameof(Steps));
        }

        if (Source == VerifyProfileSource.BuiltIn && RepositoryRelativePath is not null)
        {
            throw new ArgumentException("Built-in profiles must not have a repository-relative path.", nameof(RepositoryRelativePath));
        }
        if (Source == VerifyProfileSource.File && !RelativePathContract.IsNormalized(RepositoryRelativePath))
        {
            throw new ArgumentException("File profiles require a normalized repository-relative path.", nameof(RepositoryRelativePath));
        }

        var canonicalSteps = Steps
            .OrderBy(static step => step.Kind)
            .ToArray();
        for (var index = 1; index < canonicalSteps.Length; index++)
        {
            if (canonicalSteps[index - 1].Kind == canonicalSteps[index].Kind)
            {
                throw new ArgumentException(
                    $"Profile steps must not contain duplicate kind '{TextVocabulary.GetText(canonicalSteps[index].Kind)}'.",
                    nameof(Steps));
            }
        }

        this.Source = Source;
        this.Name = Name;
        this.RepositoryRelativePath = RepositoryRelativePath;
        this.Steps = Array.AsReadOnly(canonicalSteps);
    }

    public VerifyProfileSource Source { get; }

    public string Name { get; }

    public string? RepositoryRelativePath { get; }

    public IReadOnlyList<VerifyProfileStep> Steps { get; }
}
