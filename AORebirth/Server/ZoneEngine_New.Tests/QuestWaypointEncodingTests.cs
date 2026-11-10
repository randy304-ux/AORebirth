namespace ZoneEngine_New.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using AoWriter = SmokeLounge.AOtomation.Messaging.Serialization.StreamWriter;
    using ZoneEngine_New.Core.Quests;

    [TestClass]
    public sealed class QuestWaypointEncodingTests
    {
        [TestMethod]
        public void Playfield2Is40016()
        {
            Assert.AreEqual(40016, (int)IdentityType.Playfield2);
            Assert.AreEqual(0x00009C50, (int)IdentityType.Playfield2);
        }

        [TestMethod]
        public void OmittedWaypointKeepsEmptyIdentityAndNullPosition()
        {
            var template = new QuestTemplate();
            var id = QuestWaypointEncoding.PlayfieldId(template);
            Assert.AreEqual((IdentityType)0, id.Type);
            Assert.AreEqual(0, id.Instance);
            Assert.IsNull(QuestWaypointEncoding.Position(template));
        }

        [TestMethod]
        public void PlayfieldZeroIsTreatedAsOmitted()
        {
            var template = new QuestTemplate { Waypoint = new QuestWaypoint { Playfield = 0, X = 1, Y = 2, Z = 3 } };
            var id = QuestWaypointEncoding.PlayfieldId(template);
            Assert.AreEqual((IdentityType)0, id.Type);
            Assert.AreEqual(0, id.Instance);
            Assert.IsNull(QuestWaypointEncoding.Position(template));
        }

        [TestMethod]
        public void SetWaypointMatchesGeneratedMissionFallbackBytes()
        {
            var template = new QuestTemplate
            {
                Waypoint = new QuestWaypoint { Playfield = 4321, X = 1854.5f, Y = 92.25f, Z = 1010.125f }
            };
            var id = QuestWaypointEncoding.PlayfieldId(template);
            var pos = QuestWaypointEncoding.Position(template);
            Assert.AreEqual(IdentityType.Playfield2, id.Type);
            Assert.AreEqual(4321, id.Instance);
            Assert.IsNotNull(pos);
            Assert.AreEqual(1854.5f, pos!.X);
            Assert.AreEqual(92.25f, pos.Y);
            Assert.AreEqual(1010.125f, pos.Z);

            var generated = new Identity { Type = (IdentityType)40016, Instance = 4321 };
            var generatedPos = new Vector3(1854.5f, 92.25f, 1010.125f);
            CollectionAssert.AreEqual(MarkerBytes(generated, generatedPos), MarkerBytes(id, pos));
            CollectionAssert.AreEqual(ManualBigEndian(40016, 4321, 1854.5f, 92.25f, 1010.125f), MarkerBytes(id, pos));
        }

        [TestMethod]
        public void EmptyMarkerSerialisesTwentyZeroBytes()
        {
            var bytes = MarkerBytes(QuestWaypointEncoding.PlayfieldId(new QuestTemplate()), null);
            Assert.AreEqual(20, bytes.Length);
            Assert.IsTrue(bytes.All(b => b == 0));
        }

        private static byte[] MarkerBytes(Identity id, Vector3? position)
        {
            using var ms = new MemoryStream();
            using (var writer = new AoWriter(ms))
            {
                writer.WriteIdentity(id);
                var p = position ?? new Vector3();
                writer.WriteSingle(p.X);
                writer.WriteSingle(p.Y);
                writer.WriteSingle(p.Z);
            }
            return ms.ToArray();
        }

        private static byte[] ManualBigEndian(int type, int instance, float x, float y, float z)
        {
            static byte[] BeInt(int v) => BitConverter.GetBytes(IPAddress.HostToNetworkOrder(v));
            static byte[] BeFloat(float v)
            {
                var b = BitConverter.GetBytes(v);
                Array.Reverse(b);
                return b;
            }
            return BeInt(type).Concat(BeInt(instance)).Concat(BeFloat(x)).Concat(BeFloat(y)).Concat(BeFloat(z)).ToArray();
        }
    }
}
