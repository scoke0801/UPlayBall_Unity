# PBM UI 개편 구현 보고서

## 1. 영상 기반 현행 UI 분석

- 대상 영상: `유니티 게임 개발 작업일지( 2026.08.30 ) - 프야매 개인선수`, 약 8분 20초.
- 5초 간격 102개 프레임으로 타이틀 → 선수 생성 → Home → 경기 → 시즌 결과 → 성장/계약/은퇴 → 기록 흐름을 대조했다.
- 현행 Runtime UI는 uGUI이며 `Boot`, `Loading`, `Management`, `Match` Scene과 프로그램 생성 View를 사용한다.
- 세부 감사표와 코드 근거는 `CurrentUIMap.md`와 `PBM_UI_CurrentAudit.md`에 기록했다.

## 2. 폐기 또는 유지한 UI

### 폐기·대체

- 선수 화면마다 생성하던 하단 8탭 Chrome은 생성하지 않는다.
- 강한 cyan/glow 중심 공통 색상은 neutral graphite/off-white/muted navy/grass accent 토큰으로 교체했다.
- 선수 Home의 로컬 Top Bar는 단일 Shared Shell Header로 대체했다.

### 유지

- 기존 Career Manager, View data, 화면별 핵심 조작과 Simulation 연결은 유지했다.
- `UIPlayerCard`의 큰 카드 앞/뒤 구조와 Match event 경로는 폐기하지 않았다.
- 기존 UIScene/Popup layer와 Settings popup은 Shared Shell에서 재사용한다.

## 3. 새 Shared Shell 구조

`SharedGameShellView`가 다음 고정 구조를 런타임 생성한다.

```text
SharedGameShell
├─ GlobalTopBar
├─ PrimaryNavigation
├─ ContextHeader
│  └─ SubTabs
├─ MainWorkspaceHost
├─ OptionalRightInspector
├─ ContextActionBar
└─ OverlaySlots
   ├─ PopupHost
   ├─ ToastHost
   └─ TooltipHost
```

`PlayerCareerShellCoordinator`와 `OwnerModeShellCoordinator`가 Management Scene에서 모드별 Workspace를 같은 셸 계약에 연결한다. `UiGameModeSession`은 두 Runtime이 함께 살아 있어도 선택된 셸 하나만 노출하며, 선택 정보가 없는 상태에서 둘 다 활성화되어 있으면 임의로 Player 화면을 열지 않고 모드 선택 화면을 유지한다. Legacy Player Workspace 이관 중에는 `ChromeOverlayMode`로 Header/Navigation을 합성한다.

## 4. Shared Component와 계약

- `SharedGameShellView`, `SharedGameShellPresenter`
- `GameModeUiProfile`, `NavigationManifest`, `UiCapabilitySet`
- `ShellStatusModel`, `ShellContextModel`, `UiContentStateModel`
- `PlayerMiniCardView`, `PlayerMiniCardModel`
- `SharedScreenProfile`, `SharedScreenContext`, `SharedScreenPresentationModel<TSnapshot>`
- `ISharedScreenActionProvider`
- `ReadOnlyRosterModel`, `RecordTableModel`
- Schedule/League/Records/Team/Player Detail 공용 Snapshot
- `CompactRecordTableView`(작은 요약표), `RecordTableView`(Viewport Row Pool 기반 대량 기록표), `ReadOnlyRosterListView`(최대 40명)
- `PlayerContractPresentationModel`과 계약 이력·상여·오퍼 공용 표
- `OwnerSharedInformationSnapshotFactory`와 Owner 일정·역사 기록 읽기 전용 Action Provider
- `MatchHudPresentationModel`, `MatchHUDBase`, `MatchHudView`
- `PlayerMatchControls`, `OwnerMatchSpectatorSession`, `UI_Scene_OwnerMatchSpectator`, 안전한 `EmptyOwnerMatchOverlay`

## 5. 구단주 모드 범위

