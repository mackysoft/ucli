using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace MackySoft.Ucli.Tests.Packaging;

public sealed class PackageMetadataTests
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(30);

    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    [Trait("Size", "Small")]
    public void Source_projects_do_not_redefine_central_package_metadata ()
    {
        var centrallyOwnedProperties = new[]
        {
            "Version",
            "PackageVersion",
            "Authors",
            "Company",
            "RepositoryUrl",
            "RepositoryType",
            "PackageLicenseFile",
            "PackageReadmeFile",
            "Copyright",
        };
        var sourceProjectPaths = new[]
        {
            "src/Ucli/Ucli.csproj",
            "src/Ucli.Application/Ucli.Application.csproj",
            "src/Ucli.Contracts/Ucli.Contracts.csproj",
            "src/Ucli.Infrastructure/Ucli.Infrastructure.csproj",
            "src/Ucli.Skills/Ucli.Skills.csproj",
        };
        var violations = new List<string>();

        foreach (string projectPath in sourceProjectPaths)
        {
            XDocument document = XDocument.Load(Path.Combine(RepositoryRoot, projectPath));
            foreach (string propertyName in centrallyOwnedProperties)
            {
                if (document.Descendants(propertyName).Any())
                {
                    violations.Add($"{projectPath} redefines {propertyName}.");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "Package metadata must be defined in Directory.Build.props only: " + string.Join(", ", violations));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Packable_projects_evaluate_expected_nuget_metadata ()
    {
        IReadOnlyDictionary<string, string> centralProperties = ReadDirectoryBuildProperties();
        var expectedMetadataByProject = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal)
        {
            ["src/Ucli/Ucli.csproj"] = new(StringComparer.Ordinal)
            {
                ["PackageId"] = "MackySoft.Ucli",
                ["Description"] = "CLI workflow for Unity automation.",
                ["PackageTags"] = "ucli;unity;cli;automation",
            },
            ["src/Ucli.Contracts/Ucli.Contracts.csproj"] = new(StringComparer.Ordinal)
            {
                ["PackageId"] = "MackySoft.Ucli.Contracts",
                ["Description"] = "Shared contract types for uCLI IPC protocol.",
                ["PackageTags"] = "ucli;unity;ipc",
            },
            ["src/Ucli.Infrastructure/Ucli.Infrastructure.csproj"] = new(StringComparer.Ordinal)
            {
                ["PackageId"] = "MackySoft.Ucli.Infrastructure",
                ["Description"] = "Shared infrastructure services for uCLI runtime components.",
                ["PackageTags"] = "ucli;unity;infrastructure",
            },
        };

        foreach ((string projectPath, Dictionary<string, string> projectMetadata) in expectedMetadataByProject)
        {
            IReadOnlyDictionary<string, string> properties = await ReadEvaluatedPropertiesAsync(
                projectPath,
                "Version",
                "PackageVersion",
                "Authors",
                "Company",
                "RepositoryUrl",
                "RepositoryType",
                "PackageLicenseFile",
                "PackageReadmeFile",
                "Copyright",
                "PackageId",
                "Description",
                "PackageTags");

            AssertEvaluatedProperty(properties, projectPath, "Version", centralProperties["Version"]);
            AssertEvaluatedProperty(properties, projectPath, "PackageVersion", centralProperties["Version"]);
            AssertEvaluatedProperty(properties, projectPath, "Authors", centralProperties["Authors"]);
            AssertEvaluatedProperty(properties, projectPath, "Company", centralProperties["Company"]);
            AssertEvaluatedProperty(properties, projectPath, "RepositoryUrl", centralProperties["RepositoryUrl"]);
            AssertEvaluatedProperty(properties, projectPath, "RepositoryType", centralProperties["RepositoryType"]);
            AssertEvaluatedProperty(properties, projectPath, "PackageLicenseFile", centralProperties["PackageLicenseFile"]);
            AssertEvaluatedProperty(properties, projectPath, "PackageReadmeFile", centralProperties["PackageReadmeFile"]);
            AssertEvaluatedProperty(properties, projectPath, "Copyright", centralProperties["Copyright"]);

            foreach ((string propertyName, string expectedValue) in projectMetadata)
            {
                AssertEvaluatedProperty(properties, projectPath, propertyName, expectedValue);
            }
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Cli_tool_metadata_evaluates_expected_properties ()
    {
        const string projectPath = "src/Ucli/Ucli.csproj";

        IReadOnlyDictionary<string, string> properties = await ReadEvaluatedPropertiesAsync(
            projectPath,
            "PackAsTool",
            "ToolCommandName");

        AssertEvaluatedProperty(properties, projectPath, "PackAsTool", "true");
        AssertEvaluatedProperty(properties, projectPath, "ToolCommandName", "ucli");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Projects_declare_expected_packability ()
    {
        var expectedPackabilityByProject = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["src/Ucli.Application/Ucli.Application.csproj"] = "false",
            ["src/Ucli.Contracts/Ucli.Contracts.csproj"] = "true",
            ["src/Ucli.Infrastructure/Ucli.Infrastructure.csproj"] = "true",
            ["src/Ucli.Skills/Ucli.Skills.csproj"] = "false",
            ["src/Ucli/Ucli.csproj"] = "true",
            ["tests/Tests.Helper/Tests.Helper.csproj"] = "false",
            ["tests/Ucli.Application.Tests/Ucli.Application.Tests.csproj"] = "false",
            ["tests/Ucli.Architecture.Tests/Ucli.Architecture.Tests.csproj"] = "false",
            ["tests/Ucli.Contracts.Tests/Ucli.Contracts.Tests.csproj"] = "false",
            ["tests/Ucli.Infrastructure.Tests/Ucli.Infrastructure.Tests.csproj"] = "false",
            ["tests/Ucli.Skills.Tests/Ucli.Skills.Tests.csproj"] = "false",
            ["tests/Ucli.Tests/Ucli.Tests.csproj"] = "false",
        };

        string[] actualProjectPaths = Directory
            .EnumerateFiles(RepositoryRoot, "*.csproj", SearchOption.AllDirectories)
            .Select(NormalizeRepositoryRelativePath)
            .Where(static path => !path.StartsWith("src/Ucli.Unity/", StringComparison.Ordinal))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            expectedPackabilityByProject.Keys.OrderBy(static path => path, StringComparer.Ordinal),
            actualProjectPaths);

        foreach ((string projectPath, string expectedIsPackable) in expectedPackabilityByProject)
        {
            XDocument document = XDocument.Load(Path.Combine(RepositoryRoot, projectPath));
            string actualIsPackable = document.Descendants("IsPackable").Single().Value;
            Assert.Equal(expectedIsPackable, actualIsPackable);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Unity_nuspec_metadata_matches_central_package_metadata ()
    {
        IReadOnlyDictionary<string, string> centralProperties = ReadDirectoryBuildProperties();
        XNamespace nuspecNamespace = "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd";
        XDocument nuspecDocument = XDocument.Load(Path.Combine(RepositoryRoot, "src/Ucli.Unity/MackySoft.Ucli.Unity.nuspec"));
        XElement metadata = nuspecDocument.Root?.Element(nuspecNamespace + "metadata")
            ?? throw new InvalidOperationException("Unity nuspec metadata element was not found.");

        Assert.Equal(centralProperties["Authors"], ReadRequiredElementValue(metadata, nuspecNamespace, "authors"));
        Assert.Equal(centralProperties["Company"], ReadRequiredElementValue(metadata, nuspecNamespace, "owners"));
        Assert.Equal("file", ReadRequiredElement(metadata, nuspecNamespace, "license").Attribute("type")?.Value);
        Assert.Equal(centralProperties["PackageLicenseFile"], ReadRequiredElementValue(metadata, nuspecNamespace, "license"));
        Assert.Equal(centralProperties["PackageReadmeFile"], ReadRequiredElementValue(metadata, nuspecNamespace, "readme"));
        Assert.Equal(centralProperties["RepositoryType"], ReadRequiredElement(metadata, nuspecNamespace, "repository").Attribute("type")?.Value);
        Assert.Equal(centralProperties["RepositoryUrl"], ReadRequiredElement(metadata, nuspecNamespace, "repository").Attribute("url")?.Value);

        IReadOnlyDictionary<string, string> packageConfigVersions = ReadUnityPackageConfigVersions();
        IReadOnlyDictionary<string, string> nuspecDependencyVersions = ReadNuspecDependencyVersions(metadata, nuspecNamespace);
        Assert.Equal(centralProperties["Version"], packageConfigVersions["MackySoft.Ucli.Contracts"]);
        Assert.Equal(centralProperties["Version"], packageConfigVersions["MackySoft.Ucli.Infrastructure"]);
        Assert.Equal(packageConfigVersions["MackySoft.Ucli.Contracts"], nuspecDependencyVersions["MackySoft.Ucli.Contracts"]);
        Assert.Equal(packageConfigVersions["MackySoft.Ucli.Infrastructure"], nuspecDependencyVersions["MackySoft.Ucli.Infrastructure"]);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Verify_scope_detector_tracks_directory_build_props_changes ()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "ucli-verify-scope-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            await RunRequiredProcessAsync("git", ["init"], tempDirectory);
            await RunRequiredProcessAsync("git", ["config", "user.email", "ucli-tests@example.invalid"], tempDirectory);
            await RunRequiredProcessAsync("git", ["config", "user.name", "uCLI Tests"], tempDirectory);

            string propsPath = Path.Combine(tempDirectory, "Directory.Build.props");
            await File.WriteAllTextAsync(
                propsPath,
                "<Project><PropertyGroup><Version>0.18.0</Version></PropertyGroup></Project>",
                Encoding.UTF8);
            await RunRequiredProcessAsync("git", ["add", "Directory.Build.props"], tempDirectory);
            await RunRequiredProcessAsync("git", ["commit", "-m", "initial"], tempDirectory);
            string baseSha = (await RunRequiredProcessAsync("git", ["rev-parse", "HEAD"], tempDirectory)).StdOut.Trim();

            await File.WriteAllTextAsync(
                propsPath,
                "<Project><PropertyGroup><Version>0.18.1</Version></PropertyGroup></Project>",
                Encoding.UTF8);
            await RunRequiredProcessAsync("git", ["add", "Directory.Build.props"], tempDirectory);
            await RunRequiredProcessAsync("git", ["commit", "-m", "change props"], tempDirectory);
            string headSha = (await RunRequiredProcessAsync("git", ["rev-parse", "HEAD"], tempDirectory)).StdOut.Trim();

            string detectorScriptPath = ToBashPath(Path.Combine(RepositoryRoot, "scripts", "detect-verify-scopes.sh"));
            string bashFileName = ResolveBashFileName();
            ProcessResult result = await RunRequiredProcessAsync(
                bashFileName,
                [detectorScriptPath],
                tempDirectory,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["EVENT_NAME"] = "pull_request",
                    ["GITHUB_OUTPUT"] = string.Empty,
                    ["PR_BASE_SHA"] = baseSha,
                    ["PR_HEAD_SHA"] = headSha,
                });

            IReadOnlyDictionary<string, string> outputs = ParseDetectorOutputs(result.StdOut);
            Assert.Equal("true", outputs["needs_dotnet"]);
            Assert.Equal("true", outputs["needs_shared_pack"]);
            Assert.Equal("true", outputs["needs_cli_pack"]);
            Assert.Equal("false", outputs["needs_unity"]);
            Assert.Equal("false", outputs["needs_unity_pack"]);
        }
        finally
        {
            DeleteDirectoryBestEffort(tempDirectory);
        }
    }

    private static async Task<IReadOnlyDictionary<string, string>> ReadEvaluatedPropertiesAsync (
        string projectPath,
        params string[] propertyNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        ArgumentNullException.ThrowIfNull(propertyNames);

        var arguments = new List<string>
        {
            "msbuild",
            projectPath,
            "-nologo",
        };
        foreach (string propertyName in propertyNames.Append("MSBuildProjectName"))
        {
            arguments.Add("-getProperty:" + propertyName);
        }

        ProcessResult result = await RunProcessAsync("dotnet", arguments, RepositoryRoot);
        Assert.True(
            result.ExitCode == 0,
            $"dotnet msbuild failed for {projectPath}.{Environment.NewLine}{result.StdErr}");

        using JsonDocument document = JsonDocument.Parse(result.StdOut);
        JsonElement propertiesElement = document.RootElement.GetProperty("Properties");
        return propertyNames.ToDictionary(
            static propertyName => propertyName,
            propertyName => propertiesElement.TryGetProperty(propertyName, out JsonElement value) ? value.GetString() ?? string.Empty : string.Empty,
            StringComparer.Ordinal);
    }

    private static void AssertEvaluatedProperty (
        IReadOnlyDictionary<string, string> properties,
        string projectPath,
        string propertyName,
        string expectedValue)
    {
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        Assert.True(
            properties.TryGetValue(propertyName, out string? actualValue),
            $"{projectPath} did not evaluate {propertyName}.");
        Assert.Equal(expectedValue, actualValue);
    }

    private static IReadOnlyDictionary<string, string> ReadDirectoryBuildProperties ()
    {
        XDocument document = XDocument.Load(Path.Combine(RepositoryRoot, "Directory.Build.props"));
        var requiredProperties = new[]
        {
            "Version",
            "Authors",
            "Company",
            "RepositoryUrl",
            "RepositoryType",
            "PackageLicenseFile",
            "PackageReadmeFile",
            "Copyright",
        };

        return requiredProperties.ToDictionary(
            static propertyName => propertyName,
            propertyName => document.Descendants(propertyName).Single().Value,
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, string> ReadUnityPackageConfigVersions ()
    {
        XDocument document = XDocument.Load(Path.Combine(RepositoryRoot, "src/Ucli.Unity/Assets/packages.config"));
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

    private static IReadOnlyDictionary<string, string> ReadNuspecDependencyVersions (
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

    private static XElement ReadRequiredElement (
        XElement parent,
        XNamespace xmlNamespace,
        string elementName)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentException.ThrowIfNullOrWhiteSpace(elementName);

        return parent.Element(xmlNamespace + elementName)
            ?? throw new InvalidOperationException($"Required element was not found: {elementName}");
    }

    private static string ReadRequiredElementValue (
        XElement parent,
        XNamespace xmlNamespace,
        string elementName)
    {
        return ReadRequiredElement(parent, xmlNamespace, elementName).Value;
    }

    private static IReadOnlyDictionary<string, string> ParseDetectorOutputs (string output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        using var reader = new StringReader(output);
        while (reader.ReadLine() is { } line)
        {
            int separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            values[line[..separatorIndex]] = line[(separatorIndex + 1)..];
        }

        return values;
    }

    private static void DeleteDirectoryBestEffort (string directoryPath)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    return;
                }

                ClearReadOnlyAttributes(directoryPath);
                Directory.Delete(directoryPath, recursive: true);
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(50));
            }
        }
    }

    private static void ClearReadOnlyAttributes (string directoryPath)
    {
        foreach (string filePath in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(filePath, File.GetAttributes(filePath) & ~FileAttributes.ReadOnly);
        }

        foreach (string childDirectoryPath in Directory.EnumerateDirectories(directoryPath, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(childDirectoryPath, File.GetAttributes(childDirectoryPath) & ~FileAttributes.ReadOnly);
        }

        File.SetAttributes(directoryPath, File.GetAttributes(directoryPath) & ~FileAttributes.ReadOnly);
    }

    private static string ToBashPath (string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string fullPath = Path.GetFullPath(path).Replace('\\', '/');
        if (!OperatingSystem.IsWindows())
        {
            return fullPath;
        }

        if (fullPath.Length >= 2 && fullPath[1] == ':')
        {
            char driveLetter = char.ToLowerInvariant(fullPath[0]);
            return "/" + driveLetter + fullPath[2..];
        }

        return fullPath;
    }

    private static string ResolveBashFileName ()
    {
        if (!OperatingSystem.IsWindows())
        {
            return "bash";
        }

        foreach (string candidatePath in EnumerateGitBashCandidatePaths())
        {
            if (File.Exists(candidatePath))
            {
                return candidatePath;
            }
        }

        return "bash";
    }

    private static IEnumerable<string> EnumerateGitBashCandidatePaths ()
    {
        var visitedRootPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string?[] rootPaths =
        [
            Environment.GetEnvironmentVariable("ProgramFiles"),
            Environment.GetEnvironmentVariable("ProgramW6432"),
            Environment.GetEnvironmentVariable("ProgramFiles(x86)"),
        ];

        foreach (string? rootPath in rootPaths)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !visitedRootPaths.Add(rootPath))
            {
                continue;
            }

            yield return Path.Combine(rootPath, "Git", "bin", "bash.exe");
            yield return Path.Combine(rootPath, "Git", "usr", "bin", "bash.exe");
        }
    }

    private static async Task<ProcessResult> RunRequiredProcessAsync (
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string>? environment = null,
        CancellationToken cancellationToken = default)
    {
        ProcessResult result = await RunProcessAsync(
            fileName,
            arguments,
            workingDirectory,
            environment,
            cancellationToken);
        Assert.True(
            result.ExitCode == 0,
            $"{fileName} {string.Join(" ", arguments)} failed in {workingDirectory} with exit code {result.ExitCode}." +
            $"{Environment.NewLine}StdOut:{Environment.NewLine}{result.StdOut}" +
            $"{Environment.NewLine}StdErr:{Environment.NewLine}{result.StdErr}");
        return result;
    }

    private static async Task<ProcessResult> RunProcessAsync (
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string>? environment = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        using var process = new Process();
        ProcessStartInfo startInfo = process.StartInfo;
        startInfo.FileName = fileName;
        startInfo.WorkingDirectory = workingDirectory;
        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.StandardOutputEncoding = Encoding.UTF8;
        startInfo.StandardErrorEncoding = Encoding.UTF8;
        startInfo.CreateNoWindow = true;
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (environment is not null)
        {
            foreach ((string name, string value) in environment)
            {
                startInfo.Environment[name] = value;
            }
        }

        bool started = process.Start();
        Assert.True(started, $"Failed to start process: {fileName}");

        Task<string> stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(ProcessTimeout);
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKillProcess(process);
            throw new TimeoutException($"{fileName} did not exit within {ProcessTimeout}.");
        }

        return new ProcessResult(
            process.ExitCode,
            await stdOutTask,
            await stdErrTask);
    }

    private static void TryKillProcess (Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
        catch (NotSupportedException)
        {
        }
    }

    private static string NormalizeRepositoryRelativePath (string fullPath)
    {
        return Path.GetRelativePath(RepositoryRoot, fullPath).Replace('\\', '/');
    }

    private static string FindRepositoryRoot ()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDirectory != null)
        {
            if (File.Exists(Path.Combine(currentDirectory.FullName, "Ucli.slnx")))
            {
                return currentDirectory.FullName;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from test output directory.");
    }

    private sealed record ProcessResult (int ExitCode, string StdOut, string StdErr);
}
