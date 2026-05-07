using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Deletes one installed SKILL package directory under a resolved host target root. </summary>
public interface ISkillInstalledPackageRemover
{
    /// <summary> Deletes one installed SKILL package directory. </summary>
    /// <param name="targetRoot"> The resolved host target root. </param>
    /// <param name="skillDirectory"> The resolved skill package directory. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> Success when the directory is deleted or already absent; otherwise a failure. </returns>
    ValueTask<SkillOperationResult<bool>> DeleteAsync (
        string targetRoot,
        string skillDirectory,
        CancellationToken cancellationToken = default);
}
