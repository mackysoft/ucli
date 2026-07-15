using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Testing;

namespace MackySoft.Ucli.Application.Shared.Execution.UnityRequest;

/// <summary> Represents a host-executed Unity request without owning the IPC wire envelope. </summary>
internal abstract record UnityRequestPayload
{
    /// <summary> Represents an operation catalog read request prepared by application orchestration. </summary>
    internal sealed record OpsRead (
        bool FailFast = false,
        bool RequireReadinessGate = false,
        bool IncludeEditLoweringOnly = false) : UnityRequestPayload;

    /// <summary> Represents an asset index read request prepared by application orchestration. </summary>
    internal sealed record IndexAssetsRead (
        bool FailFast = false) : UnityRequestPayload;

    /// <summary> Represents a scene tree read request prepared by application orchestration. </summary>
    internal sealed record IndexSceneTreeLiteRead : UnityRequestPayload
    {
        /// <summary> Initializes a scene tree read request. </summary>
        public IndexSceneTreeLiteRead (
            UnityScenePath scenePath,
            bool failFast = false,
            bool loadedSceneOnly = false)
        {
            ScenePath = scenePath ?? throw new ArgumentNullException(nameof(scenePath));
            FailFast = failFast;
            LoadedSceneOnly = loadedSceneOnly;
        }

        /// <summary> Gets the project-relative scene path to read. </summary>
        public UnityScenePath ScenePath { get; }

        /// <summary> Gets whether readiness gating fails immediately. </summary>
        public bool FailFast { get; }

        /// <summary> Gets whether only an already loaded scene may be read. </summary>
        public bool LoadedSceneOnly { get; }
    }

    /// <summary> Represents a lifecycle ping request prepared by application orchestration. </summary>
    internal sealed record Ping (
        string ClientVersion,
        bool FailFast = false) : UnityRequestPayload;

    /// <summary> Represents a compile assurance request prepared by application orchestration. </summary>
    internal sealed record Compile : UnityRequestPayload
    {
        /// <summary> Initializes a compile request for one identified assurance run. </summary>
        /// <param name="runId"> The non-empty run identifier used for progress and result correlation. </param>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="runId" /> is empty. </exception>
        public Compile (Guid runId)
        {
            if (runId == Guid.Empty)
            {
                throw new ArgumentException("Run id must not be empty.", nameof(runId));
            }

            RunId = runId;
        }

        /// <summary> Gets the non-empty assurance run identifier. </summary>
        public Guid RunId { get; }
    }

    /// <summary> Represents a build assurance request prepared by application orchestration. </summary>
    internal sealed record BuildRun : UnityRequestPayload
    {
        /// <summary> Initializes a build payload from a validated IPC request. </summary>
        /// <param name="request"> The validated build request to dispatch without reconstructing its contract. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="request" /> is <see langword="null" />. </exception>
        public BuildRun (IpcBuildRunRequest request)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
        }

        /// <summary> Gets the validated IPC request represented by this application payload. </summary>
        public IpcBuildRunRequest Request { get; }
    }

    /// <summary> Represents a Unity Test Framework run request prepared by application orchestration. </summary>
    internal sealed record TestRun : UnityRequestPayload
    {
        /// <summary> Initializes a normalized Unity Test Framework request for one identified run. </summary>
        /// <param name="testPlatform"> The canonical Unity test platform value. </param>
        /// <param name="testFilter"> The optional test-name filter. </param>
        /// <param name="testCategories"> The validated test-category filters. </param>
        /// <param name="assemblyNames"> The validated assembly-name filters. </param>
        /// <param name="failFast"> Whether readiness gating fails immediately. </param>
        /// <param name="runId"> The non-empty run identifier used for progress, artifacts, and result correlation. </param>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="runId" /> is empty. </exception>
        /// <exception cref="ArgumentNullException"> Thrown when a required reference value is <see langword="null" />. </exception>
        public TestRun (
            TestRunPlatform testPlatform,
            string? testFilter,
            IReadOnlyList<string> testCategories,
            IReadOnlyList<string> assemblyNames,
            bool failFast,
            Guid runId)
        {
            if (runId == Guid.Empty)
            {
                throw new ArgumentException("Run id must not be empty.", nameof(runId));
            }

            ArgumentNullException.ThrowIfNull(testCategories);
            ArgumentNullException.ThrowIfNull(assemblyNames);
            if (testFilter is not null && string.IsNullOrWhiteSpace(testFilter))
            {
                throw new ArgumentException("Test filter must not be empty or whitespace.", nameof(testFilter));
            }

            TestPlatform = testPlatform;
            TestFilter = testFilter;
            TestCategories = CopyFilterValues(testCategories, nameof(testCategories));
            AssemblyNames = CopyFilterValues(assemblyNames, nameof(assemblyNames));
            FailFast = failFast;
            RunId = runId;
        }

        public TestRunPlatform TestPlatform { get; }

        public string? TestFilter { get; }

        public IReadOnlyList<string> TestCategories { get; }

        public IReadOnlyList<string> AssemblyNames { get; }

        public bool FailFast { get; }

        /// <summary> Gets the non-empty test run identifier. </summary>
        public Guid RunId { get; }

        private static IReadOnlyList<string> CopyFilterValues (
            IReadOnlyList<string> values,
            string parameterName)
        {
            if (values.Count == 0)
            {
                return Array.Empty<string>();
            }

            var copy = new string[values.Count];
            for (var index = 0; index < values.Count; index++)
            {
                if (string.IsNullOrWhiteSpace(values[index]))
                {
                    throw new ArgumentException(
                        $"Filter at index {index} must not be empty or whitespace.",
                        parameterName);
                }

                copy[index] = values[index];
            }

            return Array.AsReadOnly(copy);
        }
    }

    /// <summary> Represents a Play Mode status request prepared by application orchestration. </summary>
    internal sealed record PlayStatus : UnityRequestPayload;

    /// <summary> Represents a screenshot capture request prepared by application orchestration. </summary>
    internal sealed record ScreenshotCapture : UnityRequestPayload
    {
        /// <summary> Initializes a screenshot request payload variant. </summary>
        public ScreenshotCapture (IpcScreenshotCaptureRequest request)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
        }

        /// <summary> Gets the validated screenshot IPC payload. </summary>
        public IpcScreenshotCaptureRequest Request { get; }
    }

    /// <summary> Represents a Play Mode enter request prepared by application orchestration. </summary>
    internal sealed record PlayEnter : UnityRequestPayload;

    /// <summary> Represents a Play Mode exit request prepared by application orchestration. </summary>
    internal sealed record PlayExit : UnityRequestPayload;

    /// <summary> Represents an execute request whose execute-arguments JSON was already prepared. </summary>
    internal sealed record ExecuteJson (
        UcliCommand Command,
        JsonElement ExecuteArguments,
        bool FailFast,
        bool AllowDangerous = false,
        string? PlanToken = null,
        bool AllowPlayMode = false) : UnityRequestPayload;

    /// <summary> Represents a single-operation execute request prepared by application orchestration. </summary>
    internal sealed record ExecuteOperation (
        UcliCommand Command,
        IpcExecuteStepId OperationId,
        string OperationName,
        JsonElement Args,
        bool FailFast,
        bool AllowDangerous = false,
        string? PlanToken = null) : UnityRequestPayload;
}
