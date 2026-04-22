using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Paths;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Builds normalized read invalidation payloads for operation call results. </summary>
    internal static class OperationReadInvalidationUtilities
    {
        private static readonly OperationReadInvalidation AssetSearchInvalidation = new(
            OperationReadInvalidationSurface.AssetSearch,
            ScenePath: null);

        private static readonly OperationReadInvalidation GuidPathInvalidation = new(
            OperationReadInvalidationSurface.GuidPath,
            ScenePath: null);

        private static readonly OperationReadInvalidation[] AssetSearchOnlyInvalidations =
        {
            AssetSearchInvalidation,
        };

        private static readonly OperationReadInvalidation[] AssetSearchAndGuidPathInvalidations =
        {
            AssetSearchInvalidation,
            GuidPathInvalidation,
        };

        public static IReadOnlyList<OperationReadInvalidation> CreateAssetSearchOnly ()
        {
            return AssetSearchOnlyInvalidations;
        }

        public static IReadOnlyList<OperationReadInvalidation> CreateAssetSearchAndGuidPath ()
        {
            return AssetSearchAndGuidPathInvalidations;
        }

        public static IReadOnlyList<OperationReadInvalidation> CreateSceneTreeLite (string scenePath)
        {
            return new[]
            {
                new OperationReadInvalidation(
                    OperationReadInvalidationSurface.SceneTreeLite,
                    PathStringNormalizer.ToSlashSeparated(scenePath)),
            };
        }
    }
}
