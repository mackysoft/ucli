using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class RuntimePerformanceTracerTests
    {
        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator RequestTrace_WhenSameWorkloadRunsTwice_WritesColdAndWarmJson () => UniTask.ToCoroutine(async () =>
        {
            var traceDirectory = Path.Combine(Path.GetTempPath(), "ucli-runtime-trace-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(traceDirectory);

            try
            {
                using (RuntimePerformanceTracer.OverrideForTests(traceDirectory, "test-session"))
                {
                    var handler = new UnityIpcConnectionHandler(new TracingRequestProcessor());

                    await HandleShutdownAsync(handler, "req-trace-1");
                    await HandleShutdownAsync(handler, "req-trace-2");
                }

                var traces = Directory.GetFiles(traceDirectory, "*.json")
                    .Select(ReadTrace)
                    .OrderBy(static trace => trace.Iteration)
                    .ToArray();

                Assert.That(traces.Length, Is.EqualTo(2));
                Assert.That(traces[0].SessionId, Is.EqualTo("test-session"));
                Assert.That(traces[0].Runtime, Is.EqualTo("unity-editor"));
                Assert.That(traces[0].RequestId, Is.EqualTo("req-trace-1"));
                Assert.That(traces[0].Method, Is.EqualTo(IpcMethodNames.Shutdown));
                Assert.That(traces[0].CommandIsNull, Is.True);
                Assert.That(traces[0].Status, Is.EqualTo(IpcProtocol.StatusOk));
                Assert.That(traces[0].Warmth, Is.EqualTo("cold"));
                Assert.That(traces[0].Iteration, Is.EqualTo(1));
                Assert.That(traces[0].AllocationProvider, Is.EqualTo("GC.GetAllocatedBytesForCurrentThread"));
                Assert.That(traces[0].AllocationSupportedIsBoolean, Is.True);
                Assert.That(traces[0].TotalWallTimeMilliseconds, Is.GreaterThanOrEqualTo(0d));
                Assert.That(traces[0].AllSectionsExposeAllocatedBytes, Is.True);
                Assert.That(traces[0].SectionNames, Does.Contain(RuntimePerformanceTracer.SectionNames.IpcReceive));
                Assert.That(traces[0].SectionNames, Does.Contain("test.section"));
                Assert.That(traces[0].SectionNames, Does.Contain(RuntimePerformanceTracer.SectionNames.IpcResponseWrite));

                Assert.That(traces[1].RequestId, Is.EqualTo("req-trace-2"));
                Assert.That(traces[1].Warmth, Is.EqualTo("warm"));
                Assert.That(traces[1].Iteration, Is.EqualTo(2));
            }
            finally
            {
                if (Directory.Exists(traceDirectory))
                {
                    Directory.Delete(traceDirectory, recursive: true);
                }
            }
        });

        private static async Task HandleShutdownAsync (
            UnityIpcConnectionHandler handler,
            string requestId)
        {
            var request = new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: requestId,
                SessionToken: "token",
                Method: IpcMethodNames.Shutdown,
                Payload: IpcPayloadCodec.SerializeToElement(new IpcShutdownRequest("tests")),
                responseMode: IpcResponseMode.Single);

            using var stream = new MemoryStream();
            await IpcFrameCodec.WriteModelAsync(
                stream,
                request,
                IpcJsonSerializerOptions.Default,
                cancellationToken: CancellationToken.None);
            stream.Position = 0;

            await handler.HandleAsync(stream, CancellationToken.None);
        }

        private static RuntimeTraceProjection ReadTrace (string path)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            var allocation = root.GetProperty("allocation");
            var sectionNames = new List<string>();
            var allSectionsExposeAllocatedBytes = true;
            foreach (var section in root.GetProperty("sections").EnumerateArray())
            {
                sectionNames.Add(section.GetProperty("name").GetString());
                allSectionsExposeAllocatedBytes &= section.TryGetProperty("allocatedBytes", out _);
            }

            return new RuntimeTraceProjection(
                sessionId: root.GetProperty("sessionId").GetString(),
                runtime: root.GetProperty("runtime").GetString(),
                requestId: root.GetProperty("requestId").GetString(),
                method: root.GetProperty("method").GetString(),
                commandIsNull: root.GetProperty("command").ValueKind == JsonValueKind.Null,
                status: root.GetProperty("status").GetString(),
                warmth: root.GetProperty("warmth").GetString(),
                iteration: root.GetProperty("iteration").GetInt32(),
                allocationProvider: allocation.GetProperty("provider").GetString(),
                allocationSupportedIsBoolean: allocation.GetProperty("supported").ValueKind is JsonValueKind.True or JsonValueKind.False,
                totalWallTimeMilliseconds: root.GetProperty("total").GetProperty("wallTimeMs").GetDouble(),
                allSectionsExposeAllocatedBytes: allSectionsExposeAllocatedBytes,
                sectionNames: sectionNames);
        }

        private sealed class TracingRequestProcessor : IUnityIpcRequestProcessor
        {
            public async Task<IpcResponse> ProcessAsync (
                IpcRequest request,
                CancellationToken cancellationToken = default)
            {
                using (RuntimePerformanceTracer.Measure("test.section"))
                {
                    await Task.Yield();
                }

                return new IpcResponse(
                    ProtocolVersion: IpcProtocol.CurrentVersion,
                    RequestId: request.RequestId,
                    Status: IpcProtocol.StatusOk,
                    Payload: IpcPayloadCodec.SerializeToElement(new IpcShutdownResponse(true, "ok")),
                    Errors: Array.Empty<IpcError>());
            }

            public Task<IpcResponse> ProcessStreamingAsync (
                IpcRequest request,
                IIpcStreamFrameWriter streamWriter,
                CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class RuntimeTraceProjection
        {
            public RuntimeTraceProjection (
                string sessionId,
                string runtime,
                string requestId,
                string method,
                bool commandIsNull,
                string status,
                string warmth,
                int iteration,
                string allocationProvider,
                bool allocationSupportedIsBoolean,
                double totalWallTimeMilliseconds,
                bool allSectionsExposeAllocatedBytes,
                IReadOnlyList<string> sectionNames)
            {
                SessionId = sessionId;
                Runtime = runtime;
                RequestId = requestId;
                Method = method;
                CommandIsNull = commandIsNull;
                Status = status;
                Warmth = warmth;
                Iteration = iteration;
                AllocationProvider = allocationProvider;
                AllocationSupportedIsBoolean = allocationSupportedIsBoolean;
                TotalWallTimeMilliseconds = totalWallTimeMilliseconds;
                AllSectionsExposeAllocatedBytes = allSectionsExposeAllocatedBytes;
                SectionNames = sectionNames;
            }

            public string SessionId { get; }

            public string Runtime { get; }

            public string RequestId { get; }

            public string Method { get; }

            public bool CommandIsNull { get; }

            public string Status { get; }

            public string Warmth { get; }

            public int Iteration { get; }

            public string AllocationProvider { get; }

            public bool AllocationSupportedIsBoolean { get; }

            public double TotalWallTimeMilliseconds { get; }

            public bool AllSectionsExposeAllocatedBytes { get; }

            public IReadOnlyList<string> SectionNames { get; }
        }
    }
}