- 타이틀의 구단주 카드가 실제 `OwnerModeManager` 새 게임/Save Load를 수행한 뒤 Owner 세션을 선택한다.
- `OwnerModeManager.BuildRosterStatus()`가 Simulation 검증 결과와 25/14/11/3 제한을 Game 레이어에서 확정한다. Presentation은 그 결과를 다시 계산하지 않는다.
- 실제 Runtime에 연결된 화면은 Home, 선수단·라인업, 보유 선수 Collection, 구단 재정, 시설, Staff Office, Pregame, Condition·궁합, 일정, 역사 기록이다.
- Home은 다음 경기, 로스터 유효성, 보유 카드, Money/SP/DP/Pity를 실제 Save 상태에서 표시한다. 원 단위 Money는 UI에서 억/만/원으로 변환한다.
- 선수단·라인업은 25인 상태와 수비/타순/벤치/선발/불펜 역할을 세 개의 독립 Scroll 열에 표시한다. 같은 역할 그룹의 두 슬롯 교환은 실제 `UpsertLineupPreset` Command로 저장하고, Off Position Condition·실책 위험은 `PrepareNextGame().PresetValidation` 결과만 표시한다.
- 보유 선수 Collection은 실제 `OwnedCards`와 `WorldCardCatalog`를 읽어 공용 `PlayerMiniCardView`로 표시하고 이름·포지션·Cost·Edition 검색/정렬과 소유 상태 Inspector를 제공한다. 강화·판매·1군 등록 변경은 실제 Command가 없어 비활성화했다.
- Scout는 SP/Pity 저장만 있고 Production Pool Catalog·확률 Snapshot·결정론적 실행 Command·신규 카드 원자 추가 API가 없어 Capability와 Route를 숨겼다.
- TeamColor/Tactic은 실제 가용 후보 Query와 `ValidateLineupPreset`/`UpsertLineupPreset` 경로가 추가되어 선수단·라인업의 각 2슬롯을 실제로 순환·저장한다. 다만 현재 충족 인원·적용 대상·StackPolicy와 Trigger·Duration·Counter를 보여주는 전용 분석 Snapshot은 없어 별도 상세 Route는 비활성 상태다. `ManagerTacticProfile`과 감독 AI 의미는 유지한다.
- CardTraining은 Program Catalog와 Preview가 없고, Enhancement/Sale은 Owner Game Preview·Command가 없으며, ActiveRoster도 원자적 교체 Command가 없다. Presentation에서 비용·성공률·로스터 규칙을 만들지 않고 관련 조작을 비활성화했다.
- Owner 일정은 실제 Round·대진·완료 점수를, 역사 기록은 `WorldHistory.Statistics`의 확정 정규 시즌 타격 기록을 공용 가상화 표로 표시한다. 현재 시즌 순위 Aggregate가 없어 순위 Route는 비활성 상태다. 계약·트레이드도 가짜 기능으로 노출하지 않는다.
- Home과 Pregame의 경기 시작은 실제 `UI_Scene_OwnerMatchSpectator`로 연결했다. 경기는 `InternalAiOnly`로 한 번만 확정하고 공용 `MatchHudView`에서 타석 경계별로 재생한다. 구단주는 일시정지·1/2/5배속·다음 타석·즉시 결과만 제어하며, 선수 교체·불펜·전술 같은 존재하지 않는 실시간 Command는 노출하지 않는다.

## 6. 선수 모드 범위

- `PlayerCareerUiProfileFactory`가 Owner 권한 없는 메뉴와 Capability를 공급한다.
- `PlayerHomePresentationModelBuilder`가 실제 `CareerDashboardView`에서 Home/Header 모델을 만든다.
- `PlayerShellStatusProvider`가 현재 선수/구단/역할/컨디션을 공용 Header에 공급한다.
- Team 화면은 `CareerTeamOverviewSnapshotAdapter`와 공용 `ReadOnlyRosterListView`를 연결하고 전체/타자/투수 필터, 내 선수 강조, 감독의 기용 정보를 읽기 전용으로 표시한다.
- 실제로 별도 Workspace를 전환하지 않는 Sub Route는 Navigation에서 노출하지 않는다.
- Growth/Records/Schedule/Contract/League/Player/Team 화면은 중복 Top Bar와 Legacy Navigation 호출을 제거하고 공용 Chrome 아래 안전영역으로 이동했다.
- 위 7개 화면의 로컬 cyan 팔레트와 배경 Glow는 `CareerUiTheme`의 중립 토큰으로 통합했다.
- Home에는 생성된 Clubhouse 배경을 저대비 장식 레이어로 적용했다.
- 리그 순위는 `CareerLeagueSnapshotAdapter`와 공용 `CompactRecordTableView`를 실제 사용한다. Career 리더보드와 시즌/역대 기록은 `RecordTableView`로 이관해 1,000행에서도 Viewport 부근 Row만 재사용하고 정렬·Stable ID 선택·빈 상태를 지원한다.
- Schedule의 내 구단 월간 목록은 `CareerScheduleSnapshotAdapter`와 공용 `RecordTableView`를 사용한다. 기존 달력·홈/원정 Split·리그 목록·월 이동은 유지하고 경기 결과는 Snapshot의 확정 Outcome을 그대로 표시한다.
- Player Detail은 `CareerPlayerDetailSnapshotAdapter`에서 공용 신원·소속·시즌·포지션·컨디션·피로와 능력치 표를 공급한다. 기존 대형 `UIPlayerCard`, 성장 CTA와 Career 전용 Board/Skills/Career 영역은 유지한다.
- Contract는 실제 `CareerContractView`의 계약 이력·상여 진행·계약 오퍼를 공용 `CompactRecordTableView`에 연결했다. 오퍼 선택, 연장 수락·거절, 협상 시작, 서명과 은퇴 Command는 기존 Career 경로를 유지한다.
- 선수 경기의 스코어보드는 공개 경기 상태에서 만든 `MatchHudPresentationModel`을 공용 `MatchHudView`가 렌더링한다.
- 타격/투구 방침, 확정, 일시정지, 현재 선수 장면 자동 진행은 키보드와 Button 모두 `PlayerMatchControls` 권한 게이트를 거친다. MiniGame 세부 입력은 Player 전용 기존 화면에 남아 있다.

