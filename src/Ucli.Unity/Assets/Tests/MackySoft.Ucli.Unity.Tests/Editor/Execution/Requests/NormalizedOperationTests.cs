using System;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using NUnit.Framework;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class NormalizedOperationTests
    {
        [Test]
        [Category("Size.Small")]
        public void Constructor_WhenExecutionSourceDiffers_DerivesRequestLocalAliasPermission ()
        {
            var stepId = new IpcExecuteStepId("step");
            var args = JsonSerializer.SerializeToElement(new { });
            var rawOperation = CreateOperation(
                OperationExecutionKey.ForRawStep(stepId),
                args,
                OperationAliasReferenceMap.Empty,
                OperationPersistenceReportingPolicy.ReportAll);
            var editOperation = CreateOperation(
                OperationExecutionKey.ForEditPrimitive(stepId, primitiveIndex: 0),
                args,
                OperationAliasReferenceMap.Empty,
                OperationPersistenceReportingPolicy.ReportAll);

            Assert.That(rawOperation.AllowRequestLocalAliases, Is.False);
            Assert.That(editOperation.AllowRequestLocalAliases, Is.True);
        }

        [TestCase("")]
        [TestCase(" ")]
        [TestCase(" operation")]
        [TestCase("operation ")]
        [Category("Size.Small")]
        public void Constructor_WhenOperationNameIsInvalid_RejectsInvalidValue (string? operationName)
        {
            var exception = Assert.Throws<ArgumentException>(() => new NormalizedOperation(
                ExecutionKey: OperationExecutionKey.ForRawStep(new IpcExecuteStepId("step")),
                Op: operationName!,
                Args: JsonSerializer.SerializeToElement(new { }),
                As: null,
                Expect: null,
                AliasReferences: OperationAliasReferenceMap.Empty,
                PersistenceReportingPolicy: OperationPersistenceReportingPolicy.ReportAll,
                AllowExplicitPrefabAssetMutation: false));

            Assert.That(exception!.ParamName, Is.EqualTo("Op"));
        }

        [Test]
        [Category("Size.Small")]
        public void Constructor_WhenOperationNameIsNull_RejectsInvalidValue ()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new NormalizedOperation(
                ExecutionKey: OperationExecutionKey.ForRawStep(new IpcExecuteStepId("step")),
                Op: null!,
                Args: JsonSerializer.SerializeToElement(new { }),
                As: null,
                Expect: null,
                AliasReferences: OperationAliasReferenceMap.Empty,
                PersistenceReportingPolicy: OperationPersistenceReportingPolicy.ReportAll,
                AllowExplicitPrefabAssetMutation: false));

            Assert.That(exception!.ParamName, Is.EqualTo("Op"));
        }

        [TestCase("null")]
        [TestCase("[]")]
        [TestCase("\"value\"")]
        [Category("Size.Small")]
        public void Constructor_WhenArgumentsAreNotObject_RejectsInvalidValue (string json)
        {
            using var document = JsonDocument.Parse(json);

            var exception = Assert.Throws<ArgumentException>(() => new NormalizedOperation(
                ExecutionKey: OperationExecutionKey.ForRawStep(new IpcExecuteStepId("step")),
                Op: "ucli.tests.operation",
                Args: document.RootElement,
                As: null,
                Expect: null,
                AliasReferences: OperationAliasReferenceMap.Empty,
                PersistenceReportingPolicy: OperationPersistenceReportingPolicy.ReportAll,
                AllowExplicitPrefabAssetMutation: false));

            Assert.That(exception!.ParamName, Is.EqualTo("Args"));
        }

        [Test]
        [Category("Size.Small")]
        public void Constructor_WhenArgumentsAreUndefined_RejectsInvalidValue ()
        {
            var exception = Assert.Throws<ArgumentException>(() => new NormalizedOperation(
                ExecutionKey: OperationExecutionKey.ForRawStep(new IpcExecuteStepId("step")),
                Op: "ucli.tests.operation",
                Args: default,
                As: null,
                Expect: null,
                AliasReferences: OperationAliasReferenceMap.Empty,
                PersistenceReportingPolicy: OperationPersistenceReportingPolicy.ReportAll,
                AllowExplicitPrefabAssetMutation: false));

            Assert.That(exception!.ParamName, Is.EqualTo("Args"));
        }

        [Test]
        [Category("Size.Small")]
        public void Constructor_WhenAliasReferencesAreNull_RejectsInvalidValue ()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new NormalizedOperation(
                ExecutionKey: OperationExecutionKey.ForRawStep(new IpcExecuteStepId("step")),
                Op: "ucli.tests.operation",
                Args: JsonSerializer.SerializeToElement(new { }),
                As: null,
                Expect: null,
                AliasReferences: null!,
                PersistenceReportingPolicy: OperationPersistenceReportingPolicy.ReportAll,
                AllowExplicitPrefabAssetMutation: false));

            Assert.That(exception!.ParamName, Is.EqualTo("AliasReferences"));
        }

        [Test]
        [Category("Size.Small")]
        public void Constructor_WhenRawOperationHasInternalAliasReference_RejectsInvalidValue ()
        {
            var stepId = new IpcExecuteStepId("step");
            var aliasReferences = OperationAliasReferenceMap.Create(RequestLocalAliasIdentity.ForEditAction(
                stepId,
                branchIndex: 0,
                new UcliPlanAlias("created")));

            var exception = Assert.Throws<ArgumentException>(() => CreateOperation(
                OperationExecutionKey.ForRawStep(stepId),
                JsonSerializer.SerializeToElement(new { }),
                aliasReferences,
                OperationPersistenceReportingPolicy.ReportAll));

            Assert.That(exception!.ParamName, Is.EqualTo("AliasReferences"));
        }

        [Test]
        [Category("Size.Small")]
        public void Constructor_WhenInternalAliasBelongsToDifferentStep_RejectsInvalidValue ()
        {
            var aliasReferences = OperationAliasReferenceMap.Create(RequestLocalAliasIdentity.ForEditAction(
                new IpcExecuteStepId("other-step"),
                branchIndex: 0,
                new UcliPlanAlias("created")));

            var exception = Assert.Throws<ArgumentException>(() => CreateOperation(
                OperationExecutionKey.ForEditPrimitive(new IpcExecuteStepId("step"), primitiveIndex: 0),
                JsonSerializer.SerializeToElement(new { }),
                aliasReferences,
                OperationPersistenceReportingPolicy.ReportAll));

            Assert.That(exception!.ParamName, Is.EqualTo("AliasReferences"));
        }

        [Test]
        [Category("Size.Small")]
        public void Constructor_WhenInternalAliasBelongsToSameStep_AcceptsValue ()
        {
            var stepId = new IpcExecuteStepId("step");
            var aliasReferences = OperationAliasReferenceMap.Create(RequestLocalAliasIdentity.ForEditAction(
                stepId,
                branchIndex: 0,
                new UcliPlanAlias("created")));

            var operation = CreateOperation(
                OperationExecutionKey.ForEditPrimitive(stepId, primitiveIndex: 1),
                JsonSerializer.SerializeToElement(new { }),
                aliasReferences,
                OperationPersistenceReportingPolicy.ReportAll);

            Assert.That(operation.AliasReferences, Is.SameAs(aliasReferences));
        }

        [TestCase(0)]
        [TestCase(4)]
        [TestCase(byte.MaxValue)]
        [Category("Size.Small")]
        public void Constructor_WhenPersistenceReportingPolicyIsUndefined_RejectsInvalidValue (byte value)
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => CreateOperation(
                OperationExecutionKey.ForRawStep(new IpcExecuteStepId("step")),
                JsonSerializer.SerializeToElement(new { }),
                OperationAliasReferenceMap.Empty,
                (OperationPersistenceReportingPolicy)value));

            Assert.That(exception!.ParamName, Is.EqualTo("PersistenceReportingPolicy"));
        }

        [Test]
        [Category("Size.Small")]
        public void Constructor_WhenOperationNameIsUnknownButValid_AcceptsValue ()
        {
            var operation = CreateOperation(
                OperationExecutionKey.ForRawStep(new IpcExecuteStepId("step")),
                JsonSerializer.SerializeToElement(new { }),
                OperationAliasReferenceMap.Empty,
                OperationPersistenceReportingPolicy.ReportAll);

            Assert.That(operation.Op, Is.EqualTo("ucli.tests.operation"));
        }

        private static NormalizedOperation CreateOperation (
            OperationExecutionKey executionKey,
            JsonElement args,
            OperationAliasReferenceMap aliasReferences,
            OperationPersistenceReportingPolicy persistenceReportingPolicy)
        {
            return new NormalizedOperation(
                ExecutionKey: executionKey,
                Op: "ucli.tests.operation",
                Args: args,
                As: null,
                Expect: null,
                AliasReferences: aliasReferences,
                PersistenceReportingPolicy: persistenceReportingPolicy,
                AllowExplicitPrefabAssetMutation: false);
        }
    }
}
