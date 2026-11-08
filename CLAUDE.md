# CLAUDE.md

## 프로젝트 개요
MoaShowdown — 몬스터 팀을 3×2 격자에 배치해 싸우는 **8인 실시간 로비 전략 오토 배틀러** (3D). 판 흐름은 **TFT(전략적 팀 전투)**, 전투 · 몬스터 구조는 Batomon Showdown(berrymint)을 참고 — 이름 · 아트 · 문구는 새로 만든다. ~~비동기 PvP~~ (2026-11-06 사용자 변경).
기획은 GDD.md, 작업 순서와 일지는 TASKS.md, 수치는 BALANCE.md.
- **새 세션을 시작하면** 이 문서 → TASKS.md 맨 위 "진행 요약" 순서로 읽고 다음 항목을 확인한 뒤 시작한다. 다른 PC면 Docs/진행상황.md도. "다음 작업: 미정"이면 후보를 먼저 묻는다.
- 수치는 BALANCE.md와 데이터 에셋 기준이며 코드에 임의로 넣지 않는다.

## 참조 문서
| 문서 | 역할 | 상태 |
|---|---|---|
| `GDD.md` | 기획 | 초안 (2026-11-06) |
| `TASKS.md` | 진행 요약 · 작업 순서 · 일지 | 사용 중 |
| `BALANCE.md` | 수치 · 데이터 에셋 위치(0번) | 사용 중 (2026-11-06) |
| `Docs/참고게임_분석.md` | 참고 게임 구조 분석 (로컬 데이터 · 상점 페이지) | 2026-11-06 |
| `Docs/Source/MY_DEV_GUIDE.md` | 내 협업 가이드 원본 | 고치지 않음 |
| `Docs/진행상황.md` | 작업 환경 옮기기용 요약 (새 PC 설치 · 지금 상태 · 다음 할 일 · 임시값) | 2026-11-06 |

## 협업 방식
- 한국어. 1인 개발. 쉬운 말, 약어는 처음에 풀어 쓴다. 긴 작업 중에는 지금 하는 일을 한두 줄씩 알린다.
- 설계 결정은 선택지 + 추천안으로 묻고 `결정 (날짜, 사용자)`로 남긴다. 사소한 것은 정하고 "정한 것 (Claude, 확인 필요)"로 보고.
- 한 항목 = 작업 브랜치(`phaseN-…`) 커밋 + 보고. 지시 없이 main에 커밋 · 푸시하지 않는다. "main에 병합하고 푸시" = ff 병합 · 푸시 · 브랜치 삭제.
- 보고: 한 일 · 확인한 것 · 확인 못 한 것 · 확인 방법 · 커밋 상태 (+ 정한 것, 다음 추천).
- 테스트 실행은 사용자가 한다 (결정 2026-11-06, 가이드 기본값) — Claude는 작성만, "테스트 작성, 실행 안 함". 몇 항목마다 "테스트 돌릴 때 됐다"고 알린다.

## 작업 원칙
- 한 번에 기능 하나 (TASKS 현재 항목만). 요청받지 않은 시스템을 미리 만들지 않는다 — 단 핵심 설계 규칙의 구조는 처음부터.
- 구현 전 문서를 읽고 구현 후 문서를 고친다. 다르면 문서를 실제 기준으로 고치고 알린다.
- 에디터 수작업은 목록으로 안내, 반복 생성은 멱등 Setup 메뉴로.
- 완료 = 테스트 통과 + 확인 방법 + 사용자 플레이 확인. 구현만 = `— 플레이 확인 대기`.
- 실패 · 생략 · 미확인은 그대로 보고. 겪은 문제는 TASKS "피드백"에.

