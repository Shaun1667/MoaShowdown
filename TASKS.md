# TASKS.md

## 진행 요약 (2026-11-06 기준 — 새 세션은 여기부터 읽는다)
**최신 (2026-11-06): 1-2 팀 스냅샷 (JSON, formatVersion 1) — 브랜치 `phase1-team-snapshot`(← `phase1-battle-core` ← `phase0-setup`), 모두 main 병합 전. 테스트 37개 (실행 안 함 — 돌릴 때 됨). 다음: 1-3 그레이박스 전투 재생 (착수 질문부터).**

---

## Phase 0. 세팅
- [x] **0-1. 프로젝트 세팅** (2026-11-06 사용자 "Batomon Showdown 같은 게임을 온라인 대전으로 만들고 싶어")
  - 결정 (2026-11-06, 사용자): 비동기 대전 · 로컬 가짜 상대 → UGS · 3D 모델 · 한 판 구조만 참고 · 10승/5패 · 3×2 격자 · URP + Input System + uGUI · 프로젝트를 저장소 루트로
  - 조사: 참고 게임 설치 폴더 = Godot 4.7 + GodotSteam, `.pck`는 목록까지 암호화 → 열지 않음(보호 장치 우회 안 함). 게임 방식은 Steam 상점 페이지(공개 정보)로 확인.
  - 구현: `My project/` → 저장소 루트로 이동(git mv) · `.gitignore` 루트 기준 경로로 · `.gitattributes` 추가 · 어셈블리 `Moa.Showdown.Core`(엔진 참조 금지) / `Game` / `Editor` / `Tests` · 원본 가이드 `Docs/Source/MY_DEV_GUIDE.md`
  - 정한 것 (Claude, 확인 필요): 네임스페이스 `Moa.Showdown.*`, 폴더 `Assets/_Project/…`, 데이터 에셋 자리 `Assets/_Project/Data/`
  - 테스트 작성, 실행 안 함: `CoreAssemblyTests.Core_DoesNotReferenceUnityEngine`
  - 확인: Unity 배치 모드(화면 없이 실행) 컴파일 오류 0, `Core` · `Tests` 어셈블리 생성. `Game` · `Editor`는 스크립트가 없어 아직 컴파일 안 됨(정상).
  - ~~확인 못 함: 에디터 MCP 연결~~ → 2026-11-06 사용자가 Hub 등록 후 MCP 연결 · 콘솔 오류 0 확인
  - 참고: `ProjectSettings` 2개 파일에 Unity AI 패키지가 자동으로 넣은 변경(App UI 설정)이 있어 커밋에서 뺐음
  - 확인 방법: Unity Hub → Add → `MoaShowdown` 폴더 → 열기 → 콘솔 오류 0 → Test Runner(EditMode)에 테스트 1개 보임

