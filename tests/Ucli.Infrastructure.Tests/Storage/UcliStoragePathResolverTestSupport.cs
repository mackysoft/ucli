using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Infrastructure.Tests.Storage;

internal static class UcliStoragePathResolverTestSupport
{
    internal const string ProjectFingerprintText = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    internal const string RunId = "run-id";

    internal static readonly ProjectFingerprint ProjectFingerprint = new(ProjectFingerprintText);

    internal static readonly string[] UnsafeRunIds =
    [
        "../run-id",
        "..\\run-id",
        "run/id",
        "run\\id",
        "/absolute",
        "C:\\absolute",
        ".",
        "..",
    ];

    internal static string StorageRoot => Path.Combine(Path.GetTempPath(), "ucli-infrastructure-storage-root");

    internal static void AssertStoragePath (
        string actualPath,
        params string[] expectedRelativeSegments)
    {
        Assert.Equal(ExpectedStoragePath(expectedRelativeSegments), actualPath);
    }

    internal static void AssertFingerprintPath (
        string actualPath,
        params string[] expectedFingerprintRelativeSegments)
    {
        var expectedRelativeSegments = new string[expectedFingerprintRelativeSegments.Length + 4];
        expectedRelativeSegments[0] = UcliStoragePathNames.UcliDirectoryName;
        expectedRelativeSegments[1] = UcliStoragePathNames.LocalDirectoryName;
        expectedRelativeSegments[2] = UcliStoragePathNames.FingerprintsDirectoryName;
        expectedRelativeSegments[3] = ProjectFingerprint.ToString();
        Array.Copy(expectedFingerprintRelativeSegments, 0, expectedRelativeSegments, 4, expectedFingerprintRelativeSegments.Length);

        AssertStoragePath(actualPath, expectedRelativeSegments);
    }

    private static string ExpectedStoragePath (params string[] relativeSegments)
    {
        var pathSegments = new string[relativeSegments.Length + 1];
        pathSegments[0] = Path.GetFullPath(StorageRoot);
        Array.Copy(relativeSegments, 0, pathSegments, 1, relativeSegments.Length);
        return Path.Combine(pathSegments);
    }

    internal sealed record RunScopedPathResolverCase (
        string Name,
        Func<string, ProjectFingerprint, string, string> Resolve);
}
