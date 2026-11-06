# TASKS.md

## 진행 요약 (2026-11-06 기준 — 새 세션은 여기부터 읽는다)
**최신 (2026-11-06): 2-3 마우스 올림 정보 창 main 병합 · 푸시 완료 (Phase 2 끝). 테스트 113개 전부 통과 (2026-11-06 사용자 실행). 플레이 확인 대기: 1-3 · 1-4 · 2-1 · 2-2 · 2-3. 다음: 미정 — 후보(Phase 3 콘텐츠 / Phase O 온라인 / Phase 4 재미 검증)를 먼저 묻는다.**

---

## Phase 0. 세팅
- [x] **0-1. 프로젝트 세팅** (2026-11-06 사용자 "Batomon Showdown 같은 게임을 온라인 대전으로 만들고 싶어")
  - 결정 (2026-11-06, 사용자): 비동기 대전 · 로컬 가짜 상대 → UGS · 3D 모델 · 한 판 구조만 참고 · 10승/5패 · 3×2 격자 · URP + Input System + uGUI · 프로젝트를 저장소 루트로
  - 조사: 참고 게임 설치 폴더 = Godot 4.7 + GodotSteam, `.pck`는 목록까지 암호화 → 열지 않음(보호 장치 우회 안 함). 게임 방식은 Steam 상점 페이지(공개 정보)로 확인.
  - 구현: `My project/` → 저장소 루트로 이동(git mv) · `.gitignore` 루트 기준 경로로 · `.gitattributes` 추가 · 어셈블리 `Moa.Showdown.Core`(엔진 참조 금지) / `Game` / `Editor` / `Tests` · 원본 가이드 `Docs/Source/MY_DEV_GUIDE.md`
  - 정한 것 (Claude, 확인 필요): 네임스페이스 `Moa.Showdown.*`, 폴더 `Assets/_Project/…`, 데이터 에셋 자리 `Assets/_Project/Data/`
  - 테스트 통과 (2026-11-06 사용자 실행): `CoreAssemblyTests.Core_DoesNotReferenceUnityEngine`
  - 확인: Unity 배치 모드(화면 없이 실행) 컴파일 오류 0, `Core` · `Tests` 어셈블리 생성. `Game` · `Editor`는 스크립트가 없어 아직 컴파일 안 됨(정상).
  - ~~확인 못 함: 에디터 MCP 연결~~ → 2026-11-06 사용자가 Hub 등록 후 MCP 연결 · 콘솔 오류 0 확인
  - 참고: `ProjectSettings` 2개 파일에 Unity AI 패키지가 자동으로 넣은 변경(App UI 설정)이 있어 커밋에서 뺐음
  - 확인 방법: Unity Hub → Add → `MoaShowdown` 폴더 → 열기 → 콘솔 오류 0 → Test Runner(EditMode)에 테스트 1개 보임
- [x] **0-2. 제품 · 회사 이름** (2026-11-06 사용자 "제품은 MoaShowdown 회사 명은 NariGames") — 완료
  - 결정 (2026-11-06, 사용자): 제품 MoaShowdown · 회사 NariGames
  - 구현: PlayerSettings 회사 · 제품 이름 + 앱 식별자(Standalone · Android · iOS) `com.NariGames.MoaShowdown` (템플릿 값이었음)
  - 정한 것 (Claude, 확인 필요): 앱 식별자도 같이 바꿈
  - 확인: 에디터에서 `persistentDataPath` = `AppData/LocalLow/NariGames/MoaShowdown`. 예전 위치(`DefaultCompany/My project`)에는 이 프로젝트 데이터가 없어 옮길 것 없음 (확인용 폴더는 2-1에서 이미 지움)
  - 커밋: ProjectSettings.asset에서 이름 · 식별자 부분만 골라 스테이징 (Unity AI 패키지의 `APP_UI_EDITOR_ONLY` 정의는 계속 제외)

