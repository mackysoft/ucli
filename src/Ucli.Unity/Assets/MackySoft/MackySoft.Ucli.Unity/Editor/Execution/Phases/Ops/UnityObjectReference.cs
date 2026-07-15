#nullable enable

using System;
using MackySoft.Ucli.Unity.Execution.Requests;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one validated Unity-object reference used by operation arguments. </summary>
    internal sealed class UnityObjectReference
    {
        private UnityObjectReference (
            UnityObjectReferenceKind kind,
            RequestLocalAliasIdentity? alias,
            ResolveSelector? selector)
        {
            Kind = kind;
            Alias = alias;
            Selector = selector;
        }

        public UnityObjectReferenceKind Kind { get; }

        public RequestLocalAliasIdentity? Alias { get; }

        public ResolveSelector? Selector { get; }

        public static UnityObjectReference FromAlias (RequestLocalAliasIdentity alias)
        {
            if (alias == null)
            {
                throw new ArgumentNullException(nameof(alias));
            }

            return new UnityObjectReference(
                kind: UnityObjectReferenceKind.Alias,
                alias: alias,
                selector: null);
        }

        public static UnityObjectReference FromSelector (ResolveSelector selector)
        {
            if (selector == null)
            {
                throw new ArgumentNullException(nameof(selector));
            }

            return new UnityObjectReference(
                kind: UnityObjectReferenceKind.Selector,
                alias: null,
                selector: selector);
        }
    }
}
