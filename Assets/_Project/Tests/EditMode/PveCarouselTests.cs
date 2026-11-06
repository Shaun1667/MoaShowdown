using System;
using System.Collections.Generic;
using System.Linq;
using Moa.Showdown.Core.Battle;
using Moa.Showdown.Core.Content;
using Moa.Showdown.Core.Lobby;
using Moa.Showdown.Core.Run;
using Moa.Showdown.Core.Shop;
using Moa.Showdown.Core.Snapshot;
using NUnit.Framework;

namespace Moa.Showdown.Tests
{
    /// <summary>L-4 PvE · 캐러셀: 시기 표 · PvE 보상과 목숨 1 · 캐러셀 순서 · 대기석 가득 · 자동 고르기 · 풀 보존 · 저장 왕복 · 옛 버전 변환.</summary>
    public class PveCarouselTests
    {
        private static SpeciesCatalog Catalog() => new SpeciesCatalog(new[]
        {
            new SpeciesDef { Id = "s", DisplayName = "S", Attack = 10, AttackIntervalTicks = 10 },
            new SpeciesDef { Id = "w", DisplayName = "W", Attack = 1, AttackIntervalTicks = 50 },
        });

        private static readonly UnitStatRules X2 = new UnitStatRules { LevelAttackMultiplier = 2 };

        private static PveTeamDef Wild(int win = 5, int lose = 2)
        {
            var t = new PveTeamDef { WinGold = win, LoseGold = lose };
            t.Board[0] = new MonsterSnapshot { SpeciesId = "s", Level = 1 }; // 초당 피해 10 — 빈 보드는 지고, s Lv3(40)은 이김
            return t;
        }

        private static LobbyRules Rules(bool pve, bool carousel) => new LobbyRules
        {
            PlayerCount = 8, RecentOpponentMemory = 3, PoolSizeByRarity = new[] { 29, 22, 18, 12, 10 }, PrepSeconds = 30,
            PveOpeningRounds = pve ? 2 : 0, PveRepeatStart = pve ? 8 : 0, PveRepeatEvery = pve ? 6 : 0, PveLifeLoss = 1,
            PveTeams = pve ? new List<PveTeamDef> { Wild(), Wild(), Wild(10, 5) } : new List<PveTeamDef>(),
            CarouselOpeningRounds = carousel ? 1 : 0, CarouselRepeatStart = carousel ? 5 : 0, CarouselRepeatEvery = carousel ? 6 : 0, CarouselPickSeconds = 10,
        };

        private static LobbyController Ctl(bool pve = false, bool carousel = false)
        {
            var catalog = Catalog();
            var shop = new ShopController(new ShopRules
            {
                StartGold = 30, FirstIncome = 35, IncomeStep = 5, ShopSlots = 5, RerollCost = 3,
                PriceByRarity = new[] { 10, 20, 30, 40, 50 }, SellPercent = 75, MergeCountByLevel = new[] { 3, 2 }, MaxLevel = 3,
            }, catalog, X2);
            return new LobbyController(
                Rules(pve, carousel),
                // 보통 패배는 목숨 3개 — PvE는 1개만 잃는지 보려고
                new RunRules { WinsToChampion = 10, StartLives = 10, LifeLossByRound = new[] { 3 } },
                new BattleRules
                {
                    TicksPerSecond = 10, TeamHpByRound = new[] { 100 }, SuddenDeathStartTick = 300, SuddenDeathIntervalTicks = 10,
                    SuddenDeathBaseDamage = 20, SuddenDeathDamageStep = 20, MaxTicks = 2000, BurnIntervalTicks = 10, PoisonIntervalTicks = 20,
                },
                X2, catalog, shop, 1);
        }

        private static LobbyState New(LobbyController c, ulong seed = 7) => c.NewLobby("L", seed, new[] { "나" }, n => "봇 " + n);

        /// <summary>풀 + 상점 카드 + 보유 몬스터 마리 수 + 캐러셀에 놓인(아직 안 가져간) 몬스터.</summary>
        private static int Units(LobbyController c, LobbyState l)
        {
            int n = new UnitPool(l.Pool).Total;
            foreach (LobbyPlayer p in l.Players)
            {
                n += p.Run.Shop.Count(id => id != null);
                foreach (MonsterSnapshot m in p.Run.Team.Concat(p.Run.Bench))
                    if (m != null) n += (int)c.Shop.Rules.CopiesAt(m.Level);
            }
            CarouselState cs = l.Carousel;
            if (cs != null && !cs.IsDone) n += cs.TakenBy.Count(t => t < 0);
            return n;
        }

