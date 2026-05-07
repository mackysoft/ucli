using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Deletes one installed SKILL package directory under a resolved host target root. </summary>
public interface ISkillInstalledPackageRemover
{
    /// <summary> Deletes one installed SKILL package directory. </summary>
    /// <param name="targetRoot"> The resolved host target root. </param>
    /// <param name="skillDirectory"> The resolved skill package directory. </param>
    /// <param name="precondition"> The optional validation invoked for the target path immediately before move and for the moved tree before commit. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> Success when the directory is deleted or already absent; otherwise a failure. </returns>
    ValueTask<SkillOperationResult<bool>> DeleteAsync (
        string targetRoot,
        string skillDirectory,
        Func<string, CancellationToken, ValueTask<SkillOperationResult<bool>>>? precondition,
        CancellationToken cancellationToken = default);
}
