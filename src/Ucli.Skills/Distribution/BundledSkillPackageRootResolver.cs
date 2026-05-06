namespace MackySoft.Ucli.Skills.Distribution;

/// <summary> Resolves the bundled canonical <c>skills</c> package root. </summary>
public sealed class BundledSkillPackageRootResolver
{
    /// <summary> Resolves the bundled <c>skills</c> directory by walking up from the current base directory. </summary>
    /// <returns> The resolved canonical SKILL package root. </returns>
    /// <exception cref="DirectoryNotFoundException"> Thrown when the package root cannot be found. </exception>
    public string Resolve ()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "skills");

            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate bundled skills package root from the application base directory.");
    }
}