## Phase 1. 구조 기반 + 핵심 조작
- [x] **1-1. 전투 시뮬레이션 코어** (2026-11-06 사용자 "체력 300 → 500 → 800 … 1400, 누굴 때리든 상성 없이 공통 체력이 줄고 다 닳면 진다") — 테스트 통과, 화면 확인은 1-3
  - 조사: 참고 게임 로컬 데이터 폴더(암호화 안 됨)에서 판 저장 · 상대 캐시 · 로그 구조 확인 → `Docs/참고게임_분석.md`. 체력 표 · 몬스터 능력치는 `.pck` 안이라 확인 안 함.
  - 결정 (2026-11-06, 사용자): 팀 공통 체력(누구를 때리든 상대 공통 체력 감소) · 체력 표는 임시값 · 공격은 각자 공격 간격대로 · 제한 틱부터 서든데스(점점 세지는 도트 피해) · ~~5패~~ → 목숨 10개, 후반 패배는 최대 3개
  - 구현: `BattleSimulator`(전투 계산) · `BattleRules`(수치) · `BattleTeam`/`BattleUnit`(3×2 보드, 칸 = 줄×3+열) · `BattleEvent`(Attack · SuddenDeath · TeamHpChanged · End) · `DeterministicRandom`(SplitMix64) · 에셋 `Data/BattleBalance.asset` · 메뉴 `MoaShowdown/Setup/Battle Balance`
  - 정한 것 (Claude, 확인 필요): 피해는 틱 끝에 양쪽 동시 적용(먼저 친 쪽 이득 없음) · 같은 틱에 둘 다 0 = 무승부(목숨 · 승수 변화 없음) · 첫 공격은 공격 간격만큼 지난 뒤 · 서든데스 60초부터 1초마다 20, 40, 60 … · 안전 상한 5분 · 패배 비용 구간 1~4R 1 / 5~9R 2 / 10R~ 3
  - 수치: BALANCE 1~5번
  - 확인: 에디터 컴파일 오류 0 · Setup 메뉴 2번 실행해도 에셋 하나(값 보존) · 실제 에셋 값으로 샘플 전투(1 · 3 · 5라운드 A 승, 75 · 192 · 330틱) · 빈 팀 20라운드는 서든데스로 860틱(86초) 무승부
  - 테스트 통과 (2026-11-06 사용자 실행): `BattleSimulatorTests`(9) · `DeterministicRandomTests`(4) · `BalanceTests`(5)
  - 확인 방법: Window → General → Test Runner → EditMode → Run All → 19개 통과. (화면 확인은 1-3 그레이박스 재생에서)
  - 고침 (2026-11-06, 사용자 "체력 확인했어 300, 500, 800, 1400, 2400 … 63000"): 임시 체력 표(20라운드, 7400까지) → 원작 값 14라운드(63000까지), 15라운드부터는 마지막 값. 에셋 `BattleBalance.asset` · Setup 기본값 · `BalanceTests.Balance2` · BALANCE 2 · GDD 함께 고침. 확인: 컴파일 오류 0 · 에셋 값 반영 · 서든데스 검산(1R 64초 · 14R 138초, 상한 300초 안). 테스트 통과 (2026-11-06 사용자 실행): `BalanceTests.Balance2_TeamHpByRound`
- [x] **1-2. 팀 스냅샷** (2026-11-06 사용자 "1-2 진행해줘") — 완료 (화면 없음, 테스트로 확인)
  - 결정 (2026-11-06, 사용자): JSON · 몬스터는 ID + 레벨 + 버프만
  - 구현: `TeamSnapshot`/`MonsterSnapshot`/`StatBuff`(Core/Snapshot, formatVersion 1) · `TeamSnapshotJson`(직렬화 · 버전 확인 · 단계 변환 자리 · 구조 검사, 잘못되면 `SnapshotFormatException`) · `Packages/manifest.json`에 Newtonsoft JSON 3.2.1 명시(이미 다른 패키지 의존으로 설치돼 있었음)
  - 정한 것 (Claude, 확인 필요): 스냅샷 필드 = formatVersion · rulesVersion · runId · round · wins · livesLost · trainerId · trinketIds · board(6칸, 빈칸 null). 소유자 ID · 점수 · 결과는 넣지 않음(서버가 감쌈) · 능력치 ID는 문자열(`"attack"` 등) · 모르는 필드는 무시, 새 버전(formatVersion > 현재)은 거부
  - 확인: 컴파일 오류 · 경고 0 (도달 불가 코드 경고 1개 고침) · 에디터에서 직렬화 → 읽기 → 직렬화 결과 동일 · formatVersion 99 거부
  - 확인 못 함: 몬스터 표가 없어 스냅샷 → `BattleTeam` 변환은 아직 (몬스터 표 생길 때 = 1-4 또는 Phase 3)
  - 테스트 통과 (2026-11-06 사용자 실행): `TeamSnapshotTests`(고정 JSON v1 양방향 · 왕복 · 모르는 필드 · 잘못된 JSON 12종 · 빈 보드 · 저장 전 검사 → 테스트 케이스 18개)
  - 확인 방법: Test Runner → EditMode → Run All → 전부 통과 (1-1 포함 37개)
