using System.Security;
using System.Text;
using System.Xml.Linq;

namespace MackySoft.Ucli.Tests.Packaging;

internal static class PackageMetadataTestSupport
{
    private static readonly string[] DotNetProjectSearchRoots =
    [
        "src",
        "tests",
        "tools",
    ];

    public static readonly string[] CentralPackageMetadataProperties =
    [
        "Version",
        "Authors",
        "Company",
        "RepositoryUrl",
        "RepositoryType",
        "PackageLicenseFile",
        "PackageReadmeFile",
        "Copyright",
    ];

    private static readonly string[] PackableProjectMetadataProperties =
    [
        "PackageId",
        "Description",
        "PackageTags",
        "PackAsTool",
        "ToolCommandName",
    ];

    public static readonly string[] EvaluatedPackageMetadataProperties =
    [
        ..CentralPackageMetadataProperties,
        "PackageVersion",
        ..PackableProjectMetadataProperties,
    ];

    public static async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> ReadEvaluatedPropertiesByProjectAsync (
        IEnumerable<string> projectPaths,
        params string[] propertyNames)
    {
        ArgumentNullException.ThrowIfNull(projectPaths);
        ArgumentNullException.ThrowIfNull(propertyNames);

        using var scope = TestDirectories.CreateTempScope(
            "package-metadata",
            "evaluated-properties",
            DirectoryCleanupMode.BestEffort);
        string collectorProjectPath = scope.GetPath("CollectPackageMetadata.proj");
        string collectorTargetsPath = scope.GetPath("CollectPackageMetadata.targets");
        string outputPath = scope.GetPath("package-metadata.tsv");

        await File.WriteAllTextAsync(
            collectorProjectPath,
            CreateMetadataCollectorProject(projectPaths, collectorTargetsPath, outputPath));
        await File.WriteAllTextAsync(
            collectorTargetsPath,
            CreateMetadataCollectorTargets(propertyNames));

        TestProcessResult result = await TestProcessRunner.RunAsync(
            "dotnet",
            ["msbuild", collectorProjectPath, "-nologo", "-target:CollectPackageMetadata"],
            TestRepositoryPaths.RepositoryRoot);
        Assert.True(
            result.ExitCode == 0,
            $"dotnet msbuild failed while collecting package metadata." +
            $"{Environment.NewLine}StdOut:{Environment.NewLine}{result.StdOut}" +
            $"{Environment.NewLine}StdErr:{Environment.NewLine}{result.StdErr}");

        return ParseEvaluatedProjectProperties(await File.ReadAllLinesAsync(outputPath), propertyNames);
    }

    public static string[] EnumerateDotNetProjectPaths ()
    {
        return DotNetProjectSearchRoots
            .SelectMany(static root => TestRepositoryPaths.EnumerateRegularFiles(root, "*.csproj"))
            .Select(TestRepositoryPaths.NormalizeRepositoryRelativePath)
            .Where(static path => !path.StartsWith("src/Ucli.Unity/", StringComparison.Ordinal))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyDictionary<string, string> ReadDirectoryBuildProperties ()
    {
        XDocument document = XDocument.Load(TestRepositoryPaths.GetFullPath("Directory.Build.props"));
        return CentralPackageMetadataProperties.ToDictionary(
            static propertyName => propertyName,
            propertyName => document.Descendants(propertyName).Single().Value,
            StringComparer.Ordinal);
    }

    public static IReadOnlyDictionary<string, string> ReadUnityPackageConfigVersions ()
    {
        XDocument document = XDocument.Load(TestRepositoryPaths.GetFullPath("src/Ucli.Unity/Assets/packages.config"));
        return document
            .Descendants("package")
            .Select(static element => new
            {
                Id = element.Attribute("id")?.Value,
                Version = element.Attribute("version")?.Value,
            })
            .Where(static package => !string.IsNullOrWhiteSpace(package.Id) && !string.IsNullOrWhiteSpace(package.Version))
            .ToDictionary(
                static package => package.Id!,
                static package => package.Version!,
                StringComparer.Ordinal);
    }

    public static IReadOnlyDictionary<string, string> ReadNuspecDependencyVersions (
        XElement metadata,
        XNamespace nuspecNamespace)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return metadata
            .Element(nuspecNamespace + "dependencies")
            ?.Elements(nuspecNamespace + "dependency")
            .Select(static element => new
            {
                Id = element.Attribute("id")?.Value,
                Version = element.Attribute("version")?.Value,
            })
            .Where(static dependency => !string.IsNullOrWhiteSpace(dependency.Id) && !string.IsNullOrWhiteSpace(dependency.Version))
            .ToDictionary(
                static dependency => dependency.Id!,
                static dependency => dependency.Version!,
                StringComparer.Ordinal)
            ?? throw new InvalidOperationException("Unity nuspec dependencies element was not found.");
    }

