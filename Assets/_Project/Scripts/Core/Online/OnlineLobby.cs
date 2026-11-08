using System;
using System.Collections.Generic;
using System.Linq;
using Moa.Showdown.Core.Battle;
using Moa.Showdown.Core.Content;
using Moa.Showdown.Core.Lobby;
using Moa.Showdown.Core.Shop;
using Moa.Showdown.Core.Snapshot;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Moa.Showdown.Core.Online
{
    /// <summary>참가자 → 방장 행동 종류 (O-2). 온라인 계약이라 끝에만 추가한다.</summary>
    public enum LobbyActionKind
    {
        None = 0,
        Buy = 1,
        Reroll = 2,
        Sell = 3,
        Move = 4,
        /// <summary>상점 준비 완료.</summary>
        Ready = 5,
        CarouselPick = 6,
        /// <summary>결과 화면 "다음 라운드".</summary>
        Continue = 7,
    }

    /// <summary>참가자가 누른 것 하나 (메시지 종류 "action"의 body). 방장이 Core로 처리해 결과를 상태로 돌려준다.</summary>
    public sealed class LobbyAction
    {
        [JsonProperty("kind", Order = 0)] public LobbyActionKind Kind;
        /// <summary>Buy = 상점 칸, CarouselPick = 캐러셀 카드.</summary>
        [JsonProperty("index", Order = 1)] public int Index;
        /// <summary>Sell · Move의 자리 (Area 번호 · 칸).</summary>
        [JsonProperty("fromArea", Order = 2)] public int FromArea;
        [JsonProperty("fromIndex", Order = 3)] public int FromIndex;
        [JsonProperty("toArea", Order = 4)] public int ToArea;
        [JsonProperty("toIndex", Order = 5)] public int ToIndex;
        /// <summary>참가자가 매기는 번호 (답 맞추기용).</summary>
        [JsonProperty("seq", Order = 6)] public int Seq;

        public Location From => new Location((Area)FromArea, FromIndex);
        public Location To => new Location((Area)ToArea, ToIndex);

        public static LobbyAction Of(LobbyActionKind kind, int index = 0) => new LobbyAction { Kind = kind, Index = index };

        public static LobbyAction SellAt(Location at) => new LobbyAction { Kind = LobbyActionKind.Sell, FromArea = (int)at.Area, FromIndex = at.Index };

        public static LobbyAction MoveFromTo(Location from, Location to) => new LobbyAction
        {
            Kind = LobbyActionKind.Move, FromArea = (int)from.Area, FromIndex = from.Index, ToArea = (int)to.Area, ToIndex = to.Index,
        };
    }

    /// <summary>행동 처리 결과 (그 사람 화면의 안내 글용).</summary>
    public sealed class ActionAck
    {
        [JsonProperty("player", Order = 0)] public int Player;
        [JsonProperty("seq", Order = 1)] public int Seq;
        [JsonProperty("kind", Order = 2)] public LobbyActionKind Kind;
        /// <summary>처리됐는지 (차례 · 단계가 아니거나 잘못된 행동이면 false).</summary>
        [JsonProperty("ok", Order = 3)] public bool Ok;
        [JsonProperty("shopError", Order = 4)] public ShopError ShopError;
        /// <summary>산 · 판 · 고른 몬스터.</summary>
        [JsonProperty("speciesId", Order = 5)] public string SpeciesId;
        /// <summary>산 뒤 그 몬스터의 가장 높은 레벨 (합체 안내용).</summary>
        [JsonProperty("levelAfter", Order = 6)] public int LevelAfter;
        [JsonProperty("levelBefore", Order = 7)] public int LevelBefore;
        /// <summary>판 금액 · 캐러셀 대신 받은 골드.</summary>
        [JsonProperty("gold", Order = 8)] public int Gold;
        [JsonProperty("carousel", Order = 9)] public CarouselPickResult Carousel;
    }

    /// <summary>온라인 판의 단계 (방장이 정함).</summary>
    public enum OnlinePhase
    {
        Waiting = 0,
        Carousel = 1,
        Shop = 2,
        /// <summary>전투 재생 · 결과 화면 (모두 "다음 라운드"를 누르거나 시간이 끝나면 다음 라운드).</summary>
        Battle = 3,
        Finished = 4,
    }

    /// <summary>대전 하나의 기록 (참가자가 같은 전투를 다시 계산하고 방장 결과와 비교하는 데 씀).</summary>
    public sealed class MatchRecord
    {
        [JsonProperty("a", Order = 0)] public int PlayerA;
        [JsonProperty("b", Order = 1)] public int PlayerB;
        [JsonProperty("ghostOf", Order = 2)] public int GhostOf = -1;
        [JsonProperty("pve", Order = 3)] public bool IsPve;
        [JsonProperty("seed", Order = 4)] public ulong Seed;
        [JsonProperty("snapA", Order = 5)] public TeamSnapshot SnapshotA;
        [JsonProperty("snapB", Order = 6)] public TeamSnapshot SnapshotB;
        [JsonProperty("outcome", Order = 7)] public BattleOutcome Outcome;
        [JsonProperty("endTick", Order = 8)] public int EndTick;
        [JsonProperty("hpA", Order = 9)] public int HpA;
        [JsonProperty("hpB", Order = 10)] public int HpB;
        [JsonProperty("livesLostA", Order = 11)] public int LivesLostA;
        [JsonProperty("livesLostB", Order = 12)] public int LivesLostB;
        [JsonProperty("goldA", Order = 13)] public int GoldGainedA;
    }

    /// <summary>한 라운드 기록 (방장 → 모두).</summary>
    public sealed class RoundReportRecord
    {
        [JsonProperty("round", Order = 0)] public int Round;
        [JsonProperty("matches", Order = 1)] public List<MatchRecord> Matches = new List<MatchRecord>();
        [JsonProperty("eliminated", Order = 2)] public List<int> Eliminated = new List<int>();
        [JsonProperty("finished", Order = 3)] public bool FinishedAfter;

        public static RoundReportRecord From(LobbyRoundReport r) => new RoundReportRecord
        {
            Round = r.Round,
            Eliminated = new List<int>(r.Eliminated),
            FinishedAfter = r.FinishedAfter,
            Matches = r.Matches.Select(m => new MatchRecord
            {
                PlayerA = m.PlayerA, PlayerB = m.PlayerB, GhostOf = m.GhostOf, IsPve = m.IsPve, Seed = m.Seed,
                SnapshotA = m.SnapshotA, SnapshotB = m.SnapshotB,
                Outcome = m.Battle.Outcome, EndTick = m.Battle.EndTick, HpA = m.Battle.HpA, HpB = m.Battle.HpB,
                LivesLostA = m.LivesLostA, LivesLostB = m.LivesLostB, GoldGainedA = m.GoldGainedA,
            }).ToList(),
        };

        /// <summary>
        /// 기록 → 전투 보고 (각자 같은 전투를 다시 계산, CLAUDE 핵심 규칙 2). 방장 결과(승패 · 끝난 틱 · 체력)와 다른 대전 번호를 mismatches에.
        /// 내 화면은 다시 계산한 전투를 재생한다.
        /// </summary>
        public LobbyRoundReport Rebuild(BattleRules rules, SpeciesCatalog catalog, UnitStatRules statRules, List<int> mismatches)
        {
            var report = new LobbyRoundReport { Round = Round, Eliminated = new List<int>(Eliminated), FinishedAfter = FinishedAfter };
            for (int i = 0; i < Matches.Count; i++)
            {
                MatchRecord r = Matches[i];
                var m = new LobbyMatch
                {
                    PlayerA = r.PlayerA, PlayerB = r.PlayerB, GhostOf = r.GhostOf, IsPve = r.IsPve, Seed = r.Seed,
                    SnapshotA = r.SnapshotA, SnapshotB = r.SnapshotB,
                    LivesLostA = r.LivesLostA, LivesLostB = r.LivesLostB, GoldGainedA = r.GoldGainedA,
                };
                m.TeamA = TeamFactory.Build(r.SnapshotA, catalog, statRules);
                m.TeamB = TeamFactory.Build(r.SnapshotB, catalog, statRules);
                m.Battle = BattleSimulator.Run(rules, m.TeamA, m.TeamB, Round, r.Seed);
                if (m.Battle.Outcome != r.Outcome || m.Battle.EndTick != r.EndTick || m.Battle.HpA != r.HpA || m.Battle.HpB != r.HpB)
                    mismatches?.Add(i);
                report.Matches.Add(m);
            }
            return report;
        }
    }

    /// <summary>방장 → 모두: 지금 판 전부 (메시지 종류 "sync"의 body). 상태가 바뀔 때마다 통째로 보낸다 (로비 상태 JSON 5 ~ 8천 글자).</summary>
    public sealed class LobbySync
    {
        public const int CurrentFormatVersion = 1;

        [JsonProperty("v", Order = 0)] public int FormatVersion = CurrentFormatVersion;
        /// <summary>보낸 순서 (참가자는 더 작은 번호를 무시).</summary>
        [JsonProperty("version", Order = 1)] public int Version;
        /// <summary>규칙 버전 — 다르면 같은 전투가 나오지 않으므로 참가자가 거부한다.</summary>
        [JsonProperty("rulesVersion", Order = 2)] public int RulesVersion;
        [JsonProperty("phase", Order = 3)] public OnlinePhase Phase;
        /// <summary>이 단계 남은 시간 (밀리초, 보낸 순간 기준).</summary>
        [JsonProperty("phaseMs", Order = 4)] public int PhaseMs;
        /// <summary>자리 번호 → 플레이어 ID (봇 = null).</summary>
        [JsonProperty("seats", Order = 5)] public List<string> Seats = new List<string>();
        /// <summary>상점 단계에서 준비 완료한 자리 · 결과 단계에서 "다음 라운드"를 누른 자리.</summary>
        [JsonProperty("ready", Order = 6)] public List<int> Ready = new List<int>();
        [JsonProperty("lobby", Order = 7)] public LobbyState Lobby;
        /// <summary>마지막 라운드 기록 (없으면 null).</summary>
        [JsonProperty("report", Order = 8)] public RoundReportRecord Report;
        [JsonProperty("acks", Order = 9)] public List<ActionAck> Acks = new List<ActionAck>();

        public int SeatOf(string playerId) => playerId == null ? -1 : Seats.IndexOf(playerId);
    }

    /// <summary>온라인 판 메시지 JSON 한 곳 (행동 · 동기화). 형식 오류는 <see cref="NetMessageFormatException"/>.</summary>
    public static class OnlineLobbyJson
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Include,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            TypeNameHandling = TypeNameHandling.None,
            FloatParseHandling = FloatParseHandling.Decimal,
        };

        public static string SerializeAction(LobbyAction a) => JsonConvert.SerializeObject(a ?? throw new ArgumentNullException(nameof(a)), Settings);

        public static LobbyAction DeserializeAction(string json)
        {
            LobbyAction a = Parse<LobbyAction>(json);
            if (a == null || a.Kind == LobbyActionKind.None || !Enum.IsDefined(typeof(LobbyActionKind), a.Kind)) throw new NetMessageFormatException("행동 종류 오류");
            return a;
        }

        public static string SerializeSync(LobbySync s)
        {
            if (s?.Lobby == null) throw new NetMessageFormatException("동기화에 로비 없음");
            LobbyStateJson.Validate(s.Lobby);
            return JsonConvert.SerializeObject(s, Settings);
        }

        public static LobbySync DeserializeSync(string json)
        {
            LobbySync s = Parse<LobbySync>(json);
            if (s == null) throw new NetMessageFormatException("빈 동기화");
            if (s.FormatVersion != LobbySync.CurrentFormatVersion) throw new NetMessageFormatException($"동기화 형식 버전 {s.FormatVersion}은 읽을 수 없음");
            if (s.Lobby == null || s.Seats == null || s.Ready == null || s.Acks == null) throw new NetMessageFormatException("동기화 필드 없음");
            try
            {
                LobbyStateJson.Validate(s.Lobby);
            }
            catch (SnapshotFormatException e)
            {
                throw new NetMessageFormatException("로비 상태 오류", e);
            }
            if (s.Seats.Count != s.Lobby.Players.Count) throw new NetMessageFormatException("자리 수와 플레이어 수가 다름");
            return s;
        }

        private static T Parse<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json)) throw new NetMessageFormatException("빈 JSON");
            try
            {
                return JToken.Parse(json).ToObject<T>(JsonSerializer.Create(Settings));
            }
            catch (Exception e) when (e is JsonException || e is ArgumentException || e is FormatException || e is InvalidCastException)
            {
                throw new NetMessageFormatException("JSON 형식 오류", e);
            }
        }
    }

    /// <summary>방장 진행 시간 수치 (BALANCE 18). 값은 Game(인스펙터)에서.</summary>
    public sealed class HostSessionRules
    {
        /// <summary>캐러셀이 끝난 뒤 상점으로 넘어가기 전 보여 주는 시간 (초).</summary>
        public double CarouselDoneSeconds = 3;
        /// <summary>결과 단계 최대 시간 = 가장 긴 전투(1배속) + 이 값 (초). 그 전에 모두 "다음 라운드"를 누르면 바로.</summary>
        public double ResultGraceSeconds = 20;
        /// <summary>전투 1초당 틱 (가장 긴 전투 시간 계산용).</summary>
        public int TicksPerSecond = 10;
    }

    /// <summary>
    /// 온라인 판 진행 한 곳 — 방장만 실행 (O-2, CLAUDE 핵심 규칙 7: 규칙은 Core, 방장은 실행해 결과만 나눔).
    /// 판 규칙은 기존 <see cref="LobbyController"/> 그대로, 여기는 단계 · 시간 · 사람 행동만:
    /// [캐러셀: 사람 차례마다 시간, 끝나면 봇 규칙으로 대신] → [상점: 준비 시간, 살아 있는 사람 모두 준비 완료면 바로] →
    /// [전투 · 결과: 모두 "다음 라운드" 또는 시간 끝] → 다음 라운드 … → 끝.
    /// 실제 시간(초)은 밖에서 <see cref="Tick"/>으로 넣는다 — 결정론 대상 아님(언제 넘어가는지만 정함).
    /// </summary>
    public sealed class HostSession
    {
        private readonly LobbyController _ctl;
        private readonly HostSessionRules _rules;
        private readonly int _rulesVersion;
        private readonly HashSet<int> _ready = new HashSet<int>();
        private readonly List<ActionAck> _acks = new List<ActionAck>();

        public LobbyState Lobby { get; private set; }
        /// <summary>자리 번호 → 플레이어 ID (봇 = null).</summary>
        public List<string> Seats { get; } = new List<string>();
        public OnlinePhase Phase { get; private set; } = OnlinePhase.Waiting;
        public double PhaseSecondsLeft { get; private set; }
        public LobbyRoundReport LastReport { get; private set; }
        /// <summary>보낼 것이 있는지 (상태가 바뀜). <see cref="MakeSync"/>가 지운다.</summary>
        public bool Dirty { get; private set; }
        public int Version { get; private set; }

        public HostSession(LobbyController ctl, HostSessionRules rules, int rulesVersion)
        {
            _ctl = ctl ?? throw new ArgumentNullException(nameof(ctl));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _rulesVersion = rulesVersion;
        }

        /// <summary>판 시작: humans가 0번부터 차례로 앉고(방장 = 0번), 나머지는 봇.</summary>
        public void Start(string lobbyId, ulong seed, IReadOnlyList<(string playerId, string name)> humans)
        {
            if (humans == null || humans.Count == 0) throw new ArgumentException("사람이 없음");
            if (humans.Select(h => h.playerId).Distinct().Count() != humans.Count) throw new ArgumentException("같은 플레이어 ID가 둘");
            Lobby = _ctl.NewLobby(lobbyId, seed, humans.Select(h => h.name).ToList(), n => "봇 " + n);
            Seats.Clear();
            for (int i = 0; i < Lobby.Players.Count; i++) Seats.Add(i < humans.Count ? humans[i].playerId : null);
            LastReport = null;
            EnterRound();
        }

        public int SeatOf(string playerId) => playerId == null ? -1 : Seats.IndexOf(playerId);

        /// <summary>이 자리가 준비 · 계속 대기에서 빠지는지 (봇 · 탈락자).</summary>
        private bool AutoReady(int seat) => Lobby.Players[seat].IsBot || !Lobby.Players[seat].IsAlive;

        private bool AllReady() => Lobby.Players.All(p => AutoReady(p.Index) || _ready.Contains(p.Index));

        /// <summary>행동 처리. 모르는 사람 · 단계가 아님 · 잘못된 행동은 Ok = false (상태 그대로).</summary>
        public ActionAck Apply(string playerId, LobbyAction a)
        {
            int seat = SeatOf(playerId);
            var ack = new ActionAck { Player = seat, Seq = a?.Seq ?? 0, Kind = a?.Kind ?? LobbyActionKind.None };
            if (seat < 0 || a == null || Lobby == null) return ack;
            LobbyPlayer p = Lobby.Players[seat];
            ShopController shop = _ctl.Shop;
            UnitPool pool = LobbyController.PoolOf(Lobby);
            bool shopping = Phase == OnlinePhase.Shop && p.IsAlive;

            switch (a.Kind)
            {
                case LobbyActionKind.Buy when shopping:
                {
                    string id = a.Index >= 0 && a.Index < p.Run.Shop.Count ? p.Run.Shop[a.Index] : null;
                    ack.SpeciesId = id;
                    ack.LevelBefore = MaxLevelOf(p, id);
                    ack.ShopError = shop.Buy(p.Run, a.Index);
                    ack.LevelAfter = MaxLevelOf(p, id);
                    ack.Ok = ack.ShopError == ShopError.None;
                    break;
                }
                case LobbyActionKind.Reroll when shopping:
                    ack.ShopError = shop.Reroll(p.Run, pool);
                    ack.Ok = ack.ShopError == ShopError.None;
                    break;
                case LobbyActionKind.Sell when shopping:
                {
                    MonsterSnapshot m = ValidLocation(a.From) ? ShopController.Get(p.Run, a.From) : null;
                    if (m == null) break;
                    ack.SpeciesId = m.SpeciesId;
                    ack.Gold = shop.SellValue(m);
                    ack.ShopError = shop.Sell(p.Run, a.From, pool);
                    ack.Ok = ack.ShopError == ShopError.None;
                    break;
                }
                case LobbyActionKind.Move when shopping:
                    if (!ValidLocation(a.From) || !ValidLocation(a.To)) break;
                    ack.ShopError = shop.Move(p.Run, a.From, a.To);
                    ack.Ok = ack.ShopError == ShopError.None;
                    break;
                case LobbyActionKind.Ready when shopping:
                    _ready.Add(seat);
                    ack.Ok = true;
                    break;
                case LobbyActionKind.CarouselPick when Phase == OnlinePhase.Carousel:
                {
                    CarouselState c = Lobby.Carousel;
                    if (c == null || c.IsDone || c.CurrentPicker != seat) break;
                    ack.Carousel = _ctl.PickCarousel(Lobby, seat, a.Index);
                    ack.Ok = ack.Carousel == CarouselPickResult.Picked || ack.Carousel == CarouselPickResult.PickedAsGold;
                    if (!ack.Ok) break;
                    ack.SpeciesId = c.Units[a.Index];
                    if (ack.Carousel == CarouselPickResult.PickedAsGold) ack.Gold = shop.PriceOf(ack.SpeciesId);
                    AfterCarouselPick();
                    break;
                }
                case LobbyActionKind.Continue when Phase == OnlinePhase.Battle:
                    _ready.Add(seat);
                    ack.Ok = true;
                    break;
            }
            _acks.Add(ack);
            Dirty = true;
            if (Phase == OnlinePhase.Shop && AllReady()) Resolve();
            else if (Phase == OnlinePhase.Battle && AllReady()) NextRoundOrFinish();
            return ack;
        }

        /// <summary>시간을 흘린다 (초). 단계가 바뀌면 Dirty.</summary>
        public void Tick(double seconds)
        {
            if (Lobby == null || seconds <= 0) return;
            switch (Phase)
            {
                case OnlinePhase.Carousel:
                    PhaseSecondsLeft -= seconds;
                    if (PhaseSecondsLeft > 0) return;
                    CarouselState c = Lobby.Carousel;
                    if (c != null && !c.IsDone)
                    {
                        // 사람 차례 시간 끝 → 봇 규칙으로 대신
                        int picker = c.CurrentPicker;
                        CarouselPickResult r = _ctl.AutoPickCarousel(Lobby);
                        _acks.Add(new ActionAck { Player = picker, Kind = LobbyActionKind.CarouselPick, Ok = true, Carousel = r, Seq = -1 });
                        AfterCarouselPick();
                        Dirty = true;
                    }
                    else EnterShop();
                    return;
                case OnlinePhase.Shop:
                    PhaseSecondsLeft -= seconds;
                    if (PhaseSecondsLeft <= 0 || AllReady()) Resolve();
                    return;
                case OnlinePhase.Battle:
                    PhaseSecondsLeft -= seconds;
                    if (PhaseSecondsLeft <= 0 || AllReady()) NextRoundOrFinish();
                    return;
            }
        }

        /// <summary>보낼 동기화 (받은 행동 결과를 담고 비움, 순서 번호 +1).</summary>
        public LobbySync MakeSync()
        {
            Version++;
            var s = new LobbySync
            {
                Version = Version,
                RulesVersion = _rulesVersion,
                Phase = Phase,
                PhaseMs = (int)Math.Max(0, Math.Min(int.MaxValue, PhaseSecondsLeft * 1000)),
                Seats = new List<string>(Seats),
                Ready = _ready.OrderBy(x => x).ToList(),
                Lobby = Lobby,
                Report = LastReport == null ? null : RoundReportRecord.From(LastReport),
                Acks = new List<ActionAck>(_acks),
            };
            _acks.Clear();
            Dirty = false;
            return s;
        }

        // ───────── 단계 ─────────

        private void EnterRound()
        {
            _ready.Clear();
            if (Lobby.IsFinished)
            {
                SetPhase(OnlinePhase.Finished, 0);
                return;
            }
            if (_ctl.CarouselPending(Lobby))
            {
                _ctl.StartCarousel(Lobby);
                Phase = OnlinePhase.Carousel;
                AfterCarouselPick();
                Dirty = true;
                return;
            }
            EnterShop();
        }

        /// <summary>캐러셀: 봇 차례는 바로 → 사람 차례면 고르는 시간, 끝났으면 잠깐 보여 준 뒤 상점.</summary>
        private void AfterCarouselPick()
        {
            _ctl.AdvanceCarouselBots(Lobby);
            CarouselState c = Lobby.Carousel;
            PhaseSecondsLeft = c == null || c.IsDone ? _rules.CarouselDoneSeconds : _ctl.Rules.CarouselPickSeconds;
        }

        private void EnterShop()
        {
            _ctl.BeginRound(Lobby); // 안 끝난 캐러셀은 자동으로 마저 고름 · 봇은 사고 배치
            _ready.Clear();
            SetPhase(OnlinePhase.Shop, _ctl.PrepSeconds);
            if (AllReady()) Resolve(); // 사람이 모두 탈락한 경우
        }

        private void Resolve()
        {
            LastReport = _ctl.ResolveRound(Lobby);
            _ready.Clear();
            int longest = LastReport.Matches.Count == 0 ? 0 : LastReport.Matches.Max(m => m.Battle.EndTick);
            SetPhase(OnlinePhase.Battle, (double)longest / Math.Max(1, _rules.TicksPerSecond) + _rules.ResultGraceSeconds);
        }

        private void NextRoundOrFinish()
        {
            if (LastReport != null && LastReport.FinishedAfter) SetPhase(OnlinePhase.Finished, 0);
            else EnterRound();
        }

        private void SetPhase(OnlinePhase phase, double seconds)
        {
            Phase = phase;
            PhaseSecondsLeft = seconds;
            Dirty = true;
        }

        private static bool ValidLocation(Location at) =>
            at.Index >= 0 && at.Index < (at.Area == Area.Board ? BattleTeam.SlotCount : Run.RunState.BenchSlotCount) &&
            (at.Area == Area.Board || at.Area == Area.Bench);

        private static int MaxLevelOf(LobbyPlayer p, string speciesId)
        {
            if (speciesId == null) return 0;
            int max = 0;
            foreach (MonsterSnapshot m in p.Run.Team.Concat(p.Run.Bench))
                if (m != null && m.SpeciesId == speciesId && m.Level > max) max = m.Level;
            return max;
        }
    }
}
