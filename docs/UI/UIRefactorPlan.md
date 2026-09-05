# UI 리팩터 계획

## 게임 디렉션 판단

현행 프레임의 정보량은 적지 않지만 모든 패널이 같은 강도의 청록 금속 테두리를 가져 무엇이 중요한지 흐려진다. 여러 시즌을 반복할 게임에서는 화려한 한 장보다 비교·선택·원인 확인 속도가 중요하다. 따라서 Frame은 교체하고 실제 View/Simulation 연결은 보존한다.

첫 기준 화면은 Roster/Lineup이다. 25명을 한 화면에서 읽고 선택 결과와 규칙 근거를 즉시 비교할 수 있어야 Shell, Mini Card, Filter, Inspector, Action Bar의 품질을 동시에 검증할 수 있기 때문이다.

## Phase

### Phase 0 — 완료 조건

- 영상과 코드/Scene/Prefab/Save 감사
- Target Shell/Route/Capability 계약
- Theme Token과 Asset 계획
- 파일 소유권 확정

### Phase 1 — 공통 기반

- `GameMode`의 게임 모드 의미를 `OwnerCareer`로 정리하고 숫자 1/Legacy alias를 유지한다.
- `SharedGameShell`, `GlobalTopBar`, `PrimaryNavigation`, `ContextHeader`, `WorkspaceHost`, `RightInspector`, `ContextActionBar`를 만든다.
- 공통 `PlayerMiniCard`, `FilterBar`, `RecordTable`, `Tooltip`, `Empty/Loading/Error` 기초를 만든다.
- 현재 강한 cyan/glow 토큰을 graphite/off-white/muted navy/grass accent로 교체한다.

### Phase 2 — 모드 Workspace

- Player: Home, read-only Team, Growth, Contract, Match overlay를 기존 View data에 연결한다.
- Owner: 실제 `ManagerHistoricalRuntimeState`에서 공급 가능한 Home/Roster부터 연결한다.
- 백엔드가 없는 Owner 메뉴는 실제 잠금 사유를 가진 `Locked` 상태로 남기며 가짜 데이터를 만들지 않는다.
- Schedule/League/Records/Player Detail은 공용 Screen + Mode Action Adapter로 이동한다.

### Phase 3 — 통합

- 분산된 Runtime Bootstrap을 Shell/Route Registry 중심으로 합친다.
- 개별 화면의 TopBar/Nav/Popup helper 중복을 제거한다.
- Match는 `MatchHUDBase + Player/Owner Overlay`로 분리한다.
- Legacy Frame 제거 시점을 파일 단위로 기록한다.

### Phase 4 — 검증

- EditMode: Profile/Capability/route/permission/model binding/legacy terminology.
- 보조 컴파일: Baseball.Presentation/Game Tests.
- PlayMode/Unity: 실제 Navigation, input focus, Popup stack, 1280×720/1920×1080/2560×1440.
- Screenshot: 요청 목록 중 실제 데이터 연결 화면만 캡처한다.

## 정본 문서 충돌 처리

기존 `Unity_UI_Production_Guidelines_UPlayBall.md`는 하단 8탭과 기존 Skin 보존을 필수로 규정한다. 이번 사용자 요청은 공통 Shell 자체와 시각 언어 교체를 명시하므로 상위 지시다. 구현이 안정된 뒤 해당 지침을 새 Shared Shell/상단 고밀도 Navigation 기준으로 갱신한다. Player 메뉴의 의미와 권한은 유지한다.

## 완료 보고 원칙

- 컴파일과 자동 테스트 통과를 구현 완료와 동일시하지 않는다.
- Unity PlayMode/해상도/스크린샷을 실행하지 못한 항목은 미검증으로 구분한다.
- 밸런스 수치는 바꾸지 않는다. 따라서 대량 시뮬레이션 재밸런싱은 이 작업 범위가 아니다.

## 2026-09-04 구현 상태

- 완료: 현행 영상/Scene/Prefab/코드/Save 감사와 화면 분류.
- 완료: `GameMode.OwnerCareer = 1` 정식 이름 및 `ManagerCareer` Legacy alias.
- 완료: 중립 Theme token, `SharedGameShell`, 계층형 Navigation, Status Provider, Context Header, Workspace/Inspector/Action/Popup 슬롯.
- 완료: 공용 `PlayerMiniCard`, Ready/Loading/Empty/Error 계약.
- 완료: Player Mode Profile/Home model/Status adapter와 Owner Mode Profile/Home snapshot 계약.
- 완료: Schedule/League/Records/Team/Player Detail용 mode-neutral Shared Screen model과 Action Provider 계약.
- 완료: 선수 커리어 기존 하단 8탭 복제 제거, 단일 `PlayerCareerShellCoordinator` Route 연결.
- 완료: Player의 Growth/Records/Schedule/Contract/League/Player/Team 화면을 공용 Chrome 안전영역으로 이동하고 중복 Top Bar를 제거.
- 완료: 위 Player 화면의 cyan 로컬 팔레트와 Glow를 공용 중립 Theme token으로 통합.
- 완료: 미연결 Player Sub Route는 Navigation에서 숨겨 Header만 바뀌는 거짓 상호작용을 제거.
- 완료: Player Home에 생성 Clubhouse 배경을 저대비 장식 레이어로 적용.
- 보류: Owner Production Game Manager/Route가 없어 타이틀 진입은 잠금 유지. 가짜 데이터를 만들지 않는다.
- 보류: 기존 Career Workspace 전체를 `MainWorkspaceHost` 자식으로 옮기는 작업, MatchShell 분리, 25인 Owner Lineup 편집 View.
- 미검증: Unity 라이선스 부재로 PlayMode, 실제 해상도 캡처, Screenshot acceptance는 수행하지 못했다.