- [x] **1-3. 그레이박스 전투 재생** (2026-11-06 사용자 "1-3 진행해줘" + 원작 전투 화면 스크린샷) — 플레이 확인 대기
  - 결정 (2026-11-06, 사용자): 카메라 옆에서 비스듬히 · 시험 팀은 디버그 팀 에셋 · 재생 조작 1x/2x/4x + 다시 보기
  - 원작 화면 반영: 왼쪽 내 팀 / 오른쪽 상대, 팀마다 위 · 아래 2줄 × 가운데에서 3칸, 위 양쪽 HP 바(오른쪽 끝부터 줄어듦), 몬스터 옆 공격 게이지 → `Docs/참고게임_분석.md` 5-1
  - 구현: `GameRoot`(연결 · 다시 보기 = 같은 입력 재계산 후 기록 비교) · `BattlePlayback`(기록 → 시간) · `BattleStage`(캡슐 · 칸 표시 · 게이지 · 돌진 · 투사체) · `BattleHud`(HP 바 · 라운드 · 팀 이름 · 속도 버튼 · 다시 보기 · 결과 배너 · 공격력/간격 이름표 · 피해 숫자) · `HudText` · `DebugTeamSet`(고른 팀 · 빠른 팀 · 한 방 팀 · 빈 팀) · 메뉴 `Setup/Debug Teams` · `Setup/Battle Scene` · 씬 `Scenes/Battle.unity`(빌드 목록 맨 앞) · Game 어셈블리에 `UnityEngine.UI` 참조
  - 정한 것 (Claude, 확인 필요): 이름표 = "공격력 / 공격 간격(초)" · 피해 숫자는 상대 칸 중 하나(연출 난수)에 뜸 — 피해는 공통 체력으로 가므로 위치는 연출일 뿐 · 칸 번호 뜻을 원작 화면 기준으로 정리(줄 = 위/아래, 열 0 = 가운데 쪽, 번호 규칙은 그대로)
  - 확인: 컴파일 오류 0 · Setup 메뉴 2번 실행해도 씬 오브젝트 한 벌 · 플레이(일시정지 + 프레임 진행): 고른 팀 vs 빠른 팀 3라운드 → 81틱 패배, HP 0/800 vs 203/800 · 다시 보기 → "같은 결과 (확인됨)" · 빈 팀끼리 1라운드 → 60초 "서든데스!" → 64초 무승부 · 캡처 3장 · 콘솔 오류 0
  - 고침 (2026-11-06, Claude 확인 중 발견): 팀을 바꾼 뒤 다시 보기에서 "결과가 다름 (결정론 깨짐)"이 잘못 뜸 → 원인: 입력이 달라도 직전 기록과 비교 → 입력(팀 내용 · 라운드 · 시드 · 체력)이 같을 때만 비교. 재확인: 같은 입력 → 확인됨, 팀 변경 → 표시 없음, 변경 후 다시 보기 → 확인됨
  - 확인 못 함: 실제 마우스 클릭(버튼은 코드로 onClick 호출) · 1x 실제 시간 흐름(포커스 없어 프레임 진행으로 대체) · 화면 비율 16:9 외
  - 테스트 통과 (2026-11-06 사용자 실행): `BattlePlaybackTests`(4)
  - 확인 방법: `Scenes/Battle.unity` 열기 → 플레이 → 공격 · HP 감소 · 결과 배너 → 2x/4x 버튼 → 다시 보기 → 아래 "같은 시드 → 같은 결과 (확인됨)". GameRoot 인스펙터에서 팀 번호(0 고른 · 1 빠른 · 2 한 방 · 3 빈) · 라운드를 바꿔 볼 수 있음
