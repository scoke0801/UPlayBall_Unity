# 현행 UI 지도

## 감사 기준

- 기준 문서: `BaseballManager_PROJECT.md` 24절, 31절, 38절, 40절과 `BaseballManager_UI_FLOW.md`
- 영상: `DtJOn8Jj-50`, 8분 20초. YouTube storyboard 102장을 5초 간격으로 직접 확인했다.
- 영상 한계: 프레임 순서와 화면 전환은 확인했으나 음성, 포인터 위치, 5초 사이의 세부 입력은 확인하지 못했다.
- Runtime UI 기술: uGUI. UI Toolkit은 Editor의 역사 데이터 브라우저에만 사용한다.
- 기준 해상도: `UIRoot`의 네 Canvas가 모두 1920×1080, Match 0.5를 사용한다.

## 영상 기반 흐름

| 대략 시간 | 화면/행동 | 확인 결과 |
|---:|---|---|
| 00:00 | 타이틀 | 야구장 일러스트와 우측 모드 선택. 선수 커리어만 활성, 기존 `감독 커리어`는 잠김. |
| 00:05~00:35 | 새 선수 생성 | 선수 유형, 포지션/역할, 6능력치 배분, 상세 설정, 경기 방식 설정을 순서대로 진행. |
| 00:35~01:05 | 계약/홈/공용 정보 | 오퍼 선택 후 6구역 홈. 일정·구단·리그·기록 화면으로 이동. |
| 01:05~02:20 | 경기 준비/경기/결과 | 준비 화면 뒤 중앙 경기 영역, 좌측 내 선수, 우측 선택/상황, 하단 로그 구조. 타석 결과와 Box Score를 확인. |
| 02:20~03:10 | 시즌 진행/결산 | 홈 복귀, 시즌 진행 확인, 순위 확정과 수상 연출. |
| 03:10~04:45 | 성장/오프시즌 | 4×4 Skill Board, 보관함, 활동 선택, 계약 오퍼 비교를 반복. |
| 04:45~05:55 | 훈련/계약/커리어 전환 | 훈련 결과, 시즌 결과, 새 시즌 시작/은퇴 선택 흐름. |
| 06:00~07:50 | 새 커리어와 경기 반복 | 투수·타자 생성 경로와 경기 화면을 다시 검증. |
| 07:50~08:15 | 커리어 기록/수상/홈 | 기록관, 수상 발표, 홈 정보 패널을 확인. |
| 08:15~08:20 | 타이틀 복귀 | 새 커리어 선택 화면으로 복귀. |

## 공통 기반

| 대상 | Scene/Prefab/코드 | 진입/데이터 | 판정 | 현재 문제 |
|---|---|---|---|---|
| UI Root | `Assets/Resources/UI/UI_System_Root.prefab`, `UIRoot`, `UIManager` | Boot에서 Manager 생성 | 유지·확장 | HUD/Scene/Popup/System 분리와 Cancel 스택은 유효. Shell 전용 레이어/Host 계약은 없음. |
| Management Scene | `Assets/01.Scenes/Management.unity` | Boot → Loading → Management | 유지 | Scene에는 Context만 있고 실제 UI는 여러 `RuntimeInitializeOnLoadMethod`가 독립 생성. 생성 순서와 초기 화면 소유권이 분산됨. |
| Match Scene | `Assets/01.Scenes/Match.unity` | 현재 커리어 공개 경로에서 사용되지 않음 | Legacy 후보 | 경기 UI는 Management Scene 내부 `UI_Scene_CareerMatch`가 담당. |
| Theme | `CareerUiTheme`, `CareerUiSkin`, Skin PNG | 모든 `UIBase.Initialize/Show` | 교체 | 청록·파랑 Glow와 큰 절삭 금속 프레임이 지배적. 화면 코드에도 별도 Color 상수가 남아 토큰이 완전한 정본이 아님. |
| Navigation | `CareerTabNavigation`, `CareerNavigationChrome` | 각 화면이 하단 8탭을 재생성 | Frame 교체 | `FindObjectsByType` 기반 라우팅, Back stack 없음. 상단 Header/설정 위치와 하단 탭 인스턴스가 화면마다 다시 생성됨. |
| Popup | `UIManager`, `UIPopupBase`, 개별 Career Popup | Popup Canvas와 표시 스택 | 유지·리팩터링 | 레이어/Cancel은 좋지만 Confirm/Tooltip/Toast 공용 Visual component가 부족하고 개별 생성 헬퍼가 반복됨. |

## 화면별 현황

