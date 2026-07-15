using System;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Unity.Execution.Phases;
using NUnit.Framework;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class OperationResourceValueTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("/Assets/Value.asset")]
        [TestCase("../Assets/Value.asset")]
        [TestCase("Assets//Value.asset")]
        [TestCase("Assets/./Value.asset")]
        [TestCase(" Assets/Value.asset")]
        [TestCase("Assets/Value.asset ")]
        [Category("Size.Small")]
        public void Constructors_WhenPathIsNotProjectRelative_RejectInvalidValue (string? path)
        {
            Assert.Throws<ArgumentException>(() => new OperationResource(UcliTouchedResourceKind.Asset, path!));
            Assert.Throws<ArgumentException>(() => new OperationTouch(UcliTouchedResourceKind.Asset, path!, assetGuid: null));
        }

        [Test]
        [Category("Size.Small")]
        public void Constructors_WhenPathUsesBackslashes_StoreSlashSeparatedValue ()
        {
            var resource = new OperationResource(UcliTouchedResourceKind.Asset, "Assets\\Folder\\Value.asset");
            var touch = new OperationTouch(UcliTouchedResourceKind.Asset, "Assets\\Folder\\Value.asset", assetGuid: null);

            Assert.That(resource.Path, Is.EqualTo("Assets/Folder/Value.asset"));
            Assert.That(touch.Path, Is.EqualTo("Assets/Folder/Value.asset"));
        }

        [Test]
        [Category("Size.Small")]
        public void OperationTouch_WhenAssetGuidIsEmpty_RejectsInvalidValue ()
        {
            Assert.Throws<ArgumentException>(() => new OperationTouch(
                UcliTouchedResourceKind.Asset,
                "Assets/Value.asset",
                Guid.Empty));
        }

        [TestCase((UcliTouchedResourceKind)0)]
        [TestCase((UcliTouchedResourceKind)999)]
        [Category("Size.Small")]
        public void Constructors_WhenKindIsUnsupported_RejectInvalidValue (UcliTouchedResourceKind kind)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new OperationResource(kind, "Assets/Value.asset"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new OperationTouch(kind, "Assets/Value.asset", assetGuid: null));
        }
    }
}