- [x] **1-4. 상대 공급자** (2026-11-06 사용자 "1-4 진행해줘") — 플레이 확인 대기
  - 결정 (2026-11-06, 사용자): 몬스터 표 시험용 6종(능력치만) · 레벨당 공격력 ×2(임시) · 로컬 상대 = 스냅샷 JSON 파일 · 내 팀 올리기(로컬 저장) 기능
  - 구현: Core — `SpeciesCatalog`/`SpeciesDef`/`StatIds`/`UnitStatRules` · `TeamFactory`(스냅샷 → 전투 팀, 레벨 · 버프 · 상한 검사) · `IOpponentSource`(비동기) · `OpponentQuery`/`OpponentResult` · `OpponentPicker`(플레이어 · 봇 고르기) · `LocalFileOpponentSource` · `TeamSnapshotJson.SerializeMany/DeserializeMany`(잘못된 항목은 건너뛰고 기록). Game — `SpeciesTable` 에셋 · `BattleBalance`에 `levelAttackMultiplier` · `rulesVersion` · `GameRoot` "로컬 상대" 모드(기본 켜짐, 못 찾으면 디버그 팀 + 안내) · `HudText` 문구 3개. Editor — 메뉴 `Setup/Species Table` · `Setup/Local Opponents` · Battle Scene이 둘 다 부르고 몬스터 표 연결. 데이터 — `StreamingAssets/Opponents/sample_opponents.json`(14라운드 × 3팀) · `StreamingAssets/OpponentBots/bots.json`(5팀)
  - 정한 것 (Claude, 확인 필요): 고르기 = 같은 라운드 · 같은 규칙 버전 · 내 판 아님 · 만난 적 없음 → 승수가 가장 가까운 무리에서 시드 난수(원작은 점수 구간 매칭 — 점수가 생기면 바꿈) · 봇 = 같은 라운드, 없으면 그 아래 가장 높은 라운드 · 모르는 능력치 버프는 무시 · 레벨 계산 후 버프를 더함 · 능력치 안전 상한 10억 · 상대 파일은 매번 다시 읽음 · 올린 팀 저장 위치 `persistentDataPath/LocalOpponents/my_teams.json`(회사 · 제품 이름을 바꾸면 위치가 바뀜)
  - 수치: BALANCE 6 · 7 · 8
  - 확인: 컴파일 오류 · 경고 0 · 기존 BattleBalance 에셋에 새 값(배율 2 · 버전 1) 채움 · Setup 메뉴 재실행해도 샘플 파일 동일 · 샘플 42팀 · 봇 5팀 읽기 오류 0 · 플레이: 3라운드 → `sample_r03_0`(0승) 상대로 110틱 승리 · 20라운드 → 샘플 없음 → 봇 `bot_r13`(레벨 5, 공격력 ×16 표시 확인) · 3라운드 다시 보기 → 같은 상대 + "같은 결과 (확인됨)" · 캡처 2장 · 콘솔 오류 0
  - 확인 못 함: 플레이 모드에서 실제 올리기(내 저장 폴더를 더럽히지 않으려고 — 임시 폴더 테스트로만) · 빌드에서 StreamingAssets 읽기
  - 테스트 통과 (2026-11-06 사용자 실행): `TeamFactoryTests`(8) · `OpponentPickerTests`(8) · `LocalFileOpponentSourceTests`(6) · `BalanceTests.Balance6` · `Balance7` (→ 총 65개)
  - 확인 방법: `Battle.unity` 플레이 → 위 팀 이름이 "고른 팀 vs sample_r03_0 (0승)" → GameRoot 인스펙터에서 라운드 20 → 다시 보기 → "봇 bot_r13" · 오른쪽 공격력이 큰 숫자. `Use Local Opponent` 끄면 예전처럼 디버그 팀