## Phase 1. 구조 기반 + 핵심 조작
- [x] **1-1. 전투 시뮬레이션 코어** (2026-11-06 사용자 "체력 300 → 500 → 800 … 1400, 누굴 때리든 상성 없이 공통 체력이 줄고 다 닳면 진다") — 플레이 확인 대기 (화면은 1-3)
  - 조사: 참고 게임 로컬 데이터 폴더(암호화 안 됨)에서 판 저장 · 상대 캐시 · 로그 구조 확인 → `Docs/참고게임_분석.md`. 체력 표 · 몬스터 능력치는 `.pck` 안이라 확인 안 함.
  - 결정 (2026-11-06, 사용자): 팀 공통 체력(누구를 때리든 상대 공통 체력 감소) · 체력 표는 임시값 · 공격은 각자 공격 간격대로 · 제한 틱부터 서든데스(점점 세지는 도트 피해) · ~~5패~~ → 목숨 10개, 후반 패배는 최대 3개
  - 구현: `BattleSimulator`(전투 계산) · `BattleRules`(수치) · `BattleTeam`/`BattleUnit`(3×2 보드, 칸 = 줄×3+열) · `BattleEvent`(Attack · SuddenDeath · TeamHpChanged · End) · `DeterministicRandom`(SplitMix64) · 에셋 `Data/BattleBalance.asset` · 메뉴 `MoaShowdown/Setup/Battle Balance`
  - 정한 것 (Claude, 확인 필요): 피해는 틱 끝에 양쪽 동시 적용(먼저 친 쪽 이득 없음) · 같은 틱에 둘 다 0 = 무승부(목숨 · 승수 변화 없음) · 첫 공격은 공격 간격만큼 지난 뒤 · 서든데스 60초부터 1초마다 20, 40, 60 … · 안전 상한 5분 · 패배 비용 구간 1~4R 1 / 5~9R 2 / 10R~ 3
  - 수치: BALANCE 1~5번
  - 확인: 에디터 컴파일 오류 0 · Setup 메뉴 2번 실행해도 에셋 하나(값 보존) · 실제 에셋 값으로 샘플 전투(1 · 3 · 5라운드 A 승, 75 · 192 · 330틱) · 빈 팀 20라운드는 서든데스로 860틱(86초) 무승부
  - 테스트 작성, 실행 안 함: `BattleSimulatorTests`(9) · `DeterministicRandomTests`(4) · `BalanceTests`(5)
  - 확인 방법: Window → General → Test Runner → EditMode → Run All → 19개 통과. (화면 확인은 1-3 그레이박스 재생에서)
- [x] **1-2. 팀 스냅샷** (2026-11-06 사용자 "1-2 진행해줘") — 플레이 확인 대기 (화면 없음 — 테스트로 확인)
  - 결정 (2026-11-06, 사용자): JSON · 몬스터는 ID + 레벨 + 버프만
  - 구현: `TeamSnapshot`/`MonsterSnapshot`/`StatBuff`(Core/Snapshot, formatVersion 1) · `TeamSnapshotJson`(직렬화 · 버전 확인 · 단계 변환 자리 · 구조 검사, 잘못되면 `SnapshotFormatException`) · `Packages/manifest.json`에 Newtonsoft JSON 3.2.1 명시(이미 다른 패키지 의존으로 설치돼 있었음)
  - 정한 것 (Claude, 확인 필요): 스냅샷 필드 = formatVersion · rulesVersion · runId · round · wins · livesLost · trainerId · trinketIds · board(6칸, 빈칸 null). 소유자 ID · 점수 · 결과는 넣지 않음(서버가 감쌈) · 능력치 ID는 문자열(`"attack"` 등) · 모르는 필드는 무시, 새 버전(formatVersion > 현재)은 거부
  - 확인: 컴파일 오류 · 경고 0 (도달 불가 코드 경고 1개 고침) · 에디터에서 직렬화 → 읽기 → 직렬화 결과 동일 · formatVersion 99 거부
  - 확인 못 함: 몬스터 표가 없어 스냅샷 → `BattleTeam` 변환은 아직 (몬스터 표 생길 때 = 1-4 또는 Phase 3)
  - 테스트 작성, 실행 안 함: `TeamSnapshotTests`(고정 JSON v1 양방향 · 왕복 · 모르는 필드 · 잘못된 JSON 12종 · 빈 보드 · 저장 전 검사 → 테스트 케이스 18개)
  - 확인 방법: Test Runner → EditMode → Run All → 전부 통과 (1-1 포함 37개)
- [ ] 1-3. 그레이박스 전투 재생 — 이벤트 로그를 3D 도형으로 재생 (GameRoot)
- [ ] 1-4. 상대 공급자 — `IOpponentSource` + 로컬 파일 구현 + 기본(봇) 팀 몇 개

## Phase 2. 시스템 연결
- [ ] 2-1. 한 판 진행(Run) — 라운드 · 승패 · 10승/5패
- [ ] 2-2. 상점 · 골드 · 배치 화면
- [ ] 2-3. HUD

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
