using System.Collections.Generic;
using Microsoft.CodeAnalysis;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.CsEval
{
    /// <summary> Holds metadata references and their stable identity. </summary>
    internal sealed class CsEvalReferenceSet
    {
        public CsEvalReferenceSet (
            IReadOnlyList<MetadataReference> references,
            string identity)
        {
            References = references;
            Identity = identity;
        }

        public IReadOnlyList<MetadataReference> References { get; }

        public string Identity { get; }
    }
}