        [Test]
        public void Schedule_PveAndCarouselRounds()
        {
            LobbyRules r = Rules(pve: true, carousel: true);
            int[] pve = Enumerable.Range(1, 26).Where(r.IsPveRound).ToArray();
            int[] carousel = Enumerable.Range(1, 26).Where(r.IsCarouselRound).ToArray();
            Assert.That(pve, Is.EqualTo(new[] { 1, 2, 8, 14, 20, 26 }), "1 · 2 + 8부터 6마다");
            Assert.That(carousel, Is.EqualTo(new[] { 1, 5, 11, 17, 23 }), "1 + 5부터 6마다");
            Assert.That(r.PveTeamFor(8).WinGold, Is.EqualTo(10), "R8 = 세 번째 PvE 팀");
            Assert.That(r.PveTeamFor(26), Is.SameAs(r.PveTeams[2]), "표보다 뒤는 마지막 팀");
            Assert.That(r.PveTeamFor(3), Is.Null);

            LobbyRules off = Rules(pve: false, carousel: false);
            Assert.That(Enumerable.Range(1, 30).Any(x => off.IsPveRound(x) || off.IsCarouselRound(x)), Is.False, "0 = 없음");
        }

        [Test]
        public void Validate_PveWithoutTeams_Throws()
        {
            LobbyRules r = Rules(pve: true, carousel: false);
            r.PveTeams.Clear();
            Assert.Throws<InvalidOperationException>(() => r.Validate());
        }

        [Test]
        public void PveRound_EveryoneFightsWild_RewardsAndFixedLifeLoss()
        {
            var c = Ctl(pve: true);
            LobbyState l = New(c);
            l.Players[0].Run.Team[0] = new MonsterSnapshot { SpeciesId = "s", Level = 3 }; // 나만 강함, 봇은 빈 보드
            LobbyRoundReport rep = c.ResolveRound(l);

            Assert.That(rep.Matches.Count, Is.EqualTo(8), "모두 PvE (짝짓기 · 유령 없음)");
            Assert.That(rep.Matches.All(m => m.IsPve && !m.IsGhost && m.PlayerB == -1), Is.True);
            RunState me = l.Players[0].Run;
            Assert.That(rep.MatchOf(0).Battle.Outcome, Is.EqualTo(BattleOutcome.AWins));
            Assert.That((me.Gold, me.Wins, me.Lives), Is.EqualTo((5, 1, 10)), "이기면 +5골드 · 승 +1 · 목숨 그대로");
            RunState bot = l.Players[1].Run;
            Assert.That(rep.MatchOf(1).Battle.Outcome, Is.EqualTo(BattleOutcome.BWins));
            Assert.That((bot.Gold, bot.Lives), Is.EqualTo((2, 9)), "지면 +2골드 · 목숨 1 (보통 패배 3이 아님)");
            Assert.That(l.Players.All(p => p.RecentOpponents.Count == 0), Is.True, "PvE는 최근 상대 기록 없음");
            Assert.That(l.Round, Is.EqualTo(2));
        }

        [Test]
        public void PveRound_SameSeed_SameResults()
        {
            string Run()
            {
                var c = Ctl(pve: true);
                LobbyState l = New(c, 11);
                c.BeginRound(l);
                LobbyRoundReport rep = c.ResolveRound(l);
                return string.Join(",", rep.Matches.Select(m => $"{m.PlayerA}:{m.Battle.Outcome}:{m.Battle.EndTick}:{m.Seed}"));
            }
            Assert.That(Run(), Is.EqualTo(Run()));
        }

