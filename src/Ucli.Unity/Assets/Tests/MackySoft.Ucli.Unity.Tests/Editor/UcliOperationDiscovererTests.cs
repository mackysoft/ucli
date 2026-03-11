using System;
using System.Collections.Generic;
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
                if (operations[i].Metadata.OperationName == "ucli.resolve")
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

            var resolveMetadata = FindMetadata(operations, "ucli.resolve");
            using var resolveSchemaDocument = JsonDocument.Parse(resolveMetadata.ArgsSchemaJson);
            Assert.That(
                resolveSchemaDocument.RootElement.GetProperty("properties").TryGetProperty("globalObjectId", out _),
                Is.True);
            Assert.That(resolveSchemaDocument.RootElement.GetProperty("oneOf").GetArrayLength(), Is.EqualTo(4));

            var compSetMetadata = FindMetadata(operations, "ucli.comp.set");
            using var compSetSchemaDocument = JsonDocument.Parse(compSetMetadata.ArgsSchemaJson);
            var compSetProperties = compSetSchemaDocument.RootElement.GetProperty("properties");
            Assert.That(compSetProperties.TryGetProperty("target", out _), Is.True);
            Assert.That(compSetProperties.GetProperty("sets").GetProperty("minItems").GetInt32(), Is.EqualTo(1));

            var prefabCreateMetadata = FindMetadata(operations, "ucli.prefab.create");
            using var prefabCreateSchemaDocument = JsonDocument.Parse(prefabCreateMetadata.ArgsSchemaJson);
            var prefabCreateProperties = prefabCreateSchemaDocument.RootElement.GetProperty("properties");
            Assert.That(prefabCreateProperties.TryGetProperty("target", out _), Is.True);
            Assert.That(prefabCreateProperties.TryGetProperty("path", out _), Is.True);
            Assert.That(prefabCreateSchemaDocument.RootElement.GetProperty("required").GetArrayLength(), Is.EqualTo(2));

            var prefabOpenMetadata = FindMetadata(operations, "ucli.prefab.open");
            using var prefabOpenSchemaDocument = JsonDocument.Parse(prefabOpenMetadata.ArgsSchemaJson);
            var prefabOpenProperties = prefabOpenSchemaDocument.RootElement.GetProperty("properties");
            Assert.That(prefabOpenProperties.TryGetProperty("path", out _), Is.True);
            Assert.That(prefabOpenSchemaDocument.RootElement.GetProperty("required").GetArrayLength(), Is.EqualTo(1));

            var assetCreateMetadata = FindMetadata(operations, "ucli.asset.create");
            using var assetCreateSchemaDocument = JsonDocument.Parse(assetCreateMetadata.ArgsSchemaJson);
            var assetCreateProperties = assetCreateSchemaDocument.RootElement.GetProperty("properties");
            Assert.That(assetCreateProperties.TryGetProperty("type", out _), Is.True);
            Assert.That(assetCreateProperties.TryGetProperty("path", out _), Is.True);

            var assetSetMetadata = FindMetadata(operations, "ucli.asset.set");
            using var assetSetSchemaDocument = JsonDocument.Parse(assetSetMetadata.ArgsSchemaJson);
            var assetSetProperties = assetSetSchemaDocument.RootElement.GetProperty("properties");
            Assert.That(assetSetProperties.TryGetProperty("target", out _), Is.True);
            Assert.That(assetSetProperties.GetProperty("sets").GetProperty("minItems").GetInt32(), Is.EqualTo(1));

            var assetSchemaMetadata = FindMetadata(operations, "ucli.asset.schema");
            using var assetSchemaDocument = JsonDocument.Parse(assetSchemaMetadata.ArgsSchemaJson);
            Assert.That(assetSchemaDocument.RootElement.GetProperty("oneOf").GetArrayLength(), Is.EqualTo(2));
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