## Phase 2. 시스템 연결
- [x] **2-1. 한 판 진행** (2026-11-06 사용자 "2-1 진행해줘") — 플레이 확인 대기
  - 결정 (2026-11-06, 사용자): 시작 팀 고정 + 디버그 레벨업 · 결과 화면 + '다음 라운드' 버튼 · 목숨 구간 임시값 · 판 저장은 구조 + 왕복 테스트만
  - 구현: Core — `RunRules` · `RunState`/`RoundRecord`/`RunOutcome`(판 상태 스냅샷, formatVersion 1) · `RunStateJson`(저장 형식 · 버전 확인 · 검사) · `RunController`(새 판 · 라운드 진행 · 디버그 레벨업) · `SeedMix`(판 시드 → 용도 · 라운드별 시드). Game — `RunBalance` 에셋(승리 수 · 시작 목숨 · 라운드별 목숨 감소 · 시작 트레이너 · 시작 팀) · `GameRoot` 모드 Run(기본)/BattleTest · `BattleHud` 승수 · 목숨 줄 · 결과 패널(내용 + 다음 라운드/새 판 버튼) · 디버그 레벨업 버튼 · `HudText` 문구. Editor — 메뉴 `Setup/Run Balance`, Battle Scene이 부르고 연결
  - 정한 것 (Claude, 확인 필요): 내 팀은 전투 **전에** 올림(그 라운드 · 그때 승수로) · 남은 목숨보다 많이 잃으면 남은 만큼만 · 상대 없음 = 부전승 · 봇은 "만난 상대"에 넣지 않음(반복 가능) · 판 시드 0 = 판마다 새 시드 · 다시 보기는 판 상태를 바꾸지 않고 마지막 전투만 재계산 · 결과 표시 중 승수 · 목숨 줄은 전투가 끝날 때 바뀜
  - 수치: BALANCE 5 · 9
  - 확인: 컴파일 오류 · 경고 0 · Setup → RunBalance 생성 · 씬 연결 · 플레이(확인용 쓰기 폴더 `LocalOpponents_ClaudeCheck`, 판 시드 777): ① 1R 승 → 결과 "승리! 승 1/10" + 다음 라운드 ② 2~8R 연패, 목숨 −1 −1 −1 −2 −2 −2 −1(남은 1) → "탈락 · 8라운드에서 목숨을 모두 잃었습니다 (1승)" + 새 판 ③ 새 판 → 새 판 ID · 목숨 10 ④ 디버그 레벨업 ×4 → "레벨 5" ⑤ 10연승 → "챔피언! 11라운드 만에 10승 달성!" ⑥ **3라운드 상대가 앞 판에서 올린 내 팀** → 올리기 → 받기 실제로 이어짐 · 캡처 3장 · 콘솔 오류 0 · 끝난 뒤 씬 설정 되돌리고 확인용 폴더만 삭제
  - 확인 못 함: 실제 마우스 클릭(버튼 onClick 호출로 대신) · 저장 파일 쓰기/읽기(Phase 5)
  - 발견 (결정 필요): 저장 폴더 `AppData/LocalLow/DefaultCompany/My project`에 10월 `Player.log` 등 다른 프로젝트 파일이 있음 → 회사 · 제품 이름이 기본값이라 다른 Unity 프로젝트와 폴더를 함께 씀 (GDD 10 열린 항목)
  - 테스트 통과 (2026-11-06 사용자 실행): `RunControllerTests`(20) · `BalanceTests.Balance5` · `Balance9` (→ 총 87개)
  - 확인 방법: `Battle.unity` 플레이 → 자동으로 1라운드 → 결과 패널 "다음 라운드" → 반복 → 목숨이 줄다가 "탈락" → "새 판". 왼쪽 아래 "레벨업 (디버그)"를 몇 번 누르고 진행하면 이기다가 "챔피언!". 위쪽 "승 N/10 · 목숨 N/10" 확인. GameRoot `Mode`를 BattleTest로 바꾸면 예전 시험 전투
