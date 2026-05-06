namespace MackySoft.Ucli.Skills.Distribution;

/// <summary> Resolves the bundled canonical <c>skills</c> package root. </summary>
public sealed class BundledSkillPackageRootResolver
{
    private readonly string baseDirectory;

    /// <summary> Initializes a new instance of the <see cref="BundledSkillPackageRootResolver" /> class. </summary>
    public BundledSkillPackageRootResolver () : this(AppContext.BaseDirectory)
    {
    }

    /// <summary> Initializes a new instance of the <see cref="BundledSkillPackageRootResolver" /> class. </summary>
    /// <param name="baseDirectory"> The application base directory containing bundled package files. </param>
    public BundledSkillPackageRootResolver (string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);

        this.baseDirectory = Path.GetFullPath(baseDirectory);
    }

    /// <summary> Resolves the bundled <c>skills</c> directory from the current base directory. </summary>
    /// <returns> The resolved canonical SKILL package root. </returns>
    /// <exception cref="DirectoryNotFoundException"> Thrown when the package root cannot be found. </exception>
    public string Resolve ()
    {
        var candidate = Path.Combine(baseDirectory, "skills");

        if (Directory.Exists(candidate))
        {
            return candidate;
        }

        throw new DirectoryNotFoundException($"Could not locate bundled skills package root: {candidate}");
    }
}
