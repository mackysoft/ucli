using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
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
                if (operations[i].Metadata.OperationName == MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.Resolve)
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

            var resolveMetadata = FindMetadata(operations, MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.Resolve);
            using var resolveSchemaDocument = JsonDocument.Parse(resolveMetadata.ArgsSchemaJson);
            var resolveProperties = resolveSchemaDocument.RootElement.GetProperty("properties");
            Assert.That(
                resolveProperties.TryGetProperty("globalObjectId", out _),
                Is.True);
            Assert.That(resolveProperties.TryGetProperty("projectAssetPath", out _), Is.True);
            Assert.That(resolveProperties.TryGetProperty("prefab", out _), Is.True);
            Assert.That(resolveProperties.TryGetProperty("componentType", out _), Is.True);
            Assert.That(resolveSchemaDocument.RootElement.GetProperty("oneOf").GetArrayLength(), Is.EqualTo(6));
            var resolveAllOf = resolveSchemaDocument.RootElement.GetProperty("allOf");
            Assert.That(resolveAllOf.GetArrayLength(), Is.EqualTo(1));
            Assert.That(
                resolveAllOf[0].GetProperty("if").GetProperty("required")[0].GetString(),
                Is.EqualTo("componentType"));
            var resolveComponentTypeTargets = resolveAllOf[0].GetProperty("then").GetProperty("oneOf");
            Assert.That(resolveComponentTypeTargets.GetArrayLength(), Is.EqualTo(1));
            Assert.That(resolveComponentTypeTargets[0].GetProperty("required")[0].GetString(), Is.EqualTo("scene"));

            var compEnsureMetadata = FindMetadata(operations, MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.CompEnsure);
            using var compEnsureSchemaDocument = JsonDocument.Parse(compEnsureMetadata.ArgsSchemaJson);
            var compEnsureTargetProperties = compEnsureSchemaDocument.RootElement.GetProperty("properties").GetProperty("target").GetProperty("properties");
            Assert.That(compEnsureTargetProperties.TryGetProperty("prefab", out _), Is.True);
            Assert.That(compEnsureSchemaDocument.RootElement.GetProperty("properties").GetProperty("target").GetProperty("oneOf").GetArrayLength(), Is.EqualTo(4));

            var compSetMetadata = FindMetadata(operations, MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.CompSet);
            using var compSetSchemaDocument = JsonDocument.Parse(compSetMetadata.ArgsSchemaJson);
            var compSetProperties = compSetSchemaDocument.RootElement.GetProperty("properties");
            Assert.That(compSetProperties.TryGetProperty("target", out _), Is.True);
            Assert.That(compSetProperties.GetProperty("sets").GetProperty("minItems").GetInt32(), Is.EqualTo(1));
            var compSetTargetProperties = compSetProperties.GetProperty("target").GetProperty("properties");
            Assert.That(compSetTargetProperties.TryGetProperty("scene", out _), Is.True);
            Assert.That(compSetTargetProperties.TryGetProperty("prefab", out _), Is.True);
            Assert.That(compSetTargetProperties.TryGetProperty("componentType", out _), Is.True);
            Assert.That(compSetProperties.GetProperty("target").GetProperty("oneOf").GetArrayLength(), Is.EqualTo(4));

            var prefabCreateMetadata = FindMetadata(operations, MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.PrefabCreate);
            using var prefabCreateSchemaDocument = JsonDocument.Parse(prefabCreateMetadata.ArgsSchemaJson);
            var prefabCreateProperties = prefabCreateSchemaDocument.RootElement.GetProperty("properties");
            Assert.That(prefabCreateProperties.TryGetProperty("target", out _), Is.True);
            Assert.That(prefabCreateProperties.TryGetProperty("path", out _), Is.True);
            Assert.That(prefabCreateSchemaDocument.RootElement.GetProperty("required").GetArrayLength(), Is.EqualTo(2));

            var prefabOpenMetadata = FindMetadata(operations, MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.PrefabOpen);
            using var prefabOpenSchemaDocument = JsonDocument.Parse(prefabOpenMetadata.ArgsSchemaJson);
            var prefabOpenProperties = prefabOpenSchemaDocument.RootElement.GetProperty("properties");
            Assert.That(prefabOpenProperties.TryGetProperty("path", out _), Is.True);
            Assert.That(prefabOpenSchemaDocument.RootElement.GetProperty("required").GetArrayLength(), Is.EqualTo(1));

            var goDeleteMetadata = FindMetadata(operations, MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDelete);
            using var goDeleteSchemaDocument = JsonDocument.Parse(goDeleteMetadata.ArgsSchemaJson);
            var goDeleteTargetProperties = goDeleteSchemaDocument.RootElement.GetProperty("properties").GetProperty("target").GetProperty("properties");
            Assert.That(goDeleteTargetProperties.TryGetProperty("componentType", out _), Is.False);

            var goReparentMetadata = FindMetadata(operations, MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoReparent);
            using var goReparentSchemaDocument = JsonDocument.Parse(goReparentMetadata.ArgsSchemaJson);
            var goReparentProperties = goReparentSchemaDocument.RootElement.GetProperty("properties");
            Assert.That(goReparentProperties.GetProperty("target").GetProperty("properties").TryGetProperty("componentType", out _), Is.False);
            Assert.That(goReparentProperties.GetProperty("parent").GetProperty("properties").TryGetProperty("componentType", out _), Is.False);

            var sceneQueryMetadata = FindMetadata(operations, MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneQuery);
            using var sceneQuerySchemaDocument = JsonDocument.Parse(sceneQueryMetadata.ArgsSchemaJson);
            Assert.That(sceneQuerySchemaDocument.RootElement.GetProperty("additionalProperties").GetBoolean(), Is.False);
            var sceneQueryProperties = sceneQuerySchemaDocument.RootElement.GetProperty("properties");
            Assert.That(sceneQueryProperties.TryGetProperty("scene", out _), Is.True);
            Assert.That(sceneQueryProperties.TryGetProperty("pathPrefix", out _), Is.True);
            Assert.That(sceneQueryProperties.TryGetProperty("componentType", out _), Is.True);
            Assert.That(sceneQueryProperties.EnumerateObject().Count(), Is.EqualTo(3));
            Assert.That(sceneQuerySchemaDocument.RootElement.GetProperty("required").GetArrayLength(), Is.EqualTo(1));
            Assert.That(sceneQuerySchemaDocument.RootElement.GetProperty("required")[0].GetString(), Is.EqualTo("scene"));

            var assetCreateMetadata = FindMetadata(operations, MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.AssetCreate);
            using var assetCreateSchemaDocument = JsonDocument.Parse(assetCreateMetadata.ArgsSchemaJson);
            var assetCreateProperties = assetCreateSchemaDocument.RootElement.GetProperty("properties");
            Assert.That(assetCreateProperties.TryGetProperty("type", out _), Is.True);
            Assert.That(assetCreateProperties.TryGetProperty("path", out _), Is.True);

            var assetSetMetadata = FindMetadata(operations, MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.AssetSet);
            using var assetSetSchemaDocument = JsonDocument.Parse(assetSetMetadata.ArgsSchemaJson);
            var assetSetProperties = assetSetSchemaDocument.RootElement.GetProperty("properties");
            Assert.That(assetSetProperties.TryGetProperty("target", out _), Is.True);
            Assert.That(assetSetProperties.GetProperty("sets").GetProperty("minItems").GetInt32(), Is.EqualTo(1));
            var assetSetTargetProperties = assetSetProperties.GetProperty("target").GetProperty("properties");
            Assert.That(assetSetTargetProperties.TryGetProperty("projectAssetPath", out _), Is.True);

            var assetSchemaMetadata = FindMetadata(operations, MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.AssetSchema);
            using var assetSchemaDocument = JsonDocument.Parse(assetSchemaMetadata.ArgsSchemaJson);
            Assert.That(assetSchemaDocument.RootElement.GetProperty("oneOf").GetArrayLength(), Is.EqualTo(2));
            var assetSchemaTargetProperties = assetSchemaDocument.RootElement.GetProperty("properties").GetProperty("target").GetProperty("properties");
            Assert.That(assetSchemaTargetProperties.TryGetProperty("projectAssetPath", out _), Is.True);
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
                policy: OperationPolicy.Safe);

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
    }
}
