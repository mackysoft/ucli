using System.Xml.Linq;

namespace MackySoft.Ucli.Architecture.Tests.Architecture;

internal static class UnityPackagesConfigReader
{
    internal static string[] ReadPackageIds (string packagesConfigPath)
    {
        var document = XDocument.Load(ArchitectureTestRepository.ToRegularFileFullPath(packagesConfigPath));
        return document
            .Descendants("package")
            .Select(element => element.Attribute("id")?.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!)
            .ToArray();
    }
}
