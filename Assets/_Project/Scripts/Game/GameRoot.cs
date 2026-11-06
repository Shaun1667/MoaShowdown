using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Moa.Showdown.Core.Battle;
using Moa.Showdown.Core.Content;
using Moa.Showdown.Core.Online;
using Moa.Showdown.Core.Run;
using Moa.Showdown.Core.Shop;
using Moa.Showdown.Core.Snapshot;
using Moa.Showdown.Game.Battle;
using Moa.Showdown.Game.Data;
using Moa.Showdown.Game.Shop;
using Moa.Showdown.Game.Ui;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Moa.Showdown.Game
{
    /// <summary>
    /// 씬 연결 한 곳 (컴포지션 루트).
    /// Run 모드(기본): 새 판 → 라운드마다 <see cref="RunController"/>가 계산 → 전투 재생 → 결과 패널 → 다음 라운드 / 새 판.
    /// BattleTest 모드: 1-3 · 1-4의 1:1 시험 전투 (디버그 팀 vs 로컬 상대 또는 디버그 팀).
    /// 다시 보기 = 같은 입력 · 같은 시드로 다시 계산 → 기록이 똑같은지 확인 후 처음부터 재생.
    /// </summary>
    public sealed class GameRoot : MonoBehaviour
    {
        public enum Mode
        {
            Run = 0,
            BattleTest = 1,
        }

        private static readonly int[] Speeds = { 1, 2, 4 };

        [Header("모드")]
        [SerializeField] private Mode mode = Mode.Run;

        [Header("데이터")]
        [SerializeField] private BattleBalance balance;
        [SerializeField] private RunBalance runBalance;
        [SerializeField] private SpeciesTable speciesTable;
        [SerializeField] private DebugTeamSet debugTeams;

        [Header("판 (Run 모드)")]
        [Tooltip("0이면 판마다 새 시드, 아니면 이 시드로 고정 (같은 판 다시 보기용)")]
        [SerializeField] private long runSeed;
        [Tooltip("내 팀을 올리는 폴더 이름 (persistentDataPath 아래). 확인 · 시험 때는 다른 이름으로 바꿔 실제 기록과 섞지 않는다")]
        [SerializeField] private string localWriteFolder = "LocalOpponents";
        [SerializeField] private bool showDebugButtons = true;

        [Header("시험 전투 (BattleTest 모드)")]
        [SerializeField] private int teamAIndex;
        [Tooltip("로컬 상대를 끄거나 상대를 못 찾았을 때 쓰는 디버그 팀")]
        [SerializeField] private int teamBIndex = 1;
        [SerializeField] private int round = 3;
        [SerializeField] private long seed = 12345;
        [Tooltip("켜면 상대를 로컬 파일(StreamingAssets/Opponents · OpponentBots)에서 고른다")]
        [SerializeField] private bool useLocalOpponent = true;
        [Tooltip("상대 고르기에 쓰는 내 승수")]
        [SerializeField] private int wins;

        [Header("씬")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private BattleStage stage;
        [SerializeField] private BattleHud hud;
        [SerializeField] private ShopView shopView;
        [SerializeField] private TooltipView tooltip;
        [Tooltip("전투 중 몬스터 위로 마우스를 올렸다고 보는 거리 (1080p 기준 픽셀)")]
        [SerializeField] private float hoverRadiusPixels = 70f;

        private readonly object _battleHoverOwner = new object();
        private (BattleSide, int)? _battleHoverKey;

        private BattleRules _rules;
        private ShopController _shop;
        private Location? _selected;
        private string _shopMessage = "";
        private bool _emptyBoardWarned;
        private BattlePlayback _playback;
        private string _lastSignature;
        private string _lastInputKey;
        private int _speed = 1;
        private IOpponentSource _opponents;
        private SpeciesCatalog _catalog;
        private UnitStatRules _statRules;
        private RunController _run;
        private bool _roundResultPending;

        public BattlePlayback Playback => _playback;
        public int Speed => _speed;
        public IOpponentSource Opponents => _opponents;
        /// <summary>마지막 전투의 상대 정보 (BattleTest 모드, 디버그 · 확인용).</summary>
        public OpponentResult LastOpponent { get; private set; }
        /// <summary>지금 판 상태 (Run 모드).</summary>
        public RunState State { get; private set; }
        /// <summary>마지막 라운드 결과 (Run 모드).</summary>
        public RoundReport LastReport { get; private set; }
        public RunRules RunRules => _run?.Rules;
        public ShopController Shop => _shop;
        public Location? Selected => _selected;

        private void Start()
        {
            _rules = balance.ToRules();
            _catalog = speciesTable.ToCatalog();
            _statRules = balance.ToStatRules();
            _opponents = new LocalFileOpponentSource(
                new[] { Path.Combine(Application.streamingAssetsPath, "Opponents") },
                Path.Combine(Application.streamingAssetsPath, "OpponentBots"),
                Path.Combine(Application.persistentDataPath, localWriteFolder),
                s => TeamFactory.IsPlayable(s, _catalog, _statRules));

            tooltip.Build();
            hud.Build(mainCamera, Speeds);
            hud.AddSideInfo(tooltip, MyInfo, OpponentInfo);
            hud.SpeedClicked += SetSpeed;
            hud.ReplayClicked += Replay;
            hud.ContinueClicked += Continue;
            hud.DebugLevelUpClicked += DebugLevelUp;
            SetSpeed(_speed);

            if (mode == Mode.Run)
            {
                _run = new RunController(runBalance.ToRules(), _rules, _statRules, _catalog, _opponents, balance.rulesVersion);
                _shop = new ShopController(runBalance.ToShopRules(), _catalog);
                shopView.Build(mainCamera, slot => stage.SlotPosition(BattleSide.A, slot), _shop.Rules.ShopSlots);
                shopView.ShopCardClicked += OnShopCard;
                shopView.RerollClicked += OnReroll;
                shopView.SellClicked += OnSell;
                shopView.LocationClicked += OnLocation;
                shopView.StartBattleClicked += OnStartBattle;
                shopView.SetTooltips(tooltip, CardInfo, LocationInfo);
                hud.SetDebugVisible(showDebugButtons);
                StartNewRun();
                EnterShop();
            }
            else
            {
                Replay();
            }
        }

        private void Update()
        {
            if (_playback == null) return;
            float scaled = Time.deltaTime * _speed;
            _playback.Advance(Time.deltaTime, _speed, OnBattleEvent);
            stage.Tick(_playback.CurrentTick, scaled, _playback.IsFinished);
            hud.Tick(scaled);
            if (_roundResultPending && _playback.IsFinished)
            {
                _roundResultPending = false;
                ShowRoundResult();
            }
            ProbeBattleHover();
        }

        // ───────── 정보 창 (2-3) ─────────

        /// <summary>전투 화면의 3D 몬스터는 UI가 아니라서, 마우스와 화면상 거리로 올라갔는지 본다 (상점에서는 보드 칸 버튼이 맡음).</summary>
        private void ProbeBattleHover()
        {
            if (shopView != null && shopView.IsVisible) return;
            if (Mouse.current == null) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                tooltip.Hide(_battleHoverOwner); // UI 위에서는 UI 정보 창이 우선
                return;
            }
            Vector2 mouse = Mouse.current.position.ReadValue();
            (BattleSide side, int slot)? hit = UnitUnderScreenPoint(mouse);
            if (hit == null)
            {
                tooltip.Hide(_battleHoverOwner);
                return;
            }
            var info = BattleUnitInfo(hit.Value.side, hit.Value.slot);
            if (info == null) tooltip.Hide(_battleHoverOwner);
            else if (!ReferenceEquals(tooltip.Owner, _battleHoverOwner) || _battleHoverKey != hit.Value)
                tooltip.Show(_battleHoverOwner, info.Value.Item1, info.Value.Item2);
            _battleHoverKey = hit.Value;
        }

        /// <summary>화면 좌표에 가장 가까운 몬스터 (반경 안). 확인 · 테스트용으로도 쓴다.</summary>
        public (BattleSide side, int slot)? UnitUnderScreenPoint(Vector2 screen)
        {
            float radius = hoverRadiusPixels * (Screen.height / 1080f);
            float best = radius * radius;
            (BattleSide, int)? found = null;
            foreach (BattleSide side in new[] { BattleSide.A, BattleSide.B })
            for (int slot = 0; slot < BattleTeam.SlotCount; slot++)
            {
                if (!stage.HasUnit(side, slot)) continue;
                Vector3 p = mainCamera.WorldToScreenPoint(stage.SlotPosition(side, slot));
                float d = ((Vector2)p - screen).sqrMagnitude;
                if (p.z > 0f && d < best)
                {
                    best = d;
                    found = (side, slot);
                }
            }
            return found;
        }

        /// <summary>전투 중 몬스터 정보 (Run 모드: 이번 라운드 스냅샷에서).</summary>
        public (string, string)? BattleUnitInfo(BattleSide side, int slot)
        {
            if (mode != Mode.Run || LastReport == null) return null;
            TeamSnapshot snap = side == BattleSide.A ? LastReport.MySnapshot : LastReport.Opponent?.Snapshot;
            MonsterSnapshot m = snap?.Board[slot];
            return m == null ? ((string, string)?)null : MonsterInfoText(m, enemy: side == BattleSide.B, price: null, mine: false);
        }

        /// <summary>상점 카드 정보: 레벨 1 기준 능력치 · 가격 · 이미 가진 수.</summary>
        public (string, string)? CardInfo(int index)
        {
            if (State == null || index < 0 || index >= State.Shop.Count || State.Shop[index] == null) return null;
            var m = new MonsterSnapshot { SpeciesId = State.Shop[index], Level = 1 };
            return MonsterInfoText(m, enemy: false, price: _shop.PriceOf(m.SpeciesId), mine: false, mergePreview: true);
        }

        /// <summary>내 보드 · 대기석 몬스터 정보: 능력치 · 판매가 · 합체 진행.</summary>
        public (string, string)? LocationInfo(Location at)
        {
            if (State == null) return null;
            MonsterSnapshot m = ShopController.Get(State, at);
            return m == null ? ((string, string)?)null : MonsterInfoText(m, enemy: false, price: null, mine: true);
        }

        private (string, string)? MonsterInfoText(MonsterSnapshot m, bool enemy, int? price, bool mine, bool mergePreview = false)
        {
            if (!_catalog.TryGet(m.SpeciesId, out SpeciesDef def)) return (m.SpeciesId, "");
            var info = new InfoText.MonsterInfo
            {
                Species = def,
                Level = m.Level,
                Unit = TeamFactory.BuildUnit(m, _catalog, _statRules),
                AttackBuff = m.Buffs.Where(b => b.Stat == StatIds.Attack).Sum(b => b.Value),
                LevelMultiplier = _statRules.LevelAttackMultiplier,
                TicksPerSecond = _rules.TicksPerSecond,
                Price = price,
                SellValue = mine && _shop != null ? _shop.SellValue(m) : (int?)null,
                MergeCount = _shop?.Rules.MergeCount ?? 0,
                MaxLevel = _shop?.Rules.MaxLevel ?? int.MaxValue,
                Enemy = enemy,
            };
            if (_shop != null && State != null && (mine || mergePreview))
            {
                int owned = State.Team.Concat(State.Bench).Count(x => x != null && x.SpeciesId == m.SpeciesId && x.Level == m.Level);
                info.OwnedSameLevel = owned; // 카드: 이미 가진 수 (사면 +1) · 내 몬스터: 자신 포함
            }
            return (InfoText.MonsterTitle(info), InfoText.MonsterBody(info));
        }

        public (string, string)? MyInfo()
        {
            if (mode != Mode.Run || State == null) return null;
            RunRules r = _run.Rules;
            return (HudText.MyTitle, InfoText.MyBody(State.TrainerId, State.Wins, r.WinsToChampion, State.Lives, r.StartLives, State.Gold, State.Round));
        }

        public (string, string)? OpponentInfo()
        {
            OpponentResult o = mode == Mode.Run ? LastReport?.Opponent : LastOpponent;
            if (o?.Snapshot == null || (mode == Mode.Run && shopView.IsVisible)) return null;
            int count = o.Snapshot.Board.Count(m => m != null);
            return (string.Format(HudText.OpponentTitleFormat, o.Snapshot.RunId), InfoText.OpponentBody(o.Snapshot, o.IsBot, o.Source, count));
        }

        public void SetSpeed(int speed)
        {
            _speed = speed;
            hud.SetActiveSpeed(speed);
        }

        // ───────── Run 모드 ─────────

        public void StartNewRun()
        {
            ulong s = runSeed != 0 ? (ulong)runSeed : (ulong)DateTime.UtcNow.Ticks;
            string runId = $"run_{DateTime.UtcNow:yyyyMMddHHmmss}_{s & 0xFFFF:x4}";
            State = _run.NewRun(runId, s, runBalance.startTrainerId, runBalance.ToStartTeam());
            LastReport = null;
            _lastSignature = null;
            ShowRunInfo(State.Wins, State.Lives);
        }

        /// <summary>결과 패널의 계속 버튼: 판이 진행 중이면 다음 라운드 상점, 끝났으면 새 판을 시작하고 1라운드 상점.</summary>
        public void Continue()
        {
            if (mode != Mode.Run) return;
            if (State.GetOutcome(_run.Rules) != RunOutcome.InProgress) StartNewRun();
            EnterShop();
        }

        // ───────── 상점 (2-2) ─────────

        /// <summary>라운드 시작: 수입 · 진열(라운드당 한 번) → 상점 화면. 보드에는 내 팀만 보인다.</summary>
        public void EnterShop()
        {
            _shop.BeginRound(State);
            _selected = null;
            _shopMessage = "";
            _emptyBoardWarned = false;
            _playback = null;
            _roundResultPending = false;
            tooltip.HideAll();
            hud.SetSideLabels(HudText.MySideLabel, "");
            hud.HideRoundResult();
            hud.ShowStatus("");
            hud.ShowShopHeader(State.Round, HudText.ShopHeader, _rules.GetTeamHp(State.Round));
            ShowRunInfo(State.Wins, State.Lives);
            RefreshShop();
        }

        private void OnShopCard(int index)
        {
            string id = index < State.Shop.Count ? State.Shop[index] : null;
            int levelBefore = MaxLevelOf(id);
            ShopError e = _shop.Buy(State, index);
            if (e == ShopError.None)
            {
                int levelAfter = MaxLevelOf(id);
                _shopMessage = levelAfter > levelBefore && levelAfter > 1
                    ? string.Format(HudText.MergedFormat, NameOf(id), levelAfter)
                    : string.Format(HudText.BoughtFormat, NameOf(id));
                _selected = null;
            }
            else _shopMessage = ErrorText(e);
            RefreshShop();
        }

        private void OnReroll()
        {
            ShopError e = _shop.Reroll(State);
            _shopMessage = e == ShopError.None ? "" : ErrorText(e);
            RefreshShop();
        }

        private void OnSell()
        {
            if (!_selected.HasValue) return;
            MonsterSnapshot m = ShopController.Get(State, _selected.Value);
            if (m == null) return;
            int value = _shop.SellValue(m);
            string name = NameOf(m.SpeciesId);
            if (_shop.Sell(State, _selected.Value) == ShopError.None) _shopMessage = string.Format(HudText.SoldFormat, name, value);
            _selected = null;
            RefreshShop();
        }

        /// <summary>클릭 → 클릭: 고른 게 없으면 내 몬스터를 고르고, 고른 게 있으면 그 칸으로 옮기기/맞바꾸기 (같은 칸 = 선택 취소).</summary>
        private void OnLocation(Location at)
        {
            if (!_selected.HasValue)
            {
                if (ShopController.Get(State, at) != null) _selected = at;
            }
            else
            {
                if (!_selected.Value.Equals(at)) _shop.Move(State, _selected.Value, at);
                _selected = null;
            }
            _shopMessage = "";
            RefreshShop();
        }

        private async void OnStartBattle()
        {
            if (State.Team.All(m => m == null) && !_emptyBoardWarned)
            {
                _emptyBoardWarned = true;
                _shopMessage = HudText.EmptyBoardWarning;
                RefreshShop();
                return;
            }
            shopView.Hide();
            try
            {
                await PlayNextRoundAsync();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void RefreshShop()
        {
            var d = new ShopViewData
            {
                Round = State.Round,
                Gold = State.Gold,
                RerollCost = _shop.Rules.RerollCost,
                Shop = State.Shop.Select(id => id == null
                    ? new ShopCard { Empty = true }
                    : new ShopCard
                    {
                        Name = NameOf(id),
                        Rarity = RarityName(id),
                        Price = _shop.PriceOf(id),
                        Affordable = State.Gold >= _shop.PriceOf(id),
                    }).ToArray(),
                Selected = _selected,
                Message = _shopMessage,
            };
            for (int i = 0; i < d.Board.Length; i++) d.Board[i] = UnitText(State.Team[i]);
            for (int i = 0; i < d.Bench.Length; i++) d.Bench[i] = UnitText(State.Bench[i]);
            if (_selected.HasValue)
            {
                MonsterSnapshot m = ShopController.Get(State, _selected.Value);
                if (m != null) d.SellValue = _shop.SellValue(m);
            }
            shopView.Show(d);

            // 보드 미리 보기: 내 팀만 (상대 칸은 비움)
            BattleTeam mine = TeamFactory.Build(_run.MakeSnapshot(State), _catalog, _statRules);
            stage.Build(mine, new BattleTeam(), unchecked((ulong)State.Seed));
            stage.Tick(0f, 0f, true);
        }

        private string UnitText(MonsterSnapshot m) => m == null ? null : string.Format(HudText.UnitLabelFormat, NameOf(m.SpeciesId), m.Level);

        private string NameOf(string speciesId) =>
            speciesId != null && _catalog.TryGet(speciesId, out SpeciesDef d) ? d.DisplayName : speciesId;

        private string RarityName(string speciesId)
        {
            if (!_catalog.TryGet(speciesId, out SpeciesDef d)) return "";
            int i = (int)d.Rarity;
            return i >= 0 && i < HudText.RarityNames.Length ? HudText.RarityNames[i] : d.Rarity.ToString();
        }

        private int MaxLevelOf(string speciesId)
        {
            if (speciesId == null) return 0;
            int max = 0;
            foreach (MonsterSnapshot m in State.Team.Concat(State.Bench))
                if (m != null && m.SpeciesId == speciesId && m.Level > max) max = m.Level;
            return max;
        }

        private static string ErrorText(ShopError e) => e switch
        {
            ShopError.NotEnoughGold => HudText.NotEnoughGold,
            ShopError.BenchFull => HudText.BenchFull,
            _ => "",
        };

        private async Task PlayNextRoundAsync()
        {
            shopView.Hide();
            tooltip.HideAll();
            hud.HideRoundResult();
            int winsBefore = State.Wins;
            int livesBefore = State.Lives;
            RoundReport report = await _run.PlayRoundAsync(State);
            LastReport = report;
            ShowRunInfo(winsBefore, livesBefore);

            if (report.Battle == null)
            {
                _playback = null;
                hud.ShowStatus("");
                ShowRoundResult();
                return;
            }

            string opponentName = string.Format(report.Opponent.IsBot ? HudText.OpponentBotFormat : HudText.OpponentFormat,
                report.Opponent.Snapshot.RunId, report.Opponent.Snapshot.Wins);
            _lastSignature = Signature(report.Battle);
            hud.ShowStatus("");
            PlayBattle(report.MyTeam, report.OpponentTeam, report.Battle, report.Round, HudText.MyTeam, opponentName, unchecked((ulong)State.Seed) ^ (ulong)report.Round);
            _roundResultPending = true;
        }

        private void ShowRoundResult()
        {
            RoundReport r = LastReport;
            RunRules rules = _run.Rules;
            ShowRunInfo(State.Wins, State.Lives);
            string detail = r.Record.Result switch
            {
                "win" => string.Format(HudText.RoundWinFormat, State.Wins, rules.WinsToChampion),
                "lose" => string.Format(HudText.RoundLoseFormat, r.Record.LivesLost, State.Lives),
                "draw" => HudText.RoundDraw,
                _ => HudText.RoundBye,
            };
            switch (r.OutcomeAfter)
            {
                case RunOutcome.Champion:
                    hud.ShowBanner(HudText.Champion);
                    detail = string.Format(HudText.ChampionFormat, r.Round, State.Wins);
                    hud.ShowRoundResult(detail, HudText.NewRun);
                    break;
                case RunOutcome.Eliminated:
                    hud.ShowBanner(HudText.Eliminated);
                    detail = string.Format(HudText.EliminatedFormat, r.Round, State.Wins);
                    hud.ShowRoundResult(detail, HudText.NewRun);
                    break;
                default:
                    hud.ShowRoundResult(detail, HudText.NextRound);
                    break;
            }
        }

        private void ShowRunInfo(int winsNow, int livesNow)
        {
            RunRules rules = _run.Rules;
            hud.ShowRunInfo(string.Format(HudText.RunInfoFormat, winsNow, rules.WinsToChampion, livesNow, rules.StartLives));
        }

        /// <summary>디버그: 팀 전체 레벨 +1 (라운드 사이에만, 다음 라운드부터 적용).</summary>
        public void DebugLevelUp()
        {
            if (mode != Mode.Run || State == null) return;
            RunController.DebugLevelUpAll(State);
            int level = State.Team.Where(m => m != null).Select(m => m.Level).DefaultIfEmpty(0).Max();
            hud.ShowStatus(string.Format(HudText.DebugLevelUpDoneFormat, level));
            if (shopView.IsVisible) RefreshShop();
        }

        // ───────── 다시 보기 · 시험 전투 ─────────

        /// <summary>
        /// Run 모드: 마지막 라운드 전투를 같은 팀 · 같은 시드로 다시 계산해 기록이 같은지 확인하고 다시 재생 (판 상태는 그대로).
        /// BattleTest 모드: 시험 전투를 다시 계산 · 재생.
        /// </summary>
        public async void Replay()
        {
            try
            {
                if (mode == Mode.Run) ReplayLastRound();
                else await StartTestBattleAsync();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void ReplayLastRound()
        {
            RoundReport r = LastReport;
            if (r?.Battle == null) return;
            ulong rs = unchecked((ulong)State.Seed);
            BattleResult again = BattleSimulator.Run(_rules, r.MyTeam, r.OpponentTeam, r.Round,
                Core.Random.SeedMix.Derive(rs, Core.Random.SeedMix.PurposeBattle, r.Round));
            hud.ShowStatus(Signature(again) == Signature(r.Battle) ? HudText.SameResult : HudText.DifferentResult);
            hud.HideRoundResult();
            string opponentName = string.Format(r.Opponent.IsBot ? HudText.OpponentBotFormat : HudText.OpponentFormat, r.Opponent.Snapshot.RunId, r.Opponent.Snapshot.Wins);
            PlayBattle(r.MyTeam, r.OpponentTeam, again, r.Round, HudText.MyTeam, opponentName, rs ^ (ulong)r.Round);
            _roundResultPending = true;
        }

        private async Task StartTestBattleAsync()
        {
            DebugTeam a = debugTeams.teams[teamAIndex];
            BattleTeam teamA = a.ToBattleTeam();

            BattleTeam teamB;
            string nameB;
            string note = "";
            LastOpponent = null;
            if (useLocalOpponent)
            {
                var query = new OpponentQuery
                {
                    Round = round,
                    Wins = wins,
                    RulesVersion = balance.rulesVersion,
                    OwnRunId = "debug",
                    Seed = (ulong)seed ^ (ulong)round,
                };
                OpponentResult found = await _opponents.FindOpponentAsync(query);
                LastOpponent = found;
                if (found.Snapshot != null)
                {
                    teamB = TeamFactory.Build(found.Snapshot, _catalog, _statRules);
                    nameB = string.Format(found.IsBot ? HudText.OpponentBotFormat : HudText.OpponentFormat, found.Snapshot.RunId, found.Snapshot.Wins);
                }
                else
                {
                    DebugTeam b = debugTeams.teams[teamBIndex];
                    teamB = b.ToBattleTeam();
                    nameB = b.name;
                    note = HudText.OpponentNotFound;
                }
            }
            else
            {
                DebugTeam b = debugTeams.teams[teamBIndex];
                teamB = b.ToBattleTeam();
                nameB = b.name;
            }

            BattleResult result = BattleSimulator.Run(_rules, teamA, teamB, round, (ulong)seed);
            // 결정론 확인: 입력(팀 내용 · 라운드 · 시드 · 체력)이 지난번과 같을 때만 기록을 비교한다
            string inputKey = $"{InputKey(teamA)}/{InputKey(teamB)}/{round}/{seed}/{result.StartHp}";
            string signature = Signature(result);
            string check = _lastSignature != null && inputKey == _lastInputKey
                ? (signature == _lastSignature ? HudText.SameResult : HudText.DifferentResult)
                : "";
            hud.ShowStatus(string.Join("   ", new[] { note, check }.Where(x => x.Length > 0)));
            _lastSignature = signature;
            _lastInputKey = inputKey;

            PlayBattle(teamA, teamB, result, round, a.name, nameB, (ulong)seed ^ 0x5EEDF00DUL);
        }

        // ───────── 공통 ─────────

        private void PlayBattle(BattleTeam a, BattleTeam b, BattleResult result, int battleRound, string nameA, string nameB, ulong fxSeed)
        {
            _playback = new BattlePlayback(result, _rules.TicksPerSecond);
            stage.Build(a, b, fxSeed);
            hud.ShowBattle(battleRound, nameA, nameB, result.StartHp);
            hud.SetSideLabels(HudText.MySideLabel, string.Format(HudText.OpponentTitleFormat, nameB));
            AddLabels(BattleSide.A, a);
            AddLabels(BattleSide.B, b);
        }

        private static string Signature(BattleResult result) => string.Join("|", result.Events.Select(e => e.ToString()));

        private static string InputKey(BattleTeam team)
        {
            var parts = new string[BattleTeam.SlotCount];
            for (int slot = 0; slot < BattleTeam.SlotCount; slot++)
                parts[slot] = team[slot].IsEmpty ? "-" : $"{team[slot].Attack}:{team[slot].AttackIntervalTicks}";
            return string.Join(",", parts);
        }

        private void AddLabels(BattleSide side, BattleTeam team)
        {
            for (int slot = 0; slot < BattleTeam.SlotCount; slot++)
            {
                BattleUnit u = team[slot];
                if (u.IsEmpty) continue;
                hud.AddUnitLabel(stage.LabelAnchor(side, slot), u.Attack, u.AttackIntervalTicks, _rules.TicksPerSecond);
            }
        }

        private void OnBattleEvent(BattleEvent e)
        {
            switch (e.Type)
            {
                case BattleEventType.Attack:
                    Vector3 hit = stage.PlayAttack(e.Side, e.Slot);
                    hud.ShowDamage(hit, e.Amount);
                    break;
                case BattleEventType.SuddenDeath:
                    hud.ShowBanner(HudText.SuddenDeath);
                    break;
                case BattleEventType.TeamHpChanged:
                    hud.SetHp(e.Side, e.Value, _playback.Result.StartHp);
                    break;
                case BattleEventType.End:
                    var outcome = (BattleOutcome)e.Value;
                    hud.ShowBanner(outcome == BattleOutcome.AWins ? HudText.Win
                        : outcome == BattleOutcome.BWins ? HudText.Lose
                        : HudText.Draw);
                    break;
            }
        }
    }
}
