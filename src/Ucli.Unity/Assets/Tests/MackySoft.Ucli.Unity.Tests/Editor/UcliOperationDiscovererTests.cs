using System;
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
    }
}
