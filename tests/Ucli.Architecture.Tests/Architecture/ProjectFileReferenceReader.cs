using System.Xml.Linq;

namespace MackySoft.Ucli.Architecture.Tests.Architecture;

internal static class ProjectFileReferenceReader
{
    internal static string[] ReadProjectReferences (string projectPath)
    {
        var projectFullPath = ArchitectureTestRepository.ToRegularFileFullPath(projectPath);
        var projectDirectory = Path.GetDirectoryName(projectFullPath)
            ?? throw new InvalidOperationException($"Project path has no directory: {projectFullPath}");
        var document = XDocument.Load(projectFullPath);
        return document
            .Descendants()
            .Where(static element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(value => ArchitectureTestRepository.NormalizeRepositoryRelativePath(Path.GetFullPath(Path.Combine(projectDirectory, value!))))
            .ToArray();
    }

    internal static string[] ReadPackageReferences (string projectPath)
    {
        var projectFullPath = ArchitectureTestRepository.ToRegularFileFullPath(projectPath);
        var document = XDocument.Load(projectFullPath);
        return document
            .Descendants()
            .Where(static element => element.Name.LocalName == "PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!)
            .ToArray();
    }
}
