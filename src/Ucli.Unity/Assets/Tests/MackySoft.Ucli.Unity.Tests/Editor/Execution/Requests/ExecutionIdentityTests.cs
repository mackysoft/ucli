using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.Requests;
using NUnit.Framework;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class ExecutionIdentityTests
    {
        [Test]
        [Category("Size.Small")]
        public void OperationExecutionKey_WhenRawStepMatchesLegacyEditEncoding_RemainsDistinct ()
        {
            var editPrimitiveKey = OperationExecutionKey.ForEditPrimitive(new IpcExecuteStepId("a"), primitiveIndex: 0);
            var rawStepKey = OperationExecutionKey.ForRawStep(new IpcExecuteStepId("a#p0"));

            var owners = new HashSet<OperationExecutionKey>
            {
                editPrimitiveKey,
                rawStepKey,
            };

            Assert.That(rawStepKey, Is.Not.EqualTo(editPrimitiveKey));
            Assert.That(owners, Has.Count.EqualTo(2));
        }

        [Test]
        [Category("Size.Small")]
        public void OperationAliasReferenceMap_WhenPublicAndInternalAliasValuesMatch_ResolvesDistinctOwners ()
        {
            var serializedAlias = new UcliPlanAlias("shared");
            var internalAliasIdentity = RequestLocalAliasIdentity.ForEditAction(
                new IpcExecuteStepId("a"),
                branchIndex: 0,
                serializedAlias);
            var internalReferences = OperationAliasReferenceMap.Create(internalAliasIdentity);
            var publicAlias = OperationAliasReferenceMap.Empty.Resolve(serializedAlias);
            var internalAlias = internalReferences.Resolve(serializedAlias);
            var publicGlobalObjectId = new UnityGlobalObjectId(
                "GlobalObjectId_V1-2-0123456789abcdef0123456789abcdef-123-0");
            var internalGlobalObjectId = new UnityGlobalObjectId(
                "GlobalObjectId_V1-2-0123456789abcdef0123456789abcdef-456-0");
            var store = new OperationAliasStore();

            store.Set(publicAlias, publicGlobalObjectId);
            store.Set(internalAlias, internalGlobalObjectId);

            Assert.That(publicAlias.Alias, Is.EqualTo(internalAlias.Alias));
            Assert.That(publicAlias, Is.Not.EqualTo(internalAlias));
            Assert.That(store.TryGet(publicAlias, out var storedPublicGlobalObjectId), Is.True);
            Assert.That(store.TryGet(internalAlias, out var storedInternalGlobalObjectId), Is.True);
            Assert.That(storedPublicGlobalObjectId, Is.EqualTo(publicGlobalObjectId));
            Assert.That(storedInternalGlobalObjectId, Is.EqualTo(internalGlobalObjectId));
        }

        [Test]
        [Category("Size.Small")]
        public void CompiledExecutionDigest_WhenSerializedAliasValuesMatch_TracksTypedAliasOwner ()
        {
            var serializedAlias = new UcliPlanAlias("shared");
            var internalAliasIdentity = RequestLocalAliasIdentity.ForEditAction(
                new IpcExecuteStepId("edit"),
                branchIndex: 0,
                serializedAlias);
            var executionKey = OperationExecutionKey.ForEditPrimitive(new IpcExecuteStepId("edit"), primitiveIndex: 1);
            var args = JsonSerializer.SerializeToElement(new
            {
                target = new
                {
                    @var = serializedAlias.Value,
                },
            });
            var publicReferenceOperation = new NormalizedOperation(
                executionKey,
                "ucli.tests.alias-reference",
                args,
                As: null,
                Expect: null,
                AliasReferences: OperationAliasReferenceMap.Empty,
                PersistenceReportingPolicy: OperationPersistenceReportingPolicy.ReportAll,
                AllowExplicitPrefabAssetMutation: false);
            var internalReferenceOperation = new NormalizedOperation(
                executionKey,
                "ucli.tests.alias-reference",
                args,
                As: null,
                Expect: null,
                AliasReferences: OperationAliasReferenceMap.Create(internalAliasIdentity),
                PersistenceReportingPolicy: OperationPersistenceReportingPolicy.ReportAll,
                AllowExplicitPrefabAssetMutation: false);

            var publicPayload = Encoding.UTF8.GetString(CompiledExecutionDigestWriter.WriteDigestPayload(
                Array.Empty<NormalizedRequestStep>(),
                new[] { publicReferenceOperation }).ToArray());
            var internalPayload = Encoding.UTF8.GetString(CompiledExecutionDigestWriter.WriteDigestPayload(
                Array.Empty<NormalizedRequestStep>(),
                new[] { internalReferenceOperation }).ToArray());

            Assert.That(internalPayload, Is.Not.EqualTo(publicPayload));
            Assert.That(internalPayload, Does.Contain("\"aliasReferences\""));
            Assert.That(internalPayload, Does.Not.Contain("__edit:"));
        }
    }
}
