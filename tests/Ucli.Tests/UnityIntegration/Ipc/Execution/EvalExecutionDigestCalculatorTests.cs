using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Execution;

namespace MackySoft.Ucli.Tests.UnityIntegration.Ipc.Execution;

public sealed class EvalExecutionDigestCalculatorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Compute_UsesTheFixedBomlessUtf8AndRfc8785Vectors ()
    {
        const string source = "return null;";

        Assert.Equal(
            "4ad2292cb6c5c2d31325739acfa4452421493ab7819e55fbaef0c55ac99bab33",
            EvalExecutionDigestCalculator.ComputeSourceDigest(source).ToString());
        Assert.Equal(
            "0b4ead0fcee4a48555242bd5d40e50978632f1b1dd89406eb94d1826c1dcbbb7",
            EvalExecutionDigestCalculator.ComputeExecutionDigest(
                source,
                CsEvalSourceKind.Snippet,
                allowDangerous: true,
                allowPlayMode: false).ToString());
    }
}
