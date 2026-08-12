using System.Linq;
using System.Reflection;
using MackySoft.Ucli.Unity.Execution.CsEval;
using NUnit.Framework;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UcliCsEvalContextPublicApiTests
    {
        [Test]
        [Category("Size.Small")]
        public void PublicApi_ContainsOnlyTheDocumentedEvalSurface ()
        {
            var type = typeof(UcliCsEvalContext);

            Assert.That(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance), Is.Empty);
            Assert.That(
                type.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
                    .Select(static property => property.Name),
                Is.EquivalentTo(new[] { "CancellationToken" }));
            Assert.That(
                type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
                    .Where(static method => !method.IsSpecialName)
                    .Select(static method => method.Name),
                Is.EquivalentTo(new[]
                {
                    "Log",
                    "TouchScene",
                    "TouchPrefab",
                    "TouchAsset",
                    "TouchProjectSettings",
                    "DeclareNoChanges",
                }));
            Assert.That(typeof(UcliCsEvalLogLevel).IsEnum, Is.True);
        }
    }
}