- [x] **2-2. 상점 · 골드 · 배치** (2026-11-06 사용자 "2-2 진행해줘") — 플레이 확인 대기
  - 결정 (2026-11-06, 사용자): 골드 1R 30 · 수입 35 · 40 · 45 · 50 … (+5씩 계속) · 남은 골드 이월 · 새로고침 3 · 등급별 가격 10/20/30/40/50 (원작 값, "임시") · 같은 몬스터 3마리 합체 · 6종 중 무작위 5칸 · 클릭 → 클릭 조작
  - 조사: 사용자 "도감에서 볼 수 없어?" → 도감은 암호화된 `.pck` 안이라 못 봄 → 공개 공략 · itch.io에서 "$10 일반" · "Lv2 판매가치 $23" · "최대 레벨 4" 찾음 → `Docs/참고게임_분석.md` 5-2
  - 구현: Core — `ShopRules` · `ShopController`(BeginRound · Buy · Reroll · Sell · Move · 합체) · `Location`/`Area`/`ShopError` · `Rarity` + `SpeciesDef.Rarity` · `SpeciesCatalog.All`(표 순서) · `SeedMix.PurposeShop` · `RunState` v2(gold · bench · shop · shopRound · shopRollIndex) + v1 → v2 변환. Game — `ShopView`(상점 카드 5 · 새로고침 · 골드 · 대기석 4 · 보드 칸 버튼 6 · 판매 · 전투 시작 · 안내 글) · `RunBalance` 상점 수치 · `SpeciesTable` 등급 칸 · `GameRoot` 흐름(새 판 → 상점 → 전투 → 결과 → 다음 라운드 상점) · `BattleHud.ShowShopHeader` · `HudText` 상점 문구. Editor — Run Balance 기본값 · Battle Scene이 Shop 오브젝트 연결
  - 정한 것 (Claude, 확인 필요): **판매가 = 들인 골드의 75% 반올림**(공략 Lv2 $23과 일치하는 추정) · 최대 레벨 4(댓글) · **시작 팀 없음**(1라운드 30골드로 3마리) · 시험용 6종 모두 일반 · 사면 대기석 빈칸으로 · 대기석이 가득 차도 합체가 되면 살 수 있음 · 합체한 몬스터는 보드 → 대기석 순 첫 자리에 남고 버프는 합침 · 보드가 비었는데 전투 시작하면 한 번 경고 · 디버그 레벨업 버튼은 그대로 둠
  - 수치: BALANCE 7 · 9 · 10
  - 확인: 컴파일 오류 0 (NUnit `Has.All.AnyOf` 없음 → 고침) · 기존 RunBalance에 상점 값 채우고 시작 팀 비움 · 플레이(확인용 폴더, 시드 2026): 1R 골드 30 · 카드 5장 → 3마리 구매 골드 0 → 4번째 구매 · 새로고침 "골드가 부족합니다" → 대기석 → 보드 옮기기 · 맞바꾸기 → 전투 시작(상점 숨김) → 승 → 다음 라운드 → 골드 35 → 바람개비 3마리째 "합체! → Lv2" → 불씨 선택 "판매 (+8)" → 33 · 캡처 3장 · 콘솔 오류 0 · 끝난 뒤 씬 설정 되돌림 · 확인용 폴더만 삭제
  - 확인 못 함: 실제 마우스 클릭(버튼 onClick 호출로 대신) · 16:9 외 화면 비율에서 보드 칸 버튼 위치
  - 테스트 통과 (2026-11-06 사용자 실행, 고침 후): `ShopControllerTests`(19) · `BalanceTests.Balance10` · 고친 것 `Balance9`(시작 팀 없음) · `RunControllerTests` 잘못된 JSON 사례(버전 2 → 3) (→ 총 107개)
  - 확인 방법: `Battle.unity` 플레이 → 아래 카드 클릭 = 사기(대기석으로) → 대기석 몬스터 클릭 → 보드 칸 클릭 = 배치 → 같은 몬스터 3마리 = 합체 → 몬스터 고르고 "판매" → "전투 시작" → 결과 → "다음 라운드" = 골드가 들어오고 새 상점
  - 고침 (2026-11-06, 사용자 "테스트 하나 fail했어"): `ShopControllerTests.Roll_SameSeedSameShop_RerollIsDeterministic` 기대 24 · 실제 27 → 원인: **테스트 실수** (새로고침 1번 = 30 − 3 = 27인데 2번처럼 24로 적음, 코드 동작은 맞음 — 플레이에서도 새로고침 1번당 3 확인) → 기대값 27로 고침. 결과 파일: `AppData/LocalLow/NariGames/MoaShowdown/TestResults.xml` (106 통과 · 1 실패). 검사: 기대값 옆에 계산식을 주석으로 적는다