## 7. 감독 모드 → 구단주 모드 변경

- `GameMode.OwnerCareer = 1`을 정식 이름으로 사용한다.
- 기존 숫자 1과 `ManagerCareer`는 Legacy alias로 유지하여 직렬화 호환을 보존한다.
- 타이틀의 사용자 노출 명칭을 `구단주 모드`로 변경했다.
- Assets의 Runtime/Scene/Prefab/Asset 사용자 노출 `감독 모드` 검색 결과는 0건이다.
- 기획 문서 폴더를 `docs/todo/역사시뮬레이션_구단주모드`로 이동하고 경제 문서를 `05_구단주모드_경제_스카우트.md` 하나로 정리했다. 관련 Markdown 링크와 게임 모드 표현도 갱신했다.
- `ManagerModeCoordinator`, `ManagerModeRuntimeState`, `ManagerModeMatchService`와 Save DTO의 `managerMode`는 실제로 구단주 모드 의미지만, 현재 동시 변경 중인 Save 호환 경로를 깨지 않기 위해 이번 변경에서 이름을 강제 치환하지 않았다. 후속 migration에서 `OwnerMode*` 정식 타입과 Legacy JSON alias를 함께 도입해야 한다.

## 8. 유지한 Manager 개념

다음 실제 야구 감독 의미는 변경하지 않았다.

- 감독 판단, 감독 평가, 감독 방침
- Manager AI, Manager evaluation/decision
- ManagerTacticProfile과 감독 전술·교체·불펜 의미

## 9. ImageGen 생성 자산

- `bg_player_clubhouse_v1.png`: Player Home에 적용.
- `bg_owner_front_office_v1.png`: Owner Home에 실제 적용.
- 두 자산 모두 실제 로고·선수·유니폼·문자·숫자·UI가 없으며 Unity Sprite import 설정을 `.meta`로 고정했다.
- 전체 Prompt와 Import 조건은 `GeneratedUIAssets.md`에 기록했다.

## 10. Agent별 작업 범위

- Lead: 영상/코드 감사, 통합 판단, Theme, 세션 중재, 공용 HUD concrete View, 문서 migration, 통합 검증.
- Shared UI Architecture: 공용 Shell/Component/계약과 EditMode 테스트.
- Terminology/Migration: Owner Production Manager 연결, 실제 Owner Workspace/Command, 명칭·Save 호환 감사.
- Player Mode UI: Player Profile/Home/Status/Asset 계약, Player 화면 안전영역, 경기 입력 분리, 공용 HUD와 Owner 관전 Overlay 분리.
- Shared Information Screens: 공용 Snapshot/Action/Record/Roster View, 실제 League 및 가상화 Career 기록표 연결, Match/세션 독립 QA.
- Visual Asset: ImageGen 배경 2종과 Prompt manifest.

## 11. 주요 변경 파일

- `Assets/02.Scripts/Presentation/SharedUI/**`
- `Assets/02.Scripts/Presentation/SharedScreens/**`
- `Assets/02.Scripts/Presentation/Player/**`
- `Assets/02.Scripts/Presentation/Owner/**`
- `Assets/02.Scripts/Presentation/Match/**`
- `Assets/02.Scripts/Presentation/Career/PlayerCareerShellCoordinator.cs`
- `Assets/02.Scripts/Presentation/Career/CareerTabNavigation.cs`
- `Assets/02.Scripts/Presentation/Career/UI_Scene_CareerDashboard.cs`
- `Assets/02.Scripts/Presentation/UI/CareerUiTheme.cs`
- `Assets/02.Scripts/Presentation/UI/CareerUiSkin.cs`
- `Assets/Resources/UI/Generated/**`
- `Assets/Tests/EditMode/Presentation/**`
- `Assets/02.Scripts/Game/Unity/Historical/OwnerModeManager.cs`
- `docs/UI/**`

