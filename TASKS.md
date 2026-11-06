# TASKS.md

## 진행 요약 (2026-11-06 기준 — 새 세션은 여기부터 읽는다)
**최신 (2026-11-06): Phase 0 세팅 — 프로젝트 루트 이동 · .gitignore/.gitattributes · 어셈블리 4개 · 문서 3종. 브랜치 `phase0-setup`, main 병합 전. 테스트 1개 (실행 안 함). 확인 대기: Unity Hub 경로 재등록 + 에디터 MCP 연결. 다음: 1-1 전투 시뮬레이션 코어 (착수 질문부터).**

---

## Phase 0. 세팅
- [x] **0-1. 프로젝트 세팅** (2026-11-06 사용자 "Batomon Showdown 같은 게임을 온라인 대전으로 만들고 싶어")
  - 결정 (2026-11-06, 사용자): 비동기 대전 · 로컬 가짜 상대 → UGS · 3D 모델 · 한 판 구조만 참고 · 10승/5패 · 3×2 격자 · URP + Input System + uGUI · 프로젝트를 저장소 루트로
  - 조사: 참고 게임 설치 폴더 = Godot 4.7 + GodotSteam, `.pck`는 목록까지 암호화 → 열지 않음(보호 장치 우회 안 함). 게임 방식은 Steam 상점 페이지(공개 정보)로 확인.
  - 구현: `My project/` → 저장소 루트로 이동(git mv) · `.gitignore` 루트 기준 경로로 · `.gitattributes` 추가 · 어셈블리 `Moa.Showdown.Core`(엔진 참조 금지) / `Game` / `Editor` / `Tests` · 원본 가이드 `Docs/Source/MY_DEV_GUIDE.md`
  - 정한 것 (Claude, 확인 필요): 네임스페이스 `Moa.Showdown.*`, 폴더 `Assets/_Project/…`, 데이터 에셋 자리 `Assets/_Project/Data/`
  - 테스트 작성, 실행 안 함: `CoreAssemblyTests.Core_DoesNotReferenceUnityEngine`
  - 확인: Unity 배치 모드(화면 없이 실행) 컴파일 오류 0, `Core` · `Tests` 어셈블리 생성. `Game` · `Editor`는 스크립트가 없어 아직 컴파일 안 됨(정상).
  - 확인 못 함: 에디터 MCP 연결 · Test Runner 화면 (폴더 이동으로 Unity Hub 경로 재등록 필요)
  - 참고: `ProjectSettings` 2개 파일에 Unity AI 패키지가 자동으로 넣은 변경(App UI 설정)이 있어 커밋에서 뺐음
  - 확인 방법: Unity Hub → Add → `MoaShowdown` 폴더 → 열기 → 콘솔 오류 0 → Test Runner(EditMode)에 테스트 1개 보임

## Phase 1. 구조 기반 + 핵심 조작
- [ ] 1-1. 전투 시뮬레이션 코어 — 3×2 격자, 유닛(체력 · 공격 · 속도), 고정 틱, 시드 난수, 전투 이벤트 로그, 결정론 테스트
- [ ] 1-2. 팀 스냅샷 — 직렬화 형식 + 버전 + 왕복 테스트
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