| Current | Scene/Prefab/UXML | Controller/Presenter | 진입 경로 | 사용 데이터 | Target 판정 | 현재 문제 |
|---|---|---|---|---|---|---|
| 타이틀/모드 선택 | Management, Runtime uGUI | `UI_Scene_NewGame.Title` + `NewGameManager` | 활성 Career 없음 | `CareerCreationPresentationData` | Frame 교체 | `감독 커리어` 잠금 문자열, 화면별 색상 하드코딩, 타이틀 이미지에 로고 텍스트가 Bake됨. |
| 선수 생성 | Management, Runtime uGUI | `UI_Scene_NewGame.*` | 선수 커리어 선택 | `CareerCreationDraft`, 규칙/프리셋 | 부분 개편 | 기능 흐름은 완성. 큰 카드형 선택과 절대 좌표 의존이 강함. |
| Player Home | Management, Runtime uGUI | `UI_Scene_CareerDashboard.*` | 생성/계약 후 기본 | `CareerDashboardView` | Frame 교체 | 화면 내부에서 Header·6패널·Nav를 모두 생성. 전체 Clear/Rebuild. 프레임 밀도가 높지만 같은 정보 계층의 테두리가 과도함. |
| Player Detail | Management, Runtime uGUI | `UI_Scene_Player.*`, `UIPlayerCard` | 선수 탭/카드 | `PlayerProfileView` | 부분 개편 | Large Card/Front·Back은 재사용 가능. 공용 Mini Card·Inspector 계약이 없음. |
| Growth | Management, Runtime uGUI | `UI_Scene_CareerGrowth.*`, 확인 Popup | 성장 탭 | `CareerGrowthView` | 부분 개편 | 실제 성장 상태 연결은 좋음. Shell/필터/Action Bar를 개별 구현하고 전체 계층을 재생성. |
| Schedule | Management, Runtime uGUI | `UI_Scene_CareerSchedule.*` | 일정 탭 | `CareerScheduleView` | 부분 개편 | 데이터 계약 재사용 가능. 공용 Table/Filter/EmptyState가 없음. |
| League | Management, Runtime uGUI | `UI_Scene_League` | 리그 탭 | `LeagueHubView` | 부분 개편 | 순위·리더·리그 사다리 정보는 유효. 1,000줄 단일 화면과 화면 전용 표/행 헬퍼가 큼. |
| Team read-only | Management, Runtime uGUI | `UI_Scene_Team.*` | 구단 탭 | `TeamOverviewView` | 재작성 | 선수 모드 읽기 전용 의미는 맞음. 리스트 행 중심이라 카드 기반 공용 Roster Workspace의 View와 공유되지 않음. |
| Records | Management, Runtime uGUI | `UI_Scene_CareerRecords.*` | 기록 탭 | `CareerRecordsView` | 부분 개편 | 통계는 풍부하나 화면 전용 Table 구현과 Chrome 복제가 존재. |
| Contract | Management, Runtime uGUI | `UI_Scene_Contract` | 계약 탭/오프시즌 | `CareerContractView` | 부분 개편 | 실제 오퍼 비교는 보존. 공용 Team Detail 연결과 Mode action adapter가 없음. |
| Match | Management, Runtime uGUI | `UI_Scene_CareerMatch.*` | 홈 → 경기 준비 | `CareerMatchSession`, `MatchEvent`, Playback/mini-game presenter | Frame 교체 | 시뮬레이션 이벤트 경계는 우수. Shell과 Player control이 1,800줄 화면에 결합. 공용 HUD Base/Mode Overlay가 없음. |
| Match Result/Season Review | Dashboard/Match 내부 Overlay | Dashboard/Match partial | 경기·시즌 종료 | 실제 Snapshot/View | 부분 개편 | 데이터는 보존. 전용 공용 Result Base 없이 화면 내부 Overlay로 고정됨. |
| Career Presentation | `UI_CareerPresentation.prefab` | `UI_CareerPresentation.*` | 수상/성장/결산 큐 | `CareerPresentationRequest` | 유지 | 독립 Popup 레이어와 DOTween 시퀀스는 재사용 가치가 높음. |
| News/Settings/Retirement | Runtime Popup | 각 `UI_Popup_*` | Shell/이벤트 | 실제 Career state | 부분 개편 | 기능은 유지하되 Popup shell·focus·tooltip 공용화 필요. |
| Owner Mode UI | 없음 | 없음 | 타이틀 카드 잠김 | `ManagerHistoricalRuntimeState` 백엔드는 존재 | 신규 | Runtime State/Save v2는 있으나 Production 진입 Manager, Route, Presentation이 없음. |

## Save/Mode 감사

- `GameMode`는 `PlayerCareer = 0`, `ManagerCareer = 1`이다. 사용처는 생성 프로필과 타이틀 잠금 카드뿐이다.
- Owner 백엔드는 `ManagerHistoricalRuntimeState`, `ManagerHistoricalSaveData`, `ManagerHistoricalSaveAdapter(CurrentSaveVersion=3)`, `ManagerHistoricalSaveJsonStore`로 Career Save와 분리되어 있다.
- Owner 저장 DTO에는 GameMode 문자열이 없으므로 `ManagerCareer` enum 이름 변경은 현재 JSON 필드를 깨지 않는다. 숫자 1을 유지하고 Legacy enum alias 테스트를 둔다.
- Career 디스크 Save는 아직 없다는 `BaseballManager_PROJECT.md` 41.12 결정이 현재 정본이다. 존재하지 않는 Career Save migration을 만들지 않는다.

## 핵심 결론

현재 Frame은 교체 대상이다. 단, UI 레이어/표시 스택, 실제 View 데이터, 선수 Large Card, MatchEvent 기반 진행, Career Presentation 시퀀스는 보존한다. 첫 기준 화면은 Roster/Lineup Workspace로 삼고 여기서 만든 Mini Card, Filter Bar, Inspector, Action Bar를 다른 화면에 확장한다.