        [Test]
        public void Carousel_OrderByLivesThenIndex_PicksAndReturnsLeftover()
        {
            var c = Ctl(carousel: true);
            LobbyState l = New(c);
            int total = Units(c, l);
            Assert.That(total, Is.EqualTo(58), "시험 2종 × 29");
            l.Players[3].Run.Lives = 4;
            l.Players[5].Run.Lives = 4;
            l.Players[0].Run.Lives = 6;

            CarouselState cs = c.StartCarousel(l);
            Assert.That(cs.Order, Is.EqualTo(new[] { 3, 5, 0, 1, 2, 4, 6, 7 }), "목숨 적은 순, 같으면 번호 순");
            Assert.That(cs.Units.Count, Is.EqualTo(9), "생존자 8 + 1");
            Assert.That(new UnitPool(l.Pool).Total, Is.EqualTo(58 - 9), "놓인 만큼 풀에서 빠짐");
            Assert.That(Units(c, l), Is.EqualTo(total));
            Assert.That(c.StartCarousel(l), Is.SameAs(cs), "같은 라운드에 다시 열어도 그대로");

            Assert.That(c.PickCarousel(l, 0, 0), Is.EqualTo(CarouselPickResult.NotYourTurn), "아직 봇 3 차례");
            c.AdvanceCarouselBots(l);
            Assert.That(cs.CurrentPicker, Is.EqualTo(0), "봇 3 · 5가 고르고 내 차례에서 멈춤");
            int takenByBot = cs.TakenBy.IndexOf(3);
            Assert.That(c.PickCarousel(l, 0, takenByBot), Is.EqualTo(CarouselPickResult.Taken));
            Assert.That(c.PickCarousel(l, 0, 99), Is.EqualTo(CarouselPickResult.Invalid));

            int free = cs.TakenBy.IndexOf(-1);
            Assert.That(c.PickCarousel(l, 0, free), Is.EqualTo(CarouselPickResult.Picked));
            Assert.That(l.Players[0].Run.Bench[0].SpeciesId, Is.EqualTo(cs.Units[free]), "대기석으로");
            Assert.That(Units(c, l), Is.EqualTo(total));

            c.AdvanceCarouselBots(l);
            Assert.That(cs.IsDone, Is.True);
            Assert.That(cs.TakenBy.Count(t => t >= 0), Is.EqualTo(8));
            Assert.That(new UnitPool(l.Pool).Total, Is.EqualTo(58 - 8), "남은 1마리는 풀로");
            Assert.That(Units(c, l), Is.EqualTo(total));
            Assert.That(c.PickCarousel(l, 1, 0), Is.EqualTo(CarouselPickResult.NoCarousel), "끝난 뒤에는 못 고름");
        }

        [Test]
        public void Carousel_BenchFullNoMerge_GivesGoldAndReturnsUnit()
        {
            var c = Ctl(carousel: true);
            LobbyState l = New(c);
            RunState me = l.Players[0].Run;
            me.Lives = 1; // 첫 차례
            for (int i = 0; i < me.Bench.Length; i++) me.Bench[i] = new MonsterSnapshot { SpeciesId = i % 2 == 0 ? "s" : "w", Level = 2 }; // Lv1이 없어 합체 불가
            int total = Units(c, l);

            CarouselState cs = c.StartCarousel(l);
            string id = cs.Units[0];
            int poolBefore = new UnitPool(l.Pool).CountOf(id);
            Assert.That(c.PickCarousel(l, 0, 0), Is.EqualTo(CarouselPickResult.PickedAsGold));
            Assert.That(me.Gold, Is.EqualTo(10), "가격(일반 10)만큼 골드");
            Assert.That(new UnitPool(l.Pool).CountOf(id), Is.EqualTo(poolBefore + 1), "몬스터는 풀로");
            Assert.That(Units(c, l), Is.EqualTo(total));
        }

        [Test]
        public void Carousel_BenchFullButMerges_Accepted()
        {
            var c = Ctl(carousel: true);
            LobbyState l = New(c);
            RunState me = l.Players[0].Run;
            me.Lives = 1;
            CarouselState cs = c.StartCarousel(l);
            string id = cs.Units[0];
            string other = id == "s" ? "w" : "s";
            me.Bench[0] = new MonsterSnapshot { SpeciesId = id, Level = 1 };
            me.Bench[1] = new MonsterSnapshot { SpeciesId = id, Level = 1 };
            me.Bench[2] = new MonsterSnapshot { SpeciesId = other, Level = 2 };
            me.Bench[3] = new MonsterSnapshot { SpeciesId = other, Level = 3 };
            Assert.That(c.PickCarousel(l, 0, 0), Is.EqualTo(CarouselPickResult.Picked));
            Assert.That(me.Bench[0].Level, Is.EqualTo(2), "Lv1 2마리 + 받은 1마리 = Lv2");
            Assert.That(me.Gold, Is.EqualTo(0));
        }

