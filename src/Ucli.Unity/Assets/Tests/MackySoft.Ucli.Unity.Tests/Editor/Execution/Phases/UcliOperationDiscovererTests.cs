using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.Requests;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UcliOperationDiscovererTests
    {
        [Test]
        [Category("Size.Small")]
        public void DiscoverFromTypes_WhenTypeIsValidOperation_ReturnsOperationInstance ()
        {
            var operations = UcliOperationDiscoverer.DiscoverFromTypes(new Type[]
            {
                typeof(DiscoverableOperation),
            });

            Assert.That(operations.Count, Is.EqualTo(1));
            Assert.That(operations[0].Operation, Is.TypeOf<DiscoverableOperation>());
            Assert.That(operations[0].Metadata.OperationName, Is.EqualTo("ucli.tests.discover"));
        }

        [Test]
        [Category("Size.Small")]
        public void DiscoverFromTypes_WhenTypeIsGenericOperation_ReturnsTypedDescribeContract ()
        {
            var operations = UcliOperationDiscoverer.DiscoverFromTypes(new Type[]
            {
                typeof(GenericDiscoverableOperation),
            });

            Assert.That(operations.Count, Is.EqualTo(1));
            Assert.That(operations[0].Operation, Is.TypeOf<GenericDiscoverableOperation>());
            Assert.That(operations[0].Metadata.ArgsType, Is.EqualTo(typeof(GenericDiscoverableArgs)));
            Assert.That(operations[0].Metadata.ResultType, Is.EqualTo(typeof(UcliNoResult)));
            Assert.That(operations[0].Metadata.DescribeContract.Description, Is.EqualTo("Generic operation used to verify custom operation authoring."));
            Assert.That(operations[0].Metadata.DescribeContract.Inputs!.Count, Is.EqualTo(1));
            Assert.That(operations[0].Metadata.DescribeContract.Inputs[0].Name, Is.EqualTo("path"));
        }

        [Test]
        [Category("Size.Small")]
        public void DiscoverFromTypes_WhenTypedOperationMetadataArgsTypeDoesNotMatch_ThrowsInvalidOperationException ()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                _ = UcliOperationDiscoverer.DiscoverFromTypes(new Type[]
                {
                    typeof(MetadataArgsMismatchOperation),
                });
            });
        }

        [Test]
        [Category("Size.Small")]
        public void DiscoverFromTypes_WhenTypedOperationMetadataResultTypeDoesNotMatch_ThrowsInvalidOperationException ()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                _ = UcliOperationDiscoverer.DiscoverFromTypes(new Type[]
                {
                    typeof(MetadataResultMismatchOperation),
                });
            });
        }

        [Test]
        [Category("Size.Small")]
        public void UcliOperationMetadata_WhenEmittedResultTypeNameDoesNotMatch_ThrowsArgumentException ()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                _ = UcliOperationMetadata.Create<GenericDiscoverableArgs, GenericDiscoverableResult>(
                    operationName: "ucli.tests.result-contract-mismatch",
                    kind: UcliOperationKind.Query,
                    policy: OperationPolicy.Safe,
                    describeContract: new UcliOperationDescribeContract(
                        "Result contract mismatch operation.",
                        Array.Empty<UcliOperationInputContract>(),
                        new UcliOperationResultContract(
                            emitted: true,
                            resultType: "DifferentResult",
                            description: "Wrong result contract."),
                        new UcliOperationAssuranceContract(
                            Array.Empty<UcliOperationSideEffect>(),
                            mayDirty: false,
                            mayPersist: false,
                            Array.Empty<string>(),
                            UcliOperationPlanMode.ValidationOnly)));
            });
        }

        [Test]
        [Category("Size.Small")]
        public void DiscoverFromTypes_WhenAttributedTypeDoesNotImplementOperation_ThrowsInvalidOperationException ()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                _ = UcliOperationDiscoverer.DiscoverFromTypes(new Type[]
                {
                    typeof(InvalidAttributedType),
                });
            });
        }

        [Test]
        [Category("Size.Small")]
        public void Discover_WhenCurrentDomainContainsInvalidAttributedTestType_IgnoresTestAssembly ()
        {
            var operations = UcliOperationDiscoverer.Discover();

            Assert.That(operations.Count, Is.GreaterThan(0));

            var containsResolveOperation = false;
            for (var i = 0; i < operations.Count; i++)
            {
                if (operations[i].Metadata.OperationName == UcliPrimitiveOperationNames.Resolve)
                {
                    containsResolveOperation = true;
                    break;
                }
            }

            Assert.That(containsResolveOperation, Is.True);
        }

        [Test]
        [Category("Size.Small")]
        public void Discover_WhenBuiltInOperationsAreRead_ReturnsConcreteArgsSchemas ()
        {
            var operations = UcliOperationDiscoverer.Discover();

            var resolveMetadata = FindMetadata(operations, UcliPrimitiveOperationNames.Resolve);
            using var resolveSchemaDocument = JsonDocument.Parse(resolveMetadata.ArgsSchemaJson);
            var resolveProperties = resolveSchemaDocument.RootElement.GetProperty("properties");
            Assert.That(
                resolveProperties.TryGetProperty("globalObjectId", out _),
                Is.True);
            Assert.That(resolveProperties.TryGetProperty("projectAssetPath", out _), Is.True);
            Assert.That(resolveProperties.TryGetProperty("prefab", out _), Is.True);
            Assert.That(resolveProperties.TryGetProperty("componentType", out _), Is.True);
            AssertContainsNoUnsupportedSchemaKeyword(resolveSchemaDocument.RootElement);

            var compEnsureMetadata = FindMetadata(operations, UcliPrimitiveOperationNames.CompEnsure);
            using var compEnsureSchemaDocument = JsonDocument.Parse(compEnsureMetadata.ArgsSchemaJson);
            var compEnsureTargetProperties = compEnsureSchemaDocument.RootElement.GetProperty("properties").GetProperty("target").GetProperty("properties");
            Assert.That(compEnsureTargetProperties.TryGetProperty("prefab", out _), Is.True);
            AssertContainsNoUnsupportedSchemaKeyword(compEnsureSchemaDocument.RootElement);

            var compSetMetadata = FindMetadata(operations, UcliPrimitiveOperationNames.CompSet);
            using var compSetSchemaDocument = JsonDocument.Parse(compSetMetadata.ArgsSchemaJson);
            var compSetProperties = compSetSchemaDocument.RootElement.GetProperty("properties");
            Assert.That(compSetProperties.TryGetProperty("target", out _), Is.True);
            Assert.That(compSetProperties.GetProperty("sets").GetProperty("type").GetString(), Is.EqualTo("array"));
            var compSetTargetProperties = compSetProperties.GetProperty("target").GetProperty("properties");
            Assert.That(compSetTargetProperties.TryGetProperty("scene", out _), Is.True);
            Assert.That(compSetTargetProperties.TryGetProperty("prefab", out _), Is.True);
            Assert.That(compSetTargetProperties.TryGetProperty("componentType", out _), Is.True);
            AssertContainsNoUnsupportedSchemaKeyword(compSetSchemaDocument.RootElement);

            var prefabCreateMetadata = FindMetadata(operations, UcliPrimitiveOperationNames.PrefabCreate);
            using var prefabCreateSchemaDocument = JsonDocument.Parse(prefabCreateMetadata.ArgsSchemaJson);
            var prefabCreateProperties = prefabCreateSchemaDocument.RootElement.GetProperty("properties");
            Assert.That(prefabCreateProperties.TryGetProperty("target", out _), Is.True);
            Assert.That(prefabCreateProperties.TryGetProperty("path", out _), Is.True);
            Assert.That(prefabCreateSchemaDocument.RootElement.GetProperty("required").GetArrayLength(), Is.EqualTo(2));

            var prefabOpenMetadata = FindMetadata(operations, UcliPrimitiveOperationNames.PrefabOpen);
            Assert.That(prefabOpenMetadata.Kind, Is.EqualTo(UcliOperationKind.Command));
            using var prefabOpenSchemaDocument = JsonDocument.Parse(prefabOpenMetadata.ArgsSchemaJson);
            var prefabOpenProperties = prefabOpenSchemaDocument.RootElement.GetProperty("properties");
            Assert.That(prefabOpenProperties.TryGetProperty("path", out _), Is.True);
            Assert.That(prefabOpenSchemaDocument.RootElement.GetProperty("required").GetArrayLength(), Is.EqualTo(1));

            var goCreateMetadata = FindMetadata(operations, UcliPrimitiveOperationNames.GoCreate);
            using var goCreateSchemaDocument = JsonDocument.Parse(goCreateMetadata.ArgsSchemaJson);
            var goCreateParentProperties = goCreateSchemaDocument.RootElement.GetProperty("properties").GetProperty("parent").GetProperty("properties");
            Assert.That(goCreateParentProperties.TryGetProperty("prefab", out _), Is.True);
            AssertContainsNoUnsupportedSchemaKeyword(goCreateSchemaDocument.RootElement);

            var goDeleteMetadata = FindMetadata(operations, UcliPrimitiveOperationNames.GoDelete);
            using var goDeleteSchemaDocument = JsonDocument.Parse(goDeleteMetadata.ArgsSchemaJson);
            var goDeleteTargetProperties = goDeleteSchemaDocument.RootElement.GetProperty("properties").GetProperty("target").GetProperty("properties");
            Assert.That(goDeleteTargetProperties.TryGetProperty("componentType", out _), Is.False);

            var goReparentMetadata = FindMetadata(operations, UcliPrimitiveOperationNames.GoReparent);
            using var goReparentSchemaDocument = JsonDocument.Parse(goReparentMetadata.ArgsSchemaJson);
            var goReparentProperties = goReparentSchemaDocument.RootElement.GetProperty("properties");
            Assert.That(goReparentProperties.GetProperty("target").GetProperty("properties").TryGetProperty("componentType", out _), Is.False);
            Assert.That(goReparentProperties.GetProperty("parent").GetProperty("properties").TryGetProperty("componentType", out _), Is.False);

            var sceneQueryMetadata = FindMetadata(operations, UcliPrimitiveOperationNames.SceneQuery);
            using var sceneQuerySchemaDocument = JsonDocument.Parse(sceneQueryMetadata.ArgsSchemaJson);
            Assert.That(sceneQuerySchemaDocument.RootElement.GetProperty("additionalProperties").GetBoolean(), Is.False);
            var sceneQueryProperties = sceneQuerySchemaDocument.RootElement.GetProperty("properties");
            Assert.That(sceneQueryProperties.TryGetProperty("scene", out _), Is.True);
            Assert.That(sceneQueryProperties.TryGetProperty("pathPrefix", out _), Is.True);
            Assert.That(sceneQueryProperties.TryGetProperty("componentType", out _), Is.True);
            Assert.That(sceneQueryProperties.EnumerateObject().Count(), Is.EqualTo(3));
            Assert.That(sceneQuerySchemaDocument.RootElement.GetProperty("required").GetArrayLength(), Is.EqualTo(1));
            Assert.That(sceneQuerySchemaDocument.RootElement.GetProperty("required")[0].GetString(), Is.EqualTo("scene"));

            var assetCreateMetadata = FindMetadata(operations, UcliPrimitiveOperationNames.AssetCreate);
            using var assetCreateSchemaDocument = JsonDocument.Parse(assetCreateMetadata.ArgsSchemaJson);
            var assetCreateProperties = assetCreateSchemaDocument.RootElement.GetProperty("properties");
            Assert.That(assetCreateProperties.TryGetProperty("type", out _), Is.True);
            Assert.That(assetCreateProperties.TryGetProperty("path", out _), Is.True);

            var assetSetMetadata = FindMetadata(operations, UcliPrimitiveOperationNames.AssetSet);
            using var assetSetSchemaDocument = JsonDocument.Parse(assetSetMetadata.ArgsSchemaJson);
            var assetSetProperties = assetSetSchemaDocument.RootElement.GetProperty("properties");
            Assert.That(assetSetProperties.TryGetProperty("target", out _), Is.True);
            Assert.That(assetSetProperties.GetProperty("sets").GetProperty("type").GetString(), Is.EqualTo("array"));
            var assetSetTargetProperties = assetSetProperties.GetProperty("target").GetProperty("properties");
            Assert.That(assetSetTargetProperties.TryGetProperty("projectAssetPath", out _), Is.True);

            var assetSchemaMetadata = FindMetadata(operations, UcliPrimitiveOperationNames.AssetSchema);
            using var assetSchemaDocument = JsonDocument.Parse(assetSchemaMetadata.ArgsSchemaJson);
            var assetSchemaProperties = assetSchemaDocument.RootElement.GetProperty("properties");
            Assert.That(assetSchemaProperties.TryGetProperty("type", out _), Is.True);
            Assert.That(assetSchemaProperties.TryGetProperty("target", out _), Is.True);
            AssertContainsNoUnsupportedSchemaKeyword(assetSchemaDocument.RootElement);

            var goDescribeMetadata = FindMetadata(operations, UcliPrimitiveOperationNames.GoDescribe);
            using var goDescribeSchemaDocument = JsonDocument.Parse(goDescribeMetadata.ArgsSchemaJson);
            var goDescribeTargetProperties = goDescribeSchemaDocument.RootElement.GetProperty("properties").GetProperty("target").GetProperty("properties");
            Assert.That(goDescribeTargetProperties.TryGetProperty("prefab", out _), Is.True);
            AssertContainsNoUnsupportedSchemaKeyword(goDescribeSchemaDocument.RootElement);
        }

        [Test]
        [Category("Size.Small")]
        public void Discover_WhenAssetsFindOperationIsRead_ReturnsConcreteArgsSchema ()
        {
            var operations = UcliOperationDiscoverer.Discover();

            var metadata = FindMetadata(operations, UcliPrimitiveOperationNames.AssetsFind);
            using var schemaDocument = JsonDocument.Parse(metadata.ArgsSchemaJson);
            var root = schemaDocument.RootElement;
            Assert.That(root.GetProperty("additionalProperties").GetBoolean(), Is.False);
            AssertContainsNoUnsupportedSchemaKeyword(root);
            var properties = root.GetProperty("properties");
            Assert.That(properties.TryGetProperty("type", out var typeProperty), Is.True);
            Assert.That(typeProperty.GetProperty("type").GetString(), Is.EqualTo("string"));
            Assert.That(properties.TryGetProperty("pathPrefix", out var pathPrefixProperty), Is.True);
            Assert.That(pathPrefixProperty.GetProperty("type").GetString(), Is.EqualTo("string"));
            Assert.That(properties.TryGetProperty("nameContains", out var nameContainsProperty), Is.True);
            Assert.That(nameContainsProperty.GetProperty("type").GetString(), Is.EqualTo("string"));
        }

        [Test]
        [Category("Size.Small")]
        public void BuildCatalog_WhenBuiltInOperationsAreExported_RemovesVarSelectorsFromPublicSchemas ()
        {
            var operations = UcliOperationDiscoverer.Discover();

            var snapshot = UcliOperationCatalogSnapshotBuilder.Build(operations);

            var sceneOpenEntry = FindCatalogEntry(snapshot.Catalog.Operations!, UcliPrimitiveOperationNames.SceneOpen);
            Assert.That(sceneOpenEntry.Kind, Is.EqualTo(UcliOperationKindValues.Command));
            Assert.That(sceneOpenEntry.Description, Is.Not.Null.And.Not.Empty);
            Assert.That(sceneOpenEntry.ResultContract, Is.Not.Null);
            Assert.That(sceneOpenEntry.ResultContract!.Emitted, Is.False);
            Assert.That(sceneOpenEntry.Assurance, Is.Not.Null);
            Assert.That(sceneOpenEntry.Assurance!.PlanMode, Is.EqualTo(UcliOperationPlanModeValues.MayCreatePreviewState));

            var projectRefreshEntry = FindCatalogEntry(snapshot.Catalog.Operations!, UcliPrimitiveOperationNames.ProjectRefresh);
            Assert.That(projectRefreshEntry.Kind, Is.EqualTo(UcliOperationKindValues.Command));
            Assert.That(projectRefreshEntry.Assurance, Is.Not.Null);
            Assert.That(projectRefreshEntry.Assurance!.SideEffects, Does.Contain(UcliOperationSideEffectValues.RefreshesAssetDatabase));
            Assert.That(projectRefreshEntry.Assurance.SideEffects, Does.Contain(UcliOperationSideEffectValues.WritesAsset));
            Assert.That(projectRefreshEntry.Assurance.SideEffects, Does.Contain(UcliOperationSideEffectValues.WritesScene));
            Assert.That(projectRefreshEntry.Assurance.SideEffects, Does.Contain(UcliOperationSideEffectValues.WritesPrefab));
            Assert.That(projectRefreshEntry.Assurance.SideEffects, Does.Contain(UcliOperationSideEffectValues.WritesProjectSettings));
            Assert.That(projectRefreshEntry.Assurance.MayDirty, Is.True);
            Assert.That(projectRefreshEntry.Assurance.MayPersist, Is.True);

            var prefabCreateEntry = FindCatalogEntry(snapshot.Catalog.Operations!, UcliPrimitiveOperationNames.PrefabCreate);
            Assert.That(prefabCreateEntry.Assurance, Is.Not.Null);
            Assert.That(prefabCreateEntry.Assurance!.SideEffects, Does.Contain(UcliOperationSideEffectValues.WritesPrefab));
            Assert.That(prefabCreateEntry.Assurance.SideEffects, Does.Contain(UcliOperationSideEffectValues.WritesScene));
            Assert.That(prefabCreateEntry.Assurance.MayDirty, Is.True);
            Assert.That(prefabCreateEntry.Assurance.MayPersist, Is.True);
            Assert.That(prefabCreateEntry.Assurance.TouchedKinds, Does.Contain(IpcExecuteTouchedResourceKindNames.Scene));
            Assert.That(prefabCreateEntry.Assurance.TouchedKinds, Does.Contain(IpcExecuteTouchedResourceKindNames.Prefab));

            var assetSetEntry = FindCatalogEntry(snapshot.Catalog.Operations!, UcliPrimitiveOperationNames.AssetSet);
            Assert.That(assetSetEntry.Assurance, Is.Not.Null);
            Assert.That(assetSetEntry.Assurance!.SideEffects, Does.Contain(UcliOperationSideEffectValues.WritesAsset));
            Assert.That(assetSetEntry.Assurance.SideEffects, Does.Contain(UcliOperationSideEffectValues.WritesProjectSettings));

            var goCreateSchemaJson = FindCatalogSchema(snapshot.Catalog.Operations!, UcliPrimitiveOperationNames.GoCreate);
            using var goCreateSchemaDocument = JsonDocument.Parse(goCreateSchemaJson);
            var goCreateParentProperties = goCreateSchemaDocument.RootElement.GetProperty("properties").GetProperty("parent").GetProperty("properties");
            Assert.That(goCreateParentProperties.TryGetProperty("var", out _), Is.False);
            AssertContainsNoUnsupportedSchemaKeyword(goCreateSchemaDocument.RootElement);

            var goDescribeSchemaJson = FindCatalogSchema(snapshot.Catalog.Operations!, UcliPrimitiveOperationNames.GoDescribe);
            using var goDescribeSchemaDocument = JsonDocument.Parse(goDescribeSchemaJson);
            var goDescribeTargetProperties = goDescribeSchemaDocument.RootElement.GetProperty("properties").GetProperty("target").GetProperty("properties");
            Assert.That(goDescribeTargetProperties.TryGetProperty("var", out _), Is.False);
            AssertContainsNoUnsupportedSchemaKeyword(goDescribeSchemaDocument.RootElement);

            for (var i = 0; i < snapshot.Catalog.Operations!.Count; i++)
            {
                using var schemaDocument = JsonDocument.Parse(snapshot.Catalog.Operations[i].ArgsSchemaJson!);
                AssertContainsNoVarBranch(schemaDocument.RootElement);
                AssertContainsNoVarVariantArgsPath(snapshot.Catalog.Operations[i].Inputs);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void Discover_WhenUcliDefinedAssembliesAreExcluded_ReturnsNoBuiltInOperations ()
        {
            var operations = UcliOperationDiscoverer.Discover(
                new Assembly[]
                {
                    typeof(ResolveOperation).Assembly,
                },
                includeUcliDefinedAssemblies: false,
                includeUserDefinedAssemblies: true);

            Assert.That(operations, Is.Empty);
        }

        [Test]
        [Category("Size.Small")]
        public void Discover_WhenOnlyTestAssemblyIsProvided_ReturnsNoOperations ()
        {
            var operations = UcliOperationDiscoverer.Discover(
                new Assembly[]
                {
                    typeof(UcliOperationDiscovererTests).Assembly,
                },
                includeUcliDefinedAssemblies: true,
                includeUserDefinedAssemblies: true);

            Assert.That(operations, Is.Empty);
        }

        [UcliOperation]
        private sealed class DiscoverableOperation : IUcliOperation
        {
            public UcliOperationMetadata Metadata { get; } = new UcliOperationMetadata(
                operationName: "ucli.tests.discover",
                kind: UcliOperationKind.Query,
                policy: OperationPolicy.Safe,
                describeContract: CreateDescribeContract("ucli.tests.discover"));

            public Task<OperationPhaseStepResult> Validate (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(OperationPhaseStepResult.Success());
            }

            public Task<OperationPhaseStepResult> Plan (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(OperationPhaseStepResult.Success());
            }

            public Task<OperationPhaseStepResult> Call (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(OperationPhaseStepResult.Success());
            }
        }

        [UcliOperation]
        private sealed class GenericDiscoverableOperation : UcliOperation<GenericDiscoverableArgs, UcliNoResult>
        {
            public override UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<GenericDiscoverableArgs, UcliNoResult>(
                operationName: "ucli.tests.generic-discover",
                kind: UcliOperationKind.Query,
                policy: OperationPolicy.Safe,
                description: "Generic operation used to verify custom operation authoring.",
                assurance: new UcliOperationAssuranceContract(
                    Array.Empty<UcliOperationSideEffect>(),
                    mayDirty: false,
                    mayPersist: false,
                    Array.Empty<string>(),
                    UcliOperationPlanMode.ValidationOnly));

            protected override Task<OperationPhaseStepResult> Validate (
                NormalizedOperation operation,
                GenericDiscoverableArgs args,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(OperationPhaseStepResult.Success());
            }

            protected override Task<OperationPhaseStepResult> Plan (
                NormalizedOperation operation,
                GenericDiscoverableArgs args,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(OperationPhaseStepResult.Success());
            }

            protected override Task<OperationPhaseStepResult> Call (
                NormalizedOperation operation,
                GenericDiscoverableArgs args,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(OperationPhaseStepResult.Success());
            }
        }

        [UcliOperation]
        private sealed class MetadataArgsMismatchOperation : UcliOperation<GenericDiscoverableArgs, UcliNoResult>
        {
            public override UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<UcliEmptyArgs, UcliNoResult>(
                operationName: "ucli.tests.args-mismatch",
                kind: UcliOperationKind.Query,
                policy: OperationPolicy.Safe,
                description: "Metadata args mismatch operation.",
                assurance: new UcliOperationAssuranceContract(
                    Array.Empty<UcliOperationSideEffect>(),
                    mayDirty: false,
                    mayPersist: false,
                    Array.Empty<string>(),
                    UcliOperationPlanMode.ValidationOnly));

            protected override Task<OperationPhaseStepResult> Validate (
                NormalizedOperation operation,
                GenericDiscoverableArgs args,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(OperationPhaseStepResult.Success());
            }

            protected override Task<OperationPhaseStepResult> Plan (
                NormalizedOperation operation,
                GenericDiscoverableArgs args,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(OperationPhaseStepResult.Success());
            }

            protected override Task<OperationPhaseStepResult> Call (
                NormalizedOperation operation,
                GenericDiscoverableArgs args,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(OperationPhaseStepResult.Success());
            }
        }

        [UcliOperation]
        private sealed class MetadataResultMismatchOperation : UcliOperation<GenericDiscoverableArgs, GenericDiscoverableResult>
        {
            public override UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<GenericDiscoverableArgs, UcliNoResult>(
                operationName: "ucli.tests.result-mismatch",
                kind: UcliOperationKind.Query,
                policy: OperationPolicy.Safe,
                description: "Metadata result mismatch operation.",
                assurance: new UcliOperationAssuranceContract(
                    Array.Empty<UcliOperationSideEffect>(),
                    mayDirty: false,
                    mayPersist: false,
                    Array.Empty<string>(),
                    UcliOperationPlanMode.ValidationOnly));

            protected override Task<OperationPhaseStepResult> Validate (
                NormalizedOperation operation,
                GenericDiscoverableArgs args,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(OperationPhaseStepResult.Success());
            }

            protected override Task<OperationPhaseStepResult> Plan (
                NormalizedOperation operation,
                GenericDiscoverableArgs args,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(OperationPhaseStepResult.Success());
            }

            protected override Task<OperationPhaseStepResult> Call (
                NormalizedOperation operation,
                GenericDiscoverableArgs args,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(OperationPhaseStepResult.Success());
            }
        }

        [UcliDescription("Generic discoverable operation args.")]
        private sealed class GenericDiscoverableArgs
        {
            [UcliRequired]
            [UcliDescription("Scene asset path to inspect.")]
            public SceneAssetPath? Path { get; set; }
        }

        [UcliDescription("Generic discoverable operation result.")]
        private sealed class GenericDiscoverableResult
        {
        }

        [UcliOperation]
        private sealed class InvalidAttributedType
        {
        }

        private static UcliOperationMetadata FindMetadata (
            IReadOnlyList<UcliOperationRegistration> operations,
            string operationName)
        {
            for (var i = 0; i < operations.Count; i++)
            {
                if (operations[i].Metadata.OperationName == operationName)
                {
                    return operations[i].Metadata;
                }
            }

            Assert.Fail($"Operation metadata was not discovered: {operationName}");
            return null!;
        }

        private static string FindCatalogSchema (
            IReadOnlyList<IndexOpEntryJsonContract> operations,
            string operationName)
        {
            return FindCatalogEntry(operations, operationName).ArgsSchemaJson!;
        }

        private static IndexOpEntryJsonContract FindCatalogEntry (
            IReadOnlyList<IndexOpEntryJsonContract> operations,
            string operationName)
        {
            for (var i = 0; i < operations.Count; i++)
            {
                if (operations[i].Name == operationName)
                {
                    return operations[i];
                }
            }

            Assert.Fail($"Catalog operation was not discovered: {operationName}");
            return null!;
        }

        private static void AssertContainsNoVarBranch (JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        Assert.That(property.Name, Is.Not.EqualTo("var"));
                        AssertContainsNoVarBranch(property.Value);
                    }

                    return;

                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                        {
                            Assert.That(item.GetString(), Is.Not.EqualTo("var"));
                        }
                        else
                        {
                            AssertContainsNoVarBranch(item);
                        }
                    }

                    return;

                default:
                    return;
            }
        }

        private static void AssertContainsNoVarVariantArgsPath (IReadOnlyList<UcliOperationInputContract>? inputs)
        {
            if (inputs == null)
            {
                return;
            }

            for (var inputIndex = 0; inputIndex < inputs.Count; inputIndex++)
            {
                var variants = inputs[inputIndex].Variants;
                if (variants == null)
                {
                    continue;
                }

                for (var variantIndex = 0; variantIndex < variants.Count; variantIndex++)
                {
                    var argsPaths = variants[variantIndex].ArgsPaths;
                    if (argsPaths == null)
                    {
                        continue;
                    }

                    for (var pathIndex = 0; pathIndex < argsPaths.Count; pathIndex++)
                    {
                        Assert.That(argsPaths[pathIndex], Does.Not.EndWith(".var"));
                    }
                }
            }
        }

        private static void AssertContainsNoUnsupportedSchemaKeyword (JsonElement element)
        {
            AssertContainsOnlySupportedSchemaKeywords(element);
        }

        private static void AssertContainsOnlySupportedSchemaKeywords (JsonElement schemaNode)
        {
            Assert.That(schemaNode.ValueKind, Is.EqualTo(JsonValueKind.Object));
            foreach (var property in schemaNode.EnumerateObject())
            {
                Assert.That(IsSupportedSchemaKeyword(property.Name), Is.True, $"Unsupported schema keyword: {property.Name}");
                switch (property.Name)
                {
                    case "properties":
                    case "$defs":
                        AssertSchemaMapUsesSupportedKeywords(property.Value);
                        break;

                    case "items":
                        AssertContainsOnlySupportedSchemaKeywords(property.Value);
                        break;
                }
            }
        }

        private static void AssertSchemaMapUsesSupportedKeywords (JsonElement mapElement)
        {
            Assert.That(mapElement.ValueKind, Is.EqualTo(JsonValueKind.Object));
            foreach (var entry in mapElement.EnumerateObject())
            {
                AssertContainsOnlySupportedSchemaKeywords(entry.Value);
            }
        }

        private static bool IsSupportedSchemaKeyword (string keyword)
        {
            switch (keyword)
            {
                case "type":
                case "properties":
                case "required":
                case "additionalProperties":
                case "items":
                case "$ref":
                case "$defs":
                    return true;

                default:
                    return false;
            }
        }

        private static UcliOperationDescribeContract CreateDescribeContract (string operationName)
        {
            return new UcliOperationDescribeContract(
                $"{operationName} test operation.",
                Array.Empty<UcliOperationInputContract>(),
                UcliOperationResultContract.NoResult("This test operation does not emit operation-specific result data."),
                new UcliOperationAssuranceContract(
                    Array.Empty<UcliOperationSideEffect>(),
                    mayDirty: false,
                    mayPersist: false,
                    Array.Empty<string>(),
                    UcliOperationPlanMode.ValidationOnly));
        }
    }
}
