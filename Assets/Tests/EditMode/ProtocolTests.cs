using NUnit.Framework;

namespace TankDemo.Networking.Tests
{
    public class ProtocolTests
    {
        [Test]
        public void PayloadRoundTrips()
        {
            byte[] payload = Protocol.BuildPayload();

            Assert.IsTrue(Protocol.TryParsePayload(payload, out ushort version));
            Assert.AreEqual(Protocol.Version, version);
        }

        [Test]
        public void RejectsMalformedPayloads()
        {
            Assert.IsFalse(Protocol.TryParsePayload(null, out _));
            Assert.IsFalse(Protocol.TryParsePayload(new byte[1], out _));
        }

        [Test]
        public void RejectReasonsAreIdentifiable()
        {
            Assert.IsTrue(Protocol.Reject.Is(Protocol.Reject.Full, Protocol.Reject.Full));
            Assert.IsTrue(Protocol.Reject.Is(Protocol.Reject.Version, Protocol.Reject.Version));
            Assert.IsFalse(Protocol.Reject.Is(null, Protocol.Reject.Full));
            Assert.IsFalse(Protocol.Reject.Is(Protocol.Reject.Full, Protocol.Reject.Version));
        }
    }
}
