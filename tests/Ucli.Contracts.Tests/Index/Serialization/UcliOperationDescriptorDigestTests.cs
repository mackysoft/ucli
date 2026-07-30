using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.Contracts.Tests.Index;

public sealed class UcliOperationDescriptorDigestTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Calculate_IgnoresStoredDigest ()
    {
        var descriptor = IndexOpsDescribeJsonContractTestSupport
            .CreateGoDescribeIndexContract()
            .Operation!;
        var differentStoredDigest = descriptor with
        {
            DescriptorDigest = Sha256Digest.Compute("different stored digest"u8),
        };

        var expected = UcliOperationDescriptorDigest.Calculate(descriptor);
        var actual = UcliOperationDescriptorDigest.Calculate(differentStoredDigest);

        Assert.Equal(expected, actual);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Calculate_WhenSemanticVerdictContractChanges_ReturnsDifferentDigest ()
    {
        var descriptor = IndexOpsDescribeJsonContractTestSupport
            .CreateGoDescribeIndexContract()
            .Operation!;
        var changedVerdictContract = descriptor with
        {
            VerdictContract = new UcliOperationVerdictContract(
                "The requested GameObject exists, regardless of description completeness."),
        };

        var expected = UcliOperationDescriptorDigest.Calculate(descriptor);
        var actual = UcliOperationDescriptorDigest.Calculate(changedVerdictContract);

        Assert.NotEqual(expected, actual);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Calculate_WhenInternalExposureChanges_ReturnsDifferentDigest ()
    {
        var descriptor = IndexOpsDescribeJsonContractTestSupport
            .CreateGoDescribeIndexContract()
            .Operation!;
        var changedExposure = descriptor with
        {
            Exposure = UcliOperationExposure.EditLoweringOnly,
        };

        var expected = UcliOperationDescriptorDigest.Calculate(descriptor);
        var actual = UcliOperationDescriptorDigest.Calculate(changedExposure);

        Assert.NotEqual(expected, actual);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Calculate_WhenPlayModeSupportChanges_ReturnsDifferentDigest ()
    {
        var descriptor = IndexOpsDescribeJsonContractTestSupport
            .CreateGoDescribeIndexContract()
            .Operation!;
        var changedPlayModeSupport = descriptor with
        {
            PlayModeSupport = UcliOperationPlayModeSupport.Allowed,
        };

        var expected = UcliOperationDescriptorDigest.Calculate(descriptor);
        var actual = UcliOperationDescriptorDigest.Calculate(changedPlayModeSupport);

        Assert.NotEqual(expected, actual);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Writer_WhenArtifactTimestampChanges_PreservesDescriptorDigest ()
    {
        var operation = IndexOpsDescribeJsonContractTestSupport
            .CreateGoDescribeIndexContract()
            .Operation!;
        var earlier = new IndexOpsDescribeJsonContract(
            1,
            DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            "source-hash-a",
            operation);
        var later = earlier with
        {
            GeneratedAtUtc = DateTimeOffset.Parse("2026-03-04T00:00:00+00:00"),
            SourceInputsHash = "source-hash-b",
        };

        var earlierRoundTrip = IndexOpsDescribeJsonContractSerializer.Deserialize(
            new IndexOpsDescribeJsonContractWriter().Write(earlier));
        var laterRoundTrip = IndexOpsDescribeJsonContractSerializer.Deserialize(
            new IndexOpsDescribeJsonContractWriter().Write(later));

        Assert.NotNull(earlierRoundTrip);
        Assert.NotNull(laterRoundTrip);
        Assert.NotEqual(earlierRoundTrip.GeneratedAtUtc, laterRoundTrip.GeneratedAtUtc);
        Assert.NotEqual(earlierRoundTrip.SourceInputsHash, laterRoundTrip.SourceInputsHash);
        Assert.Equal(
            earlierRoundTrip.Operation!.DescriptorDigest,
            laterRoundTrip.Operation!.DescriptorDigest);
    }
}
