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
        var descriptor = IndexOpsDescribeContractTestData.CreateGoDescribeOperation();
        var differentStoredDigest = descriptor with
        {
            DescriptorDigest = Sha256Digest.Compute("different stored digest"u8),
        };

        var actual = UcliOperationDescriptorDigest.Calculate(differentStoredDigest);

        Assert.Equal(
            IndexOpsDescribeContractTestData.GoDescribeCalculatedDescriptorDigest,
            actual.ToString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Calculate_WhenSemanticVerdictContractChanges_ReturnsDifferentDigest ()
    {
        var descriptor = IndexOpsDescribeContractTestData.CreateGoDescribeOperation();
        var changedVerdictContract = descriptor with
        {
            VerdictContract = new UcliOperationVerdictContract(
                "The requested GameObject exists, regardless of description completeness."),
        };

        var actual = UcliOperationDescriptorDigest.Calculate(changedVerdictContract);

        Assert.NotEqual(
            IndexOpsDescribeContractTestData.GoDescribeCalculatedDescriptorDigest,
            actual.ToString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Calculate_WhenInternalExposureChanges_ReturnsDifferentDigest ()
    {
        var descriptor = IndexOpsDescribeContractTestData.CreateGoDescribeOperation();
        var changedExposure = descriptor with
        {
            Exposure = UcliOperationExposure.EditLoweringOnly,
        };

        var actual = UcliOperationDescriptorDigest.Calculate(changedExposure);

        Assert.NotEqual(
            IndexOpsDescribeContractTestData.GoDescribeCalculatedDescriptorDigest,
            actual.ToString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Calculate_WhenPlayModeSupportChanges_ReturnsDifferentDigest ()
    {
        var descriptor = IndexOpsDescribeContractTestData.CreateGoDescribeOperation();
        var changedPlayModeSupport = descriptor with
        {
            PlayModeSupport = UcliOperationPlayModeSupport.Allowed,
        };

        var actual = UcliOperationDescriptorDigest.Calculate(changedPlayModeSupport);

        Assert.NotEqual(
            IndexOpsDescribeContractTestData.GoDescribeCalculatedDescriptorDigest,
            actual.ToString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Calculate_WhenArtifactMetadataChanges_PreservesDescriptorDigest ()
    {
        var operation = IndexOpsDescribeContractTestData.CreateGoDescribeOperation();
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

        Assert.NotEqual(earlier.GeneratedAtUtc, later.GeneratedAtUtc);
        Assert.NotEqual(earlier.SourceInputsHash, later.SourceInputsHash);
        Assert.Equal(
            IndexOpsDescribeContractTestData.GoDescribeCalculatedDescriptorDigest,
            UcliOperationDescriptorDigest.Calculate(earlier.Operation!).ToString());
        Assert.Equal(
            IndexOpsDescribeContractTestData.GoDescribeCalculatedDescriptorDigest,
            UcliOperationDescriptorDigest.Calculate(later.Operation!).ToString());
    }
}
