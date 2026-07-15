using System;
using System.Collections.Generic;
using System.Linq;
using MackySoft.Ucli.Contracts;
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
            if (context.TouchedResourcesTruncated)
            {
                return new CsEvalTouchedResources(
                    CsEvalTouchedResourceState.Unknown,
                    declared: null);
            }

            if (context.DeclaredNoTouchedResources)
            {
                return new CsEvalTouchedResources(
                    CsEvalTouchedResourceState.None,
                    declared: null);
            }

            if (context.TouchedResources.Count == 0)
            {
                return new CsEvalTouchedResources(
                    CsEvalTouchedResourceState.Unknown,
                    declared: null);
            }

            var declared = context.TouchedResources
                .OrderBy(static resource => resource.Kind)
                .ThenBy(static resource => resource.Path, StringComparer.Ordinal)
                .ToArray();
            return new CsEvalTouchedResources(
                CsEvalTouchedResourceState.Declared,
                declared);
        }

        public static IReadOnlyList<OperationTouch> CreateTouches (UcliCsEvalContext context)
        {
            if (context.TouchedResources.Count == 0)
            {
                return Array.Empty<OperationTouch>();
            }

            var touches = new HashSet<OperationTouch>();
            for (var i = 0; i < context.TouchedResources.Count; i++)
            {
                var declared = context.TouchedResources[i];
                touches.Add(new OperationTouch(declared.Kind, declared.Path, assetGuid: null));
            }

            return touches
                .OrderBy(static touch => touch.Kind)
                .ThenBy(static touch => touch.Path, StringComparer.Ordinal)
                .ToArray();
        }

    }
}
