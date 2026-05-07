using MackySoft.Ucli.Skills.Digests;
using MackySoft.Ucli.Skills.Distribution;
using MackySoft.Ucli.Skills.Doctor;
using MackySoft.Ucli.Skills.Generation;
using MackySoft.Ucli.Skills.Hosts.Official;
using MackySoft.Ucli.Skills.Hosts.Registration;
using MackySoft.Ucli.Skills.Installation;
using MackySoft.Ucli.Skills.Installation.Validation;
using MackySoft.Ucli.Skills.Manifests;
using MackySoft.Ucli.Skills.Materialization;
using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Shared;
using MackySoft.Ucli.Skills.Sources;

namespace MackySoft.Ucli.Skills.Tests;

internal static class SkillTestData
{
    internal static readonly string[] ExpectedSkillNames =
    [
        "ucli-plan-apply",
        "ucli-read-project",
        "ucli-troubleshoot",
        "ucli-verify-changes",
    ];

    internal static string GetDefinitionsRoot ()
    {
        return Path.Combine(GetRepositoryRoot(), "src", "Ucli.Skills", "SkillDefinitions");
    }

    internal static string GetGeneratedSkillsRoot ()
    {
        return Path.Combine(GetRepositoryRoot(), "skills");
    }

    internal static string GetRepositoryRoot ()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Ucli.Skills", "SkillDefinitions");

