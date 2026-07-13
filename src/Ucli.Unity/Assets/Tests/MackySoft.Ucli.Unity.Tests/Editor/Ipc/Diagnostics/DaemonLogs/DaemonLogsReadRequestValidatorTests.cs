using System;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class DaemonLogsReadRequestValidatorTests
    {
        [Test]
        [Category("Size.Small")]
        public void TryValidate_WhenCurrentStreamIdIsEmpty_ThrowsArgumentException ()
        {
            var request = new IpcDaemonLogsReadRequest(
                Tail: null,
                After: null,
                Since: null,
                Until: null,
                Level: null,
                Query: null,
                QueryTarget: null,
                Category: null);
            var validator = new DaemonLogsReadRequestValidator();

            var exception = Assert.Throws<ArgumentException>(() => validator.TryValidate(
                request,
                Guid.Empty,
                out _,
                out _));

            Assert.That(exception.ParamName, Is.EqualTo("currentStreamId"));
        }

        [Test]
        [Category("Size.Small")]
        public void TryValidate_WhenAfterCursorBelongsToAnotherStream_ReturnsFalse ()
        {
            var currentStreamId = Guid.Parse("376b85d9-5edf-48a0-a9e8-ce698f494a0f");
            var anotherStreamId = Guid.Parse("a46efc96-ea70-45db-bc3b-47a6f61174b2");
            var request = new IpcDaemonLogsReadRequest(
                Tail: null,
                After: IpcLogCursorCodec.Encode(anotherStreamId, 1),
                Since: null,
                Until: null,
                Level: null,
                Query: null,
                QueryTarget: null,
                Category: null);
            var validator = new DaemonLogsReadRequestValidator();

            var result = validator.TryValidate(request, currentStreamId, out var filter, out var errorMessage);

            Assert.That(result, Is.False);
            Assert.That(filter, Is.Null);
            Assert.That(errorMessage, Does.Contain("does not match current daemon stream"));
        }
    }
}
