using System;
using System.Globalization;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.PlanToken;
using MackySoft.Ucli.Unity.Ipc;
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
            Assert.That(snapshot.DomainReloadGeneration, Is.Not.EqualTo("na"));

            var currentFingerprint = PlanTokenStateFingerprintCalculator.Compute(
                snapshot,
                Array.Empty<OperationPhaseTrace>());
            var nextDomainReloadGeneration = (
                    int.Parse(snapshot.DomainReloadGeneration, CultureInfo.InvariantCulture) + 1)
                .ToString(CultureInfo.InvariantCulture);
            var changedSnapshot = new PlanTokenEnvironmentSnapshot(
                projectRoot: snapshot.ProjectRoot,
                repositoryRoot: snapshot.RepositoryRoot,
                projectFingerprint: snapshot.ProjectFingerprint,
                unityVersion: snapshot.UnityVersion,
                compileState: snapshot.CompileState,
                domainReloadGeneration: nextDomainReloadGeneration);
            var changedFingerprint = PlanTokenStateFingerprintCalculator.Compute(
                changedSnapshot,
                Array.Empty<OperationPhaseTrace>());

            Assert.That(changedFingerprint, Is.Not.EqualTo(currentFingerprint));
        }
    }
}
