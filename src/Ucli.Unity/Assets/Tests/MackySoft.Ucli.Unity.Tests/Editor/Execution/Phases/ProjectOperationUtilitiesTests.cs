using System.Collections.Generic;
using System.Text.Json;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Unity.Execution.Phases;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class ProjectOperationUtilitiesTests
    {
        [Test]
        [Category("Size.Small")]
        public void TryValidateEmptyArguments_WhenArgsContainUnknownProperty_ReturnsFalse ()
        {
            var args = JsonSerializer.SerializeToElement(new
            {
                unexpected = true,
            });

            var result = ProjectOperationUtilities.TryValidateEmptyArguments(args, out var errorMessage);

            Assert.That(result, Is.False);
            Assert.That(errorMessage, Is.EqualTo("Operation 'args' contains an unknown property: unexpected."));
        }

        [Test]
        [Category("Size.Small")]
        public void CreateTouchedResources_WhenPathsIncludeMetaAndProjectSettings_NormalizesAndClassifiesPaths ()
        {
            var touched = ProjectOperationUtilities.CreateTouchedResources(
                new[]
                {
                    "Assets/Scenes/Main.unity",
                    "Assets/Prefabs/Enemy.prefab.meta",
                    "Assets/Data/Config.asset",
                    "Assets/Data/Config.asset.meta",
                    "./Assets/Data/Config.asset.meta",
                    "Packages/com.example/ignored.asset",
                },
                new[]
                {
                    "ProjectSettings/TagManager.asset",
                    "./ProjectSettings/TagManager.asset",
                });

            Assert.That(touched.Count, Is.EqualTo(4));
            Assert.That(touched[0].Path, Is.EqualTo("Assets/Data/Config.asset"));
            Assert.That(touched[0].Kind, Is.EqualTo(UcliTouchedResourceKind.Asset));
            Assert.That(touched[1].Path, Is.EqualTo("Assets/Prefabs/Enemy.prefab"));
            Assert.That(touched[1].Kind, Is.EqualTo(UcliTouchedResourceKind.Prefab));
            Assert.That(touched[2].Path, Is.EqualTo("Assets/Scenes/Main.unity"));
            Assert.That(touched[2].Kind, Is.EqualTo(UcliTouchedResourceKind.Scene));
            Assert.That(touched[3].Path, Is.EqualTo("ProjectSettings/TagManager.asset"));
            Assert.That(touched[3].Kind, Is.EqualTo(UcliTouchedResourceKind.ProjectSettings));
        }

        [Test]
        [Category("Size.Small")]
        public void GetChangedProjectSettingsPaths_WhenSnapshotsDiffer_ReturnsStableUnion ()
        {
            var before = new Dictionary<string, ProjectOperationFileSnapshot>(System.StringComparer.Ordinal)
            {
                ["ProjectSettings/A.asset"] = new ProjectOperationFileSnapshot(10, 100),
                ["ProjectSettings/B.asset"] = new ProjectOperationFileSnapshot(20, 200),
            };
            var after = new Dictionary<string, ProjectOperationFileSnapshot>(System.StringComparer.Ordinal)
            {
                ["ProjectSettings/B.asset"] = new ProjectOperationFileSnapshot(21, 201),
                ["ProjectSettings/C.asset"] = new ProjectOperationFileSnapshot(30, 300),
            };

            var changedPaths = ProjectOperationUtilities.GetChangedProjectSettingsPaths(before, after);

            CollectionAssert.AreEqual(
                new[]
                {
                    "ProjectSettings/A.asset",
                    "ProjectSettings/B.asset",
                    "ProjectSettings/C.asset",
                },
                changedPaths);
        }
    }
}