## 12. 테스트 결과

- `Baseball.Game.csproj`: 컴파일 성공, warning 0/error 0.
- `Baseball.Game.Unity.csproj`: Unity가 아직 `Library/ScriptAssemblies`에 복사하지 않은 패키지 DLL을 Bee 산출물로 보정해 컴파일 성공, warning 0/error 0.
- `Baseball.Presentation.csproj`: Unity가 아직 생성 csproj에 반영하지 않은 최신 UI 소스를 임시 MSBuild import로 포함해 컴파일 성공, warning 0/error 0.
- `Baseball.Game.Tests.csproj`: 신규 구단주 테스트 소스를 포함해 컴파일 성공, warning 0/error 0.
- `Baseball.Presentation.Tests.csproj`: 신규 UI 테스트 소스를 포함해 컴파일 성공, warning 0/error 0.
- 정적 검사: Player production namespace의 `OwnedPlayerCardState`, `ScoutingPoint`, `DevelopmentPoint`, `CardTraining`, Enhancement Service, `TeamColorResolver` 참조 0건.
- 정적 검사: Presentation의 `ActiveRosterValidator`, `TeamColorResolver`, Scout 확률 계산 참조 0건.
- 정적 검사: 공용 Match HUD의 Player/Owner Production 생성 경로 2건, 신규 UI `.meta` 누락 0건.
- Presentation EditMode 소스의 `[Test]`/`[TestCase]` 선언은 147개이며 테스트 어셈블리 컴파일에 포함됐다. Unity Test Runner가 실행된 수치는 아니다.
- UI/문서 범위 whitespace 검사는 통과했다. 저장소 전체 `git diff --check`에는 이번 UI 작업 범위 밖 `Assets/10.Datas/Resources/NewGame/NewGameDefinition.asset`의 기존 trailing whitespace 1건이 남아 있어 임의 수정하지 않았다.
- Unity EditMode Test Runner/PlayMode: Editor license가 없어 실행하지 못했다. 위 테스트 결과는 테스트 어셈블리 소스 컴파일 성공을 뜻하며 NUnit 실행 성공을 뜻하지 않는다.

## 13. Screenshot

요청된 Before/After Screenshot은 남기지 못했다. 설치된 Unity 6000.3.21f1의 batch mode가 유효한 Editor license 부재로 종료되어 실제 렌더와 해상도 QA를 수행할 수 없었다. 스크린샷이 없는 상태를 디자인 Acceptance 완료로 판정하지 않는다.

## 14. 남은 UX 작업

- Owner ActiveRoster 등록 변경, Lineup Drag & Drop, 등록 변경 Preview.
- Scout, Enhancement, CardTraining, TeamColor, Tactic을 연결하려면 먼저 Game 레이어에 Catalog/Preview/검증/결정론적 Command 계약을 추가해야 한다.
- Owner 현재 시즌 순위를 표시하려면 Game 레이어에 팀별 누적 승패와 확정 순위 Aggregate가 필요하다.
- Owner 관전 화면의 경기 로그·Box Score·하이라이트 탐색을 고밀도 보조 Panel로 확장한다.
- 기존 Career Match의 나머지 청색/SF 팔레트를 공용 Match Theme으로 이관한다.
- Player Home 외 기존 Workspace를 `MainWorkspaceHost` 자식으로 완전히 이관.
- `ManagerMode*` 내부 타입을 `OwnerMode*`로 옮기면서 기존 Save JSON alias를 보존하는 별도 migration.
- Unity에서 1280×720, 1920×1080, 2560×1440 렌더/입력/Popup/폰트 잘림 검증.

## 15. Legacy UI 잔존 여부

Legacy 하단 Navigation과 실제 렌더 호출은 제거했지만 각 Career UIScene의 프로그램 생성 Workspace와 일부 기존 Frame helper는 남아 있다. Owner Home/운영/25인 역할별 Lineup/보유 선수 Collection/일정/역사 기록/경기 관전, Player League/Records/Schedule/Team/Player Detail/Contract와 공용 Match HUD는 실제 Runtime에 연결됐다. Owner ActiveRoster 등록 변경·Scout·카드 육성·TeamColor/전술 상세 분석·현재 시즌 순위는 아직 없다. 따라서 결과는 공용 Shell 기반의 통합 단계이며 전체 UI 전면 개편 완료본으로 판정하지 않는다.

중복된 옛 `05_감독모드_경제_스카우트.md`는 새 확장 문서를 정본으로 삼아 프로젝트 문서 트리에서 제거했고, 원문은 작업 중 내용 손실을 막기 위해 `.tmp/ui_legacy_docs`에 백업했다.
