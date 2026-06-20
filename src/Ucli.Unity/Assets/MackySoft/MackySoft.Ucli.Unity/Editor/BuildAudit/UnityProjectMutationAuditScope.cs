using System;
using System.Collections.Generic;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Defines Unity project paths covered by build project mutation audits. </summary>
    internal static class UnityProjectMutationAuditScope
    {
        private static readonly string[] RootRelativePathArray =
        {
            "Assets",
            "ProjectSettings",
            "Packages",
        };

        public static IReadOnlyList<string> RootRelativePaths => RootRelativePathArray;

        public static bool IsAuditedProjectPath (string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            for (var i = 0; i < RootRelativePathArray.Length; i++)
            {
                if (path.StartsWith(RootRelativePathArray[i] + "/", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
