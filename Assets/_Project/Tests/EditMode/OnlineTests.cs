using System.Collections.Generic;
using Moa.Showdown.Core.Online;
using NUnit.Framework;

namespace Moa.Showdown.Tests
{
    /// <summary>O-1 온라인: 메시지 JSON(온라인 계약) · 방 규칙(코드 · 이름) · 메시지 규칙(시험 신호). 네트워크 없이 Core만.</summary>
    public class OnlineTests
    {
        // ───────── 메시지 JSON ─────────

        /// <summary>형식 버전 1 고정 JSON — 바꾸면 다른 버전 클라이언트와 못 주고받는다. 지우지 않는다.</summary>
        private const string FixedV1 = "{\"v\":1,\"type\":\"ping\",\"from\":\"player_a\",\"body\":\"hello\"}";

        [Test]
        public void Json_FixedV1_ReadsAndWritesSame()
        {
            NetMessage m = NetMessageJson.Deserialize(FixedV1);
            Assert.That((m.FormatVersion, m.Type, m.From, m.Body), Is.EqualTo((1, "ping", "player_a", "hello")));
            Assert.That(NetMessageJson.Serialize(m), Is.EqualTo(FixedV1));
        }

        [Test]
        public void Json_RoundTrip_NullBodyOmitted()
        {
            var m = new NetMessage { Type = NetMessageTypes.Pong, From = "host" };
            string json = NetMessageJson.Serialize(m);
            Assert.That(json, Is.EqualTo("{\"v\":1,\"type\":\"pong\",\"from\":\"host\"}"));
            NetMessage back = NetMessageJson.Deserialize(json);
            Assert.That((back.Type, back.From, back.Body), Is.EqualTo(("pong", "host", (string)null)));
        }

        [Test]
        public void Json_UnknownFieldsIgnored()
        {
            NetMessage m = NetMessageJson.Deserialize("{\"v\":1,\"type\":\"ping\",\"from\":\"a\",\"extra\":42}");
            Assert.That(m.Type, Is.EqualTo("ping"));
        }

        [TestCase("")]
        [TestCase("not json")]
        [TestCase("null")]
        [TestCase("{\"v\":2,\"type\":\"ping\",\"from\":\"a\"}")]
        [TestCase("{\"v\":0,\"type\":\"ping\",\"from\":\"a\"}")]
        [TestCase("{\"v\":1,\"from\":\"a\"}")]
        [TestCase("{\"v\":1,\"type\":\"ping\"}")]
        [TestCase("{\"v\":1,\"type\":\"\",\"from\":\"a\"}")]
        public void Json_Bad_Rejected(string json)
        {
            Assert.Throws<NetMessageFormatException>(() => NetMessageJson.Deserialize(json));
            Assert.That(NetMessageJson.TryDeserialize(json, out NetMessage m), Is.False);
            Assert.That(m, Is.Null);
        }

        [Test]
        public void Json_TooLong_Rejected()
        {
            var big = new NetMessage { Type = "ping", From = "a", Body = new string('x', NetMessageJson.MaxLength) };
            Assert.Throws<NetMessageFormatException>(() => NetMessageJson.Serialize(big));
        }

        [Test]
        public void Json_SerializeInvalid_Throws()
        {
            Assert.Throws<NetMessageFormatException>(() => NetMessageJson.Serialize(new NetMessage { Type = "ping" }), "보낸 사람 없음");
            Assert.Throws<NetMessageFormatException>(() => NetMessageJson.Serialize(null));
        }

        // ───────── 방 규칙 ─────────

        [TestCase(" ab-cd 12 ", "ABCD12")]
        [TestCase("xyz", "XYZ")]
        [TestCase("   ", null)]
        [TestCase(null, null)]
        public void NormalizeCode(string input, string expected)
        {
            Assert.That(RoomRules.NormalizeCode(input), Is.EqualTo(expected));
        }

        [TestCase("  모아  ", "모아")]
        [TestCase("", "기본")]
        [TestCase(null, "기본")]
        [TestCase("\t\n", "기본")]
        [TestCase("a\u0001b", "ab")]
        [TestCase("열두글자를넘는아주긴이름입니다", "열두글자를넘는아주긴이름")] // 12자까지
        public void SanitizeName(string input, string expected)
        {
            Assert.That(RoomRules.SanitizeName(input, "기본"), Is.EqualTo(expected));
        }

        // ───────── 메시지 규칙 (시험 신호) ─────────

        private static RoomSnapshot Room(string local) => new RoomSnapshot
        {
            Code = "ABC123",
            MaxPlayers = 8,
            LocalPlayerId = local,
            Members = new List<RoomMember>
            {
                new RoomMember { PlayerId = "host", Name = "방장", IsHost = true },
                new RoomMember { PlayerId = "guest", Name = "손님" },
            },
        };

        [Test]
        public void Host_ReceivesPing_RepliesPongToAll()
        {
            NetMessage reply = RoomProtocol.Handle(RoomProtocol.Ping("guest"), Room("host"));
            Assert.That(reply, Is.Not.Null);
            Assert.That((reply.Type, reply.From, reply.Body), Is.EqualTo((NetMessageTypes.Pong, "host", "guest")));
        }

        [Test]
        public void Guest_ReceivesPing_NoReply()
        {
            Assert.That(RoomProtocol.Handle(RoomProtocol.Ping("host"), Room("guest")), Is.Null, "방장만 답한다");
        }

        [Test]
        public void Ping_FromStranger_Ignored()
        {
            Assert.That(RoomProtocol.Handle(RoomProtocol.Ping("stranger"), Room("host")), Is.Null, "방에 없는 사람");
        }

        [Test]
        public void Host_PingsSelf_RepliesToo()
        {
            // 방장이 직접 신호를 보내면 SendToHost가 자기에게 바로 전달 → 같은 규칙으로 Pong
            NetMessage reply = RoomProtocol.Handle(RoomProtocol.Ping("host"), Room("host"));
            Assert.That(reply?.Body, Is.EqualTo("host"));
        }

        [Test]
        public void PongTarget_OnlyFromHost()
        {
            RoomSnapshot r = Room("guest");
            Assert.That(RoomProtocol.PongTarget(new NetMessage { Type = NetMessageTypes.Pong, From = "host", Body = "guest" }, r), Is.EqualTo("guest"));
            Assert.That(RoomProtocol.PongTarget(new NetMessage { Type = NetMessageTypes.Pong, From = "guest", Body = "guest" }, r), Is.Null, "방장이 아닌 사람이 보낸 Pong");
            Assert.That(RoomProtocol.PongTarget(RoomProtocol.Ping("host"), r), Is.Null, "Pong이 아님");
        }

        [Test]
        public void IsLocalHost_FollowsMembers()
        {
            Assert.That(Room("host").IsLocalHost, Is.True);
            Assert.That(Room("guest").IsLocalHost, Is.False);
            Assert.That(Room("nobody").IsLocalHost, Is.False);
        }
    }
}
