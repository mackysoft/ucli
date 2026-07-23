using System.Diagnostics.CodeAnalysis;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Infrastructure.Paths;

/// <summary> Adapts project-mutation audit contracts at the product-to-filesystem boundary. </summary>
internal static class ProjectMutationAuditPathAdapter
{
    /// <summary> Converts one guarded audit path to the current-platform filesystem path contract. </summary>
    public static RootRelativePath ToRootRelativePath (ProjectMutationAuditPath path)
    {
        if (path is null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        return RootRelativePath.Parse(path.Value);
    }

    /// <summary> Attempts to convert one current-platform relative path to the portable audit contract. </summary>
    /// <param name="path"> The guarded current-platform relative path. </param>
    /// <param name="auditPath">
    /// The portable audit path when every filename character is representable by that contract;
    /// otherwise <see langword="null" />.
    /// </param>
    /// <returns>
    /// <see langword="true" /> when the current-platform path is representable as a portable audit
    /// path; otherwise <see langword="false" />.
    /// </returns>
    public static bool TryFromRootRelativePath (
        RootRelativePath path,
        [NotNullWhen(true)] out ProjectMutationAuditPath? auditPath)
    {
        if (path is null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        if (!UcliPortablePathAdapter.TryFormat(path, out var portablePath))
        {
            auditPath = null;
            return false;
        }

        return ProjectMutationAuditPath.TryParse(portablePath, out auditPath);
    }
}
