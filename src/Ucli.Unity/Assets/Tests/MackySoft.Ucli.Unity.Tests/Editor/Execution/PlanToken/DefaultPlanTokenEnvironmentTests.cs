using System;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.PlanToken;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class DefaultPlanTokenEnvironmentTests
    {
        [Test]
        [Category("Size.Small")]
        public void Capture_WhenDomainReloadGenerationChanges_StateFingerprintChanges ()
        {
            var environment = new DefaultPlanTokenEnvironment();
            var snapshot = environment.Capture();

            Assert.That(snapshot.DomainReloadGeneration, Is.EqualTo(UnityEditorReadinessGate.CurrentDomainReloadGeneration));
            Assert.That(snapshot.DomainReloadGeneration, Is.GreaterThanOrEqualTo(0));

            var currentFingerprint = PlanTokenStateFingerprintCalculator.Compute(
                snapshot,
                Array.Empty<OperationPhaseTrace>());
            var nextDomainReloadGeneration = snapshot.DomainReloadGeneration + 1;
            var changedFingerprint = PlanTokenStateFingerprintCalculator.Compute(
                snapshot with
                {
                    DomainReloadGeneration = nextDomainReloadGeneration,
                },
                Array.Empty<OperationPhaseTrace>());

            Assert.That(changedFingerprint, Is.Not.EqualTo(currentFingerprint));
        }
    }
}
