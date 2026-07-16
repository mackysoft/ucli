using System.Reflection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStartResultConstructionInvariantTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Failure_WhenDaemonStatusHasNoContractLiteral_Throws ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DaemonStartResult.Failure(
            ExecutionError.InternalError("failure"),
            daemonStatus: (DaemonStatusKind)int.MaxValue));
    }

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
