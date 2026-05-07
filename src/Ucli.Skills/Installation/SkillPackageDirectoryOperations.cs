namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Implements SKILL package directory transactions against the local file system. </summary>
internal sealed class SkillPackageDirectoryOperations : ISkillPackageDirectoryOperations
{
    /// <inheritdoc />
    public bool Exists (string path)
    {
        return Directory.Exists(path);
    }

    /// <inheritdoc />
    public void Create (string path)
    {
        Directory.CreateDirectory(path);
    }

    /// <inheritdoc />
    public void Move (
        string sourceDirectoryName,
        string destinationDirectoryName)
    {
        Directory.Move(sourceDirectoryName, destinationDirectoryName);
    }

    /// <inheritdoc />
    public void Delete (
        string path,
        bool recursive)
    {
        Directory.Delete(path, recursive);
    }
}