## 2026-09-05 통합 상태

- 완료: `UiGameModeSession` 단일 중재. 한 Runtime만 복원되면 해당 모드를 선택하고 Player/Owner Runtime이 함께 있거나 선택한 Runtime이 사라졌으면 타이틀에서 명시 선택을 요구한다.
- 완료: Production `OwnerModeManager`를 타이틀과 Shared Shell에 연결했다.
- 완료: Owner Home, 재정, 시설, Staff Office, Pregame, Condition·궁합의 실제 Snapshot/Command 연결.
- 완료: Presentation의 로스터 규칙 재계산 제거. `OwnerModeManager.BuildRosterStatus()`가 Simulation 검증 결과를 공급한다.
- 완료: Player League 순위표가 공용 12행 `CompactRecordTableView`를 실제 사용한다.
- 완료: Career 리더보드와 시즌/역대 기록표가 가상화 `RecordTableView`를 사용한다. 1,000행에서도 Viewport 부근 Row만 재사용하며 정렬과 Stable ID 선택을 유지한다.
- 완료: Career Schedule의 내 구단 월간 목록을 공용 `RecordTableView`로 이관하고 기존 달력·Split·리그 목록 Navigation을 유지했다.
- 완료: Career Team의 선수단 목록을 공용 `ReadOnlyRosterListView`로 이관하고 전체/타자/투수 필터와 내 선수 강조를 유지했다.
- 완료: Career Player Detail의 공용 신원·상태 Snapshot과 능력치 표를 실제 화면에 연결하고 대형 카드·성장 CTA·Career 전용 탭을 유지했다.
- 완료: Owner 선수단·라인업이 실제 25인/선택 프리셋/Resolver 결과를 표시하고 같은 역할 그룹 슬롯 교환을 `UpsertLineupPreset`에 연결한다. ActiveRoster 등록 변경은 실제 Command가 없어 비활성화했다.
- 완료: Owner 보유 선수 Collection이 실제 `OwnedCards`/`WorldCardCatalog`를 공용 Mini Card로 표시하며 검색·정렬·선택 Inspector를 제공한다. 강화·판매·1군 등록 변경은 실제 Command가 없어 비활성화했다.
- 완료: Career Match 스코어보드가 concrete 공용 `MatchHudView`를 사용하고 Button/키보드의 타격·투구 핵심 입력이 `PlayerMatchControls`를 통과한다.
- 완료: Owner Home/Pregame 경기 시작을 공용 HUD 기반 관전 화면으로 연결했다. 감독 AI 경기 결과를 타석 단위로 재생하고 일시정지·1/2/5배속·즉시 결과만 허용한다.
- 완료: 문서 폴더와 사용자 노출 게임 모드 용어를 구단주 모드로 정리했다.
- 완료: Scout는 Production Pool/확률/결정론적 실행/소유 카드 원자 갱신 계약이 없음을 확인하고 `CanUseScout`와 Route를 비노출로 고정했다.
- 완료: TeamColor/Tactic의 실제 후보 Query와 프리셋 검증·저장 경로를 선수단·라인업의 각 2슬롯에 연결했다. 충족 인원·Stack/Trigger 등 전용 분석 Snapshot이 없어 별도 상세 Route는 비활성 상태다.
- 완료: CardTraining/Enhancement/Sale/ActiveRoster 등록 변경의 Preview·Catalog·원자 Command 누락을 확인하고 UI가 비용·확률·규칙을 재계산하지 않도록 비활성 계약을 테스트로 고정했다.
- 보류: Owner ActiveRoster 등록 변경/Scout/육성/TeamColor/전술, 관전 경기 로그·Box Score 보조 Panel.
- 완료: Owner 일정과 `WorldHistory` 확정 타격 기록을 공용 가상화 표로 연결했다. 현재 시즌 순위는 누적 Aggregate가 없어 비활성 상태다.
- 완료: Player Contract의 계약 이력·상여·오퍼를 공용 표로 이관하고 기존 협상/서명/은퇴 Command를 유지했다.
- 보류: Save DTO에 연결된 `ManagerMode*` 내부 타입 rename. 호환 alias와 schema migration을 동반해야 한다.
- 검증: Game/Game.Unity/Presentation/Game.Tests/Presentation.Tests 보조 컴파일 warning 0/error 0. Unity Test Runner와 해상도 Screenshot은 라이선스 부재로 미검증.
