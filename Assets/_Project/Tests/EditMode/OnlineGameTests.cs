using System.Collections.Generic;
using System.Linq;
using Moa.Showdown.Core.Battle;
using Moa.Showdown.Core.Content;
using Moa.Showdown.Core.Lobby;
using Moa.Showdown.Core.Online;
using Moa.Showdown.Core.Run;
using Moa.Showdown.Core.Shop;
using Moa.Showdown.Core.Snapshot;
using NUnit.Framework;

namespace Moa.Showdown.Tests
{
    /// <summary>O-2 온라인 판: 방장 진행(HostSession) 단계 · 시간 · 행동, 동기화 · 행동 JSON, 전투 다시 계산 · 비교. 네트워크 없이 Core만.</summary>
    public class OnlineGameTests
    {
        private static SpeciesCatalog Catalog() => new SpeciesCatalog(new[]
        {
            new SpeciesDef { Id = "s", DisplayName = "S", Attack = 10, AttackIntervalTicks = 10 },
            new SpeciesDef { Id = "w", DisplayName = "W", Attack = 1, AttackIntervalTicks = 50 },
        });

        private static readonly UnitStatRules X2 = new UnitStatRules { LevelAttackMultiplier = 2 };

        private static BattleRules Battle() => new BattleRules
        {
            TicksPerSecond = 10, TeamHpByRound = new[] { 100 }, SuddenDeathStartTick = 300, SuddenDeathIntervalTicks = 10,
            SuddenDeathBaseDamage = 20, SuddenDeathDamageStep = 20, MaxTicks = 2000, BurnIntervalTicks = 10, PoisonIntervalTicks = 20,
        };

        private static LobbyController Ctl(bool carousel = false)
        {
            var catalog = Catalog();
            var shop = new ShopController(new ShopRules
            {
                StartGold = 30, FirstIncome = 35, IncomeStep = 5, ShopSlots = 5, RerollCost = 3,
                PriceByRarity = new[] { 10, 20, 30, 40, 50 }, SellPercent = 75, MergeCountByLevel = new[] { 3, 2 }, MaxLevel = 3,
            }, catalog, X2);
            return new LobbyController(
                new LobbyRules
                {
                    PlayerCount = 8, RecentOpponentMemory = 3, PoolSizeByRarity = new[] { 29, 22, 18, 12, 10 }, PrepSeconds = 30,
                    CarouselOpeningRounds = carousel ? 1 : 0, CarouselPickSeconds = 10,
                },
                new RunRules { WinsToChampion = 10, StartLives = 10, LifeLossByRound = new[] { 1, 1, 2, 3 } },
                Battle(), X2, catalog, shop, 7);
        }

        private static readonly HostSessionRules Timing = new HostSessionRules { CarouselDoneSeconds = 3, ResultGraceSeconds = 20, TicksPerSecond = 10 };

        private static HostSession Host(bool carousel = false, ulong seed = 2026)
        {
            var h = new HostSession(Ctl(carousel), Timing, 7);
            h.Start("online_test", seed, new[] { ("p1", "방장"), ("p2", "손님") });
            return h;
        }

        // ───────── 시작 · 상점 ─────────

        [Test]
        public void Start_HumansTakeFirstSeats_BotsRest_ShopPhase()
        {
            HostSession h = Host();
            Assert.That(h.Seats.Take(2), Is.EqualTo(new[] { "p1", "p2" }));
            Assert.That(h.Seats.Skip(2).All(x => x == null), Is.True, "빈자리 = 봇");
            Assert.That(h.Lobby.Players[0].IsBot || h.Lobby.Players[1].IsBot, Is.False);
            Assert.That(h.Lobby.Players[1].Name, Is.EqualTo("손님"));
            Assert.That(h.Phase, Is.EqualTo(OnlinePhase.Shop));
            Assert.That(h.PhaseSecondsLeft, Is.EqualTo(30));
            Assert.That(h.Lobby.Players[1].Run.Gold, Is.EqualTo(30), "사람은 봇처럼 자동으로 사지 않음");
            Assert.That(h.Dirty, Is.True);
        }

