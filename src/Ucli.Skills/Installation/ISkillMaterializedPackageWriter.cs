using MackySoft.Ucli.Skills.Materialization;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Writes one materialized SKILL package into a resolved host target root. </summary>
public interface ISkillMaterializedPackageWriter
{
    /// <summary> Replaces one skill directory with a materialized package. </summary>
    /// <param name="targetRoot"> The resolved host target root. </param>
    /// <param name="skillDirectory"> The resolved skill package directory. </param>
    /// <param name="materializedPackage"> The materialized package to write. </param>
    /// <param name="writeMode"> The required target existence condition at commit time. </param>
    /// <param name="precondition"> The optional validation invoked for the target path immediately before move and for the moved tree before commit. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> Success when the directory is atomically replaced; otherwise a failure. </returns>
    ValueTask<SkillOperationResult<bool>> WriteAsync (
        string targetRoot,
        string skillDirectory,
        SkillMaterializedPackage materializedPackage,
        SkillMaterializedPackageWriteMode writeMode,
        Func<string, CancellationToken, ValueTask<SkillOperationResult<bool>>>? precondition,
        CancellationToken cancellationToken = default);
}
