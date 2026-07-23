using System;
using System.IO;
using MackySoft.FileSystem;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Project
{
    /// <summary> Resolves common Unity project paths from the current editor process. </summary>
    internal static class UnityProjectPathResolver
    {
        /// <summary> Resolves the current Unity project root path. </summary>
        /// <returns> The absolute Unity project root path. </returns>
        public static AbsolutePath ResolveProjectRootPath ()
        {
            var dataPath = Application.dataPath;
            if (!AbsolutePath.TryParse(
                    dataPath,
                    out var assetsDirectory,
                    out var failure))
            {
                if (failure.Kind == PathValidationFailureKind.EmptyPath)
                {
                    return AbsolutePath.Parse(Directory.GetCurrentDirectory());
                }

                throw new ArgumentException(
                    $"Application.dataPath is invalid. {failure.Message}",
                    nameof(dataPath));
            }

            if (!assetsDirectory.TryGetParent(out var projectRoot))
            {
                return AbsolutePath.Parse(Directory.GetCurrentDirectory());
            }

            return projectRoot;
        }
    }
}
