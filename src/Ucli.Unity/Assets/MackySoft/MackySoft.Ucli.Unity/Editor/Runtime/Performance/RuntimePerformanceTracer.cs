using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Records opt-in runtime request performance traces with wall-time and allocation sections. </summary>
    internal static class RuntimePerformanceTracer
    {
        internal const string TraceDirectoryEnvironmentVariableName = "UCLI_RUNTIME_TRACE_DIR";
        internal const string TraceSessionEnvironmentVariableName = "UCLI_RUNTIME_TRACE_SESSION";

        private const int SchemaVersion = 1;
        private const string RuntimeName = "unity-editor";
        private const string UnknownResponseStatus = "unknown";

        private static readonly object SyncRoot = new object();
        private static readonly AsyncLocal<RuntimePerformanceTraceContext?> CurrentContext = new AsyncLocal<RuntimePerformanceTraceContext?>();
        private static readonly Dictionary<string, int> WorkloadIterations = new Dictionary<string, int>(StringComparer.Ordinal);

        private static RuntimePerformanceTraceSettings? cachedSettings;
        private static RuntimePerformanceTraceSettings? testSettings;

        /// <summary> Gets a value indicating whether runtime performance tracing is currently enabled. </summary>
        public static bool IsEnabled => ResolveSettings().IsEnabled;

        /// <summary> Creates one detached section measurement for work that happens before the request context exists. </summary>
        /// <param name="name"> The section name written to the trace output. </param>
        /// <returns> A measurement that can be completed after the section finishes. </returns>
        public static RuntimePerformanceSectionMeasurement StartDetachedSection (string name)
        {
            if (!ResolveSettings().IsEnabled || string.IsNullOrWhiteSpace(name))
            {
                return default;
            }

            return RuntimePerformanceSectionMeasurement.Start(name);
        }

        /// <summary> Begins one request trace and installs it as the ambient runtime performance context. </summary>
        /// <param name="request"> The decoded IPC request envelope. </param>
        /// <param name="receiveSection"> The optional IPC receive section that completed before request tracing started. </param>
        /// <returns> A request trace scope. Disposing the scope writes one JSON trace when tracing is enabled. </returns>
        public static RuntimePerformanceRequestScope BeginRequest (
            IpcRequest request,
            RuntimePerformanceCompletedSection receiveSection = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var settings = ResolveSettings();
            if (!settings.IsEnabled)
            {
                return default;
            }

            var command = ResolveCommand(request);
            var workloadKey = CreateWorkloadKey(request.Method, command);
            var iteration = IncrementWorkloadIteration(workloadKey);
            var context = RuntimePerformanceTraceContext.Start(
                settings,
                request,
                command,
                iteration,
                receiveSection);
            var previousContext = CurrentContext.Value;
            CurrentContext.Value = context;
            return new RuntimePerformanceRequestScope(context, previousContext);
        }

        /// <summary> Begins one section in the active request trace. </summary>
        /// <param name="name"> The section name written to the trace output. </param>
        /// <returns> A section scope that records elapsed wall-time and allocations when disposed. </returns>
        public static RuntimePerformanceSectionScope Measure (string name)
        {
            if (!ResolveSettings().IsEnabled || string.IsNullOrWhiteSpace(name))
            {
                return default;
            }

            var context = CurrentContext.Value;
            if (context == null)
            {
                return default;
            }

            return new RuntimePerformanceSectionScope(context, RuntimePerformanceSectionMeasurement.Start(name));
        }

        /// <summary> Overrides trace settings for deterministic tests. </summary>
        /// <param name="outputDirectory"> The directory that receives request trace JSON files.</param>
        /// <param name="sessionId"> The session identifier written to the trace output.</param>
        /// <returns> A disposable scope that restores the previous settings.</returns>
        internal static IDisposable OverrideForTests (
            string outputDirectory,
            string sessionId)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new ArgumentException("Output directory must not be empty.", nameof(outputDirectory));
            }

            if (string.IsNullOrWhiteSpace(sessionId))
            {
                throw new ArgumentException("Session id must not be empty.", nameof(sessionId));
            }

            lock (SyncRoot)
            {
                var previousTestSettings = testSettings;
                var previousCachedSettings = cachedSettings;
                testSettings = RuntimePerformanceTraceSettings.Enabled(
                    Path.GetFullPath(outputDirectory),
                    sessionId);
                cachedSettings = testSettings;
                WorkloadIterations.Clear();
                CurrentContext.Value = null;
                return new TestSettingsScope(previousTestSettings, previousCachedSettings);
            }
        }

        private static RuntimePerformanceTraceSettings ResolveSettings ()
        {
            var settings = Volatile.Read(ref cachedSettings);
            if (settings != null)
            {
                return settings;
            }

            lock (SyncRoot)
            {
                if (cachedSettings != null)
                {
                    return cachedSettings;
                }

                cachedSettings = testSettings ?? ReadSettingsFromEnvironment();
                return cachedSettings;
            }
        }

        private static RuntimePerformanceTraceSettings ReadSettingsFromEnvironment ()
        {
            var outputDirectory = Environment.GetEnvironmentVariable(TraceDirectoryEnvironmentVariableName);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                return RuntimePerformanceTraceSettings.Disabled;
            }

            var sessionId = Environment.GetEnvironmentVariable(TraceSessionEnvironmentVariableName);
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                sessionId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
            }

            return RuntimePerformanceTraceSettings.Enabled(
                Path.GetFullPath(outputDirectory),
                sessionId);
        }

        private static int IncrementWorkloadIteration (string workloadKey)
        {
            lock (SyncRoot)
            {
                WorkloadIterations.TryGetValue(workloadKey, out var current);
                var next = checked(current + 1);
                WorkloadIterations[workloadKey] = next;
                return next;
            }
        }

        private static string CreateWorkloadKey (
            string method,
            string? command)
        {
            return string.IsNullOrWhiteSpace(command)
                ? method
                : method + ":" + command;
        }

        private static string? ResolveCommand (IpcRequest request)
        {
            if (!string.Equals(request.Method, IpcMethodNames.Execute, StringComparison.Ordinal)
                || request.Payload.ValueKind != JsonValueKind.Object
                || !request.Payload.TryGetProperty("command", out var commandElement)
                || commandElement.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return commandElement.GetString();
        }

        private static void WriteTrace (RuntimePerformanceTraceDocument document)
        {
            try
            {
                Directory.CreateDirectory(document.OutputDirectory);
                var fileName = CreateTraceFileName(document);
                var tracePath = Path.Combine(document.OutputDirectory, fileName);
                using var stream = new FileStream(tracePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
                {
                    Indented = true,
                });
                WriteTraceJson(writer, document);
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or JsonException
                or ArgumentException
                or NotSupportedException)
            {
                UnityEngine.Debug.LogWarning($"uCLI runtime performance trace write failed. {exception.Message}");
            }
        }

        private static string CreateTraceFileName (RuntimePerformanceTraceDocument document)
        {
            return string.Concat(
                document.StartedAtUtc.UtcDateTime.ToString("yyyyMMddTHHmmss.fffffffZ", CultureInfo.InvariantCulture),
                "_",
                document.Iteration.ToString(CultureInfo.InvariantCulture),
                "_",
                SanitizeFileNameFragment(document.Method),
                "_",
                SanitizeFileNameFragment(document.Command ?? "method"),
                "_",
                document.Warmth,
                "_",
                document.TraceId,
                ".json");
        }

        private static string SanitizeFileNameFragment (string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unknown";
            }

            var invalidCharacters = Path.GetInvalidFileNameChars();
            var buffer = value.ToCharArray();
            for (var i = 0; i < buffer.Length; i++)
            {
                var character = buffer[i];
                if (char.IsWhiteSpace(character) || Array.IndexOf(invalidCharacters, character) >= 0)
                {
                    buffer[i] = '_';
                }
            }

            return new string(buffer);
        }

        private static void WriteTraceJson (
            Utf8JsonWriter writer,
            RuntimePerformanceTraceDocument document)
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", SchemaVersion);
            writer.WriteString("traceId", document.TraceId);
            writer.WriteString("sessionId", document.SessionId);
            writer.WriteString("runtime", RuntimeName);
            writer.WriteString("requestId", document.RequestId);
            writer.WriteString("method", document.Method);
            WriteNullableString(writer, "command", document.Command);
            writer.WriteString("status", document.ResponseStatus);
            writer.WriteString("startedAtUtc", document.StartedAtUtc.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteString("warmth", document.Warmth);
            writer.WriteNumber("iteration", document.Iteration);
            writer.WritePropertyName("allocation");
            writer.WriteStartObject();
            writer.WriteString("provider", RuntimePerformanceAllocationCounter.ProviderName);
            writer.WriteBoolean("supported", RuntimePerformanceAllocationCounter.IsSupported);
            writer.WriteEndObject();
            writer.WritePropertyName("total");
            WriteSection(writer, document.Total);
            writer.WritePropertyName("sections");
            writer.WriteStartArray();
            for (var i = 0; i < document.Sections.Count; i++)
            {
                WriteSection(writer, document.Sections[i]);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private static void WriteSection (
            Utf8JsonWriter writer,
            RuntimePerformanceCompletedSection section)
        {
            writer.WriteStartObject();
            writer.WriteString("name", section.Name);
            writer.WriteNumber("wallTimeMs", section.WallTimeMilliseconds);
            WriteNullableNumber(writer, "allocatedBytes", section.AllocatedBytes);
            writer.WriteEndObject();
        }

        private static void WriteNullableString (
            Utf8JsonWriter writer,
            string propertyName,
            string? value)
        {
            writer.WritePropertyName(propertyName);
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStringValue(value);
        }

        private static void WriteNullableNumber (
            Utf8JsonWriter writer,
            string propertyName,
            long? value)
        {
            writer.WritePropertyName(propertyName);
            if (!value.HasValue)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteNumberValue(value.Value);
        }

        internal static class SectionNames
        {
            public const string ArtifactWrite = "artifact.write";
            public const string Dispatch = "dispatch";
            public const string IpcReceive = "ipc.receive";
            public const string IpcResponseWrite = "ipc.response_write";
            public const string LogExport = "log.export";
            public const string Normalize = "normalize";
            public const string PhaseCall = "phase.call";
            public const string PhasePlan = "phase.plan";
            public const string UnityMainThread = "unity.main_thread";
            public const string Validate = "validate";
        }

        internal readonly struct RuntimePerformanceSectionMeasurement
        {
            private readonly string? name;
            private readonly RuntimePerformanceMeasurementPoint start;

            private RuntimePerformanceSectionMeasurement (
                string name,
                RuntimePerformanceMeasurementPoint start)
            {
                this.name = name;
                this.start = start;
            }

            public bool IsActive => name != null;

            public static RuntimePerformanceSectionMeasurement Start (string name)
            {
                return new RuntimePerformanceSectionMeasurement(
                    name,
                    RuntimePerformanceMeasurementPoint.Capture());
            }

            public static RuntimePerformanceSectionMeasurement StartAt (
                string name,
                RuntimePerformanceCompletedSection section)
            {
                return new RuntimePerformanceSectionMeasurement(
                    name,
                    RuntimePerformanceMeasurementPoint.FromSectionStart(section));
            }

            public RuntimePerformanceCompletedSection Stop ()
            {
                if (name == null)
                {
                    return default;
                }

                return RuntimePerformanceCompletedSection.Create(
                    name,
                    start,
                    RuntimePerformanceMeasurementPoint.Capture());
            }
        }

        internal readonly struct RuntimePerformanceCompletedSection
        {
            private static readonly double StopwatchTickMilliseconds = 1000d / Stopwatch.Frequency;

            private RuntimePerformanceCompletedSection (
                string name,
                long startTimestamp,
                long endTimestamp,
                long? startAllocatedBytes,
                int startThreadId,
                long? allocatedBytes)
            {
                Name = name;
                StartTimestamp = startTimestamp;
                EndTimestamp = endTimestamp;
                StartAllocatedBytes = startAllocatedBytes;
                StartThreadId = startThreadId;
                AllocatedBytes = allocatedBytes;
            }

            public string Name { get; }

            public long StartTimestamp { get; }

            public long EndTimestamp { get; }

            public long? StartAllocatedBytes { get; }

            public int StartThreadId { get; }

            public bool IsActive => Name != null;

            public long? AllocatedBytes { get; }

            public double WallTimeMilliseconds => Math.Max(0d, (EndTimestamp - StartTimestamp) * StopwatchTickMilliseconds);

            public static RuntimePerformanceCompletedSection Create (
                string name,
                RuntimePerformanceMeasurementPoint start,
                RuntimePerformanceMeasurementPoint end)
            {
                long? allocatedBytes = null;
                if (start.ThreadId == end.ThreadId
                    && start.AllocatedBytes.HasValue
                    && end.AllocatedBytes.HasValue
                    && end.AllocatedBytes.Value >= start.AllocatedBytes.Value)
                {
                    allocatedBytes = end.AllocatedBytes.Value - start.AllocatedBytes.Value;
                }

                return new RuntimePerformanceCompletedSection(
                    name,
                    start.Timestamp,
                    end.Timestamp,
                    start.AllocatedBytes,
                    start.ThreadId,
                    allocatedBytes);
            }
        }

        internal struct RuntimePerformanceRequestScope : IDisposable
        {
            private readonly RuntimePerformanceTraceContext? context;
            private readonly RuntimePerformanceTraceContext? previousContext;

            internal RuntimePerformanceRequestScope (
                RuntimePerformanceTraceContext context,
                RuntimePerformanceTraceContext? previousContext)
            {
                this.context = context;
                this.previousContext = previousContext;
            }

            public void SetResponse (IpcResponse response)
            {
                if (response == null)
                {
                    throw new ArgumentNullException(nameof(response));
                }

                context?.SetResponseStatus(response.Status);
            }

            public void Dispose ()
            {
                if (context == null)
                {
                    return;
                }

                try
                {
                    WriteTrace(context.Complete());
                }
                finally
                {
                    CurrentContext.Value = previousContext;
                }
            }
        }

        internal readonly struct RuntimePerformanceSectionScope : IDisposable
        {
            private readonly RuntimePerformanceTraceContext? context;
            private readonly RuntimePerformanceSectionMeasurement measurement;

            internal RuntimePerformanceSectionScope (
                RuntimePerformanceTraceContext context,
                RuntimePerformanceSectionMeasurement measurement)
            {
                this.context = context;
                this.measurement = measurement;
            }

            public void Dispose ()
            {
                if (context == null || !measurement.IsActive)
                {
                    return;
                }

                context.AddSection(measurement.Stop());
            }
        }

        internal readonly struct RuntimePerformanceMeasurementPoint
        {
            private RuntimePerformanceMeasurementPoint (
                long timestamp,
                long? allocatedBytes,
                int threadId)
            {
                Timestamp = timestamp;
                AllocatedBytes = allocatedBytes;
                ThreadId = threadId;
            }

            public long Timestamp { get; }

            public long? AllocatedBytes { get; }

            public int ThreadId { get; }

            public static RuntimePerformanceMeasurementPoint Capture ()
            {
                return new RuntimePerformanceMeasurementPoint(
                    Stopwatch.GetTimestamp(),
                    RuntimePerformanceAllocationCounter.CaptureAllocatedBytes(),
                    Thread.CurrentThread.ManagedThreadId);
            }

            public static RuntimePerformanceMeasurementPoint FromSectionStart (RuntimePerformanceCompletedSection section)
            {
                return new RuntimePerformanceMeasurementPoint(
                    section.StartTimestamp,
                    section.StartAllocatedBytes,
                    section.StartThreadId);
            }
        }

        internal sealed class RuntimePerformanceTraceContext
        {
            private readonly object syncRoot = new object();
            private readonly RuntimePerformanceTraceSettings settings;
            private readonly RuntimePerformanceSectionMeasurement totalMeasurement;
            private readonly List<RuntimePerformanceCompletedSection> sections;

            private RuntimePerformanceTraceContext (
                RuntimePerformanceTraceSettings settings,
                IpcRequest request,
                string? command,
                int iteration,
                RuntimePerformanceSectionMeasurement totalMeasurement,
                IReadOnlyList<RuntimePerformanceCompletedSection> initialSections)
            {
                this.settings = settings;
                this.totalMeasurement = totalMeasurement;
                TraceId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
                RequestId = request.RequestId;
                Method = request.Method;
                Command = command;
                Iteration = iteration;
                Warmth = iteration == 1 ? "cold" : "warm";
                StartedAtUtc = DateTimeOffset.UtcNow;
                ResponseStatus = UnknownResponseStatus;
                sections = new List<RuntimePerformanceCompletedSection>(Math.Max(16, initialSections.Count + 8));
                for (var i = 0; i < initialSections.Count; i++)
                {
                    sections.Add(initialSections[i]);
                }
            }

            public string TraceId { get; }

            public string RequestId { get; }

            public string Method { get; }

            public string? Command { get; }

            public int Iteration { get; }

            public string Warmth { get; }

            public DateTimeOffset StartedAtUtc { get; }

            public string ResponseStatus { get; private set; }

            public static RuntimePerformanceTraceContext Start (
                RuntimePerformanceTraceSettings settings,
                IpcRequest request,
                string? command,
                int iteration,
                RuntimePerformanceCompletedSection receiveSection)
            {
                var initialSections = receiveSection.IsActive
                    ? new[] { receiveSection }
                    : Array.Empty<RuntimePerformanceCompletedSection>();
                var totalMeasurement = receiveSection.IsActive
                    ? RuntimePerformanceSectionMeasurement.StartAt("total", receiveSection)
                    : RuntimePerformanceSectionMeasurement.Start("total");
                return new RuntimePerformanceTraceContext(
                    settings,
                    request,
                    command,
                    iteration,
                    totalMeasurement,
                    initialSections);
            }

            public void AddSection (RuntimePerformanceCompletedSection section)
            {
                if (!section.IsActive)
                {
                    return;
                }

                lock (syncRoot)
                {
                    sections.Add(section);
                }
            }

            public void SetResponseStatus (string status)
            {
                ResponseStatus = string.IsNullOrWhiteSpace(status) ? UnknownResponseStatus : status;
            }

            public RuntimePerformanceTraceDocument Complete ()
            {
                RuntimePerformanceCompletedSection[] sectionSnapshot;
                lock (syncRoot)
                {
                    sectionSnapshot = sections.ToArray();
                }

                return new RuntimePerformanceTraceDocument(
                    settings.OutputDirectory!,
                    settings.SessionId!,
                    TraceId,
                    RequestId,
                    Method,
                    Command,
                    ResponseStatus,
                    StartedAtUtc,
                    Warmth,
                    Iteration,
                    totalMeasurement.Stop(),
                    sectionSnapshot);
            }
        }

        internal sealed class RuntimePerformanceTraceDocument
        {
            public RuntimePerformanceTraceDocument (
                string outputDirectory,
                string sessionId,
                string traceId,
                string requestId,
                string method,
                string? command,
                string responseStatus,
                DateTimeOffset startedAtUtc,
                string warmth,
                int iteration,
                RuntimePerformanceCompletedSection total,
                IReadOnlyList<RuntimePerformanceCompletedSection> sections)
            {
                OutputDirectory = outputDirectory;
                SessionId = sessionId;
                TraceId = traceId;
                RequestId = requestId;
                Method = method;
                Command = command;
                ResponseStatus = responseStatus;
                StartedAtUtc = startedAtUtc;
                Warmth = warmth;
                Iteration = iteration;
                Total = total;
                Sections = sections;
            }

            public string OutputDirectory { get; }

            public string SessionId { get; }

            public string TraceId { get; }

            public string RequestId { get; }

            public string Method { get; }

            public string? Command { get; }

            public string ResponseStatus { get; }

            public DateTimeOffset StartedAtUtc { get; }

            public string Warmth { get; }

            public int Iteration { get; }

            public RuntimePerformanceCompletedSection Total { get; }

            public IReadOnlyList<RuntimePerformanceCompletedSection> Sections { get; }
        }

        internal sealed class RuntimePerformanceTraceSettings
        {
            public static RuntimePerformanceTraceSettings Disabled { get; } = new RuntimePerformanceTraceSettings(false, null, null);

            private RuntimePerformanceTraceSettings (
                bool isEnabled,
                string? outputDirectory,
                string? sessionId)
            {
                IsEnabled = isEnabled;
                OutputDirectory = outputDirectory;
                SessionId = sessionId;
            }

            public bool IsEnabled { get; }

            public string? OutputDirectory { get; }

            public string? SessionId { get; }

            public static RuntimePerformanceTraceSettings Enabled (
                string outputDirectory,
                string sessionId)
            {
                return new RuntimePerformanceTraceSettings(true, outputDirectory, sessionId);
            }
        }

        private static class RuntimePerformanceAllocationCounter
        {
            public const string ProviderName = "GC.GetAllocatedBytesForCurrentThread";

            private static readonly Func<long>? AllocatedBytesProvider = CreateAllocatedBytesProvider();

            public static bool IsSupported => AllocatedBytesProvider != null;

            public static long? CaptureAllocatedBytes ()
            {
                return AllocatedBytesProvider?.Invoke();
            }

            private static Func<long>? CreateAllocatedBytesProvider ()
            {
                var method = typeof(GC).GetMethod(
                    "GetAllocatedBytesForCurrentThread",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    Type.EmptyTypes,
                    null);
                if (method == null || method.ReturnType != typeof(long))
                {
                    return null;
                }

                try
                {
                    return (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method);
                }
                catch (ArgumentException)
                {
                    return null;
                }
            }
        }

        private sealed class TestSettingsScope : IDisposable
        {
            private readonly RuntimePerformanceTraceSettings? previousTestSettings;
            private readonly RuntimePerformanceTraceSettings? previousCachedSettings;

            public TestSettingsScope (
                RuntimePerformanceTraceSettings? previousTestSettings,
                RuntimePerformanceTraceSettings? previousCachedSettings)
            {
                this.previousTestSettings = previousTestSettings;
                this.previousCachedSettings = previousCachedSettings;
            }

            public void Dispose ()
            {
                lock (SyncRoot)
                {
                    testSettings = previousTestSettings;
                    cachedSettings = previousCachedSettings;
                    WorkloadIterations.Clear();
                    CurrentContext.Value = null;
                }
            }
        }
    }
}
