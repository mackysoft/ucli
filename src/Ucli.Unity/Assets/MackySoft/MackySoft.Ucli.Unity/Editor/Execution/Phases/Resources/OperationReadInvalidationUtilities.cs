using System;
using System.Collections.Generic;
using MackySoft.Ucli.Infrastructure.Paths;

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

        private static readonly OperationReadInvalidation[] UnknownMutationInvalidations =
        {
            AssetSearchInvalidation,
            GuidPathInvalidation,
            new OperationReadInvalidation(
                OperationReadInvalidationSurface.SceneTreeLite,
                ScenePath: null),
        };

        public static IReadOnlyList<OperationReadInvalidation> CreateAssetSearchOnly ()
        {
            return AssetSearchOnlyInvalidations;
        }

        public static IReadOnlyList<OperationReadInvalidation> CreateAssetSearchAndGuidPath ()
        {
            return AssetSearchAndGuidPathInvalidations;
        }

        public static IReadOnlyList<OperationReadInvalidation> CreateUnknownMutation ()
        {
            return UnknownMutationInvalidations;
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

        public static IReadOnlyList<OperationReadInvalidation>? CreateSceneTreeLiteForSceneResource (OperationResource resource)
        {
            return resource.Kind == OperationTouchKind.Scene
                ? CreateSceneTreeLite(resource.Path)
                : null;
        }

        public static IReadOnlyList<OperationReadInvalidation> CreateForProjectSave (IReadOnlyList<OperationTouch> touched)
        {
            var invalidations = new List<OperationReadInvalidation>();
            var includesAssetSearch = false;
            for (var i = 0; i < touched.Count; i++)
            {
                var touch = touched[i];
                switch (touch.Kind)
                {
                    case OperationTouchKind.Asset:
                    case OperationTouchKind.Prefab:
                        if (!includesAssetSearch)
                        {
                            invalidations.AddRange(CreateAssetSearchOnly());
                            includesAssetSearch = true;
                        }

                        break;

                    case OperationTouchKind.Scene:
                        invalidations.AddRange(CreateSceneTreeLite(touch.Path));
                        break;
                }
            }

            return invalidations;
        }

        public static IReadOnlyList<OperationReadInvalidation> CreateForExplicitTouches (IReadOnlyList<OperationTouch> touched)
        {
            var invalidations = new List<OperationReadInvalidation>();
            var includesAssetLookupInvalidation = false;
            var scenePaths = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < touched.Count; i++)
            {
                var touch = touched[i];
                switch (touch.Kind)
                {
                    case OperationTouchKind.Asset:
                    case OperationTouchKind.Prefab:
                        if (!includesAssetLookupInvalidation)
                        {
                            invalidations.AddRange(CreateAssetSearchAndGuidPath());
                            includesAssetLookupInvalidation = true;
                        }

                        break;

                    case OperationTouchKind.Scene:
                        if (scenePaths.Add(touch.Path))
                        {
                            invalidations.AddRange(CreateSceneTreeLite(touch.Path));
                        }

                        break;
                }
            }

            return invalidations;
        }

        public static IReadOnlyList<OperationReadInvalidation> CreateForProjectRefresh (
            IReadOnlyList<OperationTouch> callbackTouched,
            IReadOnlyList<OperationTouch> touched)
        {
            var invalidations = new List<OperationReadInvalidation>();
            var includesAssetLookupInvalidation = false;
            for (var i = 0; i < callbackTouched.Count; i++)
            {
                var touch = callbackTouched[i];
                if (touch.Kind == OperationTouchKind.ProjectSettings)
                {
                    continue;
                }

                if (!includesAssetLookupInvalidation)
                {
                    invalidations.AddRange(CreateAssetSearchAndGuidPath());
                    includesAssetLookupInvalidation = true;
                }
            }

            for (var i = 0; i < touched.Count; i++)
            {
                if (touched[i].Kind != OperationTouchKind.Scene)
                {
                    continue;
                }

                invalidations.AddRange(CreateSceneTreeLite(touched[i].Path));
            }

            return invalidations;
        }
    }
}
