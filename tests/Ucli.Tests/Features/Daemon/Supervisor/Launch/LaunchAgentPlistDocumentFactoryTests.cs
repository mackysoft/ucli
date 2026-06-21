using System.Xml.Linq;

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
                ..SupervisorInvocationArguments.Build(storageRoot),
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
                ..SupervisorInvocationArguments.Build(storageRoot),
            ],
            GetArrayStrings(document, "ProgramArguments"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WhenEnvironmentVariablesAreSpecified_WritesLaunchAgentEnvironmentVariables ()
    {
        const string label = "dev.mackysoft.ucli.supervisor.test";
        const string storageRoot = "/repo";
        const string logPath = "/repo/supervisor.log";
        var launchCommand = new SupervisorLaunchCommand("ucli", ["--base"]);

        var plist = LaunchAgentPlistDocumentFactory.Build(
            label,
            launchCommand,
            storageRoot,
            logPath,
            [
                new KeyValuePair<string,string>("UCLI_RUNTIME_TRACE_DIR", "/tmp/ucli-trace"),
                new KeyValuePair<string,string>("UCLI_RUNTIME_TRACE_SESSION", "before"),
            ]);
        var document = XDocument.Parse(plist);

        Assert.Equal(
            new Dictionary<string, string>
            {
                ["UCLI_RUNTIME_TRACE_DIR"] = "/tmp/ucli-trace",
                ["UCLI_RUNTIME_TRACE_SESSION"] = "before",
            },
            GetDictionaryStrings(document, "EnvironmentVariables"));
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

    private static Dictionary<string, string> GetDictionaryStrings (
        XDocument document,
        string key)
    {
        var elements = GetValueElement(document, key).Elements().ToArray();
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < elements.Length - 1; i += 2)
        {
            if (!string.Equals(elements[i].Name.LocalName, "key", StringComparison.Ordinal))
            {
                continue;
            }

            values[elements[i].Value] = elements[i + 1].Value;
        }

        return values;
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