## 기술 스택
| 항목 | 값 | 상태 |
|---|---|---|
| 엔진 | Unity 6000.3.25f1 | 확정 2026-11-06 · 변경 금지 |
| 렌더 | URP | 확정 2026-11-06 · 변경 금지 |
| 입력 | Input System | 확정 2026-11-06 · 변경 금지 |
| UI | uGUI | 확정 2026-11-06 · 변경 금지 |
| 그래픽 | 3D (그레이박스 → AI 3D 모델, 예: Varco) | 확정 2026-11-06 |
| 온라인 | ~~비동기 (스냅샷 업로드 · 다운로드)~~ → **8인 실시간 로비, 방장(호스트) 방식** — 방장이 타이머 · 대진 · 공유 풀 · 캐러셀 관리, 라운드마다 팀 스냅샷만 주고받고 전투는 각자 결정론적으로 계산 | ~~확정 2026-11-06~~ → 변경 2026-11-06 (사용자 "TFT 플레이 흐름, 온라인") |
| 백엔드 | ~~로컬 가짜 상대 → UGS Cloud Code + Cloud Save~~ → **UGS Lobby + Relay** (+ Netcode, 패키지는 O-1 착수 때 확정). 사람이 모자라면 봇이 채움 (로컬 상대 · 상점 AI) | 변경 2026-11-06 |
| 테스트 | Unity Test Framework (EditMode) | 확정 2026-11-06 |
| JSON | Newtonsoft JSON (`com.unity.nuget.newtonsoft-json` 3.2.1, Core에서 사용) | 확정 2026-11-06 |
| 원격 저장소 | github.com/Shaun1667/MoaShowdown | — |
| 회사 · 제품 이름 | NariGames / MoaShowdown (식별자 `com.NariGames.MoaShowdown`) | 확정 2026-11-06 · **바꾸면 저장 위치(`AppData/LocalLow/NariGames/MoaShowdown`)가 바뀜 — 바꾸기 전에 알린다** |

## 폴더 구조
```
MoaShowdown/                (저장소 루트 = Unity 프로젝트 루트)
├─ Assets/_Project/
│  ├─ Scripts/Core/         Moa.Showdown.Core — 규칙 코드. 엔진 참조 금지(noEngineReferences)
│  ├─ Scripts/Game/         Moa.Showdown.Game — 엔진 어댑터(표현 · 입력 · GameRoot · 상대 공급자 구현)
│  ├─ Editor/               Moa.Showdown.Editor — Setup 메뉴 · 디버그 창
│  ├─ Data/                 데이터 에셋(ScriptableObject) — 수치 · 콘텐츠 표
│  ├─ Art/                  모델 · 텍스처 원본(Art/<도구>/)
│  ├─ Scenes/
│  └─ Tests/EditMode/       Moa.Showdown.Tests
├─ Docs/                    설계 문서 · 진행상황 · Source(원본 보존)
├─ CLAUDE.md  GDD.md  TASKS.md  BALANCE.md
```

