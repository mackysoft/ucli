using System.Xml.Linq;
using MackySoft.Ucli.Features.Daemon.Supervisor.Invocation;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class LaunchAgentPlistDocumentFactoryTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Build_WritesLaunchAgentPlistWithSupervisorInvocationArguments ()
    {
        const string label = "dev.mackysoft.ucli.supervisor.test";
        const string storageRoot = "/repo";
        const string logPath = "/repo/supervisor.log";
        var launchCommand = new SupervisorLaunchCommand("ucli", ["--base"]);

        var plist = LaunchAgentPlistDocumentFactory.Build(label, launchCommand, storageRoot, logPath);
        var document = XDocument.Parse(plist);

        Assert.Contains("<!DOCTYPE plist PUBLIC", plist, StringComparison.Ordinal);
        Assert.Equal("plist", document.Root?.Name.LocalName);
        Assert.Equal("1.0", document.Root?.Attribute("version")?.Value);
        Assert.Equal(label, GetString(document, "Label"));
        Assert.Equal(storageRoot, GetString(document, "WorkingDirectory"));
        Assert.Equal(logPath, GetString(document, "StandardOutPath"));
        Assert.Equal(logPath, GetString(document, "StandardErrorPath"));
        Assert.Equal(
            [
                "ucli",
                "--base",
                SupervisorInvocationArguments.InternalServeFlag,
                SupervisorInvocationArguments.RepositoryRootOption,
                storageRoot,
            ],
            GetArrayStrings(document, "ProgramArguments"));
        Assert.Equal("true", GetValueElement(document, "RunAtLoad").Name.LocalName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_EscapesXmlSpecialCharactersAsStructuredValues ()
    {
        const string label = "label<&>";
        const string storageRoot = "/repo<&>";
        const string logPath = "/repo/log<&>.txt";
        var launchCommand = new SupervisorLaunchCommand("ucli<&>", ["--arg<&>"]);

        var plist = LaunchAgentPlistDocumentFactory.Build(label, launchCommand, storageRoot, logPath);
        var document = XDocument.Parse(plist);

        Assert.Equal(label, GetString(document, "Label"));
        Assert.Equal(storageRoot, GetString(document, "WorkingDirectory"));
        Assert.Equal(logPath, GetString(document, "StandardOutPath"));
        Assert.Equal(
            [
                "ucli<&>",
                "--arg<&>",
                SupervisorInvocationArguments.InternalServeFlag,
                SupervisorInvocationArguments.RepositoryRootOption,
                storageRoot,
            ],
            GetArrayStrings(document, "ProgramArguments"));
    }

    private static string GetString (
        XDocument document,
        string key)
    {
        return GetValueElement(document, key).Value;
    }

    private static string[] GetArrayStrings (
        XDocument document,
        string key)
    {
        return GetValueElement(document, key)
            .Elements("string")
            .Select(static element => element.Value)
            .ToArray();
    }

    private static XElement GetValueElement (
        XDocument document,
        string key)
    {
        var elements = document.Root?.Element("dict")?.Elements().ToArray()
            ?? throw new InvalidOperationException("plist dict element was not found.");
        for (var i = 0; i < elements.Length - 1; i += 2)
        {
            if (string.Equals(elements[i].Name.LocalName, "key", StringComparison.Ordinal)
                && string.Equals(elements[i].Value, key, StringComparison.Ordinal))
            {
                return elements[i + 1];
            }
        }

        throw new InvalidOperationException($"plist key was not found: {key}");
    }
}