        [Test]
        public void BeginRound_FinishesPendingCarouselBeforeShop_Idempotent()
        {
            var c = Ctl(carousel: true);
            LobbyState l = New(c);
            int total = Units(c, l);
            c.BeginRound(l);
            Assert.That(l.Carousel.Round == 1 && l.Carousel.IsDone, Is.True, "사람도 자동으로 고름");
            RunState me = l.Players[0].Run;
            Assert.That(me.Bench.Count(m => m != null) + me.Team.Count(m => m != null), Is.EqualTo(1), "사람은 상점을 안 건드렸으니 캐러셀 1마리만");
            Assert.That(me.Shop.Count, Is.EqualTo(5), "그다음 상점");
            Assert.That(Units(c, l), Is.EqualTo(total));

            List<int> takenBefore = l.Carousel.TakenBy.ToList();
            c.BeginRound(l);
            Assert.That(l.Carousel.TakenBy, Is.EqualTo(takenBefore), "다시 불러도 그대로");
            Assert.That(Units(c, l), Is.EqualTo(total));
        }

        [Test]
        public void Carousel_SameSeed_SameUnitsAndPicks()
        {
            string Run()
            {
                var c = Ctl(carousel: true);
                LobbyState l = New(c, 5);
                c.BeginRound(l);
                return string.Join(",", l.Carousel.Units) + "|" + string.Join(",", l.Carousel.TakenBy);
            }
            Assert.That(Run(), Is.EqualTo(Run()));
        }

        [Test]
        public void LobbyJson_MidCarousel_RoundTrips_AndContinues()
        {
            var c = Ctl(carousel: true);
            LobbyState l = New(c);
            l.Players[4].IsBot = false; // 사람 둘: 0 · 4
            c.StartCarousel(l);
            c.PickCarousel(l, 0, 0);
            c.AdvanceCarouselBots(l); // 1 · 2 · 3 고르고 4(사람)에서 멈춤
            Assert.That(l.Carousel.CurrentPicker, Is.EqualTo(4));

            LobbyState back = LobbyStateJson.Deserialize(LobbyStateJson.Serialize(l));
            Assert.That(back.Carousel.Units, Is.EqualTo(l.Carousel.Units));
            Assert.That(back.Carousel.TakenBy, Is.EqualTo(l.Carousel.TakenBy));
            Assert.That(back.Carousel.Order, Is.EqualTo(l.Carousel.Order));
            Assert.That(back.Carousel.Next, Is.EqualTo(l.Carousel.Next));
            Assert.That(LobbyStateJson.Serialize(back), Is.EqualTo(LobbyStateJson.Serialize(l)), "왕복 후 같은 JSON");

            int free = back.Carousel.TakenBy.IndexOf(-1);
            Assert.That(c.PickCarousel(back, 4, free), Is.EqualTo(CarouselPickResult.Picked), "불러온 상태에서 이어서 고름");
        }

        [Test]
        public void LobbyJson_Version2_LoadsWithoutCarousel()
        {
            // 버전 2 JSON = 지금 JSON에서 carousel 칸을 빼고 버전만 2 (캐러셀 말고는 2와 같음)
            string now = LobbyStateJson.Serialize(New(Ctl()));
            Assert.That(now, Does.Contain(",\"carousel\":null"));
            string v2 = now.Replace(",\"carousel\":null", "").Replace($"\"formatVersion\":{LobbyState.CurrentFormatVersion}", "\"formatVersion\":2");
            Assert.That(v2, Does.Not.Contain("carousel"));
            LobbyState l = LobbyStateJson.Deserialize(v2);
            Assert.That(l.FormatVersion, Is.EqualTo(LobbyState.CurrentFormatVersion));
            Assert.That(l.Carousel, Is.Null);
            Assert.That(l.Players.Count, Is.EqualTo(8));
        }
    }
}
