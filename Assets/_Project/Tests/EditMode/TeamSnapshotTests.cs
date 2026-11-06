using Moa.Showdown.Core.Snapshot;
using NUnit.Framework;

namespace Moa.Showdown.Tests
{
    public class TeamSnapshotTests
    {
        /// <summary>
        /// formatVersion 1의 고정 JSON. 이 문자열이 안 읽히거나 Serialize 결과가 달라지면 온라인 계약이 깨진 것 —
        /// 의도한 변경이면 버전을 올리고, 이 v1 문자열은 지우지 말고 "옛 버전 변환" 테스트로 남긴다.
        /// </summary>
        private const string GoldenV1 =
            "{\"formatVersion\":1,\"rulesVersion\":7,\"runId\":\"run_test\",\"round\":3,\"wins\":2,\"livesLost\":1," +
            "\"trainerId\":\"trainer_a\",\"trinketIds\":[\"trinket_x\",\"trinket_y\"]," +
            "\"board\":[{\"speciesId\":\"mon_a\",\"level\":2,\"buffs\":[{\"stat\":\"attack\",\"value\":5},{\"stat\":\"interval\",\"value\":-1}]}," +
            "null,null,{\"speciesId\":\"mon_b\",\"level\":1,\"buffs\":[]},null,null]}";

        private static TeamSnapshot Sample()
        {
            var s = new TeamSnapshot
            {
                RulesVersion = 7,
                RunId = "run_test",
                Round = 3,
                Wins = 2,
                LivesLost = 1,
                TrainerId = "trainer_a",
            };
            s.TrinketIds.Add("trinket_x");
            s.TrinketIds.Add("trinket_y");
            var a = new MonsterSnapshot { SpeciesId = "mon_a", Level = 2 };
            a.Buffs.Add(new StatBuff("attack", 5));
            a.Buffs.Add(new StatBuff("interval", -1));
            s.Board[0] = a;
            s.Board[3] = new MonsterSnapshot { SpeciesId = "mon_b", Level = 1 };
            return s;
        }

        [Test]
        public void Serialize_MatchesGoldenV1()
        {
            Assert.That(TeamSnapshotJson.Serialize(Sample()), Is.EqualTo(GoldenV1));
        }

        [Test]
        public void GoldenV1_Deserializes_AllFields()
        {
            var s = TeamSnapshotJson.Deserialize(GoldenV1);
            Assert.That(s.FormatVersion, Is.EqualTo(1));
            Assert.That(s.RulesVersion, Is.EqualTo(7));
            Assert.That(s.RunId, Is.EqualTo("run_test"));
            Assert.That(s.Round, Is.EqualTo(3));
            Assert.That(s.Wins, Is.EqualTo(2));
            Assert.That(s.LivesLost, Is.EqualTo(1));
            Assert.That(s.TrainerId, Is.EqualTo("trainer_a"));
            CollectionAssert.AreEqual(new[] { "trinket_x", "trinket_y" }, s.TrinketIds);
            Assert.That(s.Board.Length, Is.EqualTo(6));
            Assert.That(s.Board[0].SpeciesId, Is.EqualTo("mon_a"));
            Assert.That(s.Board[0].Level, Is.EqualTo(2));
            Assert.That(s.Board[0].Buffs.Count, Is.EqualTo(2));
            Assert.That(s.Board[0].Buffs[1].Stat, Is.EqualTo("interval"));
            Assert.That(s.Board[0].Buffs[1].Value, Is.EqualTo(-1));
            Assert.That(s.Board[1], Is.Null);
            Assert.That(s.Board[3].SpeciesId, Is.EqualTo("mon_b"));
            Assert.That(s.Board[3].Buffs, Is.Empty);
        }

        [Test]
        public void RoundTrip_SameJson()
        {
            string once = TeamSnapshotJson.Serialize(Sample());
            string twice = TeamSnapshotJson.Serialize(TeamSnapshotJson.Deserialize(once));
            Assert.That(twice, Is.EqualTo(once));
        }

        [Test]
        public void UnknownField_IsIgnored()
        {
            string json = GoldenV1.Replace("\"round\":3,", "\"round\":3,\"futureField\":123,");
            Assert.That(TeamSnapshotJson.Deserialize(json).Round, Is.EqualTo(3));
        }

        [TestCase("not json")]
        [TestCase("{}")]
        [TestCase("{\"formatVersion\":\"1\",\"round\":1,\"board\":[null,null,null,null,null,null]}")]
        [TestCase("{\"formatVersion\":0,\"round\":1,\"board\":[null,null,null,null,null,null]}")]
        [TestCase("{\"formatVersion\":99,\"round\":1,\"board\":[null,null,null,null,null,null]}")]
        [TestCase("{\"formatVersion\":1,\"round\":1}")]
        [TestCase("{\"formatVersion\":1,\"round\":1,\"board\":[null,null,null,null,null]}")]
        [TestCase("{\"formatVersion\":1,\"round\":0,\"board\":[null,null,null,null,null,null]}")]
        [TestCase("{\"formatVersion\":1,\"round\":1,\"board\":[{\"speciesId\":\"\",\"level\":1,\"buffs\":[]},null,null,null,null,null]}")]
        [TestCase("{\"formatVersion\":1,\"round\":1,\"board\":[{\"speciesId\":\"m\",\"level\":0,\"buffs\":[]},null,null,null,null,null]}")]
        [TestCase("{\"formatVersion\":1,\"round\":1,\"board\":[{\"speciesId\":\"m\",\"level\":1,\"buffs\":[{\"value\":3}]},null,null,null,null,null]}")]
        [TestCase("{\"formatVersion\":1,\"round\":1,\"wins\":-1,\"board\":[null,null,null,null,null,null]}")]
        public void BadJson_IsRejected(string json)
        {
            Assert.Throws<SnapshotFormatException>(() => TeamSnapshotJson.Deserialize(json));
        }

        [Test]
        public void MinimalValid_EmptyBoard()
        {
            var s = TeamSnapshotJson.Deserialize("{\"formatVersion\":1,\"round\":1,\"board\":[null,null,null,null,null,null]}");
            Assert.That(s.Board, Has.All.Null);
            Assert.That(s.TrinketIds, Is.Empty);
        }

        [Test]
        public void Serialize_RejectsInvalid()
        {
            var s = Sample();
            s.Board = new MonsterSnapshot[5];
            Assert.Throws<SnapshotFormatException>(() => TeamSnapshotJson.Serialize(s));
        }
    }
}