- [x] **2-3. HUD — 마우스 올림 정보 창** (2026-11-06 사용자 "마우스를 올리면 각 몬스터의 정보나 상대 캐릭터의 정보가 자동으로 떠서 보이게, 마우스를 내리면 자동으로 사라지고") — 플레이 확인 대기
  - 결정 (2026-11-06, 사용자): 몬스터 · 상대 정보는 마우스를 올리면 뜨고 내리면 사라진다
  - 구현: `TooltipView`(맨 위 캔버스, 커서 따라감, 화면 밖으로 안 나감, owner별 숨김) · `HoverTarget`(UI 올림/내림) · `InfoText`(글 만들기) · `TeamFactory.BuildUnit`(몬스터 하나 능력치 — Build와 공용으로 분리) · `ShopView.SetTooltips`(상점 카드 · 대기석 · 보드 칸) · `BattleHud.AddSideInfo`/`SetSideLabels`(HP 바 아래 "나 (정보)" · "상대: …" 이름표) · `GameRoot` 정보 공급자 + 전투 중 3D 몬스터 판정(`ProbeBattleHover` · `UnitUnderScreenPoint`, 반경 70px@1080p) · `HudText` 정보 문구 · Battle Scene Setup이 Tooltip 연결
  - 정보 창 내용: 몬스터 = 이름 · Lv · (상대) 표시 · 등급 · 공격력(기본 × 2^(Lv−1) + 버프) · 공격 간격 · 초당 피해 · [상점] 가격 · [내 것] 판매가 · 합체 진행(같은 Lv n/3) 또는 "최대 레벨". 상대 = 플레이어/봇 · 판 ID · 트레이너 · 그때 승수 · 잃은 목숨 · 몬스터 수 · 출처 파일. 나 = 트레이너 · 라운드 · 승/목숨 · 골드
  - 정한 것 (Claude, 확인 필요): 원작 트레이너 초상화 자리 대신 HP 바 아래 이름표에 올리면 정보 · 상점 카드의 합체 숫자는 "이미 가진 수"(사면 +1) · 상점 단계에서는 상대 이름표 숨김 · 전투 중 UI 위에서는 3D 몬스터 정보 대신 UI 정보 우선 · 정보 창은 마우스를 가로채지 않음(깜빡임 방지)
  - 확인: 컴파일 오류 0 · 플레이(확인용 폴더, 시드 2026): 상점 카드 올림 → "바람개비 Lv1 · 공격력 7 · 0.6초 · 초당 11.7 · 가격 10 · 합체 0/3" → 내림 → 사라짐 · 대기석 몬스터 → 판매가 8 · 합체 2/3 · 빈 대기석 → 안 뜸 · "나" → 트레이너 · 라운드 · 승/목숨 · 골드 · 전투: 마우스를 상대 몬스터 위로 → "새싹이 Lv1 (상대)" · 내 몬스터 위 → 바람개비 · 빈 곳 → 사라짐 · "상대: sample_r01_1 (0승)" 올림 → 다른 플레이어 · 판 ID · 트레이너 · 승수 · 몬스터 2마리 · 출처 → 내림 → 사라짐 · 캡처 3장 · 끝난 뒤 씬 설정 되돌림 · 확인용 폴더만 삭제
  - 확인 못 함: 실제 손으로 마우스를 움직였을 때의 느낌(올림 이벤트는 코드로 보냄, 3D는 Input System 마우스 위치를 코드로 바꿈) · 터치 화면
  - 테스트 통과 (2026-11-06 사용자 실행): `InfoTextTests`(6) (→ 총 113개)
  - 확인 방법: `Battle.unity` 플레이 → 상점 카드 · 대기석 · 보드 몬스터 위에 마우스 → 정보 창, 떼면 사라짐 → 전투 시작 → 양쪽 몬스터 위에 마우스 → 정보 창 → 오른쪽 위 "상대: …" 이름표 · 왼쪽 위 "나 (정보)" 위에 마우스

## Phase 3. 콘텐츠 · 규칙 확장
- [ ] 트레이너 · 몬스터 · 고유 능력 · 장신구 · 아이템 · 이벤트 (한 묶음씩)

## Phase O. 온라인 (UGS)
- [ ] O-1. UGS 인증(익명) · 프로젝트 연결
- [ ] O-2. 스냅샷 업로드 · 같은 단계 상대 받기 (Cloud Code + Cloud Save)
- [ ] O-3. 상대가 없을 때 봇 팀으로 대체 · 오류/오프라인 처리

## Phase G. 그래픽 (병행 가능)
- [ ] 그레이박스 → AI 3D 모델

## Phase 4. 재미 검증 / 5. 메뉴 · 저장 · UI · 사운드 / D. 시연 · 출품
- [ ] (해당 단계에서 항목화)

---

## 다른 PC에서 할 일
- Unity Hub에서 `MoaShowdown` 폴더를 프로젝트로 추가 (이전 `My project` 경로 항목은 삭제)
