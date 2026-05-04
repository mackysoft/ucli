namespace MackySoft.Ucli.Skills.Distribution;

/// <summary> Resolves the repository-local official <c>SkillDefinitions</c> directory. </summary>
public sealed class BundledSkillDefinitionRootResolver
{
    /// <summary> Resolves <c>src/Ucli.Skills/SkillDefinitions</c> by walking up from the current base directory. </summary>
    /// <returns> The resolved definitions root. </returns>
    /// <exception cref="DirectoryNotFoundException"> Thrown when the definitions root cannot be found. </exception>
    public string Resolve ()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Ucli.Skills", "SkillDefinitions");

            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate src/Ucli.Skills/SkillDefinitions from the application base directory.");
    }
}
