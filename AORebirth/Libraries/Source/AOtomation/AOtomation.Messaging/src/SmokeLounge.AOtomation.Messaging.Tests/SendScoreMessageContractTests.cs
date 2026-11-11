namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using SmokeLounge.AOtomation.Messaging.Serialization;

    using StreamReader = SmokeLounge.AOtomation.Messaging.Serialization.StreamReader;
    using StreamWriter = SmokeLounge.AOtomation.Messaging.Serialization.StreamWriter;

    /// <summary>
    /// Live BS 31-60 (PF 4384) SendScore 0x44483B3A body from bs_score_packets_20261111_Killzaw.csv.
    /// </summary>
    [TestClass]
    public class SendScoreMessageContractTests
    {
        [TestMethod]
        public void NeutralBoardZeroScores_MatchesCaptureStart()
        {
            // After N3 type: Identity(0,0) Unknown=1 Viewer=5045 Owners=2,2,2,2 Secondary=3027 Red=0 Blue=0
            byte[] wire = HexToBytes(
                "44483B3A000000000000000001000013B50000000200000002000000020000000200000BD30000000000000000");

            var message = (SendScoreMessage)Deserialize(wire);
            Assert.AreEqual(N3MessageType.SendScore, message.N3MessageType);
            Assert.AreEqual(IdentityType.None, message.Identity.Type);
            Assert.AreEqual(0, message.Identity.Instance);
            Assert.AreEqual(1, message.Unknown);
            Assert.AreEqual(5045, message.ViewerInstance);
            Assert.AreEqual(2, message.Owner0);
            Assert.AreEqual(2, message.Owner1);
            Assert.AreEqual(2, message.Owner2);
            Assert.AreEqual(2, message.Owner3);
            Assert.AreEqual(3027, message.SecondaryInstance);
            Assert.AreEqual(0, message.RedScore);
            Assert.AreEqual(0, message.BlueScore);

            CollectionAssert.AreEqual(wire, Serialize(message));
        }

        [TestMethod]
        public void FinalScores_MatchLiveRed91Blue502()
        {
            // Final tick: Core Red, arms Blue; Red 91 / Blue 502 (match ends at 500).
            byte[] wire = HexToBytes(
                "44483B3A000000000000000001000013B50000000000000001000000010000000100000BD30000005B000001F6");

            var message = (SendScoreMessage)Deserialize(wire);
            Assert.AreEqual(0, message.Owner0);
            Assert.AreEqual(1, message.Owner1);
            Assert.AreEqual(1, message.Owner2);
            Assert.AreEqual(1, message.Owner3);
            Assert.AreEqual(91, message.RedScore);
            Assert.AreEqual(502, message.BlueScore);

            CollectionAssert.AreEqual(wire, Serialize(message));
        }

        static byte[] Serialize(MessageBody body)
        {
            var resolver = new SerializerResolverBuilder<MessageBody>().Build();
            var serializer = resolver.GetSerializer(body.GetType());
            using (var memoryStream = new MemoryStream())
            {
                using (var writer = new StreamWriter(memoryStream))
                {
                    serializer.Serialize(writer, new SerializationContext(resolver), body);
                    return memoryStream.ToArray();
                }
            }
        }

        static MessageBody Deserialize(byte[] bytes)
        {
            var resolver = new SerializerResolverBuilder<MessageBody>().Build();
            var serializer = resolver.GetSerializer(typeof(SendScoreMessage));
            using (var memoryStream = new MemoryStream(bytes))
            {
                using (var reader = new StreamReader(memoryStream))
                {
                    return (MessageBody)serializer.Deserialize(reader, new SerializationContext(resolver));
                }
            }
        }

        static byte[] HexToBytes(string hex)
        {
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }
    }
}
