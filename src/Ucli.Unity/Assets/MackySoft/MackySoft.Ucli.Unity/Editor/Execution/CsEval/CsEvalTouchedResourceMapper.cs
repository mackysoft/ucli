using System.Linq;
using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.CsEval
{
    /// <summary> Maps context touched-resource declarations to the dedicated eval result contract. </summary>
    internal static class CsEvalTouchedResourceMapper
    {
        public static CsEvalTouchedResources CreateResult (UcliCsEvalContext context)
        {
            return new CsEvalTouchedResources(
                context.DeclaredNoTouchedResources,
                context.Scenes.ToArray(),
                context.Prefabs.ToArray(),
                context.Assets.ToArray(),
                context.ProjectSettings.ToArray());
        }

    }
}
