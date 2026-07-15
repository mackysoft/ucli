using MackySoft.Ucli.Application.Features.Assurance.Build.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;

namespace MackySoft.Ucli.Application.Tests.Execution;

public sealed class ExplicitStateConstructorTests
{
    public static TheoryData<Type> ExplicitStateContractTypes => new()
    {
        typeof(ApplicationFailure),
        typeof(BuildEvidenceOutput),
        typeof(ReadyLifecycleOutput),
        typeof(DaemonStatusExecutionOutput),
        typeof(StatusDaemonObservation),
        typeof(StatusExecutionOutput),
    };

    [Theory]
    [MemberData(nameof(ExplicitStateContractTypes))]
    [Trait("Size", "Small")]
    public void Constructor_ParametersHaveNoDefaultValues (Type contractType)
    {
        var constructor = Assert.Single(contractType.GetConstructors());

        Assert.All(constructor.GetParameters(), static parameter =>
        {
            Assert.False(parameter.IsOptional);
            Assert.False(parameter.HasDefaultValue);
        });
    }
}