        [Test]
        public void Buy_ByGuest_ChangesOnlyTheirRun()
        {
            HostSession h = Host();
            int hostGold = h.Lobby.Players[0].Run.Gold;
            ActionAck a = h.Apply("p2", LobbyAction.Of(LobbyActionKind.Buy, 0));
            Assert.That(a.Ok, Is.True);
            Assert.That((a.Player, a.Kind), Is.EqualTo((1, LobbyActionKind.Buy)));
            Assert.That(h.Lobby.Players[1].Run.Gold, Is.EqualTo(20), "30 − 일반 10");
            Assert.That(h.Lobby.Players[1].Run.Bench[0]?.SpeciesId, Is.EqualTo(a.SpeciesId));
            Assert.That(h.Lobby.Players[0].Run.Gold, Is.EqualTo(hostGold));
        }

        [Test]
        public void Sell_Move_Reroll_ByGuest()
        {
            HostSession h = Host();
            h.Apply("p2", LobbyAction.Of(LobbyActionKind.Buy, 0));
            Assert.That(h.Apply("p2", LobbyAction.MoveFromTo(Location.Bench(0), Location.Board(2))).Ok, Is.True);
            Assert.That(h.Lobby.Players[1].Run.Team[2], Is.Not.Null);
            ActionAck sell = h.Apply("p2", LobbyAction.SellAt(Location.Board(2)));
            Assert.That((sell.Ok, sell.Gold), Is.EqualTo((true, 8)), "10 × 75% 반올림");
            Assert.That(h.Lobby.Players[1].Run.Gold, Is.EqualTo(28), "20 + 8");
            Assert.That(h.Apply("p2", LobbyAction.Of(LobbyActionKind.Reroll)).Ok, Is.True);
            Assert.That(h.Lobby.Players[1].Run.Gold, Is.EqualTo(25), "28 − 새로고침 3");
        }

        [Test]
        public void Unknown_WrongPhase_BadLocation_Rejected()
        {
            HostSession h = Host();
            ActionAck stranger = h.Apply("nobody", LobbyAction.Of(LobbyActionKind.Buy, 0));
            Assert.That((stranger.Player, stranger.Ok), Is.EqualTo((-1, false)));
            Assert.That(h.Apply("p2", LobbyAction.Of(LobbyActionKind.Continue)).Ok, Is.False, "상점 단계에 계속");
            Assert.That(h.Apply("p2", LobbyAction.Of(LobbyActionKind.CarouselPick, 0)).Ok, Is.False, "캐러셀 아님");
            Assert.That(h.Apply("p2", LobbyAction.MoveFromTo(Location.Board(0), Location.Board(99))).Ok, Is.False, "없는 칸");
            Assert.That(h.Apply("p2", LobbyAction.SellAt(Location.Bench(0))).Ok, Is.False, "빈 칸 팔기");
            Assert.That(h.Lobby.Players[1].Run.Gold, Is.EqualTo(30));
        }

        [Test]
        public void AllHumansReady_ResolvesRound_BattlePhase()
        {
            HostSession h = Host();
            h.Apply("p1", LobbyAction.Of(LobbyActionKind.Ready));
            Assert.That(h.Phase, Is.EqualTo(OnlinePhase.Shop), "한 명만 준비");
            h.Apply("p2", LobbyAction.Of(LobbyActionKind.Ready));
            Assert.That(h.Phase, Is.EqualTo(OnlinePhase.Battle));
            Assert.That(h.LastReport, Is.Not.Null);
            Assert.That(h.LastReport.Round, Is.EqualTo(1));
            int longest = h.LastReport.Matches.Max(m => m.Battle.EndTick);
            Assert.That(h.PhaseSecondsLeft, Is.EqualTo(longest / 10.0 + 20).Within(1e-9), "가장 긴 전투(1배속) + 20초");
        }

        [Test]
        public void ShopTimeUp_ResolvesWithoutReady()
        {
            HostSession h = Host();
            h.Tick(29.9);
            Assert.That(h.Phase, Is.EqualTo(OnlinePhase.Shop));
            h.Tick(0.2);
            Assert.That(h.Phase, Is.EqualTo(OnlinePhase.Battle));
        }

