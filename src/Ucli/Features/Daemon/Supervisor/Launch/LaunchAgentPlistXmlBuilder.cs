using System.Text;
using System.Xml;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Launch;

/// <summary> Builds macOS LaunchAgent plist XML for the worktree-local supervisor. </summary>
internal static class LaunchAgentPlistXmlBuilder
{
    private const string PlistPublicId = "-//Apple//DTD PLIST 1.0//EN";

    private const string PlistSystemId = "http://www.apple.com/DTDs/PropertyList-1.0.dtd";

    /// <summary> Builds one LaunchAgent plist document. </summary>
    /// <param name="label"> The LaunchAgent label. </param>
    /// <param name="launchCommand"> The base command used to relaunch uCLI. </param>
    /// <param name="storageRoot"> The supervisor storage root. </param>
    /// <param name="logPath"> The shared stdout and stderr log path. </param>
    /// <returns> The complete plist XML document without a trailing newline. </returns>
    /// <exception cref="ArgumentException"> Thrown when a required string value is empty or whitespace. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="launchCommand" /> is <see langword="null" />. </exception>
    public static string Build (
        string label,
        SupervisorLaunchCommand launchCommand,
        string storageRoot,
        string logPath)
    {
        ThrowIfNullOrWhiteSpace(label, nameof(label));
        ArgumentNullException.ThrowIfNull(launchCommand);
        ThrowIfNullOrWhiteSpace(storageRoot, nameof(storageRoot));
        ThrowIfNullOrWhiteSpace(logPath, nameof(logPath));

        using var output = new MemoryStream();
        using (var writer = XmlWriter.Create(output, CreateWriterSettings()))
        {
            writer.WriteStartDocument();
            writer.WriteDocType("plist", PlistPublicId, PlistSystemId, null);
            writer.WriteStartElement("plist");
            writer.WriteAttributeString("version", "1.0");
            writer.WriteStartElement("dict");

            WriteString(writer, "Label", label);
            WriteProgramArguments(writer, launchCommand, storageRoot);
            WriteString(writer, "WorkingDirectory", storageRoot);
            WriteBoolean(writer, "RunAtLoad", true);
            WriteString(writer, "StandardOutPath", logPath);
            WriteString(writer, "StandardErrorPath", logPath);

            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        return Encoding.UTF8.GetString(output.ToArray());
    }

    private static XmlWriterSettings CreateWriterSettings ()
    {
        return new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = true,
            NewLineChars = "\n",
            OmitXmlDeclaration = false,
        };
    }

    private static void WriteProgramArguments (
        XmlWriter writer,
        SupervisorLaunchCommand launchCommand,
        string storageRoot)
    {
        writer.WriteElementString("key", "ProgramArguments");
        writer.WriteStartElement("array");
        writer.WriteElementString("string", launchCommand.FileName);
        for (var i = 0; i < launchCommand.Arguments.Count; i++)
        {
            writer.WriteElementString("string", launchCommand.Arguments[i]);
        }

        var supervisorArguments = SupervisorInvocationArguments.Build(storageRoot);
        for (var i = 0; i < supervisorArguments.Length; i++)
        {
            writer.WriteElementString("string", supervisorArguments[i]);
        }

        writer.WriteEndElement();
    }

    private static void WriteString (
        XmlWriter writer,
        string key,
        string value)
    {
        writer.WriteElementString("key", key);
        writer.WriteElementString("string", value);
    }

    private static void WriteBoolean (
        XmlWriter writer,
        string key,
        bool value)
    {
        writer.WriteElementString("key", key);
        writer.WriteStartElement(value ? "true" : "false");
        writer.WriteEndElement();
    }

    private static void ThrowIfNullOrWhiteSpace (
        string value,
        string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be empty.", paramName);
        }
    }
}
