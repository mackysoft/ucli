using System;
using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Requests
{
    /// <summary> Resolves alias strings in one normalized operation to their typed request-local identities. </summary>
    internal sealed class OperationAliasReferenceMap
    {
        private readonly RequestLocalAliasIdentity.EditActionAliasIdentity[] internalAliases;

        private OperationAliasReferenceMap (RequestLocalAliasIdentity.EditActionAliasIdentity[] aliases)
        {
            internalAliases = aliases;
        }

        public static OperationAliasReferenceMap Empty { get; } = new OperationAliasReferenceMap(
            Array.Empty<RequestLocalAliasIdentity.EditActionAliasIdentity>());

        public static OperationAliasReferenceMap Create (
            RequestLocalAliasIdentity.EditActionAliasIdentity? first,
            RequestLocalAliasIdentity.EditActionAliasIdentity? second = null)
        {
            if (first == null)
            {
                return second == null
                    ? Empty
                    : new OperationAliasReferenceMap(new[] { second });
            }

            if (second == null || first.Equals(second))
            {
                return new OperationAliasReferenceMap(new[] { first });
            }

            if (first.Alias.Equals(second.Alias))
            {
                throw new ArgumentException(
                    $"One operation cannot map alias '{first.Alias}' to multiple internal identities.",
                    nameof(second));
            }

            return new OperationAliasReferenceMap(new[] { first, second });
        }

        public RequestLocalAliasIdentity Resolve (UcliPlanAlias alias)
        {
            if (alias == null)
            {
                throw new ArgumentNullException(nameof(alias));
            }

            for (var i = 0; i < internalAliases.Length; i++)
            {
                if (internalAliases[i].Alias.Equals(alias))
                {
                    return internalAliases[i];
                }
            }

            return RequestLocalAliasIdentity.FromPublicAlias(alias);
        }

        public int InternalAliasCount => internalAliases.Length;

        public RequestLocalAliasIdentity.EditActionAliasIdentity this[int index] => internalAliases[index];
    }
}
