using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc;

public sealed class UcliOperationCodeContractBuilderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void CreateCSharp_WhenApiTypeHasDescriptions_ReturnsCodeContract ()
    {
        var codeContract = UcliOperationCodeContractBuilder.CreateCSharp(
            "public static object? Run(SampleCodeContext context)",
            requiredStatic: true,
            new[] { typeof(SampleCodeContext) },
            "null or a JSON-serializable value.",
            new[] { typeof(SampleCodeContext) });

        Assert.Equal("csharp", codeContract.Language);
        Assert.NotNull(codeContract.EntryPoint);
        Assert.Equal("public static object? Run(SampleCodeContext context)", codeContract.EntryPoint!.Signature);
        Assert.Equal(typeof(SampleCodeContext).FullName, Assert.Single(codeContract.EntryPoint.ParameterTypes!));

        var apiType = Assert.Single(codeContract.ApiTypes!);
        Assert.Equal(nameof(SampleCodeContext), apiType.Name);
        Assert.Equal(typeof(SampleCodeContext).FullName, apiType.FullName);
        Assert.Equal("Sample code context.", apiType.Description);

        var property = Assert.Single(apiType.Members!, member => member.Name == nameof(SampleCodeContext.Value));
        Assert.Equal(UcliCodeApiMemberKindValues.Property, property.Kind);
        Assert.Equal("System.String", property.Type);
        Assert.Equal("Sample value.", property.Description);

        var method = Assert.Single(apiType.Members!, member => member.Name == nameof(SampleCodeContext.Log));
        Assert.Equal(UcliCodeApiMemberKindValues.Method, method.Kind);
        Assert.Equal("void", method.ReturnType);
        Assert.Equal("Records a log message.", method.Description);
        Assert.Equal("Log message.", Assert.Single(method.Parameters!).Description);
    }

    [UcliDescription("Sample code context.")]
    private sealed class SampleCodeContext
    {
        [UcliDescription("Sample value.")]
        public string Value => string.Empty;

        [UcliDescription("Records a log message.")]
        public void Log ([UcliDescription("Log message.")] string message)
        {
        }
    }
}