## 코딩 규칙
- 네임스페이스 `Moa.Showdown.<어셈블리>`. 클래스 PascalCase, private 필드 `_camelCase`.
- 화면 한글 문구는 텍스트 클래스 하나(`HudText`, 생기면 여기 기록)에 모은다.
- enum은 끝에만 추가한다 (에셋이 정수로 저장).
- 전투 계산은 `BattleSimulator.Run`(Core/Battle) 한 곳. 수치는 `BattleRules` ← 데이터 에셋 `Data/BattleBalance.asset`(`BattleBalance.ToRules`). 결과는 `BattleEvent` 목록뿐, 화면은 이것만 보고 재생. 피해는 틱 끝에 양쪽 동시 적용.
- 난수는 `DeterministicRandom`(Core/Random) 하나. 용도마다 인스턴스를 따로, 저장은 `State`. 규칙 코드에서 `System.Random` · `UnityEngine.Random` 사용 금지.
- 수치 데이터 에셋은 `Editor/Setup/BalanceSetup`이 만든다 (메뉴 MoaShowdown/Setup/…, 이미 있으면 값 보존). 새 수치 = BALANCE 표 + Setup 기본값 + `BalanceTests` 한 줄.
- 팀 스냅샷은 `TeamSnapshot`(Core/Snapshot) 하나, JSON 변환은 `TeamSnapshotJson` 한 곳(읽기 = 버전 확인 → `MigrateStep` 단계 변환 → 검사). JSON 이름은 `[JsonProperty]`로 고정. 필드를 바꾸면 `CurrentFormatVersion` +1, `MigrateStep`에 case 추가, `TeamSnapshotTests`의 옛 고정 JSON은 지우지 않는다. 몬스터는 ID · 레벨 · 버프만, 목록은 순서 고정(딕셔너리 금지).
- 씬 연결은 `GameRoot`(Game) 한 곳. 전투 화면 = `BattlePlayback`(기록을 시간에 맞춰 내보냄) → `BattleStage`(3D 그레이박스: 캡슐 · 공격 게이지 · 투사체) + `BattleHud`(uGUI 코드 생성: HP 바 · 속도 1/2/4x · 다시 보기 · 피해 숫자). 화면은 규칙을 계산하지 않는다. 연출 난수는 전투와 별도 `DeterministicRandom`.
- 화면 한글 문구는 `HudText` 한 곳. 글꼴은 지금 내장 `LegacyRuntime.ttf`(Windows 한글 대체 글꼴로 표시) — 다른 OS · 빌드 전 한글 글꼴 에셋 필요.
- 전투 씬은 메뉴 MoaShowdown/Setup/Battle Scene이 만든다 (`Scenes/Battle.unity`, 없는 것만 생성 · 빈 참조만 채움, 빌드 목록 맨 앞). 시험 팀은 `Data/DebugTeams.asset`(메뉴 Setup/Debug Teams), GameRoot 인스펙터에서 팀 번호 · 라운드 · 시드를 고른다.
- 칸 배치(화면): 왼쪽 = 내 팀(A), 오른쪽 = 상대(B). 줄 0 = 위 줄 · 1 = 아래 줄, 열 0 = 가운데 쪽 (원작 화면, 2026-11-06).
- 플레이 모드 확인(에디터 포커스 없을 때): `EditorApplication.isPaused = true` 후 `EditorApplication.Step()`을 반복 — 한 명령 안에서 바로 진행된다. 진행 확인은 `GameRoot.Playback`, 캡처는 `ScreenCapture.CaptureScreenshot` 후 Step 2번.
- 몬스터 표는 `SpeciesCatalog`(Core/Content, ID로만 조회) ← 데이터 에셋 `Data/SpeciesTable.asset`(`SpeciesTable.ToCatalog`, 메뉴 Setup/Species Table). 새 몬스터 = 표 한 줄. ID는 스냅샷에 저장되므로 바꾸지 않는다. 능력치 ID 문자열은 `StatIds`.
- 스냅샷 → 전투 팀은 `TeamFactory.Build`(Core/Content) 한 곳: 공격력 = 기본 × 레벨 배율^(레벨−1) + 공격 버프, 간격 = 기본 + 간격 버프(최소 1). 모르는 몬스터 · 상한 초과는 `SnapshotContentException`. 레벨 배율 · 규칙 버전은 `BattleBalance`(BALANCE 6).
- 상대는 `IOpponentSource`(Core/Online, 비동기) 하나로 받는다. 고르기 규칙은 `OpponentPicker` 한 곳(같은 라운드 · 같은 rulesVersion · 내 판 아님 · 만난 적 없음 · 싸울 수 있음 → 승수 가장 가까운 무리 → 시드 난수, 없으면 봇). 로컬 구현 `LocalFileOpponentSource`: 읽기 `StreamingAssets/Opponents/*.json` + 쓰기 폴더, 봇 `StreamingAssets/OpponentBots/*.json`, 올리기 = `persistentDataPath/LocalOpponents/my_teams.json`. UGS = 같은 인터페이스의 새 구현.
- 샘플 상대 파일은 메뉴 Setup/Local Opponents가 고정 시드로 만든다 (`sample_opponents.json` · `bots.json`만 덮어씀, 손으로 만든 상대는 다른 파일 이름으로).
- 판 진행은 `RunController`(Core/Run) 한 곳: 라운드 = 내 팀 스냅샷 → 올리기 → 상대 받기 → 전투 → 승(+1승) · 패(라운드별 목숨 감소, 남은 목숨까지만) · 무(변화 없음) · 상대 없음(부전승) → 진행 중이면 라운드 +1. 수치는 `RunRules` ← 에셋 `Data/RunBalance.asset`(메뉴 Setup/Run Balance, 시작 팀 포함).
- 판 상태는 `RunState` 하나(JSON `RunStateJson`, formatVersion 1). 난수는 상태에 두지 않고 `SeedMix.Derive(판 시드, 용도, 라운드)` — 용도 번호(`PurposeBattle` 1 · `PurposeOpponent` 2)는 바꾸지 않는다. 새 용도 = 새 번호.
- GameRoot 모드: Run(기본, 판 진행 · 결과 패널 · 다음 라운드/새 판 · 디버그 레벨업) / BattleTest(1-3 · 1-4 시험 전투). 진입점 `Continue()` · `DebugLevelUp()` · `Replay()` · `State` · `LastReport`.
- **확인 · 시험 때 플레이 모드에서 판을 돌리면 내 팀이 실제로 올라간다** → GameRoot `localWriteFolder`를 다른 이름(예: `LocalOpponents_ClaudeCheck`)으로 바꿔 돌리고, 끝나면 되돌리고 그 폴더만 지운다.
- 상점 · 골드 · 배치는 `ShopController`(Core/Shop) 한 곳: `BeginRound`(라운드당 한 번 수입 + 진열) · `Buy`(대기석 빈칸으로, 가득 차도 합체되면 허용) · `Reroll` · `Sell`(들인 골드 × SellPercent, 반올림) · `Move`(맞바꾸기). 합체 = 같은 몬스터 · 같은 레벨 `MergeCountFor(레벨)`마리(Lv1 3 · Lv2 2, 표 `MergeCountByLevel`) → 보드 → 대기석 순 첫 자리로 Lv+1(버프 합침), 연쇄, 최대 Lv3. 판매가 = 가격 × `CopiesAt(레벨)`(1 · 3 · 6) × SellPercent. 수치는 `ShopRules` ← `RunBalance`(BALANCE 10), 몬스터 가격 = 등급(`Rarity`) → `PriceByRarity`. 자리는 `Location`(Board 0~5 · Bench 0~3).
- 상점 난수 = `SeedMix(판 시드, PurposeShop 3, 라운드)` ⊕ `ShopRollIndex` — 몬스터 표 순서(`SpeciesCatalog.All`)를 바꾸면 같은 시드의 상점이 달라진다.
- 상점 화면은 `ShopView`(Game/Shop, uGUI 코드 생성, 별도 캔버스): 카드 클릭 = 사기, 내 몬스터 클릭 = 선택, 다른 칸 클릭 = 옮기기/맞바꾸기, 판매 · 새로고침 · 전투 시작 버튼. 그릴 내용은 GameRoot가 `ShopViewData`로 만든다 (뷰는 규칙을 모름). 보드 칸 버튼은 3D 칸 위치를 화면 좌표로 따라간다.
- Run 흐름: 새 판 → `EnterShop`(상점) → 전투 시작 → 전투 재생 → 결과 패널 → 다음 라운드 = `EnterShop` … 판 끝 → 새 판.
- 정보 창(마우스를 올리면 뜨고 내리면 사라짐)은 `TooltipView`(Game/Ui) 하나 + UI 요소에 `HoverTarget`(Provider는 띄우는 순간 최신 값). 3D 몬스터(전투 중)는 UI가 아니라 `GameRoot.ProbeBattleHover`가 화면상 거리로 판정(`UnitUnderScreenPoint`). 글은 `InfoText` 한 곳, 숫자는 규칙 코드 값 그대로(`TeamFactory.BuildUnit` — 보이는 값 = 실제 값). 정보 창은 owner별로 숨긴다(UI · 3D 판정이 서로 지우지 않게).
- 확인 요령: UI 올림/내림 = `ExecuteEvents.Execute(go, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler / pointerExitHandler)`, 3D = `InputState.Change(Mouse.current.position, 화면좌표)` 후 Step.
- 몬스터 능력 = 몬스터 표 한 줄의 `ability`(언제 `AbilityTrigger` × 무엇 `AbilityEffect` × 수치, ×레벨 · N번째 선택) — enum은 끝에만 추가. 전투 능력은 `TeamFactory.BuildUnit`이 `BattleAbility`(레벨 반영 수치)로 바꿔 `BattleSimulator`가 처리(전투 시작 · 공격할 때 · N번째 공격), 상점 능력은 `ShopController`(라운드 시작 = 보드만 · 살 때). 새 효과 = enum 끝 + `ApplyAbility`/`ApplyShopAbility`의 case + `HudText.EffectFormats` · `EffectPopupFormats` 한 줄 + 테스트.
- 전투 상태: 팀마다 방어막(모든 피해를 먼저 받음) · 화상(BurnInterval마다 스택 피해 후 −1) · 독(PoisonInterval마다 스택 피해, 유지) · 회복(시작 체력까지). 화면은 `ShieldChanged` · `StatusChanged` · `Heal` · `DamageOverTime` · `Ability` 이벤트만 보고 HP 바 아래 상태 줄 · 떠오르는 글로 그린다.
- 속성은 `ElementType`(몬스터 표 `type`, 끝에만 추가). 속성 시너지는 없음 (결정 2026-11-06) — 표시용.
- 전투 중에 바뀌는 몬스터 능력치(이웃 공격력 · 이웃 속도 · 공격할수록 강해짐, 3-2)는 `BattleSimulator`의 이번 전투 상태(`UnitState`)에만 — `BattleUnit`은 그대로, 다음 전투로 안 넘어감. "붙어 있는 칸"은 `BattleSimulator.Neighbors(칸)` 한 곳(같은 줄 좌우 + 다른 줄 같은 열).
- 상점 · 캐러셀 카드 뽑기는 `ShopController.DrawCard(pool, 라운드, rng)` 한 곳: 등급 확률표(`ShopRules.RarityOddsByRound` ← `RunBalance.rarityOdds`, BALANCE 17)로 등급 먼저 → 그 등급 안에서 풀 남은 수 비례(풀 없으면 표 균등), 빈 등급은 아래 → 위. 표가 비면 예전 방식. 새 등급 줄 = 표 한 줄.
- 몬스터 표 Setup(`SpeciesSetup`)은 기본 표에서 **없는 ID만 끝에 추가** — 새 몬스터 = `Defaults` 한 줄 + BALANCE 7 · 12 + `BalanceTests` Balance7 · 12 줄.
- 규칙 · 능력 · 수치를 바꿔 전투 결과가 달라지면 `BattleBalance.rulesVersion` +1 → `Setup/Local Opponents` 다시 실행 (샘플 상대도 새 버전으로).
- 8인 로비 진행은 `LobbyController`(Core/Lobby) 한 곳: `NewLobby`(사람 + 봇, 플레이어마다 자기 `RunState`와 시드) → `BeginRound`(살아 있는 모두 수입 · 상점 · 라운드 시작 능력, 봇은 `BotBrain`이 바로 행동) → [사람 상점 · 배치] → `ResolveRound`(대진 · 전투 · 목숨 · 탈락 순위). 상태는 `LobbyState` 하나(JSON `LobbyStateJson`). 대진 난수 = `SeedMix.PurposePairing`, 플레이어 시드 = `PurposeLobbyPlayer`, 대전 시드는 `LobbyMatch.Seed`에 남김. 수치 `LobbyRules` ← `RunBalance`(BALANCE 13).
- 봇은 `BotBrain`(난수 없음): 가진 몬스터 → 초당 피해 높은 순으로 사고, 대기석이 차면 가장 약한 것 팔고, 보드 빈칸을 강한 것부터 채우고 더 강하면 맞바꿈. 새로고침 안 함.
- GameRoot 모드 Lobby(기본, `GameRoot.Lobby.cs` partial): 나 = 0번. 내 대진만 재생하고, 기록상 내가 B면 `PlayBattle(…, flip: true)` → `ViewSide`로 좌우를 바꿔 그린다(나는 항상 왼쪽). 오른쪽 위 플레이어 목록은 `LobbyView`(Game/Ui) ← 순서 · 이번 상대는 Core `LobbyStandings.Build`(재생 중엔 `beforeResults`로 결과 숨김). 줄을 누르면 그 사람 지금 보드 + 대기석 구경(`_scout`, 상점 · 결과 화면만, 상점에서 구경 중에도 준비 시간 흐름).
- 공유 몬스터 풀은 `UnitPool`(Core/Shop) — 상태는 `LobbyState.Pool`(몬스터 표 순서 목록)에 있고 `UnitPool`은 그걸 다루기만. 상점 조작은 풀을 인자로 받는다: `BeginRound/Reroll(s, pool)`(안 산 카드 돌려놓고 남은 수 비례로 뽑기) · `Sell(s, at, pool)`(`CopiesAt` 마리 돌려놓기) · `ReturnEverything`(탈락). pool = null이면 예전처럼 무한 상점(Run 모드). GameRoot는 `CurrentPool`을 넘긴다. **불변식: 풀 + 모든 상점 카드 + 모든 보유 몬스터 마리 수 = 처음 총량** (`LobbyTests.Pool_TotalUnitsConserved…`).
- 준비 시간은 `PrepTimer`(Core/Lobby) 한 곳 — 시작 조건(시간 0 또는 살아 있는 사람 전원 준비 완료, 봇 · 탈락자는 처음부터 준비)을 여기서만 판정한다. 실제 시간이라 결정론 대상이 아니고 전투 결과에 영향 없음. GameRoot는 상점이 열려 있을 때만 `Advance`, 시간 끝 강제 시작은 `StartBattleNow(force: true)`(빈 보드 경고 없음). 인스펙터 `usePrepTimer`로 끌 수 있음.
- PvE · 캐러셀 시기는 `LobbyRules.IsPveRound` / `IsCarouselRound`(시작 N라운드 + 시작점부터 간격마다, 0 = 없음), PvE 팀은 `LobbyRules.PveTeams`(나온 순서, 뒤는 마지막) ← `RunBalance.pveTeams`(BALANCE 16). PvE 라운드는 `ResolveRound`가 대진 대신 모두를 같은 팀과 싸우게 함(`LobbyMatch.IsPve`, 보상 `GoldGainedA`, 목숨 `PveLifeLoss` 고정). 캐러셀은 `LobbyController.StartCarousel` → `PickCarousel`/`AutoPickCarousel`/`AdvanceCarouselBots`, 상태는 `LobbyState.Carousel`(formatVersion 3, 2는 변환) — 아직 안 고른 몬스터는 풀에서 빠져 있음(불변식에 포함). `BeginRound`는 안 끝난 캐러셀을 자동으로 마저 고른다. 받기는 `ShopController.Gain`(살 때 능력 없음). 화면은 `CarouselView`(Game/Shop), GameRoot는 `EnterLobbyRound`(캐러셀 → 상점).
- (시스템이 생길 때마다 한 줄씩 추가: "이 시스템은 `클래스` 한 곳, 데이터 `에셋`, 새 항목 = 표 한 줄")

