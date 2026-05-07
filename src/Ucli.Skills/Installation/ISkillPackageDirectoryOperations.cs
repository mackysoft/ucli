namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Provides replaceable directory operations for SKILL package transactions. </summary>
internal interface ISkillPackageDirectoryOperations
{
    /// <summary> Determines whether the directory exists. </summary>
    /// <param name="path"> The directory path to inspect. </param>
    /// <returns> <see langword="true" /> when the directory exists; otherwise <see langword="false" />. </returns>
    bool Exists (string path);

    /// <summary> Creates a directory and all missing parents. </summary>
    /// <param name="path"> The directory path to create. </param>
    void Create (string path);

    /// <summary> Moves a directory to a new path. </summary>
    /// <param name="sourceDirectoryName"> The existing directory path. </param>
    /// <param name="destinationDirectoryName"> The destination directory path. </param>
    void Move (string sourceDirectoryName, string destinationDirectoryName);

    /// <summary> Deletes a directory. </summary>
    /// <param name="path"> The directory path to delete. </param>
    /// <param name="recursive"> Whether child entries should be deleted. </param>
    void Delete (string path, bool recursive);
}