        [Test]
        public void Battle_AllContinue_NextRoundShop()
        {
            HostSession h = Host();
            h.Tick(31);
            Assert.That(h.Phase, Is.EqualTo(OnlinePhase.Battle));
            Assert.That(h.Apply("p1", LobbyAction.Of(LobbyActionKind.Ready)).Ok, Is.False, "전투 단계에 준비");
            h.Apply("p1", LobbyAction.Of(LobbyActionKind.Continue));
            Assert.That(h.Phase, Is.EqualTo(OnlinePhase.Battle));
            h.Apply("p2", LobbyAction.Of(LobbyActionKind.Continue));
            Assert.That(h.Phase, Is.EqualTo(OnlinePhase.Shop));
            Assert.That(h.Lobby.Round, Is.EqualTo(2));
            Assert.That(h.Lobby.Players[1].Run.Gold, Is.EqualTo(65), "30 + 2라운드 수입 35");
        }

        [Test]
        public void Battle_TimeUp_NextRound()
        {
            HostSession h = Host();
            h.Tick(31);
            h.Tick(h.PhaseSecondsLeft + 0.01);
            Assert.That((h.Phase, h.Lobby.Round), Is.EqualTo((OnlinePhase.Shop, 2)));
        }

        // ───────── 캐러셀 ─────────

        [Test]
        public void Carousel_HumanTurns_PickAndTimeout_ThenShop()
        {
            HostSession h = Host(carousel: true);
            Assert.That(h.Phase, Is.EqualTo(OnlinePhase.Carousel));
            CarouselState c = h.Lobby.Carousel;
            Assert.That(c.CurrentPicker, Is.EqualTo(0), "목숨이 같으면 번호 순 → 방장 먼저");
            Assert.That(h.PhaseSecondsLeft, Is.EqualTo(10));
            Assert.That(h.Apply("p2", LobbyAction.Of(LobbyActionKind.CarouselPick, 0)).Ok, Is.False, "차례가 아님");

            // 방장은 시간 끝 → 봇 규칙으로 대신, 다음은 손님 차례
            h.Tick(10.01);
            Assert.That(c.TakenBy.Count(x => x == 0), Is.EqualTo(1));
            Assert.That(c.CurrentPicker, Is.EqualTo(1));
            Assert.That(h.MakeSync().Acks.Any(a => a.Player == 0 && a.Seq == -1 && a.Kind == LobbyActionKind.CarouselPick), Is.True, "자동 고름 표시 (Seq −1)");

            int free = c.TakenBy.IndexOf(-1);
            ActionAck pick = h.Apply("p2", LobbyAction.Of(LobbyActionKind.CarouselPick, free));
            Assert.That(pick.Ok, Is.True);
            Assert.That(c.TakenBy[free], Is.EqualTo(1));
            Assert.That(c.IsDone, Is.True, "나머지는 봇이 바로");
            Assert.That(h.PhaseSecondsLeft, Is.EqualTo(3), "끝난 캐러셀을 잠깐 보여 줌");
            h.Tick(3.01);
            Assert.That(h.Phase, Is.EqualTo(OnlinePhase.Shop));
            Assert.That(h.Lobby.Players[1].Run.Bench.Count(m => m != null), Is.EqualTo(1), "손님이 고른 몬스터");
        }

        // ───────── 동기화 JSON · 다시 계산 ─────────

