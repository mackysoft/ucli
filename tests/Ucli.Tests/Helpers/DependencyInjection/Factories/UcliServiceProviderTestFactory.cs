using MackySoft.Ucli.Application;
using MackySoft.Ucli.Hosting.Composition.Common;
using MackySoft.Ucli.Hosting.Composition.Features;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Tests;

internal static class UcliServiceProviderTestFactory
{
    public static ServiceProvider CreateCore ()
    {
        var services = new ServiceCollection();
        services.AddUcliServices();
        return services.BuildServiceProvider();
    }

    public static ServiceProvider CreateApplication ()
    {
        var services = new ServiceCollection();
        services.AddUcliApplicationServices();
        return services.BuildServiceProvider();
    }

    public static ServiceProvider CreateSkillsFeature ()
    {
        var services = new ServiceCollection();
        services.AddUcliSkillsFeatureServices();
        return services.BuildServiceProvider();
    }
}
