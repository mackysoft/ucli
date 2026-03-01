using System;
using System.Text;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class ExecuteRequestNormalizerTests
    {
        private const string k_RequestId = "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62";

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenRequestIsValid_ReturnsNormalizedRequestAndCanonicalPayload ()
        {
            var request = CreateExecuteRequest(
                IpcExecuteCommandNames.Plan,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = k_RequestId,
                    ops = new[]
                    {
                        new
                        {
                            id = "resolve",
                            op = "ucli.resolve",
                            args = new
                            {
                                scene = "Assets/Scenes/Main.unity",
                                hierarchyPath = "Root/Enemies/Spawner"
                            },
                            expect = new
                            {
                                nonNull = true,
                                min = 1,
                                max = 1
                            }
                        }
                    }
                });
            var normalizer = new ExecuteRequestNormalizer();

            var result = normalizer.Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Error, Is.Null);
            var normalizedRequest = result.Request!;
            Assert.That(normalizedRequest.ProtocolVersion, Is.EqualTo(IpcProtocol.CurrentVersion));
            Assert.That(normalizedRequest.RequestId, Is.EqualTo("9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62"));
            Assert.That(normalizedRequest.Ops.Count, Is.EqualTo(1));

            var canonicalPayload = Encoding.UTF8.GetString(normalizedRequest.CanonicalDigestPayloadUtf8.ToArray());
            Assert.That(canonicalPayload, Does.Not.Contain("requestId"));
            Assert.That(canonicalPayload, Does.Contain("\"protocolVersion\":1"));
            Assert.That(canonicalPayload, Does.Contain("\"ops\""));
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenPlanTokenIsSpecified_TrimsAndStoresPlanToken ()
        {
            var request = CreateExecuteRequest(
                IpcExecuteCommandNames.Call,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = k_RequestId,
                    ops = Array.Empty<object>(),
                });
            request = request with
            {
                PlanToken = "  issued-token  ",
            };

            var result = new ExecuteRequestNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Request!.PlanToken, Is.EqualTo("issued-token"));
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenCommandIsValidate_ReturnsInvalidArgumentError ()
        {
            var request = CreateExecuteRequest(
                IpcExecuteCommandNames.Validate,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = k_RequestId,
                    ops = Array.Empty<object>()
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            AssertInvalidArgument(result);
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenRequestJsonKeyOrderDiffers_ProducesStableCanonicalPayload ()
        {
            var requestA = CreateExecuteRequestFromJson(
                IpcExecuteCommandNames.Plan,
                "{\"protocolVersion\":1,\"requestId\":\"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62\",\"ops\":[{\"id\":\"setSpawner\",\"op\":\"ucli.comp.set\",\"args\":{\"mode\":\"atomic\",\"target\":{\"var\":\"spawner\"},\"sets\":[{\"path\":\"spawnInterval\",\"value\":3.0}]}}]}");
            var requestB = CreateExecuteRequestFromJson(
                IpcExecuteCommandNames.Plan,
                "{\"requestId\":\"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62\",\"ops\":[{\"op\":\"ucli.comp.set\",\"args\":{\"target\":{\"var\":\"spawner\"},\"sets\":[{\"value\":3.0,\"path\":\"spawnInterval\"}],\"mode\":\"atomic\"},\"id\":\"setSpawner\"}],\"protocolVersion\":1}");
            var normalizer = new ExecuteRequestNormalizer();

            var resultA = normalizer.Normalize(requestA);
            var resultB = normalizer.Normalize(requestB);

            Assert.That(resultA.IsSuccess, Is.True);
            Assert.That(resultB.IsSuccess, Is.True);
            Assert.That(resultA.Request!.CanonicalDigestPayloadUtf8.Span.SequenceEqual(resultB.Request!.CanonicalDigestPayloadUtf8.Span), Is.True);
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenProtocolVersionMismatches_ReturnsProtocolVersionMismatchError ()
        {
            var request = CreateExecuteRequest(
                IpcExecuteCommandNames.Call,
                new
                {
                    protocolVersion = 999,
                    requestId = k_RequestId,
                    ops = Array.Empty<object>()
                });
            var normalizer = new ExecuteRequestNormalizer();

            var result = normalizer.Normalize(request);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error!.Code, Is.EqualTo(IpcErrorCodes.ProtocolVersionMismatch));
            Assert.That(result.Error.OpId, Is.Null);
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenRequestIdIsInvalid_ReturnsInvalidArgumentError ()
        {
            var request = CreateExecuteRequest(
                IpcExecuteCommandNames.Call,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = "invalid",
                    ops = Array.Empty<object>()
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            AssertInvalidArgument(result);
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenTopLevelContainsUnknownProperty_ReturnsInvalidArgumentError ()
        {
            var request = CreateExecuteRequest(
                IpcExecuteCommandNames.Resolve,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = k_RequestId,
                    ops = Array.Empty<object>(),
                    unknown = true
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            AssertInvalidArgument(result);
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenOperationContainsUnknownProperty_ReturnsInvalidArgumentError ()
        {
            var request = CreateExecuteRequest(
                IpcExecuteCommandNames.Query,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = k_RequestId,
                    ops = new[]
                    {
                        new
                        {
                            id = "q1",
                            op = "ucli.query",
                            args = new { },
                            unknown = 1
                        }
                    }
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            AssertInvalidArgument(result);
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenOperationIdIsDuplicated_ReturnsInvalidArgumentError ()
        {
            var request = CreateExecuteRequest(
                IpcExecuteCommandNames.Refresh,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = k_RequestId,
                    ops = new[]
                    {
                        new
                        {
                            id = "same",
                            op = "ucli.project.refresh",
                            args = new { }
                        },
                        new
                        {
                            id = "same",
                            op = "ucli.project.refresh",
                            args = new { }
                        }
                    }
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            AssertInvalidArgument(result, "same");
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenArgsPropertyIsMissing_ReturnsInvalidArgumentError ()
        {
            var request = CreateExecuteRequest(
                IpcExecuteCommandNames.Plan,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = k_RequestId,
                    ops = new[]
                    {
                        new
                        {
                            id = "missingArgs",
                            op = "ucli.scene.open"
                        }
                    }
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            AssertInvalidArgument(result, "missingArgs");
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenArgsPropertyIsNotObject_ReturnsInvalidArgumentError ()
        {
            var request = CreateExecuteRequest(
                IpcExecuteCommandNames.Plan,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = k_RequestId,
                    ops = new[]
                    {
                        new
                        {
                            id = "argsType",
                            op = "ucli.scene.open",
                            args = Array.Empty<object>()
                        }
                    }
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            AssertInvalidArgument(result, "argsType");
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenExpectObjectIsEmpty_ReturnsInvalidArgumentError ()
        {
            var request = CreateExecuteRequest(
                IpcExecuteCommandNames.Plan,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = k_RequestId,
                    ops = new[]
                    {
                        new
                        {
                            id = "expectEmpty",
                            op = "ucli.resolve",
                            args = new { },
                            expect = new { }
                        }
                    }
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            AssertInvalidArgument(result, "expectEmpty");
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenExpectIsNotObject_ReturnsInvalidArgumentError ()
        {
            var request = CreateExecuteRequest(
                IpcExecuteCommandNames.Plan,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = k_RequestId,
                    ops = new[]
                    {
                        new
                        {
                            id = "expectType",
                            op = "ucli.resolve",
                            args = new { },
                            expect = true
                        }
                    }
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            AssertInvalidArgument(result, "expectType");
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenExpectContainsUnknownProperty_ReturnsInvalidArgumentError ()
        {
            var request = CreateExecuteRequest(
                IpcExecuteCommandNames.Plan,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = k_RequestId,
                    ops = new[]
                    {
                        new
                        {
                            id = "expectUnknown",
                            op = "ucli.resolve",
                            args = new { },
                            expect = new
                            {
                                nonNull = true,
                                unknown = 1
                            }
                        }
                    }
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            AssertInvalidArgument(result, "expectUnknown");
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenExpectNonNullIsNotBoolean_ReturnsInvalidArgumentError ()
        {
            var request = CreateExecuteRequest(
                IpcExecuteCommandNames.Plan,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = k_RequestId,
                    ops = new[]
                    {
                        new
                        {
                            id = "expectNonNullType",
                            op = "ucli.resolve",
                            args = new { },
                            expect = new
                            {
                                nonNull = 1
                            }
                        }
                    }
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            AssertInvalidArgument(result, "expectNonNullType");
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenExpectContainsCountAndMinMax_ReturnsInvalidArgumentError ()
        {
            var request = CreateExecuteRequest(
                IpcExecuteCommandNames.Plan,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = k_RequestId,
                    ops = new[]
                    {
                        new
                        {
                            id = "expectCount",
                            op = "ucli.resolve",
                            args = new { },
                            expect = new
                            {
                                count = 1,
                                min = 1
                            }
                        }
                    }
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            AssertInvalidArgument(result, "expectCount");
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenExpectCountIsNegative_ReturnsInvalidArgumentError ()
        {
            var request = CreateExecuteRequest(
                IpcExecuteCommandNames.Plan,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = k_RequestId,
                    ops = new[]
                    {
                        new
                        {
                            id = "expectCountNegative",
                            op = "ucli.resolve",
                            args = new { },
                            expect = new
                            {
                                count = -1
                            }
                        }
                    }
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            AssertInvalidArgument(result, "expectCountNegative");
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenExpectCountIsNotInteger_ReturnsInvalidArgumentError ()
        {
            var request = CreateExecuteRequest(
                IpcExecuteCommandNames.Plan,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = k_RequestId,
                    ops = new[]
                    {
                        new
                        {
                            id = "expectCountType",
                            op = "ucli.resolve",
                            args = new { },
                            expect = new
                            {
                                count = "1"
                            }
                        }
                    }
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            AssertInvalidArgument(result, "expectCountType");
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenExpectMinGreaterThanMax_ReturnsInvalidArgumentError ()
        {
            var request = CreateExecuteRequest(
                IpcExecuteCommandNames.Plan,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = k_RequestId,
                    ops = new[]
                    {
                        new
                        {
                            id = "expectRange",
                            op = "ucli.resolve",
                            args = new { },
                            expect = new
                            {
                                min = 5,
                                max = 2
                            }
                        }
                    }
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            AssertInvalidArgument(result, "expectRange");
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenExpectMaxIsNegative_ReturnsInvalidArgumentError ()
        {
            var request = CreateExecuteRequest(
                IpcExecuteCommandNames.Plan,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = k_RequestId,
                    ops = new[]
                    {
                        new
                        {
                            id = "expectMaxNegative",
                            op = "ucli.resolve",
                            args = new { },
                            expect = new
                            {
                                max = -1
                            }
                        }
                    }
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            AssertInvalidArgument(result, "expectMaxNegative");
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenExpectContainsNegativeConstraint_ReturnsInvalidArgumentError ()
        {
            var request = CreateExecuteRequest(
                IpcExecuteCommandNames.Plan,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = k_RequestId,
                    ops = new[]
                    {
                        new
                        {
                            id = "expectNegative",
                            op = "ucli.resolve",
                            args = new { },
                            expect = new
                            {
                                min = -1
                            }
                        }
                    }
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            AssertInvalidArgument(result, "expectNegative");
        }

        private static void AssertInvalidArgument (
            ExecuteRequestNormalizationResult result,
            string expectedOpId = null)
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Request, Is.Null);
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo(IpcErrorCodes.InvalidArgument));
            Assert.That(result.Error.OpId, Is.EqualTo(expectedOpId));
        }

        private static IpcExecuteRequest CreateExecuteRequest (string command, object arguments)
        {
            var argumentsJson = JsonSerializer.Serialize(arguments);

            return CreateExecuteRequestFromJson(command, argumentsJson);
        }

        private static IpcExecuteRequest CreateExecuteRequestFromJson (string command, string argumentsJson)
        {
            using var document = JsonDocument.Parse(argumentsJson);
            return new IpcExecuteRequest(
                Command: command,
                Arguments: document.RootElement.Clone());
        }
    }
}
