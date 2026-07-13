using System.Reflection;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStartResultConstructionInvariantTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ResultTypes_ExposeOnlyCaseFactoryConstructionPaths ()
    {
        Type[] resultTypes =
        [
            typeof(DaemonStartResult),
            typeof(DaemonGuiSessionRegistrationWaitResult),
        ];

        for (var i = 0; i < resultTypes.Length; i++)
        {
            var constructors = resultTypes[i].GetConstructors(
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic);
            var properties = resultTypes[i].GetProperties(
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.DeclaredOnly);

            Assert.DoesNotContain(constructors, static constructor => !constructor.IsPrivate);
            Assert.DoesNotContain(properties, static property => property.SetMethod is not null);
        }
    }
}
