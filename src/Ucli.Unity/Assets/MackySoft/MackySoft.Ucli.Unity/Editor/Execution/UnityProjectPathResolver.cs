using System.IO;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution
{
    /// <summary> Resolves common Unity project paths from the current editor process. </summary>
    internal static class UnityProjectPathResolver
    {
        /// <summary> Resolves the current Unity project root path. </summary>
        /// <returns> The absolute Unity project root path. </returns>
        public static string ResolveProjectRootPath ()
        {
            var dataPath = Application.dataPath;
            if (string.IsNullOrWhiteSpace(dataPath))
            {
                return Directory.GetCurrentDirectory();
            }

            var projectRoot = Path.GetDirectoryName(Path.GetFullPath(dataPath));
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                return Directory.GetCurrentDirectory();
            }

            return projectRoot;
        }
    }
}