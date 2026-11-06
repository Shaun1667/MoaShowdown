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
    /// <summary>L-1 로비: 인원 · 봇 · 대진(최근 상대 피함 · 홀수 유령) · 목숨 · 탈락 순위 · 결정론 · 저장 왕복.</summary>
    public class LobbyTests
    {
        private static SpeciesCatalog Catalog() => new SpeciesCatalog(new[]
        {
            new SpeciesDef { Id = "s", DisplayName = "S", Attack = 10, AttackIntervalTicks = 10 },
            new SpeciesDef { Id = "w", DisplayName = "W", Attack = 1, AttackIntervalTicks = 50 },
        });

        private static readonly UnitStatRules X2 = new UnitStatRules { LevelAttackMultiplier = 2 };

        private static LobbyController Ctl(int players = 8)
        {
            var catalog = Catalog();
            var shop = new ShopController(new ShopRules
            {
                StartGold = 30, FirstIncome = 35, IncomeStep = 5, ShopSlots = 5, RerollCost = 3,
                PriceByRarity = new[] { 10, 20, 30, 40, 50 }, SellPercent = 75, MergeCountByLevel = new[] { 3, 2 }, MaxLevel = 3,
            }, catalog, X2);
            return new LobbyController(
                new LobbyRules { PlayerCount = players, RecentOpponentMemory = 3, PoolSizeByRarity = new[] { 29, 22, 18, 12, 10 }, PrepSeconds = 30 },
                new RunRules { WinsToChampion = 10, StartLives = 10, LifeLossByRound = new[] { 1, 1, 2, 3 } },
                new BattleRules
                {
                    TicksPerSecond = 10, TeamHpByRound = new[] { 100 }, SuddenDeathStartTick = 300, SuddenDeathIntervalTicks = 10,
                    SuddenDeathBaseDamage = 20, SuddenDeathDamageStep = 20, MaxTicks = 2000, BurnIntervalTicks = 10, PoisonIntervalTicks = 20,
                },
                X2, catalog, shop, 1);
        }

        private static LobbyState New(LobbyController c, ulong seed = 7) => c.NewLobby("L", seed, new[] { "나" }, n => "봇 " + n);

        private static MonsterSnapshot Strong() => new MonsterSnapshot { SpeciesId = "s", Level = 3 };

        [Test]
        public void NewLobby_OneHumanSevenBots_TenLives()
        {
            LobbyState l = New(Ctl());
            Assert.That(l.Players.Count, Is.EqualTo(8));
            Assert.That(l.Players[0].IsBot, Is.False);
            Assert.That(l.Players.Skip(1).All(p => p.IsBot), Is.True);
            Assert.That(l.Players[3].Name, Is.EqualTo("봇 3"));
            Assert.That(l.Players.All(p => p.Run.Lives == 10 && p.IsAlive), Is.True);
            Assert.That(l.Players.Select(p => p.Run.Seed).Distinct().Count(), Is.EqualTo(8), "플레이어마다 상점 시드가 다름");
        }

        [Test]
        public void BeginRound_BotsBuyAndPlace_HumanUntouched()
        {
            var c = Ctl();
            LobbyState l = New(c);
            c.BeginRound(l);
            Assert.That(l.Players[0].Run.Gold, Is.EqualTo(30));
            Assert.That(l.Players[0].Run.Team.All(m => m == null), Is.True);
            foreach (LobbyPlayer bot in l.Players.Skip(1))
            {
                Assert.That(bot.Run.Gold, Is.LessThan(10), bot.Name + ": 살 수 있는 만큼 산다 (가격 10)");
                // 30골드 = 3마리 → 모두 보드에. 같은 몬스터 3장이면 합체해 Lv2 1마리가 되므로 "들어간 마리 수"(Lv1 = 1, Lv2 = 3)로 센다
                int copies = bot.Run.Team.Where(m => m != null).Sum(m => m.Level == 1 ? 1 : 3);
                Assert.That(copies, Is.EqualTo(3), bot.Name + ": 30골드 = 3마리 → 보드에 (합체 포함)");
                Assert.That(bot.Run.Bench.All(m => m == null), Is.True, bot.Name + ": 대기석은 비어 있음");
            }
            int goldBefore = l.Players[1].Run.Gold;
            c.BeginRound(l);
            Assert.That(l.Players[1].Run.Gold, Is.EqualTo(goldBefore), "같은 라운드 두 번째는 아무 일 없음");
        }

        [Test]
        public void EightAlive_FourMatches_EveryoneOnce()
        {
            var c = Ctl();
            LobbyState l = New(c);
            c.BeginRound(l);
            LobbyRoundReport r = c.ResolveRound(l);
            Assert.That(r.Matches.Count, Is.EqualTo(4));
            Assert.That(r.Matches.Any(m => m.IsGhost), Is.False);
            var seen = r.Matches.SelectMany(m => new[] { m.PlayerA, m.PlayerB }).OrderBy(x => x).ToList();
            Assert.That(seen, Is.EqualTo(Enumerable.Range(0, 8).ToList()));
            Assert.That(l.Round, Is.EqualTo(2));
        }

        [Test]
        public void OddAlive_OneGhostMatch_OwnerUnaffectedByGhost()
        {
            var c = Ctl();
            LobbyState l = New(c);
            l.Players[7].Run.Lives = 0;
            l.Players[7].Placement = 8;
            c.BeginRound(l);
            LobbyRoundReport r = c.ResolveRound(l);
            Assert.That(r.Matches.Count, Is.EqualTo(4));
            LobbyMatch ghost = r.Matches.Single(m => m.IsGhost);
            Assert.That(ghost.GhostOf, Is.Not.EqualTo(ghost.PlayerA));
            Assert.That(ghost.GhostOf, Is.Not.EqualTo(7), "탈락자는 유령이 되지 않음");
            Assert.That(ghost.LivesLostB, Is.EqualTo(0));
            Assert.That(r.Matches.Any(m => m.Involves(7)), Is.False);
        }

        [Test]
        public void RecentOpponents_AreAvoided_NextRound()
        {
            var c = Ctl();
            LobbyState l = New(c, 11);
            var met = new Dictionary<int, int>();
            for (int round = 0; round < 3; round++)
            {
                c.BeginRound(l);
                LobbyRoundReport r = c.ResolveRound(l);
                foreach (LobbyMatch m in r.Matches)
                {
                    if (met.TryGetValue(m.PlayerA, out int prev))
                        Assert.That(m.PlayerB, Is.Not.EqualTo(prev), $"라운드 {r.Round}: {m.PlayerA}가 직전 상대와 또 만남");
                }
                met = r.Matches.Where(m => !m.IsGhost).SelectMany(m => new[] { (m.PlayerA, m.PlayerB), (m.PlayerB, m.PlayerA) }).ToDictionary(x => x.Item1, x => x.Item2);
            }
        }

        [Test]
        public void TwoPlayers_LoserOut_WinnerFirst_LobbyFinished()
        {
            var c = Ctl(players: 2);
            LobbyState l = New(c);
            l.Players[0].Run.Team[0] = Strong();
            l.Players[1].IsBot = false; // 봇이 사지 않게 (빈 팀)
            l.Players[1].Run.Lives = 1;
            c.BeginRound(l);
            LobbyRoundReport r = c.ResolveRound(l);
            Assert.That(r.Eliminated, Is.EqualTo(new[] { 1 }));
            Assert.That(l.Players[1].Placement, Is.EqualTo(2));
            Assert.That(l.Players[0].Placement, Is.EqualTo(1));
            Assert.That(r.FinishedAfter, Is.True);
            Assert.That(l.IsFinished, Is.True);
            Assert.That(l.Round, Is.EqualTo(1), "끝난 라운드에서 멈춤");
            Assert.Throws<System.InvalidOperationException>(() => c.ResolveRound(l));
        }

        [Test]
        public void SameRoundEliminations_LowerIndexWorseOnTie()
        {
            // 4명: 0 · 1 강함, 2 · 3 빈 팀 · 목숨 1. 강한 쪽과 빈 쪽이 붙는 시드를 찾는다 → 2 · 3이 같은 라운드 탈락, 목숨이 같으니 번호 작은 2가 4등
            for (ulong seed = 0; seed < 200; seed++)
            {
                var c = Ctl(players: 4);
                LobbyState l = New(c, seed);
                foreach (LobbyPlayer p in l.Players) p.IsBot = false;
                l.Players[0].Run.Team[0] = Strong();
                l.Players[1].Run.Team[0] = Strong();
                l.Players[2].Run.Lives = 1;
                l.Players[3].Run.Lives = 1;
                c.BeginRound(l);
                LobbyRoundReport r = c.ResolveRound(l);
                if (r.Eliminated.Count != 2) continue;
                Assert.That(r.Eliminated, Is.EqualTo(new[] { 2, 3 }));
                Assert.That(l.Players[2].Placement, Is.EqualTo(4));
                Assert.That(l.Players[3].Placement, Is.EqualTo(3));
                Assert.That(l.IsFinished, Is.False);
                return;
            }
            Assert.Fail("강한 팀과 빈 팀이 붙는 시드를 못 찾음");
        }

        [Test]
        public void SameSeed_SameLobby()
        {
            string Play()
            {
                var c = Ctl();
                LobbyState l = New(c, 99);
                for (int i = 0; i < 6 && !l.IsFinished; i++)
                {
                    c.BeginRound(l);
                    c.ResolveRound(l);
                }
                return LobbyStateJson.Serialize(l);
            }
            Assert.That(Play(), Is.EqualTo(Play()));
        }

        [Test]
        public void LobbyState_JsonRoundTrip()
        {
            var c = Ctl();
            LobbyState l = New(c, 5);
            c.BeginRound(l);
            c.ResolveRound(l);
            c.BeginRound(l);
            string once = LobbyStateJson.Serialize(l);
            LobbyState back = LobbyStateJson.Deserialize(once);
            Assert.That(LobbyStateJson.Serialize(back), Is.EqualTo(once));
            Assert.That(back.Players[3].RecentOpponents.Count, Is.EqualTo(1));
        }

        [Test]
        public void Snapshot_IsBoardOnly_AndCopied()
        {
            var c = Ctl();
            LobbyState l = New(c);
            RunState me = l.Players[0].Run;
            me.Team[2] = Strong();
            me.Bench[0] = new MonsterSnapshot { SpeciesId = "w", Level = 1 };
            TeamSnapshot snap = c.MakeSnapshot(me);
            Assert.That(snap.Board[2].Level, Is.EqualTo(3));
            Assert.That(snap.Board.Count(m => m != null), Is.EqualTo(1), "대기석은 전투에 안 나감");
            me.Team[2].Level = 1;
            Assert.That(snap.Board[2].Level, Is.EqualTo(3), "복사본");
        }

        // ───────── L-2 공유 풀 ─────────

        /// <summary>전체 마리 수 = 풀 + 모든 상점 카드 + 모든 보유 몬스터(Lv1 1 · Lv2 3 · Lv3 6). 처음 총량과 항상 같아야 한다.</summary>
        private static int Units(LobbyController c, LobbyState l)
        {
            int n = new UnitPool(l.Pool).Total;
            foreach (LobbyPlayer p in l.Players)
            {
                n += p.Run.Shop.Count(id => id != null);
                foreach (MonsterSnapshot m in p.Run.Team.Concat(p.Run.Bench))
                    if (m != null) n += (int)c.Shop.Rules.CopiesAt(m.Level);
            }
            return n;
        }

        [Test]
        public void Pool_StartsWithRaritySizes()
        {
            LobbyState l = New(Ctl());
            Assert.That(l.Pool.Select(e => (e.SpeciesId, e.Count)), Is.EqualTo(new[] { ("s", 29), ("w", 29) }), "시험 2종 모두 일반 → 29");
        }

        [Test]
        public void Pool_TotalUnitsConserved_AcrossRoundsSellsRerollsAndEliminations()
        {
            var c = Ctl();
            LobbyState l = New(c, 21);
            int total = Units(c, l);
            Assert.That(total, Is.EqualTo(58));
            UnitPool pool = LobbyController.PoolOf(l);
            for (int i = 0; i < 12 && !l.IsFinished; i++)
            {
                c.BeginRound(l);
                Assert.That(Units(c, l), Is.EqualTo(total), $"R{l.Round} 진열 후");
                RunState me = l.Players[0].Run;
                if (me.Gold >= 3) c.Shop.Reroll(me, pool);
                for (int k = 0; k < me.Shop.Count; k++) c.Shop.Buy(me, k);
                if (me.Bench[0] != null) c.Shop.Sell(me, Location.Bench(0), pool);
                for (int b = 0; b < 4; b++)
                {
                    int empty = System.Array.IndexOf(me.Team, null);
                    if (empty >= 0 && me.Bench[b] != null) c.Shop.Move(me, Location.Bench(b), Location.Board(empty));
                }
                Assert.That(Units(c, l), Is.EqualTo(total), $"R{l.Round} 사람 조작 후");
                c.ResolveRound(l);
                Assert.That(Units(c, l), Is.EqualTo(total), $"R{l.Round} 전투 · 탈락 후");
            }
        }

        [Test]
        public void Pool_Draw_ProportionalAndNeverEmptySpecies()
        {
            var entries = new List<PoolEntry> { new PoolEntry { SpeciesId = "a", Count = 0 }, new PoolEntry { SpeciesId = "b", Count = 5 } };
            var pool = new UnitPool(entries);
            var rng = new Moa.Showdown.Core.Random.DeterministicRandom(3);
            for (int i = 0; i < 5; i++) Assert.That(pool.Draw(rng), Is.EqualTo("b"), "0인 a는 안 나옴");
            Assert.That(pool.Draw(rng), Is.Null, "다 뽑으면 null");
            pool.Return("a", 2);
            Assert.That(pool.CountOf("a"), Is.EqualTo(2));
        }

        [Test]
        public void Pool_Empty_ShopShowsEmptySlots()
        {
            var c = Ctl();
            LobbyState l = New(c);
            foreach (PoolEntry e in l.Pool) e.Count = 2; // 풀에 4마리뿐
            c.BeginRound(l);
            int shown = l.Players.Sum(p => p.Run.Shop.Count(id => id != null));
            int bought = l.Players.Sum(p => p.Run.Team.Concat(p.Run.Bench).Where(m => m != null).Sum(m => (int)c.Shop.Rules.CopiesAt(m.Level)));
            Assert.That(shown + bought + new UnitPool(l.Pool).Total, Is.EqualTo(4));
            Assert.That(l.Players[7].Run.Shop.All(id => id == null), Is.True, "늦게 뽑는 사람은 빈 상점");
        }

        [Test]
        public void Pool_SellReturnsCopies_EliminationReturnsAll()
        {
            var c = Ctl(players: 2);
            LobbyState l = New(c);
            foreach (LobbyPlayer p in l.Players) p.IsBot = false;
            UnitPool pool = LobbyController.PoolOf(l);
            int before = pool.CountOf("s");
            RunState me = l.Players[0].Run;
            me.Team[0] = new MonsterSnapshot { SpeciesId = "s", Level = 2 };
            c.Shop.Sell(me, Location.Board(0), pool);
            Assert.That(pool.CountOf("s"), Is.EqualTo(before + 3), "Lv2 = 3마리 돌아감");

            RunState other = l.Players[1].Run;
            other.Bench[1] = new MonsterSnapshot { SpeciesId = "w", Level = 3 };
            other.Lives = 1;
            me.Team[0] = Strong();
            int wBefore = pool.CountOf("w");
            c.BeginRound(l);
            int myShopW = me.Shop.Count(id => id == "w"); // 내 상점에 나온 w는 계속 빠져 있음
            c.ResolveRound(l);
            Assert.That(l.Players[1].Placement, Is.EqualTo(2));
            Assert.That(other.Bench.All(m => m == null) && other.Shop.Count == 0, Is.True, "탈락자는 비워짐");
            // 풀 w = 처음 − (내 상점 w + 탈락자 상점 w) + 탈락자 상점 w(돌아옴) + Lv3 6마리(돌아옴)
            Assert.That(pool.CountOf("w"), Is.EqualTo(wBefore - myShopW + 6), "Lv3 = 6마리 + 상점 카드가 돌아감");
        }

        [Test]
        public void LobbyJson_OldVersion1_IsRejected()
        {
            string v2 = LobbyStateJson.Serialize(New(Ctl()));
            Assert.Throws<SnapshotFormatException>(() => LobbyStateJson.Deserialize(v2.Replace("\"formatVersion\":2", "\"formatVersion\":1")));
        }
    }
}