        [Test]
        public void Sync_RoundTrip_AndRebuildMatchesHost()
        {
            HostSession h = Host(seed: ulong.MaxValue - 5); // 큰 시드도 그대로
            h.Tick(31);
            LobbySync s = h.MakeSync();
            Assert.That(h.Dirty, Is.False);
            string json = OnlineLobbyJson.SerializeSync(s);
            LobbySync back = OnlineLobbyJson.DeserializeSync(json);
            Assert.That((back.Version, back.RulesVersion, back.Phase), Is.EqualTo((s.Version, 7, OnlinePhase.Battle)));
            Assert.That(back.Seats, Is.EqualTo(s.Seats));
            Assert.That(back.Lobby.Round, Is.EqualTo(h.Lobby.Round));
            Assert.That(back.Lobby.Seed, Is.EqualTo(h.Lobby.Seed));
            Assert.That(back.Report.Matches.Select(m => m.Seed), Is.EqualTo(h.LastReport.Matches.Select(m => m.Seed)));
            Assert.That(OnlineLobbyJson.SerializeSync(back), Is.EqualTo(json), "다시 써도 같은 글");

            var mismatches = new List<int>();
            LobbyRoundReport rebuilt = back.Report.Rebuild(Battle(), Catalog(), X2, mismatches);
            Assert.That(mismatches, Is.Empty, "같은 기록 → 같은 전투 (결정론)");
            for (int i = 0; i < rebuilt.Matches.Count; i++)
                Assert.That(rebuilt.Matches[i].Battle.EndTick, Is.EqualTo(h.LastReport.Matches[i].Battle.EndTick));
        }

