using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Infrastructure.Tests.Storage;

internal static class UcliStoragePathResolverTestSupport
{
    internal const string ProjectFingerprintText = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    internal const string RunIdText = "3a1c6904-6c83-4e8d-a39d-0d9e2459ae16";
    internal const string ProjectFingerprintSegment = "04hkaps9lf6uu0938ljojaudts0i6hb7h6lsrro14d2mf2dbpnng";
    internal const string RunIdSegment = "78e6i13cgd78r8st1mf28mde2o";

    internal static readonly ProjectFingerprint ProjectFingerprint = new(ProjectFingerprintText);
    internal static readonly Guid RunId = Guid.Parse(RunIdText);

    internal static AbsolutePath StorageRoot { get; } =
        AbsolutePath.Parse(Path.Combine(Path.GetTempPath(), "ucli-infrastructure-storage-root"));

    internal static void AssertStoragePath (
        AbsolutePath actualPath,
        params string[] expectedRelativeSegments)
    {
        Assert.Equal(ExpectedStoragePath(expectedRelativeSegments), actualPath.Value);
    }

    internal static void AssertProjectPath (
        AbsolutePath actualPath,
        params string[] expectedFingerprintRelativeSegments)
    {
        var expectedRelativeSegments = new string[expectedFingerprintRelativeSegments.Length + 4];
        expectedRelativeSegments[0] = UcliStoragePathNames.UcliDirectoryName;
        expectedRelativeSegments[1] = UcliStoragePathNames.LocalDirectoryName;
        expectedRelativeSegments[2] = UcliStoragePathNames.ProjectsDirectoryName;
        expectedRelativeSegments[3] = ProjectFingerprintSegment;
        Array.Copy(expectedFingerprintRelativeSegments, 0, expectedRelativeSegments, 4, expectedFingerprintRelativeSegments.Length);

        AssertStoragePath(actualPath, expectedRelativeSegments);
    }

    private static string ExpectedStoragePath (params string[] relativeSegments)
    {
        var pathSegments = new string[relativeSegments.Length + 1];
        pathSegments[0] = StorageRoot.Value;
        Array.Copy(relativeSegments, 0, pathSegments, 1, relativeSegments.Length);
        return Path.Combine(pathSegments);
    }

    internal sealed record RunScopedPathResolverCase (
        string Name,
        Func<AbsolutePath, ProjectFingerprint, Guid, AbsolutePath> Resolve);
}
