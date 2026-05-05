using MackySoft.Ucli.Hosting.Supervisor;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class InternalSupervisorExecutionRunnerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void BuildServiceProvider_ResolvesSupervisorHostThroughSharedCompositionRoot ()
    {
        using var serviceProvider = InternalSupervisorExecutionRunner.BuildServiceProvider();

        var supervisorHost = serviceProvider.GetRequiredService<SupervisorHost>();

        Assert.NotNull(supervisorHost);
    }
}
