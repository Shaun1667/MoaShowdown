using Moa.Showdown.Core.Battle;
using Moa.Showdown.Core.Content;
using Moa.Showdown.Core.Snapshot;
using Moa.Showdown.Game;
using Moa.Showdown.Game.Ui;
using NUnit.Framework;

namespace Moa.Showdown.Tests
{
    public class InfoTextTests
    {
        private static readonly SpeciesDef Gust = new SpeciesDef { Id = "gust", DisplayName = "바람개비", Attack = 7, AttackIntervalTicks = 6, Rarity = Rarity.Common };

        private static InfoText.MonsterInfo Info(int level, BattleUnit unit, int buff = 0) => new InfoText.MonsterInfo
        {
            Species = Gust,
            Level = level,
            Unit = unit,
            AttackBuff = buff,
            LevelMultiplier = 2,
            TicksPerSecond = 10,
            MergeCount = 3,
            MaxLevel = 4,
        };

        [Test]
        public void MonsterBody_ShowsActualStats()
        {
            var i = Info(2, new BattleUnit(14, 6));
            i.SellValue = 23;
            i.OwnedSameLevel = 1;
            string body = InfoText.MonsterBody(i);
            StringAssert.Contains("공격력: 14", body);
            StringAssert.Contains("기본 7 × 2^(Lv−1) + 버프 0", body);
            StringAssert.Contains("공격 간격: 0.6초", body);
            StringAssert.Contains("초당 피해: 23.3", body);
            StringAssert.Contains("판매가: 23골드", body);
            StringAssert.Contains("합체: 같은 Lv2 1/3마리 보유", body);
            StringAssert.Contains("등급: 일반", body);
        }

        [Test]
        public void MonsterTitle_MarksEnemy()
        {
            var i = Info(1, new BattleUnit(7, 6));
            Assert.That(InfoText.MonsterTitle(i), Is.EqualTo("바람개비  Lv1"));
            i.Enemy = true;
            Assert.That(InfoText.MonsterTitle(i), Is.EqualTo("바람개비  Lv1  (상대)"));
        }

        [Test]
        public void ShopCard_ShowsPrice_NoSell()
        {
            var i = Info(1, new BattleUnit(7, 6));
            i.Price = 10;
            string body = InfoText.MonsterBody(i);
            StringAssert.Contains("가격: 10골드", body);
            StringAssert.DoesNotContain("판매가", body);
            StringAssert.DoesNotContain("합체", body);
        }

        [Test]
        public void MaxLevel_SaysMax()
        {
            var i = Info(4, new BattleUnit(56, 6));
            i.OwnedSameLevel = 1;
            StringAssert.Contains(HudText.InfoMaxLevel, InfoText.MonsterBody(i));
        }

        [Test]
        public void OpponentBody_BotAndPlayer()
        {
            var s = new TeamSnapshot { RunId = "sample_r03_0", Wins = 1, LivesLost = 2, TrainerId = "sample" };
            string bot = InfoText.OpponentBody(s, true, "bots.json", 3);
            StringAssert.Contains(HudText.InfoBot, bot);
            StringAssert.Contains("판 ID: sample_r03_0", bot);
            StringAssert.Contains("그때 승수 1 · 잃은 목숨 2", bot);
            StringAssert.Contains("몬스터 3마리", bot);
            StringAssert.Contains("출처: bots.json", bot);
            StringAssert.Contains(HudText.InfoPlayer, InfoText.OpponentBody(s, false, null, 3));
        }

        [Test]
        public void BuildUnit_MatchesTeamBuild()
        {
            var catalog = new SpeciesCatalog(new[] { Gust });
            var rules = new UnitStatRules { LevelAttackMultiplier = 2 };
            var m = new MonsterSnapshot { SpeciesId = "gust", Level = 3 };
            m.Buffs.Add(new StatBuff(StatIds.Attack, 5));
            var snap = new TeamSnapshot { Round = 1 };
            snap.Board[2] = m;
            BattleUnit fromTeam = TeamFactory.Build(snap, catalog, rules)[2];
            BattleUnit single = TeamFactory.BuildUnit(m, catalog, rules);
            Assert.That(single.Attack, Is.EqualTo(fromTeam.Attack));
            Assert.That(single.AttackIntervalTicks, Is.EqualTo(fromTeam.AttackIntervalTicks));
            Assert.That(single.Attack, Is.EqualTo(7 * 4 + 5));
        }
    }
}
