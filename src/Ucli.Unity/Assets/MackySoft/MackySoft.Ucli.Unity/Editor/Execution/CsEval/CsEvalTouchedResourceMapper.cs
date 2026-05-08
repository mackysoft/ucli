using System;
using System.Collections.Generic;
using System.Linq;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Phases;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.CsEval
{
    /// <summary> Maps context touched-resource declarations to result and trace contracts. </summary>
    internal static class CsEvalTouchedResourceMapper
    {
        public static CsEvalTouchedResources CreateResult (UcliCsEvalContext context)
        {
            if (context.DeclaredNoTouchedResources)
            {
                return new CsEvalTouchedResources(
                    CsEvalTouchedResourceStateValues.None,
                    declared: null);
            }

            if (context.TouchedResources.Count == 0)
            {
                return new CsEvalTouchedResources(
                    CsEvalTouchedResourceStateValues.Unknown,
                    declared: null);
            }

            var declared = context.TouchedResources
                .OrderBy(static resource => resource.Kind, StringComparer.Ordinal)
                .ThenBy(static resource => resource.Path, StringComparer.Ordinal)
                .ToArray();
            return new CsEvalTouchedResources(
                CsEvalTouchedResourceStateValues.Declared,
                declared);
        }

        public static IReadOnlyList<OperationTouch> CreateTouches (UcliCsEvalContext context)
        {
            if (context.TouchedResources.Count == 0)
            {
                return Array.Empty<OperationTouch>();
            }

            var touchesByKey = new SortedDictionary<string, OperationTouch>(StringComparer.Ordinal);
            for (var i = 0; i < context.TouchedResources.Count; i++)
            {
                var declared = context.TouchedResources[i];
                var kind = MapKind(declared.Kind);
                var key = declared.Kind + "\n" + declared.Path;
                touchesByKey[key] = new OperationTouch(kind, declared.Path, Guid: null);
            }

            return touchesByKey.Values.ToArray();
        }

        private static OperationTouchKind MapKind (string kind)
        {
            return kind switch
            {
                IpcExecuteTouchedResourceKindNames.Scene => OperationTouchKind.Scene,
                IpcExecuteTouchedResourceKindNames.Prefab => OperationTouchKind.Prefab,
                IpcExecuteTouchedResourceKindNames.Asset => OperationTouchKind.Asset,
                IpcExecuteTouchedResourceKindNames.ProjectSettings => OperationTouchKind.ProjectSettings,
                _ => OperationTouchKind.Unknown,
            };
        }
    }
}
