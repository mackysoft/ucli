#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one parsed Unity-object reference used by operation arguments. </summary>
    internal readonly struct UnityObjectReference
    {
        public UnityObjectReference (
            UnityObjectReferenceKind kind,
            string? alias,
            ResolveSelector selector)
        {
            Kind = kind;
            Alias = alias;
            Selector = selector;
        }

        public UnityObjectReferenceKind Kind { get; }

        public string? Alias { get; }

        public ResolveSelector Selector { get; }

        public static UnityObjectReference FromAlias (string alias)
        {
            return new UnityObjectReference(
                kind: UnityObjectReferenceKind.Alias,
                alias: alias,
                selector: default);
        }

        public static UnityObjectReference FromSelector (ResolveSelector selector)
        {
            return new UnityObjectReference(
                kind: UnityObjectReferenceKind.Selector,
                alias: null,
                selector: selector);
        }
    }
}