        [Test]
        public void Rebuild_FlagsDifferentResult()
        {
            HostSession h = Host();
            h.Tick(31);
            RoundReportRecord r = h.MakeSync().Report;
            r.Matches[1].EndTick += 1;
            var mismatches = new List<int>();
            r.Rebuild(Battle(), Catalog(), X2, mismatches);
            Assert.That(mismatches, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void Sync_Version_IncreasesEachTime()
        {
            HostSession h = Host();
            int v1 = h.MakeSync().Version;
            int v2 = h.MakeSync().Version;
            Assert.That(v2, Is.EqualTo(v1 + 1));
        }

        [Test]
        public void DeserializeSync_RejectsBad()
        {
            HostSession h = Host();
            string good = OnlineLobbyJson.SerializeSync(h.MakeSync());
            Assert.Throws<NetMessageFormatException>(() => OnlineLobbyJson.DeserializeSync(""));
            Assert.Throws<NetMessageFormatException>(() => OnlineLobbyJson.DeserializeSync("nope"));
            Assert.Throws<NetMessageFormatException>(() => OnlineLobbyJson.DeserializeSync(good.Replace("\"v\":1,", "\"v\":2,")), "다른 형식 버전");
            Assert.Throws<NetMessageFormatException>(() => OnlineLobbyJson.DeserializeSync(good.Replace("\"seats\":[\"p1\",", "\"seats\":[")), "자리 수 ≠ 플레이어 수");
            Assert.Throws<NetMessageFormatException>(() => OnlineLobbyJson.DeserializeSync("{\"v\":1,\"seats\":[],\"ready\":[],\"acks\":[]}"), "로비 없음");
        }

        [Test]
        public void Action_RoundTrip_AndBadKindRejected()
        {
            LobbyAction a = LobbyAction.MoveFromTo(Location.Bench(3), Location.Board(5));
            a.Seq = 42;
            LobbyAction back = OnlineLobbyJson.DeserializeAction(OnlineLobbyJson.SerializeAction(a));
            Assert.That((back.Kind, back.From, back.To, back.Seq), Is.EqualTo((LobbyActionKind.Move, Location.Bench(3), Location.Board(5), 42)));
            Assert.Throws<NetMessageFormatException>(() => OnlineLobbyJson.DeserializeAction("{\"kind\":0}"));
            Assert.Throws<NetMessageFormatException>(() => OnlineLobbyJson.DeserializeAction("{\"kind\":99}"));
            Assert.Throws<NetMessageFormatException>(() => OnlineLobbyJson.DeserializeAction("x"));
        }

        // ───────── O-3: 나간 사람 → 봇 · 방장 넘기기 · 다름 보고 ─────────

        [Test]
        public void ReplaceWithBot_InShop_BotActsAndRoundResolvesWhenOthersReady()
        {
            HostSession h = Host();
            h.Apply("p1", LobbyAction.Of(LobbyActionKind.Ready));
            Assert.That(h.ReplaceWithBot("p2"), Is.True);
            LobbyPlayer guest = h.Lobby.Players[1];
            Assert.That((guest.IsBot, h.Seats[1]), Is.EqualTo((true, (string)null)));
            Assert.That(guest.Name, Is.EqualTo("손님"), "이름 · 목숨 · 몬스터는 그대로");
            Assert.That(h.Phase, Is.EqualTo(OnlinePhase.Battle), "남은 사람(방장)이 이미 준비 → 바로 전투");
            Assert.That(h.LastReport.Matches.Any(m => m.PlayerA == 1 || m.PlayerB == 1), Is.True, "봇이 된 자리도 싸움");
            Assert.That(h.MakeSync().Lobby.Players[1].Run.Team.Any(m => m != null), Is.True, "봇이 바로 사고 배치 (30골드)");
        }

        [Test]
        public void ReplaceWithBot_CurrentCarouselPicker_PickedAtOnce()
        {
            HostSession h = Host(carousel: true);
            CarouselState c = h.Lobby.Carousel;
            Assert.That(c.CurrentPicker, Is.EqualTo(0));
            h.ReplaceWithBot("p1");
            Assert.That(c.TakenBy.Count(x => x == 0), Is.EqualTo(1), "봇 규칙으로 바로 고름");
            Assert.That(c.CurrentPicker, Is.EqualTo(1), "다음 사람(손님) 차례");
            Assert.That(h.PhaseSecondsLeft, Is.EqualTo(10));
        }

        [Test]
        public void ReplaceWithBot_InBattle_OthersContinueAdvances()
        {
            HostSession h = Host();
            h.Tick(31);
            h.Apply("p1", LobbyAction.Of(LobbyActionKind.Continue));
            Assert.That(h.Phase, Is.EqualTo(OnlinePhase.Battle));
            h.ReplaceWithBot("p2");
            Assert.That((h.Phase, h.Lobby.Round), Is.EqualTo((OnlinePhase.Shop, 2)), "남은 사람이 모두 계속 → 다음 라운드");
            Assert.That(h.Apply("p2", LobbyAction.Of(LobbyActionKind.Buy, 0)).Ok, Is.False, "봇이 된 사람의 행동은 무시");
        }

        [Test]
        public void ReplaceWithBot_Unknown_False()
        {
            HostSession h = Host();
            h.MakeSync();
            Assert.That(h.ReplaceWithBot("nobody"), Is.False);
            Assert.That(h.Dirty, Is.False);
        }

        [Test]
        public void Resume_ContinuesPhaseTimeReadyAndVersion()
        {
            HostSession a = Host();
            a.Tick(5);
            a.Apply("p2", LobbyAction.Of(LobbyActionKind.Buy, 0));
            a.Apply("p2", LobbyAction.Of(LobbyActionKind.Ready));
            string json = OnlineLobbyJson.SerializeSync(a.MakeSync());

            var b = new HostSession(Ctl(), Timing, 7);
            b.Resume(OnlineLobbyJson.DeserializeSync(json));
            Assert.That(b.Phase, Is.EqualTo(OnlinePhase.Shop));
            Assert.That(b.PhaseSecondsLeft, Is.EqualTo(25).Within(0.001), "30 − 5초");
            Assert.That(b.Seats, Is.EqualTo(a.Seats));
            Assert.That(b.Lobby.Players[1].Run.Gold, Is.EqualTo(20));
            Assert.That(b.MakeSync().Version, Is.EqualTo(a.Version + 1), "참가자가 새 것으로 받아들이게 이어서 셈");
            Assert.That(b.MakeSync().Ready, Is.EqualTo(new[] { 1 }), "손님 준비 완료 유지");

            // 새 방장은 손님(p2): 나간 옛 방장(p1) 자리를 봇으로 → 남은 사람이 모두 준비 → 바로 전투
            b.ReplaceWithBot("p1");
            Assert.That(b.Phase, Is.EqualTo(OnlinePhase.Battle));
        }

        [Test]
        public void Resume_InBattle_ThenFinishesWithRecordOnly()
        {
            HostSession a = Host(carousel: true, seed: 99);
            a.Tick(11); a.Tick(11); a.Tick(4); // 캐러셀 사람 둘 시간 끝 + 끝난 캐러셀
            Assert.That(a.Phase, Is.EqualTo(OnlinePhase.Shop));
            a.Tick(31);
            Assert.That(a.Phase, Is.EqualTo(OnlinePhase.Battle));

            var b = new HostSession(Ctl(carousel: true), Timing, 7);
            b.Resume(OnlineLobbyJson.DeserializeSync(OnlineLobbyJson.SerializeSync(a.MakeSync())));
            Assert.That(b.LastReport, Is.Null, "계산은 옛 방장이 함 — 기록만 이어받음");
            Assert.That(b.MakeSync().Report.Round, Is.EqualTo(1), "기록은 계속 보냄");
            b.ReplaceWithBot("p1");
            int guard = 0;
            while (b.Phase != OnlinePhase.Finished && guard++ < 500)
            {
                if (b.Phase == OnlinePhase.Shop) b.Apply("p2", LobbyAction.Of(LobbyActionKind.Ready));
                else if (b.Phase == OnlinePhase.Battle) b.Apply("p2", LobbyAction.Of(LobbyActionKind.Continue));
                b.Tick(11);
            }
            Assert.That(b.Phase, Is.EqualTo(OnlinePhase.Finished));
            Assert.That(b.Lobby.IsFinished, Is.True);
        }

        [Test]
        public void Resume_Rejects_WrongRulesVersion_AndWaiting()
        {
            HostSession a = Host();
            LobbySync s = a.MakeSync();
            Assert.Throws<System.InvalidOperationException>(() => new HostSession(Ctl(), Timing, 8).Resume(s), "규칙 버전 다름");
            s.Phase = OnlinePhase.Waiting;
            Assert.Throws<System.InvalidOperationException>(() => new HostSession(Ctl(), Timing, 7).Resume(s), "시작 전");
        }

        [Test]
        public void MismatchReport_RoundTrip_AndBadRejected()
        {
            var r = new MismatchReport { LobbyId = "L", Round = 3, Matches = new List<int> { 0, 2 }, RulesVersion = 7 };
            MismatchReport back = OnlineLobbyJson.DeserializeMismatch(OnlineLobbyJson.SerializeMismatch(r));
            Assert.That((back.LobbyId, back.Round, back.RulesVersion), Is.EqualTo(("L", 3, 7)));
            Assert.That(back.Matches, Is.EqualTo(new[] { 0, 2 }));
            Assert.Throws<NetMessageFormatException>(() => OnlineLobbyJson.DeserializeMismatch("{\"round\":0,\"matches\":[]}"));
            Assert.Throws<NetMessageFormatException>(() => OnlineLobbyJson.DeserializeMismatch("x"));
        }

        [Test]
        public void FullGame_HumansAlwaysReady_FinishesAndEverySyncFitsOneMessage()
        {
            HostSession h = Host(carousel: true, seed: 99);
            int guard = 0;
            while (h.Phase != OnlinePhase.Finished && guard++ < 500)
            {
                foreach (string id in new[] { "p1", "p2" })
                {
                    if (h.Phase == OnlinePhase.Shop) h.Apply(id, LobbyAction.Of(LobbyActionKind.Ready));
                    else if (h.Phase == OnlinePhase.Battle) h.Apply(id, LobbyAction.Of(LobbyActionKind.Continue));
                }
                h.Tick(11); // 캐러셀 차례 · 끝난 캐러셀 시간 넘김
                string body = OnlineLobbyJson.SerializeSync(h.MakeSync());
                string msg = NetMessageJson.Serialize(new NetMessage { Type = NetMessageTypes.Sync, From = "p1", Body = body });
                Assert.That(msg.Length, Is.LessThan(NetMessageJson.MaxLength), $"{h.Lobby.Round}라운드 동기화 크기");
            }
            Assert.That(h.Phase, Is.EqualTo(OnlinePhase.Finished));
            Assert.That(h.Lobby.IsFinished, Is.True);
            Assert.That(h.Lobby.Players.Count(p => p.Placement == 1), Is.EqualTo(1));
        }
    }
}