## 핵심 설계 규칙 (변경 금지)
바꾸고 싶어지면 영향 범위를 표로 정리하고 사용자 결정을 받은 뒤에만.
1. **규칙과 표현 분리** — 전투 · 상점 · 판 진행 규칙은 Core(엔진 모름). Game은 Core 이벤트를 받아 그리기만 한다.
2. **결정론적 전투** — 같은 (내 팀 스냅샷, 상대 팀 스냅샷, 시드) → 항상 같은 결과. 전투 규칙에 `float` · `UnityEngine.Random` · 시간 · 딕셔너리 순회 순서 의존 금지 (정수 / 고정 소수점 사용). 다른 PC · 서버에서 다시 계산해도 같아야 한다.
3. **고정 틱** — 전투는 틱 단위로만 진행. 화면 재생 속도는 표현 쪽에서만 바꾼다.
4. **시드 난수 주입 · 용도별 분리** — 상점 ≠ 전투 ≠ 이벤트 ≠ 연출.
5. **상태는 스냅샷 하나** — 판 상태(Run)는 하나의 직렬화 가능한 구조. 새 상태 = 스냅샷 필드 + 버전 증가 + 왕복 테스트.
6. **팀 스냅샷 = 온라인 계약** — 라운드마다 주고받는 팀 형식은 버전 번호를 가진다. 옛 버전 스냅샷도 읽을 수 있어야 한다.
7. **네트워크는 인터페이스 뒤 · 규칙은 Core** — 로비 진행(대진 · 공유 풀 · 캐러셀 · 타이머)은 Core의 순수 코드, 방장은 그것을 실행해 결과만 나눈다. 오프라인(나 + 봇) ↔ 온라인은 전송 구현 교체만. 나중에 전용 서버로 옮겨도 Core 그대로. ~~상대는 `IOpponentSource` 하나로~~ → `IOpponentSource`는 봇 팀 공급용으로 남김.
8. **모든 엔티티 고유 ID** — 데이터는 비주얼을 ID로만 참조, 프리팹이 없으면 그레이박스로 그린다.

## 아트 · 그래픽 규칙
- 3D. 그레이박스(도형) → AI 3D 모델. 원본은 `Art/<도구>/`, 표시용 프리팹은 Setup 메뉴가 만든다. 모델 크기 · 방향 보정 = Setup 표 한 줄.
- 참고 게임의 캐릭터 · 아트 · 이름을 가져오지 않는다.

## Git · 환경
- 커밋 메시지 `MM.DD/Phase-항목/PascalCase요약`, 수정은 `…Fix`. 한 항목 = 한 커밋.
- 커밋 전 `git status` → 내 파일만 `git add` → `git diff --cached --stat`. 다른 세션 변경 · 의도하지 않은 변경(패키지가 자동으로 바꾼 설정 등)은 빼거나 되돌린다.
- `.gitignore`는 루트 기준 경로(`/[Bb]uild/`), 넓은 `**/` 패턴 금지. 줄바꿈은 `.gitattributes`로 고정.
- Windows 셸: CRLF 파일은 sed 치환이 잘 실패 → 편집 도구로. Python 없음 → 스크립트는 PowerShell.
- Unity MCP: 파일 수정 직후 첫 호출은 실패하기 쉽다 → 콘솔 읽기로 깨운 뒤 무거운 호출.
