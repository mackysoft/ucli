using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.Requests;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    /// <summary> Adapts public alias literals at test setup and assertion boundaries. </summary>
    internal static class PublicAliasTestExtensions
    {
        public static void Set (
            this OperationAliasStore store,
            string alias,
            UnityGlobalObjectId globalObjectId)
        {
            store.Set(CreatePublicAlias(alias), globalObjectId);
        }

        public static bool TryGet (
            this OperationAliasStore store,
            string alias,
            [NotNullWhen(true)] out UnityGlobalObjectId? globalObjectId)
        {
            return store.TryGet(CreatePublicAlias(alias), out globalObjectId);
        }

        public static void SetTemporaryAlias (
            this OperationExecutionContext executionContext,
            string alias,
            UnityEngine.Object unityObject,
            OperationResource resource,
            RequestLocalObjectIdentity? sourceTrackingKey = null)
        {
            executionContext.SetTemporaryAlias(
                CreatePublicAlias(alias),
                unityObject,
                resource,
                sourceTrackingKey);
        }

        public static bool TryGetTemporaryAliasState (
            this OperationExecutionContext executionContext,
            string alias,
            out TemporaryAliasRegistry.TemporaryAliasState state)
        {
            return executionContext.TryGetTemporaryAliasState(CreatePublicAlias(alias), out state);
        }

        private static RequestLocalAliasIdentity CreatePublicAlias (string alias)
        {
            return RequestLocalAliasIdentity.FromPublicAlias(new UcliPlanAlias(alias));
        }
    }
}