            if (Directory.Exists(candidate))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate src/Ucli.Skills/SkillDefinitions from the test output directory.");
    }

    internal static async Task<IReadOnlyList<CanonicalSkillPackage>> GenerateOfficialPackagesAsync ()
    {
        var service = CreatePackageGenerationService();
        var result = await service.GenerateAllAsync(GetDefinitionsRoot(), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Failure?.Message);
        return result.Value!;
    }

    internal static SkillHostAdapterSet CreateOfficialHostAdapterSet ()
    {
        return OfficialSkillHostAdapters.CreateSet();
    }

    internal static SkillPackageGenerationService CreatePackageGenerationService ()
    {
        return new SkillPackageGenerationService(
            new SkillSourceDefinitionReader(),
            CreateOfficialHostAdapterSet(),
            new SkillDigestCalculator(),
            new SkillManifestJsonSerializer());
    }

    internal static CanonicalSkillPackageReader CreatePackageReader ()
    {
        var hostAdapters = CreateOfficialHostAdapterSet();
        var manifestSerializer = new SkillManifestJsonSerializer();
        return new CanonicalSkillPackageReader(
            hostAdapters,
            new SkillDigestCalculator(),
            manifestSerializer,
            new SkillManifestValidator(hostAdapters));
    }

    internal static SkillManifestValidator CreateManifestValidator ()
    {
        return new SkillManifestValidator(CreateOfficialHostAdapterSet());
    }

    internal static SkillMaterializationService CreateMaterializationService ()
    {
        return new SkillMaterializationService(CreateOfficialHostAdapterSet());
    }

    internal static SkillExportService CreateExportService ()
    {
        return new SkillExportService(CreateMaterializationService());
    }

    internal static SkillInstallService CreateInstallService ()
    {
        var hostAdapters = CreateOfficialHostAdapterSet();
        var installedPackageValidator = CreateInstalledPackageValidator(hostAdapters);
        return new SkillInstallService(
            new SkillInstallTargetResolver(hostAdapters),
            new SkillMaterializationService(hostAdapters),
            new SkillInstalledTargetStateAnalyzer(installedPackageValidator, CreateInstalledPackageIntegrityVerifier(hostAdapters)),
            CreatePackageWriter(),
            new SkillMaterializedPackageDiffBuilder());
    }

    internal static SkillUpdateService CreateUpdateService (ISkillMaterializedPackageWriter? packageWriter = null)
    {
        var hostAdapters = CreateOfficialHostAdapterSet();
        var installedPackageValidator = CreateInstalledPackageValidator(hostAdapters);
        return new SkillUpdateService(
            new SkillInstallTargetResolver(hostAdapters),
            new SkillMaterializationService(hostAdapters),
            new SkillInstalledTargetStateAnalyzer(installedPackageValidator, CreateInstalledPackageIntegrityVerifier(hostAdapters)),
            packageWriter ?? CreatePackageWriter(),
            new SkillMaterializedPackageDiffBuilder());
    }

    internal static SkillUninstallService CreateUninstallService ()
    {
        var hostAdapters = CreateOfficialHostAdapterSet();
        var installedPackageValidator = CreateInstalledPackageValidator(hostAdapters);
        return new SkillUninstallService(
            new SkillInstallTargetResolver(hostAdapters),
            new SkillInstalledTargetStateAnalyzer(installedPackageValidator, CreateInstalledPackageIntegrityVerifier(hostAdapters)),
            CreatePackageRemover());
    }

    internal static SkillInstallationScanner CreateInstallationScanner ()
    {
        var hostAdapters = CreateOfficialHostAdapterSet();
        return new SkillInstallationScanner(
            hostAdapters,
            CreateInstalledManifestReader(hostAdapters),
            CreateInstalledPackageValidator(hostAdapters));
    }

    internal static SkillMaterializedPackageWriter CreatePackageWriter ()
    {
        return new SkillMaterializedPackageWriter(new SkillPackageDirectoryOperations());
    }

    internal static SkillInstalledPackageRemover CreatePackageRemover ()
    {
        return new SkillInstalledPackageRemover(new SkillPackageDirectoryOperations());
    }

    internal static SkillDoctorService CreateDoctorService ()
    {
        var hostAdapters = CreateOfficialHostAdapterSet();
        return new SkillDoctorService(
            hostAdapters,
            new SkillInstalledTargetStateAnalyzer(CreateInstalledPackageValidator(hostAdapters), CreateInstalledPackageIntegrityVerifier(hostAdapters)),
            new SkillInstalledPackageDriftAnalyzer(
                CreateInstalledManifestReader(hostAdapters),
                new SkillMaterializationService(hostAdapters),
                new SkillDigestCalculator()));
    }

    internal static IReadOnlyList<CanonicalSkillPackage> ReplacePackage (
        IReadOnlyList<CanonicalSkillPackage> packages,
        CanonicalSkillPackage replacement)
    {
        return packages
            .Select(package => string.Equals(package.Manifest.SkillName, replacement.Manifest.SkillName, StringComparison.Ordinal) ? replacement : package)
            .ToArray();
    }

    internal static CanonicalSkillPackage CreatePackageWithUpdatedBody (CanonicalSkillPackage package)
    {
        var files = package.Files
            .Select(static file => string.Equals(file.RelativePath, "SKILL.md", StringComparison.Ordinal)
                ? SkillPackageFile.Create("SKILL.md", file.Content + "\nOfficial update.\n")
                : file)
            .ToArray();
        var contentDigest = new SkillDigestCalculator().ComputeDigest(files
            .Where(static file => string.Equals(file.RelativePath, "SKILL.md", StringComparison.Ordinal)
                || file.RelativePath.StartsWith("references/", StringComparison.Ordinal))
            .Select(static file => new SkillDigestInputFile(file.RelativePath, file.Content)));
        var manifest = package.Manifest with
        {
            ContentDigest = contentDigest,
        };
        var manifestText = new SkillManifestJsonSerializer().Serialize(manifest);
        files = files
            .Select(file => string.Equals(file.RelativePath, "ucli-skill.json", StringComparison.Ordinal)
                ? SkillPackageFile.Create("ucli-skill.json", manifestText)
                : file)
            .ToArray();

        return package with
        {
            Manifest = manifest,
            Files = files,
        };
    }

    internal static CanonicalSkillPackage WithFileEnumerationCallback (
        CanonicalSkillPackage package,
        Action callback)
    {
        return package with
        {
            Files = new CallbackPackageFileList(package.Files, callback),
        };
    }

    internal static SkillInstalledPackageValidator CreateInstalledPackageValidator (SkillHostAdapterSet hostAdapters)
    {
        return new SkillInstalledPackageValidator(
            CreateInstalledManifestReader(hostAdapters),
            new SkillMaterializationService(hostAdapters),
            new SkillInstalledContentDigestVerifier(new SkillDigestCalculator()),
            new SkillInstalledFileSetVerifier(),
            new SkillHostMaterializationInspector(hostAdapters, new SkillDigestCalculator()));
    }

    internal static SkillInstalledPackageIntegrityVerifier CreateInstalledPackageIntegrityVerifier (SkillHostAdapterSet hostAdapters)
    {
        return new SkillInstalledPackageIntegrityVerifier(
            CreateInstalledManifestReader(hostAdapters),
            new SkillHostMaterializationInspector(hostAdapters, new SkillDigestCalculator()),
            new SkillDigestCalculator());
    }

    internal static SkillInstalledManifestReader CreateInstalledManifestReader (SkillHostAdapterSet hostAdapters)
    {
        return new SkillInstalledManifestReader(
            new SkillManifestJsonSerializer(),
            new SkillManifestValidator(hostAdapters));
    }

    private sealed class CallbackPackageFileList : IReadOnlyList<SkillPackageFile>
    {
        private readonly IReadOnlyList<SkillPackageFile> files;
        private readonly Action callback;

        private bool invoked;

        internal CallbackPackageFileList (
            IReadOnlyList<SkillPackageFile> files,
            Action callback)
        {
            this.files = files ?? throw new ArgumentNullException(nameof(files));
            this.callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        public SkillPackageFile this[int index] => files[index];

        public int Count => files.Count;

        public IEnumerator<SkillPackageFile> GetEnumerator ()
        {
            if (!invoked)
            {
                invoked = true;
                callback();
            }

            return files.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator ()
        {
            return GetEnumerator();
        }
    }
}
