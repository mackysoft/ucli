using MackySoft.Ucli.Infrastructure.Project;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Project;
using NUnit.Framework;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityProjectIdentityFactoryTests
    {
        [Test]
        [Category("Size.Small")]
        public void Create_WithCurrentProjectFingerprint_ReturnsCurrentSessionIdentity ()
        {
            var projectPath = UnityProjectPathResolver.ResolveProjectRootPath();
            var storageRoot = UcliStoragePathResolver.ResolveStorageRoot(projectPath);
            var projectFingerprint = UnityProjectFingerprintCalculator.Create(storageRoot, projectPath);

            var identity = UnityProjectIdentityFactory.Create(projectFingerprint);

            Assert.That(identity.ProjectPath, Is.EqualTo(projectPath));
            Assert.That(identity.ProjectFingerprint, Is.EqualTo(projectFingerprint));
            Assert.That(identity.IpcIdentity.ProjectPath, Is.EqualTo(projectPath.Value));
            Assert.That(identity.IpcIdentity.UnityVersion, Is.EqualTo(Application.unityVersion));
        }

        [Test]
        [Category("Size.Small")]
        public void Create_WhenExpectedFingerprintDoesNotMatchCurrentProject_Throws ()
        {
            var exception = Assert.Throws<System.ArgumentException>(() =>
                UnityProjectIdentityFactory.Create(
                    ProjectFingerprintTestFactory.Create("mismatched-project-fingerprint")));

            Assert.That(exception!.ParamName, Is.EqualTo("expectedProjectFingerprint"));
        }
    }
}