    public static XElement ReadRequiredElement (
        XElement parent,
        XNamespace xmlNamespace,
        string elementName)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentException.ThrowIfNullOrWhiteSpace(elementName);

        return parent.Element(xmlNamespace + elementName)
            ?? throw new InvalidOperationException($"Required element was not found: {elementName}");
    }

    public static string ReadRequiredElementValue (
        XElement parent,
        XNamespace xmlNamespace,
        string elementName)
    {
        return ReadRequiredElement(parent, xmlNamespace, elementName).Value;
    }

    private static string CreateMetadataCollectorProject (
        IEnumerable<string> projectPaths,
        string collectorTargetsPath,
        string outputPath)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<Project>");
        builder.AppendLine("  <ItemGroup>");
        foreach (string projectPath in projectPaths)
        {
            string fullPath = TestRepositoryPaths.GetFullPath(projectPath);
            builder
                .Append("    <PackableProject Include=\"")
                .Append(EscapeXmlAttribute(fullPath))
                .AppendLine("\" />");
        }

        builder.AppendLine("  </ItemGroup>");
        builder.AppendLine("  <Target Name=\"CollectPackageMetadata\">");
        builder
            .Append("    <Delete Files=\"")
            .Append(EscapeXmlAttribute(outputPath))
            .AppendLine("\" />");
        builder
            .Append("    <MSBuild Projects=\"@(PackableProject)\" Targets=\"WritePackageMetadata\" BuildInParallel=\"false\" Properties=\"CustomAfterMicrosoftCommonTargets=")
            .Append(EscapeXmlAttribute(collectorTargetsPath))
            .Append(";CustomAfterMicrosoftCommonCrossTargetingTargets=")
            .Append(EscapeXmlAttribute(collectorTargetsPath))
            .Append(";PackageMetadataOutputPath=")
            .Append(EscapeXmlAttribute(outputPath))
            .AppendLine("\" />");
        builder.AppendLine("  </Target>");
        builder.AppendLine("</Project>");
        return builder.ToString();
    }

    private static string CreateMetadataCollectorTargets (IEnumerable<string> propertyNames)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<Project>");
        builder.AppendLine("  <Target Name=\"WritePackageMetadata\">");
        foreach (string propertyName in propertyNames)
        {
            builder
                .Append("    <WriteLinesToFile File=\"$(PackageMetadataOutputPath)\" Lines=\"$([MSBuild]::Escape('$(MSBuildProjectFullPath)'))&#9;")
                .Append(EscapeXmlAttribute(propertyName))
                .Append("&#9;$([MSBuild]::Escape('$(")
                .Append(EscapeXmlAttribute(propertyName))
                .AppendLine(")'))\" Overwrite=\"false\" Encoding=\"UTF-8\" />");
        }

        builder.AppendLine("  </Target>");
        builder.AppendLine("</Project>");
        return builder.ToString();
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ParseEvaluatedProjectProperties (
        string[] lines,
        IReadOnlyCollection<string> propertyNames)
    {
        var propertyNameSet = propertyNames.ToHashSet(StringComparer.Ordinal);
        var values = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        foreach (string line in lines)
        {
            string[] parts = line.Split('\t');
            Assert.True(parts.Length is 2 or 3, "Package metadata collector emitted malformed output: " + line);

            string projectPath = TestRepositoryPaths.NormalizeRepositoryRelativePath(Uri.UnescapeDataString(parts[0]));
            string propertyName = parts[1];
            Assert.Contains(propertyName, propertyNameSet);

            if (!values.TryGetValue(projectPath, out Dictionary<string, string>? projectValues))
            {
                projectValues = new Dictionary<string, string>(StringComparer.Ordinal);
                values.Add(projectPath, projectValues);
            }

            projectValues[propertyName] = parts.Length == 3 ? Uri.UnescapeDataString(parts[2]) : string.Empty;
        }

        foreach ((string projectPath, Dictionary<string, string> projectValues) in values)
        {
            foreach (string propertyName in propertyNames)
            {
                Assert.True(projectValues.ContainsKey(propertyName), $"{projectPath} did not emit {propertyName}.");
            }
        }

        return values.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyDictionary<string, string>)pair.Value,
            StringComparer.Ordinal);
    }

    private static string EscapeXmlAttribute (string value)
    {
        return SecurityElement.Escape(value) ?? string.Empty;
    }
}
